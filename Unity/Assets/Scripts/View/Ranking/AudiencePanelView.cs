// 仕様書: Unity/docs/.sdd/ranking-view/07-audience-panel.md
// 11〜99位の89店を 9列×10行 のグリッドで一覧するパネル（自店脱落時の ResultCanvas 用）。
// 順位・スコアを計算しない。並びは Rank 昇順のまま再ソートしない。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>11〜99位の89店を 9列×10行 のグリッドで一覧するパネル。</summary>
    public sealed class AudiencePanelView : MonoBehaviour
    {
        /// <summary>
        /// パネルごと出し入れする根（§5.6「パネルごと非表示」）。通常は AudiencePanel 自身を指す。
        /// 背景 Panel も一緒に消すため rowsRoot ではなくここを切る（CullCountdownPanelView と同じ作り）。
        /// </summary>
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private RankingRowPalette palette;

        /// <summary>先頭から除外する件数。上位パネルの表示件数と揃える（既定10＝1〜10位を除く）。</summary>
        [SerializeField] private int skipCount = 10;

        /// <summary>並べる件数。99 - skipCount（既定89）。</summary>
        [SerializeField] private int visibleCount = 89;

        [SerializeField] private int columnCount = 9;
        [SerializeField] private int rowsPerColumn = 10;

        /// <summary>
        /// グリッド全体の寸法。ここから1マスの寸法を割り出す。
        /// <see cref="fitGridToPanel"/> が true のときは <see cref="panelRoot"/> の実寸で上書きされるため、
        /// **こちらはフォールバック値**（panelRoot 未配線・寸法0のとき）になる。
        /// </summary>
        [SerializeField] private Vector2 gridSize = new Vector2(550f, 400f);

        /// <summary>
        /// パネルの実寸へグリッドを合わせる（07 §5.2）。
        /// **既定で true。** シーンで AudiencePanel の大きさを変えたときに Inspector 値の直し漏れで
        /// グリッドがはみ出す・余るのを防ぐ。
        /// </summary>
        [SerializeField] private bool fitGridToPanel = true;

        private RankingRowPool pool;
        private IRankingSlotSource slotSource;

        /// <summary>いまグリッドを組んである寸法。変わったフレームだけ組み直す。</summary>
        private Vector2 appliedGridSize;
        private readonly HashSet<string> visibleIds = new HashSet<string>();
        private readonly List<RankingRowStyle> styleBuffer = new List<RankingRowStyle>();

        // §5.4: 入れ替えアニメーションを持たない（移動即時・強調なし）。
        private RankingSwapSettings swapSettings;

        private void Awake()
        {
            pool = new RankingRowPool(rowPrefab, rowsRoot);
            EnsureGrid();

            swapSettings = new RankingSwapSettings
            {
                moveDuration = 0f,
                emphasisScale = 1f,
                emphasisDuration = 0f,
                maxEmphasisRows = 0,
            };
        }

        public void Apply(ClientState state)
        {
            if (state == null)
            {
                return;
            }

            var rows = RankingRowsBuilder.BuildRange(
                state.Ranking,
                state.SelfStoreId,
                state.Rank,
                state.Score,
                skipCount,
                visibleCount);

            if (rows.Count == 0)
            {
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(true);
            EnsureGrid();
            BuildStyles(rows);
            RankingRowLayout.Apply(pool, rows, styleBuffer, visibleIds, slotSource, palette, swapSettings);
        }

        /// <summary>
        /// グリッドをパネルの実寸へ合わせる（§5.2）。寸法が変わったフレームだけ組み直す。
        /// **パネルを縮めたら 1マスも同じ比率で縮む**（セルの中身も比率で追従する。value-objects/12 §3.2）。
        /// </summary>
        private void EnsureGrid()
        {
            var size = ResolveGridSize();
            if (slotSource != null && size == appliedGridSize)
            {
                return;
            }

            appliedGridSize = size;

            // §5.1: 列優先（index = rowsPerColumn * col + row）。GridSlotSource の割り当てと一致する。
            var cellWidth = size.x / columnCount;
            var cellHeight = size.y / rowsPerColumn;
            slotSource = new GridSlotSource(columnCount, rowsPerColumn, cellHeight, cellWidth);
        }

        /// <summary>
        /// パネルの実寸を採る。取れなければ Inspector の <see cref="gridSize"/> を使う
        /// （寸法0のグリッドを組んで全行を1点に重ねるより、既定値で描くほうが軽症）。
        /// </summary>
        private Vector2 ResolveGridSize()
        {
            if (!fitGridToPanel || panelRoot == null)
            {
                return gridSize;
            }

            if (!(panelRoot.transform is RectTransform panelRect))
            {
                return gridSize;
            }

            var size = panelRect.rect.size;
            return size.x > 0f && size.y > 0f ? size : gridSize;
        }

        /// <summary>
        /// §8 未確定：帯は当面 Normal 固定にする（リザルトは全員脱落済みで、Dead 一色になるのを避ける）。
        /// </summary>
        private void BuildStyles(IReadOnlyList<RankingRowViewState> rows)
        {
            styleBuffer.Clear();
            var cellSize = new Vector2(appliedGridSize.x / columnCount, appliedGridSize.y / rowsPerColumn);

            for (var i = 0; i < rows.Count; i++)
            {
                styleBuffer.Add(RankingRowStyle.ForAudienceCell(cellSize, RankingRowTone.Normal));
            }
        }

        /// <summary>
        /// §5.6。背景ごと消すため panelRoot を切る。未配線のときだけ rowsRoot にフォールバックする
        /// （背景が残るが、行だけでも消えるほうが「試合中に一覧が出たまま」より軽症）。
        /// </summary>
        public void SetPanelVisible(bool visible)
        {
            var target = panelRoot != null ? panelRoot : rowsRoot != null ? rowsRoot.gameObject : null;
            if (target != null && target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }
    }
}
