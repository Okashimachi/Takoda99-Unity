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

        private static readonly Color SelfColor = new Color32(0xE0, 0x4A, 0x4A, 0xFF);
        private static readonly Color DefaultColor = Color.white;

        public void Apply(string displayName, bool isSelf)
        {
            if (nameText != null)
            {
                nameText.text = displayName;
            }

            if (panelImage != null)
            {
                panelImage.color = isSelf ? SelfColor : DefaultColor;
            }
        }
    }
}
