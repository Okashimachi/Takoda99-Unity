// 仕様書: Unity/docs/.sdd/hud/01-hud-composition.md §5
// 自店HUD（順位の大表示＋スコア＋生存数）。本選では順位が画面の主役になる。

using TMPro;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>
    /// 自店の順位・スコア・生存数。値が変わらないフレームは TMP への代入ごと省く。
    /// </summary>
    /// <remarks>
    /// このViewは更新頻度が低い（EvaluationUpdate は 2〜4Hz）。打鍵1回ごとに再描画される
    /// <c>MainStoreCanvas</c> と同じ Canvas に置くと、打鍵のたびに順位まで含めた
    /// メッシュ再構築が走る。**入れ子Canvasで切り離すこと**（match-view/07-match-hud.md §1 と同じ理由）。
    /// </remarks>
    public sealed class SelfRankView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text aliveCountText;

        private SelfRankViewState current;
        private bool hasCurrent;

        /// <summary>順位・スコア・生存数をまとめて反映する。</summary>
        public void SetState(SelfRankViewState state)
        {
            // 値が変わらないフレームは ToString 済み文字列の代入ごと省く。
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

            if (scoreText != null)
            {
                scoreText.text = state.ScoreText;
            }

            if (aliveCountText != null)
            {
                aliveCountText.text = state.AliveCountText;
            }
        }
    }
}
