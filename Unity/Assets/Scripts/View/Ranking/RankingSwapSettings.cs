// 仕様書: Unity/docs/.sdd/ranking-view/06-rank-swap-animation.md
// 入れ替え演出の調整値と、座標供給元の抽象化。

using System;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>座標（と、あれば寸法）の供給元。上位はスロット、下位は等間隔。</summary>
    internal interface IRankingSlotSource
    {
        int Count { get; }

        Vector2 PositionOf(int index);

        /// <summary>
        /// 供給元が寸法も持つなら true（04 §5.2.1）。true のとき RankingRowStyle.Size を上書きする。
        /// 等間隔の供給元は常に false を返し、表の既定寸法をそのまま使わせる。
        /// </summary>
        bool TryGetSize(int index, out Vector2 size);
    }

    /// <summary>入れ替え演出の調整値。Inspector から触る。</summary>
    [Serializable]
    public struct RankingSwapSettings
    {
        [Tooltip("移動と見た目の補間にかける秒数")]
        public float moveDuration;          // 既定 0.25

        [Tooltip("順位が変わった行の強調の強さ。1 で等倍（強調なし）")]
        public float emphasisScale;         // 既定 1.08

        [Tooltip("強調の往復にかける秒数。moveDuration 以下にする")]
        public float emphasisDuration;      // 既定 0.15

        [Tooltip("同時に強調する行の上限。超えたら強調せず移動だけ行う")]
        public int maxEmphasisRows;         // 既定 4

        public static RankingSwapSettings Default => new RankingSwapSettings
        {
            moveDuration = 0.25f,
            emphasisScale = 1.08f,
            emphasisDuration = 0.15f,
            maxEmphasisRows = 4,
        };
    }

    /// <summary>
    /// 等間隔の縦積み用 <see cref="IRankingSlotSource"/>（下位パネル・スロット未配線時のフォールバック）。
    /// <c>-rowHeight * index</c> で下へ積む（ranking-view/05 §5.3）。
    /// <paramref name="rowHeight"/> には**正の行高**を渡すこと（負値を渡すと上へ積み上がる）。
    /// </summary>
    internal sealed class EvenlySpacedSlotSource : IRankingSlotSource
    {
        private readonly float rowHeight;
        private readonly float x;

        public EvenlySpacedSlotSource(int count, float rowHeight, float x = 0f)
        {
            Count = count;
            this.rowHeight = rowHeight;
            this.x = x;
        }

        /// <summary>行数が毎回変わる用途（観戦画面の99行）で、インスタンスを作り直さずに更新できる。</summary>
        public int Count { get; set; }

        public Vector2 PositionOf(int index) => new Vector2(x, -rowHeight * index);

        /// <summary>等間隔の縦積みは寸法を持たない。行の寸法は RankingRowStyle の表が決める。</summary>
        public bool TryGetSize(int index, out Vector2 size)
        {
            size = Vector2.zero;
            return false;
        }
    }

    /// <summary>
    /// 横 columnCount × 縦 rowCount のグリッド用 <see cref="IRankingSlotSource"/>（下位パネルの2列表示）。
    /// index は列優先で埋める（0列目を上から rowCount 件埋めたのち、1列目へ移る）。
    /// 縦は rowCount 件を中央揃えで積み、横は columnCount 列を columnSpacing 間隔で中央揃えする。
    /// </summary>
    internal sealed class GridSlotSource : IRankingSlotSource
    {
        private readonly int columnCount;
        private readonly int rowCount;
        private readonly float rowHeight;
        private readonly float columnSpacing;

        public GridSlotSource(int columnCount, int rowCount, float rowHeight, float columnSpacing)
        {
            this.columnCount = columnCount;
            this.rowCount = rowCount;
            this.rowHeight = rowHeight;
            this.columnSpacing = columnSpacing;
        }

        public int Count => columnCount * rowCount;

        public Vector2 PositionOf(int index)
        {
            var column = index / rowCount;
            var row = index % rowCount;

            var x = (column - (columnCount - 1) / 2f) * columnSpacing;
            var y = ((rowCount - 1) / 2f - row) * rowHeight;
            return new Vector2(x, y);
        }

        /// <summary>グリッドも等間隔と同じく寸法を持たない。</summary>
        public bool TryGetSize(int index, out Vector2 size)
        {
            size = Vector2.zero;
            return false;
        }
    }
}
