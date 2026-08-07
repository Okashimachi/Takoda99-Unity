// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// root/SubStoreCanvas/RankinGage 配下の順位バー。RankBarViewState を受けて
// DangerZone（生存最下位の位置）・SafeZone（下位淘汰対象の上端）・自店ポインタをリアルタイムに描画する。
// SafeZone が DangerZone の上に重なることで、境界の帯だけが正確にオレンジ色に見える。

using UnityEngine;
using Takoda99.View.ValueObjects;

namespace Takoda99.View
{
    /// <summary>順位バー（生存数/下位淘汰の帯・自店ポインタ）の Unity 実体。</summary>
    public sealed class RankBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform gage;
        [SerializeField] private RectTransform dangerZone;
        [SerializeField] private RectTransform safeZone;
        [SerializeField] private RectTransform playerRankPointer;

        private void Awake()
        {
            if (gage == null || dangerZone == null || safeZone == null || playerRankPointer == null)
            {
                Debug.LogError($"{nameof(RankBarView)} の参照が未設定です。", this);
            }
        }

        /// <summary>生存数の帯・安全領域の帯・自店ポインタの位置を更新する。</summary>
        public void SetState(RankBarViewState state)
        {
            if (dangerZone != null)
            {
                // 右端（1位側）は固定したまま、左端を「生存している最下位の順位」の位置に合わせる。
                var anchorMin = dangerZone.anchorMin;
                anchorMin.x = Mathf.Clamp01(state.AliveBoundaryRatio);
                dangerZone.anchorMin = anchorMin;
            }

            if (safeZone != null)
            {
                // 右端（1位側）は固定したまま、左端を下位淘汰対象の上端（最も安全側）の位置に合わせる。
                var anchorMin = safeZone.anchorMin;
                anchorMin.x = Mathf.Clamp01(state.DangerBoundaryRatio);
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
