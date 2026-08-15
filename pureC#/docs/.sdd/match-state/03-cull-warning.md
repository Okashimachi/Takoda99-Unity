# 03-足切りの予告と一斉脱落（`ForcedEliminationWarning` / `StoreEliminatedBatch`）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`ForcedEliminationWarning` / `StoreEliminatedBatch` / `StoreEliminated` / `CullStageView`）／[本選企画書 3.6・3.7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[30_通信シーケンス §4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。矛盾したら上流優先。

予選の `StormWarning { UntilTick, ThresholdPct }` を捨て、**秒読みできる形**に置き換える。足切りは 20/40/60/80/100/120 秒の6段階で、1ステージで**最大49店が同時に脱落**する。

## 1. 責務

**する**

- `ForcedEliminationWarning` を、**ローカル補間できる形**（受信時刻を添えて）保持する
- `StoreEliminatedBatch` を1つのイベントとして `Ranking` へ反映し、自店が含まれるかを判定する
- 自店が脱落したら `ClientPhase.Spectating` へ移し、打鍵を止められる状態にする

**しない**

- **経過秒を state に書き込まない。** 秒読みの数値は「受信値＋ローカル経過」から**描画時に計算する**。`ClientState` を毎フレーム書き換えない（`Store` の通知が毎フレーム走り、全 View が再描画される）
- 足切りの発生を自前で判定しない（サーバーが `StoreEliminatedBatch` を送るまで誰も脱落していない）
- `CutStoreIds` から自店が対象かを判定しない（`SelfAtRisk` がサーバーから届く）

## 2. 値オブジェクト

```csharp
namespace Takoda99.Client.State;

/// <summary>次の足切りの予告。予選の StormWarning を置き換える。</summary>
public sealed class CullWarning
{
    /// <summary>受信時点での「次の足切りまでの残りミリ秒」（サーバー値そのまま）。</summary>
    public int UntilMs { get; init; }

    /// <summary>この予告を受信したローカル単調時刻（ms）。補間の起点。</summary>
    public long ReceivedAtLocalMs { get; init; }

    /// <summary>第何段階か（1始まり）。</summary>
    public int StageIndex { get; init; }

    /// <summary>全何段階か。</summary>
    public int StageTotal { get; init; }

    /// <summary>この順位より下が切られる境界。最終ステージのみ 2 が届く（企画意図。Proto コメント参照）。</summary>
    public int CutLineRank { get; init; }

    /// <summary>切られる予定の店（サーバーが表示件数ぶんに上限を切っている）。null では入らない。</summary>
    public IReadOnlyList<string> CutStoreIds { get; init; } = System.Array.Empty<string>();

    /// <summary>自店が淘汰の対象圏内か。**クライアントで rank と比較しない**（サーバー値をそのまま使う）。</summary>
    public bool SelfAtRisk { get; init; }

    /// <summary>
    /// 現在時刻における残りミリ秒。0 未満にはならない（足切り実行前後の揺れを負数で出さない）。
    /// 描画側が毎フレーム呼ぶ純関数。state は変化しない。
    /// </summary>
    public int RemainingMsAt(long nowLocalMs)
        => System.Math.Max(0, UntilMs - (int)(nowLocalMs - ReceivedAtLocalMs));
}
```

`ClientState` へ追加（`StormWarning? Storm` を**置き換える**）：

```csharp
/// <summary>次の足切りの予告。未受信なら null。</summary>
public CullWarning? Cull { get; init; }
```

`StormWarning` クラスは削除する。

## 3. `ForcedEliminationWarningAction`

```csharp
public sealed class ForcedEliminationWarningAction : IAction
{
    public int UntilMs { get; init; }
    public long ReceivedAtLocalMs { get; init; }   // Dispatcher が IClock.MonotonicMs を入れる
    public int StageIndex { get; init; }
    public int StageTotal { get; init; }
    public int CutLineRank { get; init; }
    public IReadOnlyList<string> CutStoreIds { get; init; } = System.Array.Empty<string>();
    public bool SelfAtRisk { get; init; }
}
```

Reducer：`state.With(cull: new CullWarning { …a の写し… })`。**毎回まるごと差し替える**（定期更新なのでマージしない）。

`UntilTick` / `ThresholdPct` は Obsolete。`Dispatcher` の Decode で**読まない**。

### 3.1 秒読みの表示（描画側の約束）

```
表示秒 = ceil(cull.RemainingMsAt(now) / 1000)
```

| 約束 | 内容 |
|---|---|
| サーバーは1秒ごとの正確な配信を保証しない | 1〜2Hz で届く。滑らかさはクライアントの責務（[30_通信シーケンス 3-B](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)） |
| 新しい予告が来たら上書き | 補間中の値を優先しない。**サーバー値が常に正** |
| 0 に達しても勝手に脱落させない | 実際の脱落は `StoreEliminatedBatch` の到着で確定する。0 のまま待たせる |
| `Cull == null` の間 | 秒読みを出さない（`--` 等）。0秒と区別する |

**エッジケース**

| ケース | 扱い |
|---|---|
| `UntilMs` が負で届いた | `RemainingMsAt` が 0 を返す（`Math.Max` が吸収） |
| `CutStoreIds` が `null` | `Dispatcher` が空リストへ正規化（[contract/01 §5](../contract/01-proto-v0.8.0-migration.md)） |
| `StageIndex > StageTotal` | そのまま保持する。表示側でクランプ |
| 最終ステージで `CutLineRank == 2` | **仕様どおり。**「1位以外が脱落対象」と表示する。処理上は1位も脱落するが、決勝の緊張を最大化する企画意図（Proto コメント） |

## 4. `StoreEliminatedBatchAction`

```csharp
public sealed class StoreEliminatedBatchAction : IAction
{
    public int StageIndex { get; init; }
    public IReadOnlyList<StoreEliminated> Entries { get; init; } = System.Array.Empty<StoreEliminated>();
}
```

`StoreEliminated` は Proto DTO をそのまま運ぶ（`StoreId` / `Reason` / `FinalRank`）。`Reason` は常に `Cull` で、**読まない**。

### 4.1 Reducer のふるまい

| # | 処理 |
|---|---|
| 1 | `Entries` が空なら `state` を変えずに返す |
| 2 | 各 `Entries[i]` について、`Ranking` の該当行を `Alive = false` / `Rank = FinalRank` に更新する（**確定順位はここで入る**） |
| 3 | 表に無い `storeId` は行を追加する（`DisplayName` は辞書から解決） |
| 4 | `Rows` を `Rank` 昇順 → `StoreId` 序数昇順で整列し直す。**生存店の再ランクは行わない**（直後に届く `EvaluationUpdate` と `RankingSnapshot` が正しい値を運ぶ。ここで独自計算すると一瞬だけ嘘の順位が出る） |
| 5 | 自店の `storeId` が `Entries` に含まれる場合のみ、以下を追加で行う |

自店が含まれる場合の追加処理：

```
Alive        = false
Phase        = ClientPhase.Spectating
CurrentOrder = null（clearCurrentOrder）
Queue        = 空
```

> **`Phase` は `Result` にしない。** 脱落しても試合は続き、観戦しながら `MatchEnd`（120秒）を待つ（[30_通信シーケンス 4-D](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)）。

### 4.2 順序の前提（★サーバーとの約束）

足切り時の配信順序は**サーバーが固定している**（4-A）：

```
StoreEliminatedBatch → PersonalResult（該当店のみ） → EvaluationUpdate → RankingSnapshot → 次の ForcedEliminationWarning
```

クライアントは**この順に届くことを前提に書いてよい**。「順位を配る前に脱落を配る」ため、脱落者を含んだ順位が表示される瞬間が生まれない。

ただし**順序が崩れても壊れないこと**は満たす：各 Reducer は独立して冪等であり、どの順で来ても最終状態は同じになる（§4.1 の手順4で生存店を再ランクしないのはこのため）。

**エッジケース**

| ケース | 扱い |
|---|---|
| 同じ `storeId` が2つのバッチに含まれる | 2回目も同じ処理をする（冪等）。`Rank` は最後の値で上書き |
| 120秒の最終バッチ（`FinalRank == 1` を含む10件） | 通常のバッチと同じ処理。**優勝者も脱落する**（企画書 3.7）。特別扱いをしない |
| 自店が既に `Spectating` のときに自店を含むバッチ | 冪等に処理する（`Phase` は `Spectating` のまま） |
| 49件同時 | Reducer は1回の `Apply` で処理する。**`Store` の通知も1回**。1件ずつ `Apply` しない（通知が49回走り、View が49回再描画される） |

## 5. `IRenderer` への通知

`MatchClientController` が `OnActionApplied` で受けて呼ぶ（[../result/02-lifecycle-and-renderer.md](../result/02-lifecycle-and-renderer.md)）。

```csharp
void OnCullWarning(CullWarning warning);
void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf);
```

| 旧 | 新 |
|---|---|
| `OnForcedEliminationWarning(int untilTick, double thresholdPct)` | `OnCullWarning(CullWarning)` |
| `OnStoreEliminated(string, EliminationReason, int)` | `OnStoreEliminatedBatch(int, IReadOnlyList<StoreEliminated>, bool)` |

`includesSelf` は `MatchClientController` が `SelfStoreId` と突き合わせて渡す。**描画側に判定させない。**

自店が含まれる場合、`MatchClientController` は併せて `ITypingJudge.AbortOrder()` を呼び、`_servingCustomerId` を `null` にする（打鍵の途中でシーンが変わるため。これは「客が消える割り込み」とは別の話で、本選でも残る唯一の中断経路）。

## 6. `Dispatcher` の phase ゲート

| MessageType | 受け付ける `ClientPhase` |
|---|---|
| `ForcedEliminationWarning` | `InMatch` / `Spectating` |
| `StoreEliminatedBatch` | `InMatch` / `Spectating` / `Result` |

`Result` でも `StoreEliminatedBatch` を受けるのは、120秒に `MatchEnd` より先に届く設計だが、通信の揺れで逆転しても取りこぼさないため。

## 7. 依存関係

- 依存するモジュール：[contract/01](../contract/01-proto-v0.8.0-migration.md)、[02-ranking-store.md](./02-ranking-store.md)
- 依存されるモジュール：[../result/01-personal-result.md](../result/01-personal-result.md)、Unity `ranking-view/02` `elimination/01`

## 8. テスト観点

| # | 観点 |
|---|---|
| 1 | `ForcedEliminationWarning` 受信後、`RemainingMsAt(received + 5000)` が `UntilMs - 5000` になる |
| 2 | `RemainingMsAt` が経過超過で 0 を返し、負にならない |
| 3 | 新しい予告で `Cull` がまるごと差し替わる（前の `CutStoreIds` が残らない） |
| 4 | 24件のバッチを1回 `Apply` して、`Store` の通知が**1回**であること |
| 5 | バッチ後、対象24店の `Alive == false` / `Rank == FinalRank` になり、**生存店の `Rank` が変わっていない** |
| 6 | 自店を含むバッチで `Phase == Spectating` / `CurrentOrder == null` / `Queue` が空 |
| 7 | 自店を含まないバッチで `Phase` が `InMatch` のまま |
| 8 | 同じバッチを2回 `Apply` しても最終状態が同じ（冪等） |
| 9 | `FinalRank == 1` を含む最終バッチでも、自店が含まれれば `Spectating` へ行く |

## 9. 未確定事項

- `CutStoreIds` の件数上限はサーバー判断（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点4）。**クライアントは届いた件数をそのまま保持し、表示件数の絞り込みは描画側で行う**
