// 仕様書: Unity/docs/.sdd/hud/01-hud-composition.md §5
// 自店HUD（順位の大表示＋スコア＋生存数）。本選では順位が画面の主役になる。
//
// 順位・スコアを計算しない。順位と CutLineRank を比較して自分が危険かを判定しない
// （危険の根拠は CutStoreIds と下位パネルの表示範囲。value-objects/12 §4.2）。

using TMPro;
using Takoda99.Client.State;
using Takoda99.Sound;
using Takoda99.View.Ranking;
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

        /// <summary>
        /// 順位テキストの配色。**ランキング行と同じ ScriptableObject を割り当てる**
        /// （金銀銅・警告色をHUDと一覧で別々に持たない）。未割り当てなら色を触らない。
        /// </summary>
        [Header("順位の色（value-objects/12 §3.1・§4.2.1）")]
        [SerializeField] private RankingRowPalette palette;

        /// <summary>
        /// 「ぎりぎり圏外（AtRisk）」の判定に使う下位の件数。
        /// **下位パネルの visibleCount と揃える**（画面上の警告帯と色が一致するように）。
        /// </summary>
        [SerializeField] private int bottomRangeCount = SelfRankToneResolver.DefaultBottomRangeCount;

        [Header("順位帯のSE（SoundLibrary の Ranking グループ）")]
        [Tooltip("この順位までを上位圏とし、入った瞬間に「上位ランク入り」を鳴らす。")]
        [SerializeField] private int topRankThreshold = RankSoundRule.DefaultTopRankThreshold;

        [Tooltip("淘汰される件数に対する「ぎりぎり圏外」の割合。24店が切られるなら 0.25 で直前6店まで。")]
        [SerializeField, Range(0f, 1f)]
        private float cullMarginRatio = RankSoundRule.DefaultCullMarginRatio;

        private SelfRankViewState current;
        private bool hasCurrent;

        /// <summary>いま順位テキストへ適用しているトーン。変わらないフレームは色の代入も省く。</summary>
        private RankingRowTone currentTone;
        private bool hasTone;

        /// <summary>
        /// 直近の順位帯。**帯が変わった瞬間だけ**SEを鳴らすために持つ。
        /// Apply は 2〜4Hz で呼ばれ続けるので、状態そのものを条件にすると鳴りっぱなしになる。
        /// </summary>
        private RankSoundBand lastBand = RankSoundBand.None;
        private bool hasBand;

        /// <summary>state から表示値と色を作って反映する。Renderer が state 変化のたびに呼ぶ。</summary>
        public void Apply(ClientState state)
        {
            if (state == null)
            {
                return;
            }

            SetState(
                SelfRankViewState.From(state.Rank, state.Score, state.AliveCount),
                ResolveTone(state));

            PlayBandSe(state);
        }

        /// <summary>
        /// 順位帯が変わった瞬間にSEを鳴らす。
        /// **淘汰圏内かどうかの判定はサーバー値だけを根拠にする**（CutStoreIds / SelfAtRisk）。
        /// 順位と CutLineRank を比較しない（ranking-view/02 §1）。CutLineRank を使うのは
        /// 「何店が切られるか」という件数を出すためだけで、そこから自店の危険は判断しない。
        /// </summary>
        private void PlayBandSe(ClientState state)
        {
            var isCutTarget = SelfRankToneResolver.ContainsStoreId(state.Cull?.CutStoreIds, state.SelfStoreId)
                || (state.Cull != null && state.Cull.SelfAtRisk);

            var bandCount = state.Cull != null
                ? RankSoundRule.CullBandCount(state.AliveCount, state.Cull.CutLineRank, cullMarginRatio)
                : 0;

            var isInCullBand = bandCount > 0
                && RankingRowsBuilder.IsInBottomRange(
                    state.Ranking, state.SelfStoreId, state.AliveCount, bandCount);

            var band = RankSoundRule.Resolve(state.Alive, state.Rank, topRankThreshold, isCutTarget, isInCullBand);

            if (hasBand && band == lastBand)
            {
                return;
            }

            var wasFirst = !hasBand;
            lastBand = band;
            hasBand = true;

            // 初回（試合開始直後の1回目の反映）では鳴らさない。まだ「入った」のではなく、
            // 単に最初の順位が届いただけであり、全員に一斉に鳴ってしまう。
            if (wasFirst)
            {
                return;
            }

            switch (band)
            {
                case RankSoundBand.Top:
                    SoundPlayer.Play(SoundId.RankEnteredTop);
                    break;
                case RankSoundBand.CullRange:
                    SoundPlayer.Play(SoundId.RankEnteredCullRange);
                    break;
                case RankSoundBand.CullMargin:
                    SoundPlayer.Play(SoundId.RankEnteredCullMargin);
                    break;
                default:
                    // 中位へ戻ったときは鳴らさない（危険を脱したこと自体は画面の色で分かる）。
                    break;
            }
        }

        /// <summary>順位・スコア・生存数と、順位テキストの色をまとめて反映する。</summary>
        public void SetState(SelfRankViewState state, RankingRowTone tone)
        {
            ApplyTone(tone);

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

        /// <summary>
        /// 危険の根拠は2つだけ：サーバーの <c>CutStoreIds</c> と、下位パネルの表示範囲
        /// （value-objects/12 §4.2）。**順位と `CutLineRank` を比較しない。**
        /// </summary>
        /// <remarks>
        /// 判定そのものは <see cref="SelfRankToneResolver"/> に置いてある
        /// （右下のネオンパネル ranking-view/08 と必ず同じ色になるように）。
        /// </remarks>
        private RankingRowTone ResolveTone(ClientState state)
            => SelfRankToneResolver.Resolve(state, bottomRangeCount);

        /// <summary>色を変えるのは順位テキストだけ（スコア・生存数は据え置き）。</summary>
        private void ApplyTone(RankingRowTone tone)
        {
            if (palette == null || rankText == null)
            {
                return;
            }

            if (hasTone && currentTone == tone)
            {
                return;
            }

            currentTone = tone;
            hasTone = true;
            rankText.color = palette.Of(tone);
        }

    }
}
