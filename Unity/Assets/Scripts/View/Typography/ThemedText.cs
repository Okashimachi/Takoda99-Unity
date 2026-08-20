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

        /// <summary>
        /// 差し替えたいマテリアルプリセット（ネオンの Glow 等）。未指定ならフォント既定のまま。
        /// <see cref="TMP_Text.font"/> への代入はマテリアルを既定へ戻すため、ここで必ず入れ直す。
        /// </summary>
        [SerializeField] private Material _materialPreset;

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

            var text = GetComponent<TMP_Text>();
            if (text == null) return;

            var font = _theme.Resolve(_weight);
            if (font != null && text.font != font)
            {
                text.font = font;
            }

            // font 代入の後で流すこと（代入時にフォント既定のマテリアルへ戻されるため）。
            if (_materialPreset != null && text.fontSharedMaterial != _materialPreset)
            {
                text.fontSharedMaterial = _materialPreset;
            }
        }
    }
}
