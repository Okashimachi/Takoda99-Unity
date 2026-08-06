# 02-シーン構成と結線

> 参照する上流：[Takoda99-Docs 03_Unity仕様書](https://github.com/Okashimachi/Takoda99-Docs/blob/main/04_クライアント仕様/03_Unity仕様書.md)／[08](../match-view/02-main-store-view.md)〜[10](../match-view/04-sub-store-board-view.md)（試合画面View）／[01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md)（マッチング画面）。矛盾したら上流優先。

**`.unity` / `.prefab` アセットの作成はUnityエディタでの人手作業。** 本ドキュメントは、そこに何を置き何を結線するかの方針と、コード側の遷移ロジックを定める。

## 1. 責務

- シーン構成・GameObject階層・結線順序・シーン遷移の起点を定める
- **しない**こと：`.unity`/`.prefab` アセットそのものの作成（エディタ作業）

## 2. シーン構成（5シーン・確定）

画面ごとにシーンを分ける。接続・`Store`・`MatchClientController` はシーンをまたいで生存させる必要があるため、それらを持つ `GameBootstrapper` を `Boot` シーンに置き、`DontDestroyOnLoad` で生かし続ける。

| # | シーン | 役割 | 遷移の起点 |
|---|---|---|---|
| 0 | `Boot` | 生成と結線のみ。**接続はしない**（§4） | 起動 |
| 1 | `Title` | Start ボタン | `ClientPhase.Title` |
| 2 | `MatchiMaking` | 名前入力 → 待機 → マッチング（3パネル） | Title の Start ボタン（**フェーズ変化ではない**） |
| 3 | `MainGame` | 試合画面 | `ClientPhase.InMatch` |
| 4 | `Result` | リザルト（未設計） | `ClientPhase.Result` |

**Build Settings の Scenes In Build にこの順で登録済み。`Boot` が index 0。** シーン名は `GameBootstrapper` の Inspector 値（`titleSceneName` / `matchmakingSceneName` / `matchSceneName` / `resultSceneName`）と一致させる。

```
Boot
├── Main Camera / Global Light 2D
├── BootStrap                       ← GameBootstrapper（DontDestroyOnLoad 対象）
│   ├── Net                         ← WebGLNetworkClient
│   ├── Input                       ← UnityInputSource
│   └── DebugCanvas                 ← DebugPanel（03-debug-panel.md）
│       ├── DebugPanel              （既定非表示）
│       └── DebugButton             （常時表示・右上）
└── EventSystem
```

**`Net` / `Input` / `DebugCanvas` は `BootStrap` の子にする。** `DontDestroyOnLoad` は呼び出したGameObjectの階層ごと保護するため、子にしておけば自動で生存する。**独立させると `Boot` シーンごと破棄され、以後 `WebGLNetworkClient` の接続イベントが失われる。**

## 3. シーン遷移

`GameBootstrapper.HandlePhaseRouting` が `Store` の `Phase` 変化を購読して切り替える。

| `ClientPhase` | 遷移先 |
|---|---|
| `Title` | `Title` |
| `InMatch` | `MainGame` |
| `Result` | `Result` |
| `Connecting` / `Matchmaking` | **遷移しない**（下記） |
| `Spectating` | **遷移しない**（脱落後も観戦のため試合画面に留まる。[matchmaking/01](../matchmaking/01-matchmaking-flow.md) §8.3） |

**`Connecting` / `Matchmaking` でシーンをロードしてはいけない。** これらのフェーズに入る時点で既に `MatchiMaking` シーンにいる（Title の Start ボタンでロード済み）。ここでロードすると、**入力済みの名前ごと画面が作り直される**。

`Title` → `MatchiMaking` だけはフェーズ変化ではなく**ボタン操作**が起点になる（`GameBootstrapper.GoToMatchmaking()`）。名前入力が接続より先に来るため、シーンが変わってもフェーズはまだ `Title` のままだからである（§4）。

## 4. ★ `Boot` では接続しない

**`Boot` が行うのはオブジェクトの生成と結線だけ。実接続はしない。**

サーバーは接続を受けると最初の1メッセージを**最大3秒**しか待たず、それを過ぎると表示名が空になりフォールバック名が割り当たる（[matchmaking/01](../matchmaking/01-matchmaking-flow.md) §4.1）。したがって接続は「名前が確定した瞬間」まで遅らせる必要がある。

```
Boot（生成のみ）
  → Title（Start ボタン）
  → MatchiMaking / WriteNameModal（名前入力）
  → Decide 押下 ＝ 名前確定 → ★ここで初めて Connect
  → 接続確立と同時に MatchmakingJoin 送信
```

「Bootで通信の疎通確認をしてから進む」という設計は、**この制約と両立しない**ため採らない。疎通の確認はデバッグパネル（[05](../platform/03-debug-panel.md)）で行う。

## 5. 各Viewの自己登録（Pull型の結線）

`MatchmakingScreenView` / `Takoda99.View.Renderer` は、**Inspector で `GameBootstrapper` への参照を貼らない**（シーンをまたぐ参照はUnityでは貼れない）。自分の `OnEnable` で `GameBootstrapper.Instance` から `Store` / `Dispatcher` / `TypingJudge` を取得する。

- `Renderer` はさらに `AttachRenderer(this)` / `DetachRenderer(this)` を呼び、`MatchClientController` が持つ `IRenderer`（内部の `RendererProxy`）の転送先として自己登録する。**試合シーンが未ロードの間は転送先が `null` のため、離散イベントは無害に捨てられる**
- `DebugPanel` だけは `Boot` シーン内に常駐するため、`GameBootstrapper.Awake` から直接 `Bind(log)` される（同一シーンなので Inspector 参照で足りる）

## 6. Inspector 配線チェックリスト

### `Boot/BootStrap` の `GameBootstrapper`

| フィールド | 割り当て |
|---|---|
| `webSocketUrl` | ローカル開発用のURL（**本番URLはコミットしない**） |
| `titleSceneName` / `matchmakingSceneName` / `matchSceneName` / `resultSceneName` | `Title` / `MatchiMaking` / `MainGame` / `Result` |
| `networkClient` | `BootStrap/Net` |
| `inputSource` | `BootStrap/Input` |
| `debugPanel` | `BootStrap/DebugCanvas` ← **未割り当て（要対応）** |

### `Boot/BootStrap/DebugCanvas` の `DebugPanel`

`panelRoot` = `DebugPanel` / `toggleButton` = `DebugButton` / `copyButton` = `DebugPanel/Copy` / `logText` = `DebugPanel/Text (TMP)`

### `Title/Canvas/Start`（Button）

`TitleScreenView`（§7）を `Title/Canvas` にアタッチし、`startButton` に `Start` を割り当てる。**`OnClick` に `GameBootstrapper` を直接指定することはできない**（別シーンにあるため Inspector から参照できない）。

### `MatchiMaking/MatchMakingCanvas` の `MatchmakingScreenView`（未アタッチ）

3パネル・`NameInputField` / `Decide` / `Timer` の Text / `PaticipantsNumPanel` の Text を割り当てる。

### `MainGame` の `Renderer`（未アタッチ）

`mainStore` / `subStoreBoard` / `patienceTimer` を同一シーン内から割り当てる（[01-renderer.md](../match-view/01-renderer.md) §3）。

## 7. `TitleScreenView`（Title シーン）

Start ボタン1つだけの画面。**シーン遷移そのものは `GameBootstrapper` の責務**であり、本コンポーネントは押下を転送するだけ。

```csharp
namespace Takoda99.View
{
    /// <summary>Title シーン。Start ボタンをマッチングシーンへの遷移に繋ぐだけ。</summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private Button startButton;
    }
}
```

### ふるまい

- `OnEnable`：`startButton.onClick` を購読する
- 押下 → `GameBootstrapper.Instance.GoToMatchmaking()`
- `OnDisable`：購読解除
- `GameBootstrapper.Instance` が `null`（`Boot` シーンを経ずに `Title` から再生した）場合は `Debug.LogError` を出してボタンを無効化する。**黙って無反応にしない**（原因が分からなくなるため）

### ★ここで接続しない

Start ボタンは**シーンを切り替えるだけ**で、`BeginPlay()` を呼ばない。接続は表示名が確定してから（[01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md) §8.5）。**ここで接続すると、名前入力に3秒以上かかった時点で表示名が失われる。**

### 実装

`Assets/Scripts/View/TitleScreenView.cs`（**未実装**）

## 8. 未確定事項

- **`TitleScreenView`（§7）／`MatchmakingScreenView`／`Renderer` が各シーンに未アタッチ。** スクリプトはあるが（`TitleScreenView` は未作成）、シーン側の配線が済んでいない
- **`Result` シーンの設計**（`MatchEnd` 受信後の表示内容）。シーンは存在するが中身が無い
- `LeaveMatchmaking()` / `BackToTitle()` 実行時の遷移（現状 `Title` フェーズへ戻れば `Title` シーンがロードされるが、導線が画面に無い）
- `MainGame` シーンに残っている `MainGameViewSampleDriver`（[11](../match-view/06-view-sample-data.md)）と `Renderer` の共存。実データ駆動に切り替える際に無効化が要る
