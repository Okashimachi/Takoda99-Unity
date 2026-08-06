// 全テキストのフォントを ScriptableObject 1つで一括管理する。表示のみ（経営ロジックは持たない）。

using TMPro;
using UnityEngine;

namespace Takoda99.View.Typography
{
    /// <summary>
    /// 太さ（ウェイト）の種類。テキスト側はこの3種類から必要なものを指定する。
    /// </summary>
    public enum FontWeight
    {
        Light = 0,
        Normal = 1,
        Bold = 2,
    }

    /// <summary>
    /// 全テキストコンポーネントのフォントを一括管理するScriptableObject。
    /// フォントを差し替えるときはこのアセットの参照だけを変更する。
    /// </summary>
    [CreateAssetMenu(fileName = "FontTheme", menuName = "Takoda99/Font Theme")]
    public sealed class FontTheme : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _light;
        [SerializeField] private TMP_FontAsset _normal;
        [SerializeField] private TMP_FontAsset _bold;

        public TMP_FontAsset Light => _light;
        public TMP_FontAsset Normal => _normal;
        public TMP_FontAsset Bold => _bold;

        public TMP_FontAsset Resolve(FontWeight weight)
        {
            switch (weight)
            {
                case FontWeight.Light: return _light;
                case FontWeight.Bold: return _bold;
                default: return _normal;
            }
        }
    }
}
