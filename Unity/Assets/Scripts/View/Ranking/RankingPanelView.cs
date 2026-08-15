// 仕様書: Unity/docs/.sdd/ranking-view/01-ranking-panel.md
// 試合中のランキングパネル（上位N＋自分）。順位・スコアは計算せず、Ranking が持つ値を描くだけ。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>上位N名＋自分を描くランキングパネル。</summary>
    public sealed class RankingPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;

        [Header("配置（ranking-view/04）")]
        [SerializeField] private TopRankingSlots slots;
        [SerializeField] private RankingRowPalette palette;

        /// <summary>
        /// 表示件数。**10 を下回る値を設定しない**。100秒時点の生存数が10人＝
        /// 上位10名リストがそのまま生存者全員になるため（ranking-view/01 §4）。
        /// slots が配線されていれば、その要素数で上書きされる（04 §5.1）。
        /// </summary>
        [SerializeField] private int visibleCount = 10;

        /// <summary>入れ替え演出の調整値（ranking-view/06）。moveDuration が旧 rowMoveDuration を兼ねる。</summary>
        [SerializeField] private RankingSwapSettings swapSettings = RankingSwapSettings.Default;

        private RankingRowPool pool;
        private IRankingSlotSource slotSource;
        private readonly HashSet<string> visibleIds = new HashSet<string>();
        private readonly List<RankingRowStyle> styleBuffer = new List<RankingRowStyle>();

        private void Awake()
        {
            // 04 §5.1: スロットの要素数を正とする。
            if (slots == null)
            {
                // 1. slots が未配線 → 従来どおり visibleCount を使い、警告を出す（縦積みへフォールバック）。
                Debug.LogWarning(
                    $"{nameof(RankingPanelView)}.{nameof(slots)} が未配線です。" +
                    "スロットを使わず、visibleCount による縦積みへフォールバックします。",
                    this);

                if (visibleCount < RankingRowsBuilder.MinVisibleCount)
                {
                    Debug.LogWarning(
                        $"{nameof(RankingPanelView)}.{nameof(visibleCount)} が {visibleCount} でした。" +
                        $"決勝では上位{RankingRowsBuilder.MinVisibleCount}名＝生存者全員になるため、" +
                        $"{RankingRowsBuilder.MinVisibleCount} にクランプします。",
                        this);
                    visibleCount = RankingRowsBuilder.MinVisibleCount;
                }

                slotSource = new EvenlySpacedSlotSource(visibleCount, 56f);
            }
            else if (slots.Count < RankingRowsBuilder.MinVisibleCount)
            {
                // 2. slots.Count < 10 → 警告して 10 にクランプ（01 §4 の要件は維持）。
                Debug.LogWarning(
                    $"{nameof(RankingPanelView)}.{nameof(slots)} の要素数が {slots.Count} でした。" +
                    $"{RankingRowsBuilder.MinVisibleCount} 未満のため、10 にクランプします。",
                    this);
                visibleCount = RankingRowsBuilder.MinVisibleCount;
                slotSource = slots;
            }
            else
            {
                // 3. それ以外 → visibleCount = slots.Count。
                visibleCount = slots.Count;
                slotSource = slots;
            }

            // 04 §3.1.1: スロットは目印として見える状態のまま残してよい（デバッグの下敷き）。
            // uGUI は後の兄弟を手前に描くため、RowsRoot を最後の兄弟にしておけば
            // 実行時の行が必ずスロットの上に乗る。
            if (rowsRoot != null)
            {
                rowsRoot.SetAsLastSibling();
            }

            pool = new RankingRowPool(rowPrefab, rowsRoot);
        }

        /// <summary>state から表示行を組み立てて反映する。Renderer が state 変化のたびに呼ぶ。</summary>
        public void Apply(ClientState state)
        {
            if (state == null)
            {
                return;
            }

            var rows = RankingRowsBuilder.Build(
                state.Ranking,
                state.SelfStoreId,
                state.Rank,
                state.Score,
                visibleCount);

            // Ranking.Rows が空ならパネルごと非表示。空リストの枠だけ出さない。
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
        /// value-objects/12 §3.2・§4.1・§4.2。上位パネルの見た目は順位（＝表示順の index）で決まり、
        /// 脱落確定／脱落済みだけが Tone を上書きする。
        /// 寸法だけはシーンのスロットが持つ値を優先する（04 §5.2.1）。
        /// </summary>
        private void BuildStyles(ClientState state, IReadOnlyList<RankingRowViewState> rows)
        {
            styleBuffer.Clear();
            var cutStoreIds = state.Cull?.CutStoreIds;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rank = i + 1;
                var style = RankingRowStyle.ForTopRank(rank);

                // 04 §5.2.1: 大きさと配置はエディタで決める。スロットが寸法を持つならそちらが正。
                // フォントサイズと Tone は表の値のまま（行の中身の可読性は外形とは別の判断）。
                if (slotSource != null && slotSource.TryGetSize(i, out var slotSize))
                {
                    style = style.WithSize(slotSize);
                }

                var isCutTarget = cutStoreIds != null && Contains(cutStoreIds, row.StoreId);
                if (!row.IsAlive)
                {
                    style = style.WithTone(RankingRowTone.Dead);
                }
                else if (isCutTarget)
                {
                    style = style.WithTone(RankingRowTone.Doomed);
                }

                styleBuffer.Add(style);
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

        /// <summary>待機中・リザルトで畳む。</summary>
        public void SetPanelVisible(bool visible)
        {
            if (rowsRoot != null && rowsRoot.gameObject.activeSelf != visible)
            {
                rowsRoot.gameObject.SetActive(visible);
            }
        }
    }
}
