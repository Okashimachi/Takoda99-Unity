# 06-MatchClientController（統括・ライフサイクル）

> 参照する上流：[Takoda99-Client-Docs 第7章 全体](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/07_ライフサイクルと画面遷移.md)（phase 状態機械が正典）／[第3章 §1・§3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（統括層の責務・`IRenderer`/`IInputSource`）。矛盾したら上流優先。

> **このモジュールは最後に実装する。** 他5本がすべて揃ってから結線するため（[README §2](./README.md) の実装順）。

## 1. 責務

**する：**
- ライフサイクル状態機械（`Boot → Title → Connecting → Matchmaking → InMatch →（Spectating）→ Result`）の駆動。
- 各モジュール（`NetworkClient` / `Dispatcher` / `Store` / `TypingJudge` / `SendQueue`）の**結線**。
- 入力（`OnCharKey`）を `TypingJudge` へ流し、判定結果を Action として `Store` へ、`OrderCleared` を `OrderServed` 送信へつなぐ**起点**。
- `Renderer` への**イベント通知**（継続値は Store 購読で描くため、ここで通知するのは離散イベントのみ）。

**しない：**
- **経営ロジック**（すべてサーバー権威）。
- 状態の直接書き換え（必ず `Store.Apply` 経由）。
- 打鍵の判定そのもの（→ [03-typing-judge](./03-typing-judge.md)）。
- 描画・入力の**実装**（`IRenderer` / `IInputSource` は IF のみ。実体は Unity 側）。

## 2. 公開インターフェース

```csharp
namespace Takoda99.Client.Lifecycle;

/// <summary>描画への離散イベント通知。実体は Unity 側（第3章 §3）。</summary>
public interface IRenderer
{
    void OnCustomerArrived(CustomerView customer);
    void OnCustomerLeft(string customerId, LeaveReason reason);
    void OnKeyFeedback(KeyResult result);                    // 正打/ミスの即時演出
    void OnOrderServed(string customerId);                   // 提供演出
    void OnPhaseChanged(Phase phase);
    void OnForcedEliminationWarning(int untilTick, double thresholdPct);
    void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank);
    void OnMatchEnd(int finalRank, MatchStats stats);
    void OnLifecycleChanged(ClientPhase from, ClientPhase to);
    void OnConnectionTrouble(string kind);
}

/// <summary>入力の抽象。実体は Unity 側（Input System で文字キーのみへ正規化）。</summary>
public interface IInputSource
{
    event Action<char> OnCharKey;
}

public interface IMatchClientController
{
    ClientPhase Phase { get; }

    /// <summary>ブートストラップ設定を受けて開始する（Boot → Title）。</summary>
    void Start(BootstrapConfig config);

    /// <summary>プレイ開始操作（Title → Connecting）。</summary>
    void BeginPlay();

    /// <summary>キュー離脱操作（Matchmaking → Title）。</summary>
    void LeaveMatchmaking();

    /// <summary>「もう一度」操作（Result → Connecting・接続を張り直す）。</summary>
    void Rematch();

    /// <summary>タイトルへ戻る操作（Result → Title）。</summary>
    void BackToTitle();

    void Dispose();
}
```

- 継続値（信用・評価・順位・生存数・火力・行列・注文進捗・我慢ゲージ残量）は `IRenderer` のメソッドではなく **`IStore` の購読**で描く（[第3章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）。

## 3. ふるまいの詳細

### 3.1 phase 遷移トリガー（[第7章 §2](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/07_ライフサイクルと画面遷移.md) が正典）

| From → To | トリガー |
|---|---|
| Boot → Title | ブートストラップ設定読込完了・バージョン OK |
| Title → Connecting | `BeginPlay()` |
| Connecting → Matchmaking | 接続確立 → `MatchmakingJoin` 送信 |
| Matchmaking → InMatch | `MatchStart` 受信 |
| InMatch → Spectating | **自店の** `StoreEliminated` 受信（試合は継続中） |
| InMatch / Spectating → Result | `MatchEnd` 受信 |
| Result → Connecting | `Rematch()`（**接続を張り直す**） |
| Result → Title | `BackToTitle()` |
| 任意 → Connecting | 接続断からの再接続開始 |

- **自店の脱落と試合終了は別イベント。** `StoreEliminated`（自店）で `Spectating` へ、`MatchEnd` で `Result` へ。**`StoreEliminated` を受けて直接 `Result` に飛ばさない**（最後の1店が確定するまで観戦する）。
- **再マッチは接続の張り直し**（1試合1接続）。既存接続を再利用して新試合に入る設計にしない。

### 3.2 打鍵〜提供の結線（これが本モジュールの中心）

```
IInputSource.OnCharKey(c)
  → phase が InMatch でなければ捨てる（Spectating/Result では入力を止める）
  → TypingJudge.PressKey(c)
      ├ Correct / Miss  → Store.Apply(LocalKeyJudged) → Renderer.OnKeyFeedback
      ├ WordCleared     → 同上（wordIndex 更新）
      └ OrderCleared    → ① TypingJudge.BuildReport()
                          ② Store.Apply(LocalOrderCleared)   ← 先に CurrentOrder を null に
                          ③ SendQueue.Enqueue("OrderServed", report)
                          ④ Renderer.OnOrderServed
```

- **②→③の順序を守る**（`CurrentOrder` を `null` にしてから送信＝二重送信を作らない。[05-dispatcher §3.3 冪等性](./05-dispatcher.md)）。
- `OrderServed` は **`InMatch` のみ**送る。

### 3.3 客の対応開始／中断

| 契機 | 挙動 |
|---|---|
| `CustomerArrived` で行列が空→1件目、または提供・離脱で先頭が入れ替わった | 新しい先頭に対して `TypingJudge.BeginOrder(customerId, words)` ＋ `Store.Apply(LocalOrderBegan)` |
| **対応中の客**が `CustomerLeft` | `TypingJudge.AbortOrder()`（**`OrderServed` を送らない**）→ 次の先頭で対応開始 |
| 待機中の客が `CustomerLeft` | 行列から除去するだけ（対応中の判定に影響させない） |
| `Spectating` へ遷移 | `InputSource` を止め、`TypingJudge` を `Idle` に固定（`AbortOrder()`） |

- **並行対応はしない**（直列・1客ずつ）。

### 3.4 接続断時
- `OnConnectionChanged` を受けて `Store.Apply(LocalConnectionChanged)` し、`Renderer.OnConnectionTrouble(kind)` で通知する。
- 試合中の切断では**入力を停止**する。再接続の試行そのものは [05-dispatcher §3.4](./05-dispatcher.md) の責務。
- **再接続中も最後の state を保持**する（画面を空にしない）。

### 3.5 ブートストラップ
```csharp
public sealed class BootstrapConfig
{
    public string WebSocketUrl { get; init; } = "";  // コード直書き禁止。Unity 側の設定から渡す
    public string ProtoVersion { get; init; } = "";  // ビルド時定数
    public bool DevMode { get; init; }               // デバッグパネル・モック導線の有効化
}
```
- **接続先 URL とバージョンゲートだけはクライアント自身のブートストラップ設定**として持つ（クライアントは外部DBを直接取得しない、の唯一の例外。[第5章 §5.1](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）。

## 4. 依存関係

- 依存するモジュール：**全モジュール**（[01](./01-contract.md) / [03](./03-typing-judge.md) / [04](./04-store-reducer.md) / [05](./05-dispatcher.md)、`IClock`）
- 依存されるモジュール：Unity 側の起動スクリプト（`MonoBehaviour` がこれを生成・保持する）
- `IRenderer` / `IInputSource` は**このモジュールが IF を定義し、Unity が実装する**（[第3章 §2 ルール2](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)：これらに依存してよいモジュールは無く、統括層が IF 経由で保持するのみ）

## 5. テスト観点

| # | ケース | 期待 |
|---|---|---|
| 1 | phase 遷移の全経路 | §3.1 の表どおりに遷移する |
| 2 | 自店 `StoreEliminated` | `Spectating` へ（`Result` へ飛ばない） |
| 3 | 他店 `StoreEliminated` | phase が変わらない |
| 4 | `MatchEnd`（InMatch から） | `Result` へ |
| 5 | `MatchEnd`（Spectating から） | `Result` へ |
| 6 | `OrderCleared` の順序 | `LocalOrderCleared` → `OrderServed` Enqueue の順 |
| 7 | 同一客への二重 `OrderServed` | 送られない |
| 8 | `Spectating` 中の打鍵 | `TypingJudge` に届かない・`OrderServed` が送られない |
| 9 | 対応中の客の `CustomerLeft` | `AbortOrder` され `OrderServed` が送られない |
| 10 | 提供後の先頭入れ替わり | 次の客に `BeginOrder` される |
| 11 | `Rematch()` | 接続が張り直される（既存接続を再利用しない） |
| 12 | 接続断 | 入力が止まり `OnConnectionTrouble` が発火する |

> `INetworkClient` / `IRenderer` / `IInputSource` / `IClock` はすべてフェイクを用意し、Unity なしで全経路をテストする。

## 6. 未確定事項

- 同一試合への再接続復帰の可否（サーバー仕様待ち。入ったら §3.4 と第7章 §5 を同時に更新）。
- `Spectating` を独立画面にするか（Unity 側の裁量。共通契約は phase 区別と入力停止のみ）。
- `Title` でのモード選択・表示名入力の具体（アート未確定）。なお現行 proto の `MatchmakingJoin` は**空ペイロード**で表示名を持たないため、表示名入力を入れるなら Proto の人間承認フローが要る。
- `--mode solo` / モックモードの結線方法（[第8章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/08_エラーとオフラインと開発モード.md)）。`INetworkClient` のフェイク実装を `DevMode` で差し込む形を想定。
