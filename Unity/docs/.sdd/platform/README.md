# platform — プラットフォーム実体（pureC# のインターフェースをUnityで実装する）

`pureC#` 側が**インターフェースとしてだけ持っている**ものの、Unity 実体を置く。

## 1. このディレクトリの位置づけ

`pureC#` は Unity 非依存であるため、WebSocket 通信・キーボード入力といった**プラットフォームに触る部分をインターフェースで切り出し、実体をUnity側に置く**（[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）。ここに入るのはその実体である。

| ディレクトリ | 何を書くか |
|---|---|
| **`platform/`（ここ）** | 「`pureC#` の IF を Unity でどう実装するか」 |
| [`match-view/`](../match-view/README.md) | 「受け取った state をどう描くか」 |

> **`pureC#` 側のモジュール仕様をここに書かない。** `INetworkClient` / `IInputSource` が何を約束する型なのかは [`pureC#/docs/.sdd/`](../../../../pureC%23/docs/.sdd/README.md) が正典。ここに書くのは**Unity側の実装方法だけ**。

## 2. ファイル一覧

| # | ファイル | 実装する IF | 実装 | 状態 |
|---|---|---|---|---|
| 01 | [01-network-client.md](./01-network-client.md) | `Takoda99.Client.Net.INetworkClient` | `Assets/Scripts/Net/WebGLNetworkClient.cs` | ✅ |
| 02 | [02-input-source.md](./02-input-source.md) | `Takoda99.Client.Lifecycle.IInputSource` | `Assets/Scripts/Input/UnityInputSource.cs` | ✅ |
| 03 | [03-debug-panel.md](./03-debug-panel.md) | （IFなし。`IEnvelopeLog` を読むだけ） | `Assets/Scripts/Debug/DebugPanel.cs` | ✅ |

03 は IF の実体ではないが、**送受信した生JSONを見るための開発用モジュール**であり、通信・入力と同じ「プラットフォームに触る層」に属するためここに置く。

## 3. 共通の制約

### 3.1 WebGL 制約

- **`System.Net.WebSockets` を使わない。** WebGLビルドで動かないため、`NativeWebSocket` 経由にする（01）
- **`Thread` 前提の非同期を書かない。** WebGL はシングルスレッド。`async/await` を IF の契約に持ち込まず、コールバック／イベントで表現する
- WebGL で動かない API（`GUIUtility.systemCopyBuffer` 等）を使う場合、**各仕様書の「未確定事項」に必ず書き残す**（03 のコピー機能がこれに当たる）

### 3.2 名前空間

**`Takoda99.Debug` / `Takoda99.Input` という名前空間を作らない。** `UnityEngine.Debug` / `UnityEngine.Input` と衝突し、**`Takoda99.*` の他の名前空間から `Debug.LogError` を呼んでいる箇所が軒並み `CS0104`（曖昧な参照）になる**（実機で発生済み）。`Takoda99.DebugUI` / `Takoda99.InputSource` を使う。

### 3.3 テスト

`UnityEngine` に依存するため **xUnit（`Unity/tests/`）では検証できない。** 各仕様書の「テスト・確認観点」は Unity Editor 実行／WebGLビルドでの確認手順として書く。純粋関数として切り出せる判定は [`value-objects/`](../value-objects/README.md) 側へ置き、そちらでテストする。
