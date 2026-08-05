using UnityEngine;

namespace Takoda99.View
{
    /// <summary>
    /// SubStoreCanvas 配下の Left / Right（片側48店舗分の親）に、
    /// SubStorePanel プレハブを 7×7 の等間隔グリッドで配置する。
    /// </summary>
    public sealed class SubStoreCreator : MonoBehaviour
    {
        private const int Columns = 7;
        private const int Rows = 7;

        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;
        [SerializeField] private GameObject subStorePanelPrefab;

        private void Start()
        {
            Populate(left);
            Populate(right);
        }

        private void Populate(RectTransform parent)
        {
            if (parent == null || subStorePanelPrefab == null)
            {
                return;
            }

            var size = parent.rect.size;
            var cellWidth = size.x / Columns;
            var cellHeight = size.y / Rows;

            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Columns; col++)
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
                }
            }
        }
    }
}
