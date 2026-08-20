// リザルト画面の成績1項目ぶんのパネル。項目名と内容を注入するだけの受け身のビュー。

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>ResultUnitPanel プレハブにアタッチし、成績の「項目名」と「内容」を表示する。</summary>
    public sealed class ResultUnitPanelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI unitTitleText;
        [SerializeField] private TextMeshProUGUI unitParamText;

        [Tooltip("背景の Panel。生成時にランダムな明るい色を割り当て、ネオンの縁取りと合わせて色鮮やかに見せる。")]
        [SerializeField] private Image panelImage;

        [Tooltip("ランダム色の彩度の範囲。色鮮やかさが目的なので Paticipant 側より高め。")]
        [SerializeField] private float randomColorSaturationMin = 0.6f;
        [SerializeField] private float randomColorSaturationMax = 0.9f;
        [SerializeField] private float randomColorValue = 1f;

        // プレハブに設定された文字サイズを基準にするため、倍率を掛ける前の値を控えておく。
        private float baseTitleFontSize;
        private float baseParamFontSize;

        private void Awake()
        {
            if (unitTitleText != null)
            {
                baseTitleFontSize = unitTitleText.fontSize;
            }

            if (unitParamText != null)
            {
                baseParamFontSize = unitParamText.fontSize;
            }

            if (panelImage != null)
            {
                var hue = Random.value;
                var saturation = Random.Range(randomColorSaturationMin, randomColorSaturationMax);
                panelImage.color = Color.HSVToRGB(hue, saturation, randomColorValue);
            }
        }

        /// <summary>項目名と内容を流し込む。</summary>
        public void SetValue(string title, string param)
        {
            SetValue(title, param, 1f, 1f);
        }

        /// <summary>
        /// 項目名と内容を流し込む。2つの倍率は項目の優先度（目立たせ具合）を表し、
        /// それぞれプレハブに設定された文字サイズに対して掛かる。
        /// </summary>
        public void SetValue(string title, string param, float paramFontScale, float titleFontScale)
        {
            if (unitTitleText != null)
            {
                if (titleFontScale > 0f && baseTitleFontSize > 0f)
                {
                    unitTitleText.fontSize = baseTitleFontSize * titleFontScale;
                }

                unitTitleText.text = title;
            }

            if (unitParamText != null)
            {
                if (paramFontScale > 0f && baseParamFontSize > 0f)
                {
                    unitParamText.fontSize = baseParamFontSize * paramFontScale;
                }

                unitParamText.text = param;
            }
        }

        /// <summary>
        /// 内容（数字）の文字にネオン用マテリアルを差し込む（未指定なら何もしない）。
        /// 項目名側は当てない：ネオン素材のフォントアトラスは unitParamText 側のフォントに合わせて
        /// 作られているため、unitTitleText（別フォント）へ流用すると文字化けする。
        /// </summary>
        public void SetNeonMaterial(Material neonMaterial)
        {
            if (neonMaterial == null || unitParamText == null)
            {
                return;
            }

            unitParamText.fontSharedMaterial = neonMaterial;
        }
    }
}
