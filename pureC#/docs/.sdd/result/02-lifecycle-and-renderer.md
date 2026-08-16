# 02-ライフサイクルと `IRenderer`（`Dispatcher` / `MatchClientController`）

> 参照する上流：[12_差分_クライアント §3・§4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス 3-E](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。既存 [05-dispatcher.md](../05-dispatcher.md) / [06-match-client-controller.md](../06-match-client-controller.md) と矛盾する場合は**本書が優先**。

本選 `pureC#` 対応の**最後の1本**。`match-state/` と `result/01` で決めた state を、通信と描画に結線する。

## 1. 責務

**する**

- `Dispatcher` の受理表（`AcceptedPhases`）と Decode を v0.8.0 の12メッセージに合わせる
- `IRenderer` を本選の形へ差し替える
- `MatchClientController` から、廃止された割り込み処理（客の離脱による入力中断）を取り除く

**しない**

- `ClientPhase` の enum 値を増やさない（§4）
- 描画側に判定を押し付けない（「自店が含まれるか」等は `MatchClientController` が解決して渡す）

## 2. `Dispatcher` の差分

### 2.1 `AcceptedPhases`（本選の完全な表）

```csharp
private static readonly IReadOnlyDictionary<string, HashSet<ClientPhase>> AcceptedPhases =
    new Dictionary<string, HashSet<ClientPhase>>
    {
        [MessageType.MatchmakingStatus]         = new() { ClientPhase.Connecting, ClientPhase.Matchmaking },
        [MessageType.MatchStart]                = new() { ClientPhase.Matchmaking },
        [MessageType.CustomerArrived]           = new() { ClientPhase.InMatch },
        [MessageType.EvaluationUpdate]          = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.DifficultyUpdate]          = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.PhaseChange]               = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.RankingSnapshot]           = new() { ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
        [MessageType.RankingDelta]              = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.ForcedEliminationWarning]  = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.StoreEliminatedBatch]      = new() { ClientPhase.InMatch, ClientPhase.Spectating, ClientPhase.Result },
        [MessageType.PersonalResult]            = new() { ClientPhase.InMatch, ClientPhase.Spectating },
        [MessageType.MatchEnd]                  = new() { ClientPhase.InMatch, ClientPhase.Spectating },
    };
```

**削除する行**：`CustomerLeft` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated`（単体）。表から消えた型は既存の未知メッセージ経路（`OnUnknownMessage`）で捨てられる（[contract/01 §6](../contract/01-proto-v0.8.0-migration.md)）。

### 2.2 Decode の差分

| MessageType | Action | 注意 |
|---|---|---|
| `MatchStart` | `MatchStartAction` | `StartedAtLocalMs = _clock.MonotonicMs` を入れる |
| `EvaluationUpdate` | `EvaluationUpdateAction` | `EvalRaw` / `Normalized` / `StarRating` / `StarDelta` を**読まない** |
| `RankingSnapshot` | `RankingSnapshotAction` | `Entries` を `OrEmpty` で正規化 |
| `RankingDelta` | `RankingDeltaAction` | 同上 |
| `ForcedEliminationWarning` | `ForcedEliminationWarningAction` | `ReceivedAtLocalMs = _clock.MonotonicMs` を入れる。`UntilTick` / `ThresholdPct` を読まない。`CutStoreIds` を `OrEmpty` |
| `StoreEliminatedBatch` | `StoreEliminatedBatchAction` | `Entries` を `OrEmpty` |
| `PersonalResult` | `PersonalResultAction` | `Stats` が `null` なら `new MatchStats()`。`Reason` / `CreditLeft` / `EvalRaw` / `EvalNormalized` を読まない |
| `MatchEnd` | `MatchEndAction`（引数なし） | ペイロードが `{}` でも成功する |

**削除する Decode 分岐**：`CustomerLeft` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated`。

> **`_clock.MonotonicMs` を Decode の中で読む理由**：`ForcedEliminationWarning` の補間の起点は「その予告を受け取った瞬間」でなければならない。Reducer は純関数なので時刻を取れず、描画側で取ると1フレーム遅れる。既存の `CustomerArrivedAction.ArrivedAtLocalMs` と同じ方式。

## 3. `IRenderer` の新しい形

```csharp
namespace Takoda99.Client.Lifecycle;

/// <summary>描画への離散イベント通知。実体は Unity 側。</summary>
public interface IRenderer
{
    void OnCustomerArrived(CustomerView customer);
    void OnKeyFeedback(KeyResult result);
    void OnOrderServed(string customerId);
    void OnPhaseChanged(Phase phase);

    /// <summary>足切りの予告。常時届く（1〜2Hz）。秒読みは CullWarning.RemainingMsAt で補間する。</summary>
    void OnCullWarning(CullWarning warning);

    /// <summary>
    /// 1ステージぶんの一斉脱落。**最大49件が1回で届く。**
    /// 1件ずつ演出せず、まとめて1つの演出に集約すること（音も1回）。
    /// </summary>
    /// <param name="includesSelf">自店が entries に含まれるか。描画側で判定しない。</param>
    void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf);

    /// <summary>個人成績を受信した。保持は Store が行うので、ここは演出の契機としてだけ使う。</summary>
    void OnPersonalResult(PersonalResultState result);

    /// <summary>試合全体の終了。**引数を持たない**（MatchEnd は空ペイロード）。
    /// 順位別の演出分岐は state.PersonalResult.FinalRank を読んで行う。</summary>
    void OnMatchEnd();

    void OnLifecycleChanged(ClientPhase from, ClientPhase to);
    void OnConnectionTrouble(string kind);
}
```

### 3.1 予選からの差分表

| メソッド | 本選での扱い |
|---|---|
| `OnCustomerArrived` | 変更なし |
| `OnKeyFeedback` / `OnOrderServed` | 変更なし |
| `OnPhaseChanged` | 変更なし |
| `OnCustomerLeft(string, LeaveReason)` | **削除**（`CustomerLeft` が届かない） |
| `OnForcedEliminationWarning(int, double)` | **`OnCullWarning(CullWarning)` へ置き換え**（重要度上昇・常設UI） |
| `OnStoreEliminated(string, EliminationReason, int)` | **`OnStoreEliminatedBatch(int, IReadOnlyList<StoreEliminated>, bool)` へ置き換え** |
| `OnMatchEnd(int finalRank, MatchStats stats)` | **`OnMatchEnd()` へ置き換え**（引数の供給源が消えた） |
| — | **`OnPersonalResult(PersonalResultState)` を新設** |
| `OnLifecycleChanged` / `OnConnectionTrouble` | 変更なし |

`IInputSource` / `INetworkClient` / `BootstrapConfig` は**変更なし**。

## 4. `ClientPhase` は増やさない

```csharp
public enum ClientPhase { Boot, Title, Connecting, Matchmaking, InMatch, Spectating, Result }
```

脱落モーダル・個人成績画面は、この enum に値を足さず **Unity 側のシーン／画面遷移**で表現する。理由：

| 観点 | 内容 |
|---|---|
| 通信 | 脱落モーダルを見ていても個人成績を見ていても、**受信すべきメッセージは同じ**（`Spectating` の受理表）。phase を分けると受理表が二重管理になる |
| 予選のバグの再発防止 | 「どの画面にいるか」でデータの扱いが変わる設計が予選のバグの原因だった。`pureC#` は画面を知らないままにする |

`Spectating` の意味：**「自店は脱落したが試合は続いている」**。この間、入力は無効（`MatchClientController` が弾く）で、受信は継続する。

## 5. `MatchClientController` の差分

### 5.1 削除する処理 ★本選最大の単純化

```
【予選】お題を表示 → 打鍵中 → ┬ 打ち切った → OrderServed
                              └ CustomerLeft が来た → 入力中断・行列から除去・次の客へ

【本選】お題を表示 → 打鍵中 → 打ち切った → OrderServed
```

`HandleActionApplied` の `case CustomerLeftAction a:` の分岐を**まるごと削除する**。ここにあった以下の処理が不要になる：

- `_renderer.OnCustomerLeft(...)`
- 対応中の客が離脱した場合の `_typingJudge.AbortOrder()` と `_servingCustomerId = null`
- その後の `TryBeginNextOrder()`

> **一度出たお題は必ず打ち切られる。**「打っている最中に対象が消える」ことに起因するバグが構造的に発生しなくなる。

### 5.2 残る唯一の中断経路

**自店の脱落**だけは打鍵を中断する。これは「客が消える」のではなく「試合から出る」ため別物。

```csharp
case StoreEliminatedBatchAction a:
{
    var includesSelf = a.Entries.Any(e => e.StoreId == _store.State.SelfStoreId);
    _renderer.OnStoreEliminatedBatch(a.StageIndex, a.Entries, includesSelf);
    if (includesSelf)
    {
        _typingJudge.AbortOrder();
        _servingCustomerId = null;
    }
    break;
}
```

> **`includesSelf` の判定はここで1回だけ行い、描画側へ渡す。** `Renderer` にも `EliminationResultView` にも同じ判定を書かない。

`HandleStateChanged` にある `Phase == Spectating` での `AbortOrder()` は**残す**（`Reducer` 経由で `Spectating` に入る別経路への保険。冪等なので二重に呼んでも害はない）。

### 5.3 追加・変更する処理

```csharp
case ForcedEliminationWarningAction a:
    _renderer.OnCullWarning(_store.State.Cull!);   // Reducer 適用後なので non-null
    break;

case PersonalResultAction:
    _renderer.OnPersonalResult(_store.State.PersonalResult!);
    break;

case MatchEndAction:
    _renderer.OnMatchEnd();
    break;
```

> `OnActionApplied` は `_store.Apply(action)` の**後**に発火するため、`state` は既に更新済み。Action のフィールドを組み直さず `state` から読む。

### 5.4 `LocalMatchReset` の呼び出し

```csharp
public void BeginPlay(string displayName = "")
{
    _displayName = displayName ?? "";
    _store.Apply(new LocalMatchResetAction());          // ★追加
    _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Connecting));
    _networkClient.Connect(_config.WebSocketUrl);
}

public void Rematch()
{
    _networkClient.Disconnect();
    _store.Apply(new LocalMatchResetAction());          // ★追加
    _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Connecting));
    _networkClient.Connect(_config.WebSocketUrl);
}
```

`LeaveMatchmaking()` / `BackToTitle()` では**呼ばない**（次に必ず `BeginPlay` か `Rematch` を通るため。破棄の契機を増やすと責務が散る）。

### 5.5 変更しない処理

`HandleCharKey` / `HandleOrderCleared` / `TryBeginNextOrder` / `HandleConnectionChanged` は**そのまま**。`OrderServed` の送信も変更なし（C2S は本選でも無変更）。

## 6. 依存関係

- 依存するモジュール：[contract/01](../contract/01-proto-v0.8.0-migration.md)、[../match-state/](../match-state/README.md) 全3本、[01-personal-result.md](./01-personal-result.md)
- 依存されるモジュール：Unity 側すべて（`Renderer` が `IRenderer` を実装する）

## 7. テスト観点

| # | 観点 |
|---|---|
| 1 | `Spectating` 中に `RankingDelta` が受理され、`Result` 中は落ちる（`OnMessageDropped("RankingDelta", "phase-not-allowed")`） |
| 2 | `Result` 中の `RankingSnapshot` が受理される |
| 3 | `Matchmaking` 中の `PersonalResult` が落ちる |
| 4 | `CustomerLeft` の Envelope で `OnUnknownMessage` が1回発火し、`state` が変わらない |
| 5 | `ForcedEliminationWarning` の Decode で `ReceivedAtLocalMs` に `IClock.MonotonicMs`（FakeClock 値）が入る |
| 6 | 自店を含む `StoreEliminatedBatch` で `FakeRenderer.OnStoreEliminatedBatch` が `includesSelf == true` で1回呼ばれ、`ITypingJudge.AbortOrder` が呼ばれる |
| 7 | 自店を含まないバッチで `AbortOrder` が呼ばれない |
| 8 | 49件のバッチで `OnStoreEliminatedBatch` が**1回だけ**呼ばれる |
| 9 | `MatchEnd` で `FakeRenderer.OnMatchEnd()` が引数なしで呼ばれる |
| 10 | `Rematch()` で `PersonalResult` が破棄され、その後 `MatchmakingJoin` が送られる |
| 11 | 打鍵中に `CustomerArrived` が来ても、対応中の注文が中断されない（離脱経路の削除の裏取り） |

## 8. 未確定事項

- なし
