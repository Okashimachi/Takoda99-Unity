// 仕様書: Unity/docs/.sdd/ranking-view/01-ranking-panel.md §2
// ランキング1行（順位・名前・スコア）。ランキングパネル・秒読みパネル・観戦画面で共用する。

using TMPro;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>1行。順位・名前・スコアの3点セット。</summary>
    public sealed class RankingRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;

        [Header("強調・減光")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject selfHighlight;

        /// <summary>脱落済みの行の不透明度。**リストからは消さない**（順位は確定値として並び続ける）。</summary>
        [SerializeField] private float deadAlpha = 0.4f;

        private RankingRowViewState current;
        private bool hasCurrent;

        /// <summary>この行が今どの店を描いているか。プールの引き当てに使う。</summary>
        public string StoreId => hasCurrent ? current.StoreId : null;

        public void SetState(RankingRowViewState state)
        {
            // 99行のリストで「値が変わった行だけ TMP を更新する」ための早期リターン
            // （ranking-view/03 §4 P2）。WebGL では 1〜2Hz でも無視できない差になる。
            if (hasCurrent && current.Equals(state))
            {
                return;
            }

            current = state;
            hasCurrent = true;

            if (rankText != null)
            {
                rankText.text = state.RankText;
            }

            if (nameText != null)
            {
                nameText.text = state.NameText;
            }

            if (scoreText != null)
            {
                scoreText.text = state.ScoreText;
            }

            if (selfHighlight != null)
            {
                selfHighlight.SetActive(state.IsSelf);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = state.IsAlive ? 1f : deadAlpha;
            }
        }

        /// <summary>スコア欄を持たない用途（脱落予定リスト等）で、名前だけを差し替える。</summary>
        public void SetNameOnly(string storeId, string displayName)
        {
            SetState(RankingRowViewState.SelfOnly(storeId, 0, 0, displayName));

            if (rankText != null)
            {
                rankText.text = string.Empty;
            }

            if (scoreText != null)
            {
                scoreText.text = string.Empty;
            }

            if (selfHighlight != null)
            {
                selfHighlight.SetActive(false);
            }
        }

        /// <summary>プールへ戻すときに呼ぶ。次に使い回すとき確実に描き直されるようにする。</summary>
        public void Recycle()
        {
            hasCurrent = false;
            gameObject.SetActive(false);
        }
    }
}
