// リザルト画面で、試合中に造ったたこ焼きの数を可視化する。
// サーバーからの「造ったたこ焼き数」の契約はまだ未定義のため、外部からの数値注入は
// SetTakoyakiCount(int) 経由の暫定インターフェースとする（契約確定後、呼び出し元を差し替える）。

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>ResultScene にアタッチし、注入された個数分だけ TakoyakiObj を少しずつ生成する。自身は左右に揺れ続ける。</summary>
    public sealed class TakoyakiCreator : MonoBehaviour
    {
        [SerializeField] private GameObject takoyakiPrefab;
        [SerializeField] private GameObject takoyakiParent;
        [SerializeField] private float spawnIntervalSeconds = 0.05f;

        [Header("左右の揺れ")]
        [SerializeField] private float swayDistance = 1f;
        [SerializeField] private float swayDurationSeconds = 1f;

        [Header("生成完了後の表示演出（ResultCanvas/Result配下）")]
        [SerializeField] private GameObject rank;
        [SerializeField] private GameObject others;
        [SerializeField] private GameObject buttons;
        [SerializeField] private float revealIntervalSeconds = 2f;

        [Header("テストモード")]
        [SerializeField] private bool testMode;
        [SerializeField] private int testTakoyakiCount;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private int injectedTakoyakiCount;
        private bool hasInjectedCount;
        private CancellationTokenSource spawnCts;
        private Tween swayTween;

        private void Start()
        {
            swayTween = transform
                .DOLocalMoveX(transform.localPosition.x + swayDistance, swayDurationSeconds)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            SetActiveIfAssigned(rank, false);
            SetActiveIfAssigned(others, false);
            SetActiveIfAssigned(buttons, false);

            if (testMode)
            {
                Spawn(testTakoyakiCount).Forget();
            }
            else if (hasInjectedCount)
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

        /// <summary>外部（サーバーから受け取ったリザルト情報）から、造ったたこ焼きの数を注入する。</summary>
        public void SetTakoyakiCount(int count)
        {
            injectedTakoyakiCount = count < 0 ? 0 : count;
            hasInjectedCount = true;

            if (!testMode)
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

            for (var i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var parent = takoyakiParent.transform;
                var takoyaki = Instantiate(takoyakiPrefab, transform.position, transform.rotation, parent);
                spawned.Add(takoyaki);

                if (i < count - 1)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(spawnIntervalSeconds), cancellationToken: token);
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

            await UniTask.Delay(System.TimeSpan.FromSeconds(revealIntervalSeconds), cancellationToken: token);
            SetActiveIfAssigned(buttons, true);
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
