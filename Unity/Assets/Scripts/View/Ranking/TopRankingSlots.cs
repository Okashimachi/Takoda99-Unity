// 仕様書: Unity/docs/.sdd/ranking-view/04-top-ranking-slots.md
// 上位N行の配置先。座標と寸法をシーンに持たせ、コードから数式で決めない。

using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>
    /// 上位N行の配置先。座標と寸法をシーンに持たせ、コードから数式で決めない。
    /// </summary>
    public sealed class TopRankingSlots : MonoBehaviour, IRankingSlotSource
    {
        /// <summary>1位から順に並べる。要素数が上位表示件数になる。</summary>
        [SerializeField] private RectTransform[] slots;

        /// <summary>スロット数。RankingPanelView の visibleCount より優先する。</summary>
        public int Count => slots?.Length ?? 0;

        /// <summary>index は 0 始まり（0 = 1位）。範囲外は null。</summary>
        public RectTransform Slot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return null;
            }

            return slots[index];
        }

        /// <summary>index 番目のスロットの座標。</summary>
        public Vector2 PositionOf(int index)
        {
            var slot = Slot(index);
            return slot != null ? slot.anchoredPosition : Vector2.zero;
        }

        /// <summary>
        /// index 番目のスロットの寸法（04 §5.2.1）。RankingRowStyle.Size を上書きする。
        /// 座標と同じく「エディタでスロットを動かせば決まる」ようにするため、寸法もシーンに持たせる。
        /// </summary>
        public bool TryGetSize(int index, out Vector2 size)
        {
            var slot = Slot(index);
            if (slot == null)
            {
                size = Vector2.zero;
                return false;
            }

            size = slot.sizeDelta;

            // 0 は「まだ詰めていないスロット」とみなし、表の既定寸法へ委ねる
            // （幅0・高さ0の行を描いて見失わないため）。
            return size.x > 0f && size.y > 0f;
        }
    }
}
