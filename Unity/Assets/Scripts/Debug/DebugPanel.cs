// 仕様書: Unity/docs/.sdd/platform/03-debug-panel.md
// 送受信 Envelope の生JSONをそのまま整形表示する。読み取り専用（編集・再送信はしない）。
//
// 階層: root/BootStrap/DebugCanvas
//   ├── DebugPanel   … 既定非表示。Panel / Text (TMP) / Copy ボタンを持つ
//   └── DebugButton  … 常時表示（右上）。押すたびに DebugPanel の表示/非表示をトグルする
// DebugCanvas は BootStrap（DontDestroyOnLoad）の子なので、シーンをまたいでも生存する。

using System.Text;
using Takoda99.Client.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.DebugUI
{
    /// <summary>送受信 Envelope の生JSON表示パネル（03-debug-panel.md）。</summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;   // DebugPanel（トグル対象）
        [SerializeField] private Button toggleButton;    // DebugButton（常時表示）
        [SerializeField] private Button copyButton;      // DebugPanel/Copy
        [SerializeField] private TextMeshProUGUI logText; // DebugPanel/Text (TMP)
        [SerializeField] private int maxDisplayedEntries = 50;

        private IEnvelopeLog log;

        /// <summary>GameBootstrapper から IEnvelopeLog を注入する。</summary>
        public void Bind(IEnvelopeLog boundLog)
        {
            log = boundLog;
        }

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(Toggle);
            }
            else
            {
                Debug.LogError($"{nameof(DebugPanel)}.{nameof(toggleButton)} が未設定です。DebugButton を割り当ててください。", this);
            }

            if (copyButton != null)
            {
                copyButton.onClick.AddListener(CopyAll);
            }
        }

        private void OnDestroy()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(Toggle);
            }

            if (copyButton != null)
            {
                copyButton.onClick.RemoveListener(CopyAll);
            }
        }

        private void Update()
        {
            // 表示中のみ再描画する（非表示中は毎フレーム文字列を組み立てない）。
            if (panelRoot != null && panelRoot.activeSelf)
            {
                Render();
            }
        }

        /// <summary>DebugButton から呼ばれる。パネルの表示/非表示を切り替える。</summary>
        public void Toggle()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(!panelRoot.activeSelf);

            if (panelRoot.activeSelf)
            {
                Render();
            }
        }

        /// <summary>
        /// Copy ボタンから呼ばれる。**表示中の件数ではなく、リングバッファに残っている全ログ**を
        /// クリップボードへ入れる（切り分けのために丸ごと貼り付けたいのが通常のため）。
        /// </summary>
        public void CopyAll()
        {
            GUIUtility.systemCopyBuffer = BuildText(int.MaxValue);
        }

        private void Render()
        {
            if (logText == null)
            {
                return;
            }

            logText.text = BuildText(maxDisplayedEntries);
        }

        private string BuildText(int limit)
        {
            var builder = new StringBuilder();

            // 先頭にクライアント側イベント（客の到着/離脱、脱落）を出す。
            // Envelope の生JSONは量が多く、客の流れがその中に埋もれてしまうため。
            builder.Append("=== client events (new -> old) ===\n");
            builder.Append("NET = サーバー由来 / VIEW = 画面上の増減\n");
            builder.Append(ClientEventLog.BuildText(limit));

            builder.Append("\n=== envelopes (raw, new -> old) ===\n");

            if (log == null)
            {
                builder.Append("未接続\n");
                return builder.ToString();
            }

            var count = 0;

            foreach (var entry in log.Entries)
            {
                if (count >= limit)
                {
                    break;
                }

                var direction = entry.Direction == EnvelopeLogDirection.Incoming ? "IN " : "OUT";
                builder.Append('[').Append(direction).Append("] ").Append(entry.Json).Append('\n');
                count++;
            }

            return builder.ToString();
        }
    }
}
