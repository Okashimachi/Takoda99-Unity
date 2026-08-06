// テキスト側で使うウェイトを指定し、FontTheme から実フォントを受け取る。表示のみ。

using TMPro;
using UnityEngine;

namespace Takoda99.View.Typography
{
    /// <summary>
    /// テキスト側で使いたいウェイトを指定し、<see cref="FontTheme"/> から実フォントを受け取る。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class ThemedText : MonoBehaviour
    {
        [SerializeField] private FontTheme _theme;
        [SerializeField] private FontWeight _weight = FontWeight.Normal;

        public FontWeight Weight
        {
            get => _weight;
            set
            {
                _weight = value;
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        private void Apply()
        {
            if (_theme == null) return;

            var font = _theme.Resolve(_weight);
            if (font == null) return;

            var text = GetComponent<TMP_Text>();
            if (text == null || text.font == font) return;

            text.font = font;
        }
    }
}
