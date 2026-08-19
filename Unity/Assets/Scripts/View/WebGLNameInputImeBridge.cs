// WebGL（Unityroom等）では TMP_InputField はブラウザ上のHTML要素ではなく、Unity が keydown を
// Input.inputString に流し込むだけなので、IME の変換中（composition イベント）が一切届かない。
// = 日本語変換ができない。これは Unity WebGL 全般の既知の制限で、Unity 6 でも直っていない。
//
// 対策として WebGLInput（kou-yeung/WebGLInput, MIT, UPM: com.github.kou-yeung）を使う。
// キャンバスに透明な HTML <input> を重ね、ブラウザネイティブの入力（＝IMEが効く）を
// TMP_InputField に同期する。文字数上限は TMP_InputField.characterLimit をそのまま尊重する。
//
// WebGLInput はシーンに置かず、ここから実行時に付ける。シーン側へのアタッチ漏れを防ぐためで、
// 付ける先は MatchmakingScreenView が握っている入力欄そのもの。
// エディタ/他プラットフォームでは何もしない（TMP_InputField 標準の入力に任せる）。

using TMPro;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>WebGL ビルドでだけ、名前入力欄に WebGLInput を付けて日本語入力（IME）を成立させる。</summary>
    public static class WebGLNameInputImeBridge
    {
        /// <summary>入力欄に IME 対応を取り付ける。WebGL 以外・二重呼び出しでは何もしない。</summary>
        public static void Attach(TMP_InputField field)
        {
            if (field == null)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // UnityEngine.WebGLInput と同名なので必ず名前空間付きで書く。
            if (field.GetComponent<WebGLSupport.WebGLInput>() == null)
            {
                field.gameObject.AddComponent<WebGLSupport.WebGLInput>();
            }
#endif
        }
    }
}
