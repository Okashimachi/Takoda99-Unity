// リザルト画面で生成されるたこ焼き1個の見た目を、確率で抽選して決める。
// 抽選対象（スプライトと重み）は Inspector で指定する。

using System;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>TakoyakiObj プレハブにアタッチし、生成時に確率でスプライトを選んで適用する。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TakoyakiAppearance : MonoBehaviour
    {
        /// <summary>抽選候補ひとつ分。weight が大きいほど選ばれやすい（重み付き抽選）。</summary>
        [Serializable]
        public struct Variant
        {
            [SerializeField] private Sprite sprite;
            [Tooltip("出現の重み。全候補の合計に対する比率で抽選される（0以下は抽選対象外）。")]
            [SerializeField] private float weight;

            public Sprite Sprite => sprite;
            public float Weight => weight;
        }

        [SerializeField] private Variant[] variants;

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Apply();
        }

        /// <summary>確率抽選をやり直して見た目を適用する。</summary>
        public void Apply()
        {
            var picked = Pick();
            if (picked != null)
            {
                spriteRenderer.sprite = picked;
            }
        }

        private Sprite Pick()
        {
            if (variants == null || variants.Length == 0)
            {
                return null;
            }

            var totalWeight = 0f;
            foreach (var variant in variants)
            {
                if (variant.Sprite != null && variant.Weight > 0f)
                {
                    totalWeight += variant.Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            var threshold = UnityEngine.Random.value * totalWeight;
            foreach (var variant in variants)
            {
                if (variant.Sprite == null || variant.Weight <= 0f)
                {
                    continue;
                }

                threshold -= variant.Weight;
                if (threshold <= 0f)
                {
                    return variant.Sprite;
                }
            }

            // 浮動小数の誤差で抜けた場合の保険として、最後の有効候補を返す。
            for (var i = variants.Length - 1; i >= 0; i--)
            {
                if (variants[i].Sprite != null && variants[i].Weight > 0f)
                {
                    return variants[i].Sprite;
                }
            }

            return null;
        }
    }
}
