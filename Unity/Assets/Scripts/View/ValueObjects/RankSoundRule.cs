// 自店の順位が「どの帯に入ったか」を決める純粋なルール。SEを鳴らす契機の判定に使う。
//
// ★順位と CutLineRank を直接比較して自分の危険を判定しない（ranking-view/02 §1・hud/01 §5）。
// 淘汰圏内かどうかの権威はサーバー（CutStoreIds / SelfAtRisk）であり、こちらは受け取るだけ。
// CutLineRank を使うのは「何店が切られるか」という件数の算出だけで、自店の危険判定には使わない。

using System;

namespace Takoda99.View.ValueObjects
{
    /// <summary>自店の順位が入っている帯。SEの鳴らし分けの単位。</summary>
    public enum RankSoundBand
    {
        /// <summary>どの帯にも入っていない（中位）。</summary>
        None = 0,

        /// <summary>上位圏。</summary>
        Top = 1,

        /// <summary>次の淘汰で切られる圏内。</summary>
        CullRange = 2,

        /// <summary>淘汰圏のすぐ上（ぎりぎり圏外）。</summary>
        CullMargin = 3,
    }

    /// <summary>順位帯の判定。値を持たない純粋な関数の置き場。</summary>
    public static class RankSoundRule
    {
        /// <summary>上位圏の既定の境目。</summary>
        public const int DefaultTopRankThreshold = 10;

        /// <summary>
        /// ぎりぎり圏外の既定の幅。淘汰される件数に対する割合で決める
        /// （24店が切られるなら、その直前の 6 店までを「ぎりぎり」とする）。
        /// </summary>
        public const float DefaultCullMarginRatio = 0.25f;

        /// <summary>
        /// 「淘汰圏＋ぎりぎり圏外」を合わせた下位の件数。
        /// <see cref="RankingRowsBuilder.IsInBottomRange"/> に渡して、自店がこの帯にいるかを見る。
        /// </summary>
        /// <param name="aliveCount">生存店数。</param>
        /// <param name="cutLineRank">この順位より下が切られる境界（サーバー値）。</param>
        /// <param name="marginRatio">淘汰件数に対するぎりぎり圏外の割合。</param>
        public static int CullBandCount(int aliveCount, int cutLineRank, float marginRatio)
        {
            var cullCount = aliveCount - cutLineRank;
            if (cullCount <= 0)
            {
                return 0;
            }

            var margin = (int)Math.Ceiling(cullCount * Math.Max(marginRatio, 0f));
            return cullCount + margin;
        }

        /// <summary>
        /// 自店がいまどの帯にいるかを決める。
        /// **淘汰側を上位圏より優先する**（最終段階は CutLineRank が 2 になり、上位圏と淘汰圏が重なるため。
        /// そこで祝福の音を鳴らすのは明らかに誤り）。
        /// </summary>
        /// <param name="alive">自店が生存しているか。脱落後は常に <see cref="RankSoundBand.None"/>。</param>
        /// <param name="rank">自店の順位（サーバー値）。0 は未確定。</param>
        /// <param name="topRankThreshold">この順位までを上位圏とする。</param>
        /// <param name="isCutTarget">サーバーが「切る」と名指ししているか（CutStoreIds / SelfAtRisk）。</param>
        /// <param name="isInCullBand">淘汰圏＋ぎりぎり圏外の帯に入っているか。</param>
        public static RankSoundBand Resolve(
            bool alive,
            int rank,
            int topRankThreshold,
            bool isCutTarget,
            bool isInCullBand)
        {
            if (!alive)
            {
                return RankSoundBand.None;
            }

            if (isCutTarget)
            {
                return RankSoundBand.CullRange;
            }

            if (isInCullBand)
            {
                return RankSoundBand.CullMargin;
            }

            return rank > 0 && rank <= topRankThreshold ? RankSoundBand.Top : RankSoundBand.None;
        }
    }

    /// <summary>リザルトで全パネルが出そろったときに鳴らすSEの区分。</summary>
    public enum ResultRankSound
    {
        /// <summary>上位（既定は3位まで）。</summary>
        Top = 0,

        /// <summary>下位（既定は最下位から20店）。</summary>
        Bottom = 1,

        /// <summary>上位でも下位でもない。</summary>
        Normal = 2,
    }

    /// <summary>リザルトの順位表示SEの選択。</summary>
    public static class ResultRankSoundRule
    {
        /// <summary>上位とみなす件数（1〜3位）。</summary>
        public const int DefaultTopCount = 3;

        /// <summary>下位とみなす件数（最下位から20店）。</summary>
        public const int DefaultBottomCount = 20;

        /// <summary>1試合の参加店数。</summary>
        public const int DefaultStoreCount = 99;

        /// <summary>
        /// 最終順位から区分を決める。
        /// <paramref name="finalRank"/> が 0（MatchEnd 未着で順位が確定していない）のときは
        /// <see cref="ResultRankSound.Normal"/> にする。無音にすると「演出が終わったのに何も起きない」に見える。
        /// </summary>
        public static ResultRankSound From(int finalRank, int topCount, int bottomCount, int storeCount)
        {
            if (finalRank > 0 && finalRank <= topCount)
            {
                return ResultRankSound.Top;
            }

            if (finalRank > 0 && finalRank > storeCount - bottomCount)
            {
                return ResultRankSound.Bottom;
            }

            return ResultRankSound.Normal;
        }
    }
}
