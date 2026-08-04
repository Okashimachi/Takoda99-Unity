# 04-Store / Reducer（状態管理）

> 参照する上流：[Takoda99-Client-Docs 第4章 全体](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md)（`ClientState` の形・畳み込み表・楽観的更新の境界が正典）／[第2章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/02_アーキテクチャ選定.md)（MVU）。矛盾したら上流優先。

## 1. 責務

**する：**
- `ClientState`（クライアント唯一の可変状態）の保持と、購読者への変更通知。
- `Action` を畳み込んで次の `ClientState` を作る **純粋関数の Reducer**。
- 想定外の順序・未知 ID を**無視＋ログで継続**させる（クラッシュさせない）。

**しない：**
- **経営数値の算出**（客分配・評価・信用・脱落・下位淘汰・フェーズ・火力）。受信値を**そのまま格納するだけ**。
- `CreditUpdate.delta` からの自前加算、`StoreEliminated` を数えての生存数推定（[第4章 §4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md) 禁止事項）。
- ネットワークの知識（`Store` は `NetworkClient` / `Dispatcher` を知らない。[第3章 §2 ルール3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）。
- 我慢ゲージ残量の保持（`arrivedAtLocalMs` + `patienceMaxMs` からの**描画時の派生値**。[第4章 §2](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md)）。

## 2. 公開インターフェース

```csharp
namespace Takoda99.Client.State;

public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting, Failed }

public enum ClientPhase { Boot, Title, Connecting, Matchmaking, InMatch, Spectating, Result }

/// <summary>行列の1エントリ。CustomerView は Proto DTO をそのまま保持する（加工しない）。</summary>
public sealed class CustomerEntry
{
    public CustomerView View { get; init; } = new();
    /// <summary>表示専用カウントダウンの基準となるローカル受信時刻（単調ms）。</summary>
    public long ArrivedAtLocalMs { get; init; }
}

/// <summary>現在の注文（表示専用ローカル値）。</summary>
public sealed class CurrentOrder
{
    public string CustomerId { get; init; } = "";
    public int WordIndex { get; init; }   // x
    public int OrderCount { get; init; }  // N
    public int TypedLength { get; init; }
    public int MissCount { get; init; }
    public long StartedAtMs { get; init; }
}

/// <summary>クライアントの可変状態すべて。第4章 §2 の形に対応する。</summary>
public sealed class ClientState
{
    // 接続・ライフサイクル
    public ConnectionState Connection { get; init; }
    public ClientPhase Phase { get; init; }
    public string? LastError { get; init; }

    // マッチング
    public int WaitingCount { get; init; }
    public int MinPlayers { get; init; }
    public int? CountdownMs { get; init; }

    // 試合の同定・公開パラメータ
    public string MatchId { get; init; } = "";
    public string SelfStoreId { get; init; } = "";
    public GameParametersPublicSubset Params { get; init; } = new();
    public Phase MatchPhase { get; init; }
    public long StartedAtMs { get; init; }

    // 自店（すべて受信値。自前算出しない）
    public int CreditLife { get; init; }
    public double EvalRaw { get; init; }
    public double Normalized { get; init; }
    public int Rank { get; init; }
    public int AliveCount { get; init; }
    public int HeatLevel { get; init; }
    public bool Alive { get; init; }

    public IReadOnlyList<CustomerEntry> Queue { get; init; }        // 先頭＝対応中
    public CurrentOrder? CurrentOrder { get; init; }
    public IReadOnlyList<StoreSummary> Stores { get; init; }        // 99店概況
    public StormWarning? Storm { get; init; }
    public MatchResult? Result { get; init; }
    public IReadOnlyList<LogEntry> EventLog { get; init; }
}

/// <summary>すべての状態更新はこの型を通す。1 S2C メッセージ ＝ 1 Action。</summary>
public interface IAction { }

public interface IStore
{
    ClientState State { get; }
    void Apply(IAction action);
    /// <summary>購読解除は戻り値の Dispose で行う（イベント解除漏れを防ぐ）。</summary>
    IDisposable Subscribe(Action<ClientState> listener);
}

/// <summary>純粋関数。同じ (state, action) からは常に同じ結果を返す。</summary>
public static class Reducer
{
    public static ClientState Apply(ClientState state, IAction action);
}
```

- `ClientState` は **イミュータブル**（`init` のみ）とし、Reducer は新しいインスタンスを返す。差分検知と「誰がいつ書き換えたか分からない」事故の防止のため。
- `CustomerView` / `StoreSummary` / `GameParametersPublicSubset` / `Phase` / `MatchStats` は **Proto の型をそのまま使う**（再定義しない。[第3章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）。

## 3. ふるまいの詳細

### 3.1 S2C Action の畳み込み（[第4章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md) の表が正典）

**原則：1 S2C メッセージ ＝ 1 Action ＝ 1 reducer ケース。** 複数メッセージをまとめた合成 Action を作らない。

| Action | 更新箇所 |
|---|---|
| `MatchStartAction` | `MatchId` / `SelfStoreId` / `Params` / `MatchPhase` / `StartedAtMs`、`CreditLife = Params.InitialLife`、`Stores`、`Phase = InMatch`、`Queue` を空に |
| `CustomerArrivedAction` | `Queue` に push（`ArrivedAtLocalMs` 記録） |
| `CustomerLeftAction` | `Queue` から該当 ID を除去。対応中なら `CurrentOrder = null` |
| `CreditUpdateAction` | `CreditLife = life`（**`delta` は加算に使わない**） |
| `EvaluationUpdateAction` | `EvalRaw` / `Normalized` / `Rank` / `AliveCount` |
| `DifficultyUpdateAction` | `HeatLevel` |
| `PhaseChangeAction` | `MatchPhase` |
| `StoreListUpdateAction` | `Stores` を**全置換**、`AliveCount` |
| `ForcedEliminationWarningAction` | `Storm` |
| `StoreEliminatedAction` | `Stores` の該当店 `Alive = false`。自店なら `Alive = false`・`Phase = Spectating`・`CurrentOrder = null`・`Queue` を空に |
| `MatchEndAction` | `Result`、`Phase = Result` |
| `MatchmakingStatusAction` | `WaitingCount` / `MinPlayers` / `CountdownMs` |

### 3.2 ローカル Action

| Action | 更新箇所 |
|---|---|
| `LocalOrderBeganAction(customerId, orderCount)` | `CurrentOrder` を初期化 |
| `LocalKeyJudgedAction(result, view)` | `TypedLength` / `MissCount` / `WordIndex` |
| `LocalOrderClearedAction(customerId)` | `CurrentOrder = null`、`Queue` の先頭を除去（**提供済み。`CustomerLeft` は来ない**） |
| `LocalConnectionChangedAction(state, error?)` | `Connection` / `LastError` |
| `LocalLifecycleChangedAction(phase)` | `Phase` |

### 3.3 楽観的更新の境界（[第4章 §4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md)）

| 種類 | 楽観的更新 |
|---|---|
| 打鍵の正誤・`x/N`・`missCount` | **する（即時）** |
| 提供時の行列先頭の除去 | **する** |
| 我慢ゲージの表示減少 | **する（表示のみ。0 でも離脱扱いにしない）** |
| 評価・順位・生存数・信用・脱落・最終順位・フェーズ・火力 | **しない**（受信値のみ） |

### 3.4 整合性の扱い（[第4章 §5](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md)）

| 状況 | 挙動 |
|---|---|
| 未知の `customerId` の `CustomerLeft` | 無視（ログのみ）。エラーにしない |
| 対応中の客に重複 `CustomerArrived` | 後着を無視（ログのみ） |
| `MatchStart` 前の試合系メッセージ | 無視（受理判定は [05-dispatcher](./05-dispatcher.md) が担当） |
| `StoreListUpdate` に自店が含まれない | `Stores` のみ更新し、自店フィールドは書き換えない |

### 3.5 通知
- `Apply` の結果 state が**変化した場合のみ**購読者へ通知する（同値なら通知しない）。
- 通知中に購読者が `Apply` を呼ぶ**再入**は想定しない。呼ばれた場合は例外にせず、次のループで処理する（無限再帰を防ぐ）。

## 4. 依存関係

- 依存するモジュール：`Takoda99.Proto`（DTO）のみ
- 依存されるモジュール：[05-dispatcher](./05-dispatcher.md)、[06-match-client-controller](./06-match-client-controller.md)、Unity 側 `Renderer`
- **ネットワーク層を知らない**（Dispatcher が一方向に押し込む）

## 5. テスト観点

| # | ケース | 期待 |
|---|---|---|
| 1 | 各 Action の畳み込み | §3.1 / §3.2 の表どおりに更新される |
| 2 | Reducer の純粋性 | 同じ入力で同じ出力・入力 state を変更しない |
| 3 | `CreditUpdate` の `delta` | `Life` の絶対値のみ採用（`delta` 加算をしない） |
| 4 | `StoreEliminated`（自店） | `Phase = Spectating`・`Queue` が空・`CurrentOrder = null` |
| 5 | `StoreEliminated`（他店） | 自店フィールドが変わらない |
| 6 | `MatchEnd` | `Phase = Result`・`Result` が入る |
| 7 | 未知 `customerId` の `CustomerLeft` | 例外にならず state 不変 |
| 8 | 重複 `CustomerArrived` | 後着が無視される |
| 9 | `LocalOrderCleared` | 行列先頭が除去される |
| 10 | `StoreListUpdate` に自店なし | 自店フィールド不変 |
| 11 | 購読通知 | 変化時のみ発火／`Dispose` 後は発火しない |
| 12 | `MatchStart` | `CreditLife == Params.InitialLife` になる |

## 6. 未確定事項

- `EventLog` の保持件数上限（[第4章 §8](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/04_状態管理.md)）。提案：200 件。
- **`Stores` を配列で持つか `storeId` キーのマップで持つか。** 将来 `StoreDelta`（差分配信）が入る前提では**マップ寄りが有利**（第4章 §6・§8）。差分型が Proto で確定するまでは全件置換をたたき台とし、**Reducer を全件／差分の両対応にできる形**で書く。
- `ClientState` のイミュータブル化が Unity（IL2CPP・GC）で許容できるか。毎ティックの `EvaluationUpdate` で新インスタンスを作る頻度を計測し、問題があれば購読粒度（セクション別通知）と併せて見直す（[第2章 §8](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/02_アーキテクチャ選定.md)）。
