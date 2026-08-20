using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Paticipant プレハブ1個ぶんの表示。名前と自店強調の反映だけを行う。</summary>
    public sealed class PaticipantView : MonoBehaviour
    {
        [SerializeField] private Image panelImage;
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("ランダム色の彩度・明度の範囲。Panel の素材（takoyaki_neon_panel）は白地なので、" +
                 "彩度を上げすぎず明度を高いまま保つと『色のついたネオン』らしく見える。")]
        [SerializeField] private float randomColorSaturationMin = 0.45f;
        [SerializeField] private float randomColorSaturationMax = 0.7f;
        [SerializeField] private float randomColorValue = 1f;

        private static readonly Color SelfColor = new Color32(0xE0, 0x4A, 0x4A, 0xFF);

        /// <summary>この枠だけの色。生成時（Awake）に1回だけ決め、以後 Apply を何度呼んでも変えない。</summary>
        private Color randomColor;

        private void Awake()
        {
            var hue = Random.value;
            var saturation = Random.Range(randomColorSaturationMin, randomColorSaturationMax);
            randomColor = Color.HSVToRGB(hue, saturation, randomColorValue);
        }

        public void Apply(string displayName, bool isSelf)
        {
            if (nameText != null)
            {
                nameText.text = displayName;
            }

            if (panelImage != null)
            {
                panelImage.color = isSelf ? SelfColor : randomColor;
            }
        }
    }
}
