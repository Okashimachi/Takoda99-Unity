// WebGL（Unityroom等）では TMP_InputField 自前のキャレット入力はブラウザ上のHTML要素ではないため、
// OSのIME（日本語変換）が素通りしない。Unity の WebGL 実装は TouchScreenKeyboard.Open() を呼ぶと
// 内部で本物の HTML input/textarea を生成してそちらに入力を委譲するため、そこ経由でだけIMEが機能する。
// TouchScreenKeyboard.isSupported はデスクトップブラウザでは false になり得るため、判定を待たず
// 明示的に開く。エディタ/他プラットフォームでは何もせず、TMP_InputField 標準の入力に任せる。

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Takoda99.View
{
    /// <summary>WebGL ビルドでだけ、TMP_InputField の入力を TouchScreenKeyboard 経由に切り替えてIMEを成立させる。</summary>
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class WebGLNameInputImeBridge : MonoBehaviour, ISelectHandler
    {
        /// <summary>MatchmakingScreenView.DisplayNameInputLimit と同じ値を実行時に注入する（単一の情報源はそちら）。</summary>
        public int CharacterLimit { get; set; } = 6;

        private TMP_InputField field;

        private void Awake()
        {
            field = GetComponent<TMP_InputField>();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private TouchScreenKeyboard keyboard;

        public void OnSelect(BaseEventData eventData)
        {
            keyboard = TouchScreenKeyboard.Open(
                field.text,
                TouchScreenKeyboardType.Default,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false,
                textPlaceholder: "",
                characterLimit: CharacterLimit);
        }

        private void Update()
        {
            if (keyboard == null)
            {
                return;
            }

            var text = keyboard.text ?? string.Empty;
            if (text.Length > CharacterLimit)
            {
                text = text.Substring(0, CharacterLimit);
            }

            if (field.text != text)
            {
                field.text = text;
            }

            if (keyboard.status == TouchScreenKeyboard.Status.Done
                || keyboard.status == TouchScreenKeyboard.Status.Canceled
                || keyboard.status == TouchScreenKeyboard.Status.LostFocus)
            {
                keyboard = null;
            }
        }
#else
        public void OnSelect(BaseEventData eventData)
        {
        }
#endif
    }
}
