# 01-WebGLNetworkClient

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`INetworkClient`）／[Takoda99-Docs 03_Unity仕様書](https://github.com/Okashimachi/Takoda99-Docs/blob/main/04_クライアント仕様/03_Unity仕様書.md) §3（WebGL通信制約）／[05-dispatcher.md](../../../../pureC%23/docs/.sdd/05-dispatcher.md)（`INetworkClient` / `ISendQueue` の使われ方）。矛盾したら上流優先。

`Takoda99.Client.Net.INetworkClient`（[01-purecs-dll-reference.md](../foundation/01-purecs-dll-reference.md) で参照する DLL 側のインターフェース）の Unity 実体。WebSocket 通信そのものを持つ。

## 1. 責務

- `INetworkClient` を実装し、実際の WebSocket 接続・送受信を行う
- 受信した生JSONを `OnReceiveRaw` でそのまま流す（デコードしない。デコードは `Dispatcher` の責務）
- 接続状態の変化を `OnConnectionChanged` で通知する
- **しない**こと：
  - Envelope のエンコード／デコード（`Contract` の責務。`Send` は type/payload をそのまま渡され、呼び出し元（`SendQueue`）が既に JSON を組み立てている前提には**しない**——本実装は `EnvelopeCodec.EncodeEnvelope` を自前で呼んで JSON 化する）
  - 再接続の判断・待機列への復帰判断（`MatchClientController` の責務。本モジュールは「今の接続状態」を伝えるだけ）
  - メッセージの送信順序保証（`ISendQueue` の責務）

## 2. 公開インターフェース

```csharp
namespace Takoda99.Net
{
    /// <summary>INetworkClient の Unity 実体。NativeWebSocket を使う。</summary>
    public sealed class WebGLNetworkClient : MonoBehaviour, INetworkClient
    {
        [SerializeField] private int reconnectDelayMs = 2000;
        [SerializeField] private int maxReconnectAttempts = 5;

        public ConnectionState State { get; }

        public void Connect(string url);
        public void Disconnect();
        public void Send(string type, object payload);

        public event Action<string> OnReceiveRaw;
        public event Action<ConnectionState, string?> OnConnectionChanged;
    }
}
```

## 3. Unity構成

- **MonoBehaviour のライフサイクル**
  - `Awake`：`EnvelopeCodec` のインスタンスを1つ持つ（送信JSONの組み立てにのみ使う）
  - `Update`：**使わない**。NativeWebSocket は自前のディスパッチループを持たないため、代わりに `Update` 内で `WebSocket.DispatchMessageQueue()` を呼ぶ（WebGLビルドでは no-op。エディタ/スタンドアロンでのみ必要）
  - `OnDestroy`：接続中なら `Disconnect()` を呼ぶ
- **使用するUnityパッケージ**：`NativeWebSocket`（`com.endel.nativewebsocket`、[manifest.json](../../../Packages/manifest.json) に追加済み）。WebGLビルドでは `System.Net.WebSockets` が使えないため必須
- **Inspector 公開値**：`reconnectDelayMs` / `maxReconnectAttempts`。接続先URLはここでは持たない（`BootstrapConfig.WebSocketUrl` 経由で `Connect(url)` に渡される。[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) 参照）

## 4. ふるまいの詳細

### 4.1 接続

- `Connect(url)` は `NativeWebSocket.WebSocket` を新規生成し、`OnOpen` / `OnMessage` / `OnClose` / `OnError` を購読してから `ConnectAsync()` を呼ぶ（WebGLは Task を待たない fire-and-forget。§4.4 参照）
- 呼び出し前の状態に関わらず、直前の `WebSocket` インスタンスが残っていれば先に破棄する（`Rematch()` 等で連続 `Connect` される想定。[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) の `Rematch`）
- `OnOpen` → `State = Connected`、`OnConnectionChanged(Connected, null)` を発火
- 接続確立前は `State = Connecting`

### 4.2 送信

- `Send(type, payload)` は `EnvelopeCodec.EncodeEnvelope(type, payload)` で JSON化し、`WebSocket.SendText(json)` を呼ぶ
- 接続していない状態で呼ばれた場合は何もしない（`SendQueue` が接続後にしか `Flush` しない前提のため、通常到達しない防御）

### 4.3 受信・切断

- `OnMessage(byte[] bytes)` → UTF-8 でデコードし `OnReceiveRaw` を発火
- `OnClose(code)` → `State = Disconnected`、`OnConnectionChanged(Disconnected, code.ToString())` を発火。**このモジュールは自動再接続しない**（再接続の判断は `MatchClientController` 側。[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) は現状 `Rematch()` が明示操作でのみ再接続する設計のため、当面は自動再接続を実装しない。将来 `Reconnecting` 状態を使う場合は本仕様書を更新する）
- `OnError(string message)` → `OnConnectionChanged(Failed, message)` を発火

### 4.4 WebGL固有の注意点

- WebGL ビルドでは `Thread` が使えないため、`NativeWebSocket` の `ConnectAsync()` は内部的にブラウザの `WebSocket` API を呼ぶだけで、実際の非同期待機はブラウザ側イベントループに委ねられる。**この Task を `await` で待ち受けない**（呼びっぱなしにする）。エディタ/スタンドアロンでは実際に別スレッドで待つため挙動は同じだが、`Connect` 呼び出し側を Task から `async void` にしない（`INetworkClient.Connect` の契約が `void` のため）
- `WebSocket.DispatchMessageQueue()` は WebGL では no-op（ブラウザのイベントループが直接コールバックを呼ぶため）。エディタ/スタンドアロンでは受信キューを消化するために毎フレーム呼ぶ必要がある。**呼び忘れるとエディタでのみ受信イベントが発火しない**という事故になるため、プラットフォーム分岐せず常に呼ぶ

## 5. 依存関係

- 依存する `pureC#` モジュール：`Takoda99.Client.Net.INetworkClient` / `ConnectionState`（`Takoda99.Client.State`）／`Takoda99.Client.Contract.EnvelopeCodec`（送信JSONの組み立てにのみ使う）
- 依存するUnity側モジュール：`NativeWebSocket`（サードパーティ）
- 依存されるモジュール：`MatchClientController`（`INetworkClient` として注入される。[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md)）
- `INetworkClient` に依存してよいモジュールは `Dispatcher`/`SendQueue`/`MatchClientController` のみ（Client-Docs 第3章）。本モジュールから `Store`/`Dispatcher` を直接参照しない

## 6. テスト・確認観点

`UnityEngine`/`NativeWebSocket` 依存のため xUnit では検証できない。Unity Editor 実行と WebGLビルドの両方で確認する。

- ローカル起動したサーバー（`--mode solo`）に接続し、`OnConnectionChanged(Connected, null)` が発火するか
- 送信した `MatchmakingJoin` がサーバーログに届くか（デバッグパネル併用）
- サーバーを落としたときに `OnClose` 経由で `OnConnectionChanged(Disconnected, ...)` が発火するか
- WebGLビルドで `DispatchMessageQueue` を呼んでいなくても受信できる（＝呼んでも副作用が無い）ことを確認する

## 7. 未確定事項

- 自動再接続の要否・再試行間隔（[01-matchmaking-flow.md 未確定事項](../matchmaking/01-matchmaking-flow.md#11-未確定事項) と同じ未解決事項）。現状は明示的な `Rematch()` 操作のみ
- `reconnectDelayMs` / `maxReconnectAttempts` は将来の自動再接続実装のために先置きした Inspector 値。現状は未使用（配線されていない）
