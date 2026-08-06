// リザルト画面で、試合中に造ったたこ焼きの数を可視化する。
// サーバーからの「造ったたこ焼き数」の契約はまだ未定義のため、外部からの数値注入は
// SetTakoyakiCount(int) 経由の暫定インターフェースとする（契約確定後、呼び出し元を差し替える）。

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>ResultScene にアタッチし、注入された個数分だけ TakoyakiObj を少しずつ生成する。</summary>
    public sealed class TakoyakiCreator : MonoBehaviour
    {
        [SerializeField] private GameObject takoyakiPrefab;
        [SerializeField] private float spawnIntervalSeconds = 0.05f;

        [Header("テストモード")]
        [SerializeField] private bool testMode;
        [SerializeField] private int testTakoyakiCount;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private int injectedTakoyakiCount;
        private bool hasInjectedCount;
        private CancellationTokenSource spawnCts;

        private void Start()
        {
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

                spawned.Add(Instantiate(takoyakiPrefab, transform));

                if (i < count - 1)
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(spawnIntervalSeconds), cancellationToken: token);
                }
            }
        }

        private void Clear()
        {
            spawnCts?.Cancel();
            spawnCts?.Dispose();
            spawnCts = null;

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
