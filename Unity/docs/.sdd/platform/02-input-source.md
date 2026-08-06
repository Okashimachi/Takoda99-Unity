# 02-UnityInputSource

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`IInputSource`）／[03-typing-judge.md](../../../../pureC%23/docs/.sdd/03-typing-judge.md)（`TypingJudge.PressKey(char)` の入力形）。矛盾したら上流優先。

`Takoda99.Client.Lifecycle.IInputSource` の Unity 実体。Input System から文字キーのみを取り出す。

## 1. 責務

- Input System のキーボード入力を、`TypingJudge.PressKey(char)` がそのまま受け取れる `char` に正規化して `OnCharKey` で流す
- **しない**こと：
  - 打鍵の正誤判定（`TypingJudge` の責務）
  - IME・日本語入力の変換（ローマ字入力前提。用語集の通り「ローマ字のみ受理」）
  - ゲームパッド・タッチ等の入力（対象外。Takoda99-Docs `03_Unity仕様書` の対象デバイスはPCブラウザキーボードのみ）

## 2. 公開インターフェース

```csharp
namespace Takoda99.InputSource
{
    /// <summary>IInputSource の Unity 実体。Input System の文字入力イベントを使う。</summary>
    public sealed class UnityInputSource : MonoBehaviour, IInputSource
    {
        public event Action<char> OnCharKey;
    }
}
```

## 3. Unity構成

- **使用するUnityパッケージ**：Input System（`com.unity.inputsystem`、`manifest.json` に導入済み）
- **MonoBehaviour のライフサイクル**
  - `OnEnable`：`UnityEngine.InputSystem.Keyboard.current.onTextInput += HandleTextInput` を購読
  - `OnDisable`：購読解除
  - `Update` / `Awake`：使わない
- Input Action Asset は作らない。`Keyboard.current.onTextInput` はブラウザの `keypress` 相当のテキスト入力イベントで、シフト・IME・キーリピートをOS/ブラウザ側で解決済みの `char` を渡してくるため、Action Map を介した個別キーの割り当てよりこの用途に適する

## 4. ふるまいの詳細

- `onTextInput` で受け取った `char` をそのまま `OnCharKey` へ転送する。フィルタリング（英数字のみ等）は行わない — `TypingJudge.PressKey` 側が制御文字を無視し、ローマ字パターンに合わない文字は `Miss` として扱う契約になっている（[03-typing-judge.md](../../../../pureC%23/docs/.sdd/03-typing-judge.md)）ため、ここで二重に判定しない
- `Keyboard.current` が `null`（キーボード未接続・フォーカス外）の間は購読自体が成立しないため、`OnEnable` 時に `null` なら何もしない。以後は起動時点の判定のみで、動的な接続検知は行わない（未確定事項参照）
- `MatchClientController` 側が `Phase != InMatch` の間は `OnCharKey` を受け取っても無視する（[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) `HandleCharKey`）ため、本モジュールでは画面状態による on/off の切り替えを持たない

## 5. 依存関係

- 依存する `pureC#` モジュール：`Takoda99.Client.Lifecycle.IInputSource`
- 依存するUnity側モジュール：Input System（サードパーティではなく公式パッケージ）
- 依存されるモジュール：`MatchClientController`（`IInputSource` として注入される）
- `IInputSource` に依存してよいモジュールは `MatchClientController` のみ（Client-Docs 第3章）

## 6. テスト・確認観点

`UnityEngine.InputSystem` 依存のため xUnit では検証できない。Unity Editor 実行で確認する。

- ローマ字キーを連打したとき、`OnCharKey` が押した順序通りに発火するか
- 大文字/小文字（Shift）が正しく区別されずに小文字化されて渡っても `TypingJudge` 側で吸収されるか（`PressKey` が `ToLowerInvariant` する契約）
- IME がオンの状態でも、確定前の変換候補文字ではなく実際のキー入力がそのまま届くか（ブラウザのIME挙動に依存。WebGLビルドで確認）

## 7. 未確定事項

- `Keyboard.current` の動的な着脱（後からキーボードが接続された場合）への対応要否
- モバイル/タッチデバイス対応の要否（現状PC専用の想定）
