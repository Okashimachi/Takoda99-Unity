// リザルト画面で、試合中に造ったたこ焼きの数を可視化する。
// サーバーからの「造ったたこ焼き数」の契約はまだ未定義のため、外部からの数値注入は
// SetTakoyakiCount(int) 経由の暫定インターフェースとする（契約確定後、呼び出し元を差し替える）。

using System.Collections.Generic;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>ResultScene にアタッチし、注入された個数分だけ TakoyakiObj を生成する。</summary>
    public sealed class TakoyakiCreator : MonoBehaviour
    {
        [SerializeField] private GameObject takoyakiPrefab;

        [Header("テストモード")]
        [SerializeField] private bool testMode;
        [SerializeField] private int testTakoyakiCount;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private int injectedTakoyakiCount;
        private bool hasInjectedCount;

        private void Start()
        {
            if (testMode)
            {
                Spawn(testTakoyakiCount);
            }
            else if (hasInjectedCount)
            {
                Spawn(injectedTakoyakiCount);
            }
        }

        /// <summary>外部（サーバーから受け取ったリザルト情報）から、造ったたこ焼きの数を注入する。</summary>
        public void SetTakoyakiCount(int count)
        {
            injectedTakoyakiCount = count < 0 ? 0 : count;
            hasInjectedCount = true;

            if (!testMode)
            {
                Spawn(injectedTakoyakiCount);
            }
        }

        private void Spawn(int count)
        {
            Clear();

            if (takoyakiPrefab == null)
            {
                Debug.LogError($"{nameof(TakoyakiCreator)}: {nameof(takoyakiPrefab)} が未設定です。Inspector で TakoyakiObj プレハブをアタッチしてください。", this);
                return;
            }

            for (var i = 0; i < count; i++)
            {
                spawned.Add(Instantiate(takoyakiPrefab, transform));
            }
        }

        private void Clear()
        {
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
