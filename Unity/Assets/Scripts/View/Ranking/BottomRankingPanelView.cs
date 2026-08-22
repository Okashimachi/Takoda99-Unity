// 仕様書: Unity/docs/.sdd/ranking-view/05-bottom-ranking-panel.md
// 下位30行を常に描き、足切りの帯（脱落確定／警告／通常）で塗り分ける常設パネル。
// 順位・スコアを計算しない。自分が落ちるかどうかを順位比較で推測しない（CutStoreIds / SelfAtRisk に従う）。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>下位N行（既定30）を常に描き、足切りの帯で塗り分けるパネル。</summary>
    public sealed class BottomRankingPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private RankingRowPalette palette;

        /// <summary>表示件数。淘汰人数の最大(24)より大きい値にする。</summary>
        [SerializeField] private int visibleCount = 30;

        /// <summary>
        /// 1行の高さ(px)。<c>RankingRowStyle.BottomRowSize.y</c> と一致させる
        /// （行の寸法はそちらが返す値で上書きされるため、ここがズレると行間だけが合わなくなる）。
        /// </summary>
        [SerializeField] private float rowHeight = 29f;

        /// <summary>横の列数。columnCount × rowsPerColumn は visibleCount と一致させる。</summary>
        [SerializeField] private int columnCount = 3;

        /// <summary>1列あたりの行数。columnCount × rowsPerColumn は visibleCount と一致させる。</summary>
        [SerializeField] private int rowsPerColumn = 10;

        /// <summary>
        /// 列と列の中心間の距離(px)。<c>RankingRowStyle.BottomColumnSpacing</c> と一致させる。
        /// <c>RankingRowStyle.ForBottomBand</c> が返す行の幅（約 78.7px）より広くして重ならないようにする。
        /// 3列で約 244px・10行で約 193px なので `BottomRrankers`（370×300）に余裕を持って収まる。
        /// </summary>
        [SerializeField] private float columnSpacing = 81.33f;

        [SerializeField] private float rowMoveDuration = 0.25f;

        private RankingRowPool pool;
        private IRankingSlotSource slotSource;
        private readonly HashSet<string> visibleIds = new HashSet<string>();
        private readonly List<RankingRowStyle> styleBuffer = new List<RankingRowStyle>();

        // 06 §6: 下位パネルには入れ替えの強調を適用しない（emphasisScale = 1）。
        private RankingSwapSettings swapSettings;

        private void Awake()
        {
            pool = new RankingRowPool(rowPrefab, rowsRoot);
            // 05 §5.3: 横3列×縦10行のグリッドに配置する（画面右上に収まる形へ変更。旧: 横2列×縦15行）。
            // index は列優先（0列目を上から10件埋めたのち1列目へ）で埋まる。
            slotSource = new GridSlotSource(columnCount, rowsPerColumn, rowHeight, columnSpacing);
            swapSettings = new RankingSwapSettings
            {
                moveDuration = rowMoveDuration,
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

            var rows = RankingRowsBuilder.BuildBottom(
                state.Ranking,
                state.SelfStoreId,
                state.Rank,
                state.Score,
                state.AliveCount,
                visibleCount);

            if (rows.Count == 0)
            {
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(true);
            BuildStyles(state, rows);
            RankingRowLayout.Apply(pool, rows, styleBuffer, visibleIds, slotSource, palette, swapSettings);
        }

        /// <summary>
        /// value-objects/12 §4.2 / ranking-view/05 §5.2。
        /// Cull が null（未受信）のときは全行 Normal にする（警告が意味を失うため）。
        /// </summary>
        private void BuildStyles(ClientState state, IReadOnlyList<RankingRowViewState> rows)
        {
            styleBuffer.Clear();
            var cull = state.Cull;
            var cutStoreIds = cull?.CutStoreIds;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                RankingRowTone tone;
                if (!row.IsAlive)
                {
                    tone = RankingRowTone.Dead;
                }
                else if (cull == null)
                {
                    tone = RankingRowTone.Normal;
                }
                else if (cutStoreIds != null && Contains(cutStoreIds, row.StoreId))
                {
                    tone = RankingRowTone.Doomed;
                }
                else
                {
                    tone = RankingRowTone.AtRisk;
                }

                styleBuffer.Add(RankingRowStyle.ForBottomBand(tone));
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string storeId)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], storeId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetPanelVisible(bool visible)
        {
            if (rowsRoot != null && rowsRoot.gameObject.activeSelf != visible)
            {
                rowsRoot.gameObject.SetActive(visible);
            }
        }
    }
}
