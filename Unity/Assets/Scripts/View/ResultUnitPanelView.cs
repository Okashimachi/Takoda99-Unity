// リザルト画面の成績1項目ぶんのパネル。項目名と内容を注入するだけの受け身のビュー。

using TMPro;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>ResultUnitPanel プレハブにアタッチし、成績の「項目名」と「内容」を表示する。</summary>
    public sealed class ResultUnitPanelView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI unitTitleText;
        [SerializeField] private TextMeshProUGUI unitParamText;

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
    }
}
