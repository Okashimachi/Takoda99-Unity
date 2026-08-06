# 05-デバッグパネル

> 参照する上流：[02-Unity実装ルール.md](../../../../docs/rules/02-Unity実装ルール.md) §4（デバッグパネルを最初に作る）／[05-dispatcher.md](../../../../pureC%23/docs/.sdd/05-dispatcher.md)（`IEnvelopeLog`）。矛盾したら上流優先。

送受信した `Envelope` の生JSONをそのまま整形表示するパネル。Textro99-WebFront の `RawStateDebugPane` を実装の参考にする（正典ではない。用語・型は流用しない）。

## 1. 責務

- `IEnvelopeLog.Entries`（直近N件の送受信JSON）を画面に一覧表示する
- AIが生成したコードにバグがあっても、**サーバーからの正データをここで常に確認できる**ようにする（「サーバー側のバグか表示のバグか」を切り分ける目的。§4 rule参照）
- **しない**こと：JSONの編集・再送信（読み取り専用。デバッグ用の疎通確認メッセージ送信機能は持たない。[01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md) §4.1 の「他のどのメッセージよりも先に `MatchmakingJoin` を送る」を壊しかねないため）

## 2. 公開インターフェース

> 名前空間は `Takoda99.Debug` にしない。`UnityEngine.Debug`（`Debug.LogError` 等）と名前が衝突し、**`Takoda99.*` の他の名前空間から `Debug.LogError`/`LogWarning` を呼んでいる箇所が軒並み `CS0104`（曖昧な参照）になる**（実機で確認済み）。同じ理由で `UnityInputSource` の名前空間も `Takoda99.Input` ではなく `Takoda99.InputSource` にしてある（[02-input-source.md](./02-input-source.md)）。

```csharp
namespace Takoda99.DebugUI
{
    /// <summary>送受信 Envelope の生JSON表示パネル（03-debug-panel.md）。</summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;        // DebugPanel（トグル対象）
        [SerializeField] private Button toggleButton;         // DebugButton（常時表示）
        [SerializeField] private Button copyButton;           // DebugPanel/Copy
        [SerializeField] private TextMeshProUGUI logText;     // DebugPanel/Text (TMP)
        [SerializeField] private int maxDisplayedEntries = 50;

        /// <summary>GameBootstrapper から IEnvelopeLog を注入する。</summary>
        public void Bind(IEnvelopeLog log);

        public void Toggle();    // DebugButton から
        public void CopyAll();   // Copy ボタンから
    }
}
```

## 3. Unity構成

### 3.1 シーン階層

```
Boot/BootStrap/DebugCanvas          ← DebugPanel をアタッチ
├── DebugPanel                      （既定非表示。トグル対象）
│   ├── Panel                       （下地）
│   ├── Text (TMP)                  （ログ本文）
│   └── Copy                        （Button）
└── DebugButton                     （Button・常時表示・右上）
```

`DebugCanvas` は `BootStrap`（`DontDestroyOnLoad`）の子なので、**シーンをまたいでも生存し、どの画面でも右上のボタンから開ける**（[02-scene-composition.md](../foundation/02-scene-composition.md) §2）。

### 3.2 MonoBehaviour のライフサイクル

- `Awake`：`panelRoot` を非表示にし、`toggleButton` / `copyButton` の `onClick` を購読する
- `Update`：**表示中のみ** `Entries` を再描画する（非表示中は文字列を組み立てない）
- `OnDestroy`：`onClick` の購読解除

**キーボードショートカット（旧 F1）は廃止した。** 打鍵がそのままゲーム入力になるため（[02-input-source.md](./02-input-source.md)）、キーでのトグルは誤爆する。ボタンのみとする。

- **Inspector 公開値**：`panelRoot` / `toggleButton` / `copyButton` / `logText` / `maxDisplayedEntries`

## 4. ふるまいの詳細

- 表示中は `Update` のたびに `log.Entries`（新しい順）の先頭 `maxDisplayedEntries` 件を整形して `logText.text` に流し込む
- **`Copy` は表示中の件数ではなく、リングバッファに残っている全件をコピーする。** 切り分けのために丸ごと貼り付けたいのが通常のため、画面に出ている50件だけをコピーしても用を成さない
- 各エントリは `[方向] JSON` の形式（例：`[← IN] {"type":"MatchmakingStatus","payload":{...}}` / `[→ OUT] {"type":"MatchmakingJoin","payload":{}}`）で1行に収める（改行させない。長大なJSONでも横スクロール/折り返しはTMPに任せる）
- `EnvelopeLog` はリングバッファ（既定200件、[05-dispatcher.md](../../../../pureC%23/docs/.sdd/05-dispatcher.md) §3.6）のため、本パネルは古いものを自前で間引く必要はない
- `Bind` されていない間（`log == null`）は「未接続」の固定文言を表示する

## 5. 依存関係

- 依存する `pureC#` モジュール：`Takoda99.Client.Net.IEnvelopeLog`
- 依存するUnity側モジュール：なし
- 依存されるモジュール：なし（末端。`Bootstrap` から `Bind` される）

## 6. テスト・確認観点

`UnityEngine` 依存のため xUnit では検証できない。Unity Editor 実行で確認する。

- `F1` でパネルの表示/非表示が切り替わるか
- サーバーへ接続した状態で、送信した `MatchmakingJoin` と受信した `MatchmakingStatus` の両方が表示されるか
- 200件を超えて送受信しても表示がクラッシュ・極端に重くならないか

## 7. 未確定事項

- **WebGL で `Copy` が動くか未検証。** `GUIUtility.systemCopyBuffer` は WebGL ビルドでは機能しないとされる（ブラウザのクリップボードAPIはユーザー操作を伴うJS側の呼び出しが要る）。ボタン押下＝ユーザー操作なので `.jslib` プラグインを足せば実現できる見込みだが、**現状はエディタ／スタンドアロンでのみ動く前提**。WebGLビルドを最初に通すときに確認する
- 表示のフィルタリング（特定 `type` だけ表示する等）の要否
