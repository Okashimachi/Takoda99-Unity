// 仕様書: Unity/docs/.sdd/value-objects/12-ranking-row-style.md
// 順位・帯からランキング行の見た目（寸法・フォントサイズ・配色区分）を決める。
// 順位を計算しない。RectTransform / Image を触らない（適用は View の仕事）。

using System;
using UnityEngine;

namespace Takoda99.View.ValueObjects
{
    /// <summary>行の配色区分。Panel の色をこれで引く。</summary>
    public enum RankingRowTone
    {
        Gold,      // 1位
        Silver,    // 2位
        Bronze,    // 3位
        Upper,     // 4〜10位
        Normal,    // 帯なしの下位
        AtRisk,    // 警告帯（次の足切りで落ちる可能性がある）
        Doomed,    // 脱落確定帯（CutStoreIds に入っている）
        Dead,      // 脱落済み
    }

    /// <summary>1行が取るべき見た目。座標は含まない（スロットが持つ）。</summary>
    public readonly struct RankingRowStyle : IEquatable<RankingRowStyle>
    {
        public Vector2 Size { get; }          // RectTransform.sizeDelta

        public float RankFontSize { get; }

        public float NameFontSize { get; }

        public float ScoreFontSize { get; }

        public RankingRowTone Tone { get; }

        private RankingRowStyle(Vector2 size, float rankFontSize, float nameFontSize, float scoreFontSize, RankingRowTone tone)
        {
            Size = size;
            RankFontSize = rankFontSize;
            NameFontSize = nameFontSize;
            ScoreFontSize = scoreFontSize;
            Tone = tone;
        }

        /// <summary>上位パネル用。順位だけで決まる（value-objects/12 §3.2・§4.1）。</summary>
        public static RankingRowStyle ForTopRank(int rank)
        {
            if (rank == 1)
            {
                return new RankingRowStyle(new Vector2(230f, 44f), 20f, 24f, 20f, RankingRowTone.Gold);
            }

            if (rank == 2)
            {
                return new RankingRowStyle(new Vector2(230f, 44f), 20f, 24f, 20f, RankingRowTone.Silver);
            }

            if (rank == 3)
            {
                return new RankingRowStyle(new Vector2(230f, 44f), 20f, 24f, 20f, RankingRowTone.Bronze);
            }

            if (rank >= 4 && rank <= 6)
            {
                return new RankingRowStyle(new Vector2(130f, 66f), 16f, 20f, 14f, RankingRowTone.Upper);
            }

            // 7〜10位、11位以上、0位以下（不明）はすべて同じ見た目（§4.1）。
            return new RankingRowStyle(new Vector2(100f, 50f), 12f, 16f, 12f, RankingRowTone.Upper);
        }

        /// <summary>下位パネル用。寸法は固定で、色だけが帯で変わる。フォントサイズは BottomRanker.prefab の authored 値(12/12)と一致させる。</summary>
        public static RankingRowStyle ForBottomBand(RankingRowTone tone)
        {
            return new RankingRowStyle(new Vector2(120f, 29f), 12f, 12f, 0f, tone);
        }

        /// <summary>同じ寸法・フォントサイズのまま Tone だけ差し替える。</summary>
        public RankingRowStyle WithTone(RankingRowTone tone)
        {
            return new RankingRowStyle(Size, RankFontSize, NameFontSize, ScoreFontSize, tone);
        }

        /// <summary>
        /// 寸法だけシーンのスロットの値で差し替える（§3.2 の注記・ranking-view/04 §5.2.1）。
        /// フォントサイズと Tone は表の値のまま残す。
        /// </summary>
        public RankingRowStyle WithSize(Vector2 size)
        {
            return new RankingRowStyle(size, RankFontSize, NameFontSize, ScoreFontSize, Tone);
        }

        /// <summary>
        /// 帯（Tone）の決め方（value-objects/12 §4.2）。優先順に最初に当たったもので確定する。
        /// クライアントは Rank と CutLineRank を比較しない。
        /// </summary>
        /// <param name="alive">RankingRow.Alive。</param>
        /// <param name="isCutTarget">ForcedEliminationWarning.CutStoreIds に storeId が含まれるか。</param>
        /// <param name="isInBottomPanel">下位パネルに表示されているか（上位パネルでは false を渡す）。</param>
        public static RankingRowTone ResolveTone(bool alive, bool isCutTarget, bool isInBottomPanel)
        {
            if (!alive)
            {
                return RankingRowTone.Dead;
            }

            if (isCutTarget)
            {
                return RankingRowTone.Doomed;
            }

            if (isInBottomPanel)
            {
                return RankingRowTone.AtRisk;
            }

            return RankingRowTone.Normal;
        }

        public bool Equals(RankingRowStyle other)
        {
            return Size == other.Size
                && RankFontSize.Equals(other.RankFontSize)
                && NameFontSize.Equals(other.NameFontSize)
                && ScoreFontSize.Equals(other.ScoreFontSize)
                && Tone == other.Tone;
        }

        public override bool Equals(object obj) => obj is RankingRowStyle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Size.GetHashCode();
                hash = (hash * 397) ^ RankFontSize.GetHashCode();
                hash = (hash * 397) ^ NameFontSize.GetHashCode();
                hash = (hash * 397) ^ ScoreFontSize.GetHashCode();
                hash = (hash * 397) ^ (int)Tone;
                return hash;
            }
        }
    }
}
