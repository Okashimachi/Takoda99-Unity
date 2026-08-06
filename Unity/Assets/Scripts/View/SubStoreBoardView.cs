// 仕様書: Unity/docs/.sdd/match-view/04-sub-store-board-view.md
// 小画面（root/SubStoreCanvas）の他店98店ぶんのミニ盤面。SubStoreCreator を置き換える。

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>
    /// SubStoreCanvas 配下の Left / Right（片側49店舗分の親）に SubStorePanel を配置し、
    /// StoreId ↔ タイルの対応表を保持して更新を振り分ける。
    /// </summary>
    public sealed class SubStoreBoardView : MonoBehaviour
    {
        public const int ColumnsPerSide = 7;
        public const int RowsPerSide = 7;
        public const int TilesPerSide = ColumnsPerSide * RowsPerSide; // 49
        public const int TileCount = TilesPerSide * 2;                 // 98

        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;
        [SerializeField] private GameObject subStorePanelPrefab;

        private readonly List<SubStoreTileView> tiles = new List<SubStoreTileView>(TileCount);
        private readonly Dictionary<string, SubStoreTileView> tilesByStoreId = new Dictionary<string, SubStoreTileView>();

        private void Awake()
        {
            if (left == null || right == null || subStorePanelPrefab == null)
            {
                Debug.LogError($"{nameof(SubStoreBoardView)} の参照が未設定です。", this);
                return;
            }

            Populate(left);
            Populate(right);
        }

        private void Populate(RectTransform parent)
        {
            var size = parent.rect.size;
            var cellWidth = size.x / ColumnsPerSide;
            var cellHeight = size.y / RowsPerSide;

            for (var row = 0; row < RowsPerSide; row++)
            {
                for (var col = 0; col < ColumnsPerSide; col++)
                {
                    var panel = Instantiate(subStorePanelPrefab, parent);
                    var rectTransform = (RectTransform)panel.transform;

                    rectTransform.anchorMin = new Vector2(0f, 1f);
                    rectTransform.anchorMax = new Vector2(0f, 1f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = new Vector2(cellWidth, cellHeight);

                    var x = cellWidth * (col + 0.5f);
                    var y = -cellHeight * (row + 0.5f);
                    rectTransform.anchoredPosition = new Vector2(x, y);

                    var tile = panel.GetComponent<SubStoreTileView>();
                    if (tile == null)
                    {
                        Debug.LogError($"{nameof(SubStoreBoardView)}: subStorePanelPrefab に {nameof(SubStoreTileView)} がありません。", this);
                        continue;
                    }

                    tiles.Add(tile);
                }
            }
        }

        /// <summary>自店を除く他店の StoreId 一覧を割り当てる。昇順に整列してから左上詰めで配置する。</summary>
        public void Bind(IReadOnlyList<string> otherStoreIds)
        {
            tilesByStoreId.Clear();

            var sorted = otherStoreIds.OrderBy(id => id, System.StringComparer.Ordinal).ToList();
            if (sorted.Count > TileCount)
            {
                Debug.LogWarning($"{nameof(SubStoreBoardView)}: otherStoreIds が {TileCount} を超えたため超過分を捨てます。", this);
                sorted = sorted.Take(TileCount).ToList();
            }

            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (i < sorted.Count)
                {
                    tile.gameObject.SetActive(true);
                    tile.Bind(sorted[i]);
                    tilesByStoreId[sorted[i]] = tile;
                }
                else
                {
                    tile.gameObject.SetActive(false);
                }
            }
        }

        public void SetSummary(string storeId, int creditLife, bool alive)
        {
            if (tilesByStoreId.TryGetValue(storeId, out var tile))
            {
                tile.SetSummary(creditLife, alive);
            }
            else
            {
                Debug.LogWarning($"{nameof(SubStoreBoardView)}: 未知の StoreId '{storeId}' への SetSummary を無視しました。", this);
            }
        }

        public void SetRank(string storeId, int rank)
        {
            if (tilesByStoreId.TryGetValue(storeId, out var tile))
            {
                tile.SetRank(rank);
            }
            else
            {
                Debug.LogWarning($"{nameof(SubStoreBoardView)}: 未知の StoreId '{storeId}' への SetRank を無視しました。", this);
            }
        }
    }
}
