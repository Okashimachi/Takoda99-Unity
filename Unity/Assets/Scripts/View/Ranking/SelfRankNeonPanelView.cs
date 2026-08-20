// 仕様書: Unity/docs/.sdd/ranking-view/08-self-rank-neon-panel.md
// 下位ランキングパネル（05）が 3列×10行 へ縮んで空いた右下領域に置く、自店の順位・名前・スコアの大表示。
// TopRanker.prefab の RankText/NameText/ScoreText/Panel 構成をそのまま流用し、Panel の裏に
// ネオン発光風の Glow Image を足しただけの構成にする。
//
// 順位・スコア・トーンを計算しない。色は SelfRankToneResolver（SelfRankView と共用）と
// RankingRowPalette から引くだけ。

using TMPro;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Ranking
{
    /// <summary>
    /// 自店の順位を大きく見せるネオン風パネル。TopRanker.prefab をベースにしたレイアウトを想定する。
    /// </summary>
    /// <remarks>
    /// SelfRankView と同じく更新頻度が低い（EvaluationUpdate は 2〜4Hz）。値が変わらない呼び出しでは
    /// 文字列の組み立てと TMP への代入をまとめて省く（打鍵ごとのメッシュ再構築と WebGL の GC を避ける）。
    /// </remarks>
    public sealed class SelfRankNeonPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;

        /// <summary>
        /// Panel の外側に置くネオン発光風の縁取り。**トーンで色が変わるのはここと順位テキストだけ。**
        /// 本体パネル（Panel）の暗い塗りはシーンの authored 値のまま触らない
        /// （塗りまで塗り替えると順位の文字が背景と同色になって読めなくなる）。
        /// </summary>
        [SerializeField] private Image glowImage;

        /// <summary>順位・帯の配色。ランキング行・自店HUDと同じ ScriptableObject を割り当てる。</summary>
        [SerializeField] private RankingRowPalette palette;

        /// <summary>
        /// 「ぎりぎり圏外（AtRisk）」の判定に使う下位の件数。下位パネルの visibleCount と揃える。
        /// </summary>
        [SerializeField] private int bottomRangeCount = SelfRankToneResolver.DefaultBottomRangeCount;

        [SerializeField] private string scoreFormat = "スコア {0}";
        [SerializeField] private string rankFormat = "{0}位";

        /// <summary>いま表示している順位・スコア。変わらない呼び出しでは string.Format ごと省く。</summary>
        private SelfRankViewState current;
        private bool hasCurrent;

        /// <summary>いま表示している屋号。DisplayNames は試合中ほぼ不変なので毎回代入しない。</summary>
        private string currentName;

        private RankingRowTone currentTone;
        private bool hasTone;

        /// <summary>
        /// Glow の authored なアルファ。パレットの色は不透明なので、そのまま代入すると
        /// 発光のにじみ（半透明）が消えてベタ塗りの矩形になる。色相だけ差し替える。
        /// </summary>
        private float glowAlpha = 1f;
        private bool hasGlowAlpha;

        private void Awake()
        {
            if (glowImage != null)
            {
                glowAlpha = glowImage.color.a;
                hasGlowAlpha = true;
            }
        }

        /// <summary>state から表示値と色を作って反映する。Renderer が state 変化のたびに呼ぶ。</summary>
        public void Apply(ClientState state)
        {
            if (state == null)
            {
                return;
            }

            // 試合終了で畳んだあと（OnMatchEnd → SetPanelVisible(false)）に次の試合が始まっても
            // 描き直せるよう、描く直前に必ず開く。他のランキングパネルと同じ約束。
            SetPanelVisible(true);

            ApplyTone(SelfRankToneResolver.Resolve(state, bottomRangeCount));

            var next = SelfRankViewState.From(state.Rank, state.Score, state.AliveCount);
            var name = state.DisplayNames.TryGetValue(state.SelfStoreId, out var displayName)
                ? displayName
                : string.Empty;

            if (hasCurrent && current.Equals(next) && string.Equals(currentName, name, System.StringComparison.Ordinal))
            {
                return;
            }

            current = next;
            currentName = name;
            hasCurrent = true;

            if (rankText != null)
            {
                // RankText は順位未確定（0以下）なら "--"。そこへ「位」を付けると意味が壊れるため、
                // 確定しているときだけ書式を当てる。
                rankText.text = state.Rank >= 1
                    ? string.Format(rankFormat, next.RankText)
                    : next.RankText;
            }

            if (nameText != null)
            {
                nameText.text = name;
            }

            if (scoreText != null)
            {
                scoreText.text = string.Format(scoreFormat, next.ScoreText);
            }
        }

        /// <summary>色を変えるのはネオンの縁取りと順位テキストだけ（本体パネルの塗りは据え置き）。</summary>
        private void ApplyTone(RankingRowTone tone)
        {
            if (palette == null)
            {
                return;
            }

            if (hasTone && currentTone == tone)
            {
                return;
            }

            currentTone = tone;
            hasTone = true;

            var color = palette.Of(tone);

            if (glowImage != null)
            {
                var glow = color;
                glow.a = hasGlowAlpha ? glowAlpha : color.a;
                glowImage.color = glow;
            }

            if (rankText != null)
            {
                rankText.color = color;
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
