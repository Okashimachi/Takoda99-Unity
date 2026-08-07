// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// root/SubStoreCanvas/RankinGage 配下の順位バー。RankBarViewState を受けて安全領域の帯と自店ポインタを描画する。
// DangerZone は Gage いっぱいに固定表示済みで、SafeZone がその上に重なることで境界だけがオレンジ色に見える。

using UnityEngine;
using Takoda99.View.ValueObjects;

namespace Takoda99.View
{
    /// <summary>順位バー（安全領域/下位淘汰領域の帯・自店ポインタ）の Unity 実体。</summary>
    public sealed class RankBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform gage;
        [SerializeField] private RectTransform safeZone;
        [SerializeField] private RectTransform playerRankPointer;

        private void Awake()
        {
            if (gage == null || safeZone == null || playerRankPointer == null)
            {
                Debug.LogError($"{nameof(RankBarView)} の参照が未設定です。", this);
            }
        }

        /// <summary>安全領域の帯と自店ポインタの位置を更新する。</summary>
        public void SetState(RankBarViewState state)
        {
            var thresholdRatio = Mathf.Clamp01(state.StormThresholdPct);

            if (safeZone != null)
            {
                // 右端（1位側）は固定したまま、左端だけを淘汰閾値の位置に合わせる。
                var anchorMin = safeZone.anchorMin;
                anchorMin.x = thresholdRatio;
                safeZone.anchorMin = anchorMin;
            }

            if (playerRankPointer != null && gage != null)
            {
                var positionRatio = Mathf.Clamp01(state.SelfPositionRatio);
                var anchoredPosition = playerRankPointer.anchoredPosition;
                anchoredPosition.x = (positionRatio - 0.5f) * gage.rect.width;
                playerRankPointer.anchoredPosition = anchoredPosition;
            }
        }
    }
}
