// リザルト画面で、試合中に造ったたこ焼きの数を可視化する。
// サーバーからの「造ったたこ焼き数」の契約はまだ未定義のため、外部からの数値注入は
// SetTakoyakiCount(int) 経由の暫定インターフェースとする（契約確定後、呼び出し元を差し替える）。

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using Takoda99.Sound;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>ResultScene にアタッチし、注入された個数分だけ TakoyakiObj を少しずつ生成する。自身は左右に揺れ続ける。</summary>
    public sealed class TakoyakiCreator : MonoBehaviour
    {
        [SerializeField] private GameObject takoyakiPrefab;
        [SerializeField] private GameObject takoyakiParent;

        [Header("生成テンポ（ゆっくり始まり、徐々に速く、最速で頭打ち）")]
        [Tooltip("1個目と2個目の間隔（秒）。ここが一番ゆっくり。")]
        [SerializeField] private float firstIntervalSeconds = 0.35f;
        [Tooltip("どれだけ加速してもこれより短くならない間隔（秒）＝最高速度。")]
        [SerializeField] private float minIntervalSeconds = 0.04f;
        [Tooltip("1個生成するごとに間隔へ掛ける倍率。1未満で加速する（小さいほど速く頭打ちに達する）。")]
        [Range(0.5f, 1f)]
        [SerializeField] private float intervalDecayRate = 0.9f;

        [Header("左右の揺れ")]
        [SerializeField] private float swayDistance = 1f;
        [SerializeField] private float swayDurationSeconds = 1f;

        [Header("生成完了後の表示演出（ResultCanvas/Result配下）")]
        [SerializeField] private GameObject rank;
        [SerializeField] private GameObject others;
        [Tooltip("ResultCanvas/Noren。個人成績（others）と同時に出す。屋号はここに出るため。")]
        [SerializeField] private GameObject noren;
        [SerializeField] private GameObject buttons;
        [SerializeField] private float revealIntervalSeconds = 2f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private int injectedTakoyakiCount;
        private bool hasInjectedCount;
        private bool hasStarted;
        private CancellationTokenSource spawnCts;
        private Tween swayTween;

        /// <summary>
        /// Rank → Others → Buttons がすべて出そろった瞬間に1度だけ発火する。
        /// リザルトの順位表示SEはこの合図で鳴らす（ResultScreenView が購読する）。
        /// </summary>
        public event Action RevealCompleted;

        private void Start()
        {
            swayTween = transform
                .DOLocalMoveX(transform.localPosition.x + swayDistance, swayDurationSeconds)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            SetActiveIfAssigned(rank, false);
            SetActiveIfAssigned(others, false);
            SetActiveIfAssigned(noren, false);
            SetActiveIfAssigned(buttons, false);

            hasStarted = true;

            // OnEnable で先に注入されていた場合は、ここで初めて生成を始める。
            if (hasInjectedCount)
            {
                Spawn(injectedTakoyakiCount).Forget();
            }
        }

        private void OnDestroy()
        {
            spawnCts?.Cancel();
            spawnCts?.Dispose();
            swayTween?.Kill();
        }

        /// <summary>外部（サーバー受信値、またはテストモードのサンプル）から、造ったたこ焼きの数を注入する。</summary>
        public void SetTakoyakiCount(int count)
        {
            injectedTakoyakiCount = count < 0 ? 0 : count;
            hasInjectedCount = true;

            // Start より前に呼ばれた場合は、揺れ演出の初期化を待ってから Start 側で生成する。
            if (hasStarted)
            {
                Spawn(injectedTakoyakiCount).Forget();
            }
        }

        private async UniTaskVoid Spawn(int count)
        {
            Clear();

            if (takoyakiPrefab == null)
            {
                Debug.LogError($"{nameof(TakoyakiCreator)}: {nameof(takoyakiPrefab)} が未設定です。Inspector で TakoyakiObj プレハブをアタッチしてください。", this);
                return;
            }

            spawnCts = new CancellationTokenSource();
            var token = spawnCts.Token;

            // 間隔を1個ごとに intervalDecayRate 倍していく（等比）。減り幅が自分の大きさに比例するので
            // 最初はぐっと速くなり、minIntervalSeconds に近づくほど変化が緩やかになって頭打ちになる。
            var interval = Mathf.Max(firstIntervalSeconds, minIntervalSeconds);

            for (var i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var parent = takoyakiParent.transform;
                var takoyaki = Instantiate(takoyakiPrefab, transform.position, transform.rotation, parent);
                spawned.Add(takoyaki);

                // 1個ごとに鳴らす。終盤は 0.04 秒間隔まで詰まるため、音量は
                // SoundLibrary 側（Result グループ / ResultTakoyakiSpawn）で低めに設定する。
                SoundPlayer.Play(SoundId.ResultTakoyakiSpawn);

                if (i < count - 1)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(interval), cancellationToken: token);
                    interval = Mathf.Max(interval * intervalDecayRate, minIntervalSeconds);
                }
            }

            await RevealResultPanels(token);
        }

        /// <summary>全個数の生成が終わってから、Rank → Others → Buttons の順に2秒間隔で表示する。</summary>
        private async UniTask RevealResultPanels(CancellationToken token)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(revealIntervalSeconds), cancellationToken: token);
            SetActiveIfAssigned(rank, true);

            await UniTask.Delay(System.TimeSpan.FromSeconds(revealIntervalSeconds), cancellationToken: token);
            SetActiveIfAssigned(others, true);
            SetActiveIfAssigned(noren, true);

            await UniTask.Delay(System.TimeSpan.FromSeconds(revealIntervalSeconds), cancellationToken: token);
            SetActiveIfAssigned(buttons, true);

            // 順位・成績・次へボタンが出そろった。ここがリザルトの「表示完了」。
            RevealCompleted?.Invoke();
        }

        private static void SetActiveIfAssigned(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void Clear()
        {
            spawnCts?.Cancel();
            spawnCts?.Dispose();
            spawnCts = null;

            SetActiveIfAssigned(rank, false);
            SetActiveIfAssigned(others, false);
            SetActiveIfAssigned(noren, false);
            SetActiveIfAssigned(buttons, false);

            foreach (var obj in spawned)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            spawned.Clear();
        }
    }
}
