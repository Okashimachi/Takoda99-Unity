// 仕様書: Unity/docs/.sdd/value-objects/05-rank-bar-and-eval-delta-view-state.md
// root/MainStoreCanvas/EvalCanvas 配下の星評価表示。EvaluationUpdate.starRating（0..5・受信値そのまま）を
// 星5つの塗り比率へ割って描く。星の値は算出しない（サーバー権威）。
//
// スクリプトを持つのは EvalCanvas だけにする。Star プレハブ側には一切コンポーネントを足さず、
// ここから各 Star の Image を Filled として操作する。

using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>星評価（0..5・小数）の表示。端数の星は部分的に塗る。</summary>
    public sealed class StarRatingView : MonoBehaviour
    {
        [Tooltip("星の親（EvalCanvas/Stars）。stars 未設定のとき、ここから名前が Star で始まる子を順に拾う。")]
        [SerializeField] private Transform starsRoot;

        [Tooltip("星1つぶんの Image。左（低評価側）から順に並べる。未設定なら starsRoot から自動解決する。")]
        [SerializeField] private Image[] stars;

        [Tooltip("端数の星の塗り方向。Horizontal + 原点 Left なら「左半分だけ」の見え方になる。")]
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;

        [Tooltip("塗りの原点。fillMethod に対応する OriginHorizontal 等の値をそのまま入れる（Horizontal の 0 = Left）。")]
        [SerializeField] private int fillOrigin;

        /// <summary>直近に反映した星評価。</summary>
        public double StarRating { get; private set; }

        private void Awake()
        {
            ResolveStars();

            if (stars == null || stars.Length == 0)
            {
                Debug.LogError($"{nameof(StarRatingView)}: 星の Image が1つも見つかりません（{nameof(starsRoot)} / {nameof(stars)} を確認してください）。", this);
                return;
            }

            // 端数を塗るには Filled が要る。プレハブ側の設定に依存させず、ここで揃える。
            foreach (var star in stars)
            {
                if (star == null)
                {
                    continue;
                }

                star.type = Image.Type.Filled;
                star.fillMethod = fillMethod;
                star.fillOrigin = fillOrigin;
            }

            SetRating(0d);
        }

        /// <summary>
        /// <paramref name="starRating"/> を反映する。0..星の数へクランプし、端数は境目の星1つだけを部分的に塗る。
        /// </summary>
        public void SetRating(double starRating)
        {
            StarRating = starRating;

            if (stars == null)
            {
                return;
            }

            var fills = StarRatingFill.From(starRating, stars.Length);
            for (var i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].fillAmount = fills[i];
                }
            }
        }

        /// <summary>
        /// <see cref="stars"/> 未設定時に <see cref="starsRoot"/> の子から拾う。
        /// Star 本体は RectTransform だけなので、その配下（Panel）の Image を取る。
        /// BG など Star 以外の子は名前で弾く。
        /// </summary>
        private void ResolveStars()
        {
            if (stars != null && stars.Length > 0)
            {
                return;
            }

            if (starsRoot == null)
            {
                return;
            }

            var found = new System.Collections.Generic.List<Image>();
            for (var i = 0; i < starsRoot.childCount; i++)
            {
                var child = starsRoot.GetChild(i);
                if (!child.name.StartsWith("Star"))
                {
                    continue;
                }

                var image = child.GetComponentInChildren<Image>(true);
                if (image != null)
                {
                    found.Add(image);
                }
            }

            stars = found.ToArray();
        }
    }
}
