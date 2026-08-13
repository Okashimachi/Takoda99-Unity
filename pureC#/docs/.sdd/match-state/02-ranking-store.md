# 02-ランキング表（`RankingSnapshot` / `RankingDelta`）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`RankingSnapshot` / `RankingDelta` / `RankingEntry` / `RankingChange`）／[10_差分_プロト §2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/10_差分_プロト.md)／[30_通信シーケンス §3.1・6-E](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。矛盾したら上流優先。

**予選の `StoreListUpdate`（99店フル・低頻度）を置き換える。** 試合中は上位・下位の表示に、観戦中は99人全員の順位一覧に使う。

## 1. 責務

**する**

- 全99店の `{ storeId, displayName, rank, score, alive }` を1本の表として保持する
- `RankingSnapshot`（全量）で表を**丸ごと置き換える**
- `RankingDelta`（差分）で該当店の `score` / `alive` を**上書きする**
- 差分適用後、**表示用の `rank` をローカルで振り直す**（`RankingDelta` は `rank` を持たないため）

**しない**

- **自店の順位をここから読ませない。** 自店の権威は `EvaluationUpdate.Rank`（[01](./01-score-and-self-rank.md)）。差分の取りこぼしでズレ得る値を自分の順位に使わない
- 差分を累積しない（受け取った値で置き換えるだけ）
- 表示名をここで受け取らない（`MatchStart` の `DisplayNames` から引く）

## 2. 値オブジェクト

```csharp
namespace Takoda99.Client.State;

/// <summary>ランキング表の1行。表示に必要な値をすべて持つ（描画側が他を引かなくて済む形）。</summary>
public sealed class RankingRow
{
    public string StoreId { get; init; } = "";

    /// <summary>MatchStart のキャッシュから解決済みの表示名。未解決なら空文字。</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>生存店は現在順位、脱落店は確定順位（以後不変）。1始まり。</summary>
    public int Rank { get; init; }

    public int Score { get; init; }
    public bool Alive { get; init; }
}

/// <summary>全店のランキング。Rows は常に Rank の昇順（1位が先頭）で保持する。</summary>
public sealed class RankingTable
{
    public IReadOnlyList<RankingRow> Rows { get; init; } = System.Array.Empty<RankingRow>();

    /// <summary>上位 n 件（不足分は詰めて返す。n <= 0 なら空）。</summary>
    public IReadOnlyList<RankingRow> Top(int n);

    /// <summary>storeId で1行引く。無ければ null。</summary>
    public RankingRow? Find(string storeId);
}
```

`ClientState` へ追加：

```csharp
/// <summary>全99店のランキング。MatchStart で初期化し、Snapshot/Delta で更新する。</summary>
public RankingTable Ranking { get; init; } = new();
```

> **`ClientState.Stores`（`IReadOnlyList<StoreSummary>`）は削除する**（[01](./01-score-and-self-rank.md) §2.2）。役割はすべて `Ranking` へ移る。

## 3. Action と Reducer

### 3.1 `MatchStart` からの初期化

`MatchStartAction.Stores`（`StoreSummary[]`）から構築する。

```
Rows = Stores.Select(s => new RankingRow {
           StoreId = s.StoreId,
           DisplayName = s.DisplayName,
           Rank = s.Rank,
           Score = s.Score,
           Alive = s.Alive,
       })
       .OrderBy(r => r.Rank).ThenBy(r => r.StoreId, Ordinal)
```

`StoreSummary.EvalNormalized` / `CreditLife` は**読まない**（Obsolete・0が届く）。

### 3.2 `RankingSnapshotAction`（全量）

```csharp
public sealed class RankingSnapshotAction : IAction
{
    /// <summary>Dispatcher が null を空リストへ正規化して渡す。</summary>
    public IReadOnlyList<RankingEntry> Entries { get; init; } = System.Array.Empty<RankingEntry>();
}
```

Reducer：

| # | 処理 |
|---|---|
| 1 | `Entries` から `RankingRow` を作る。`DisplayName` は `state.DisplayNames` から解決（無ければ空文字） |
| 2 | `Rank` は**サーバー値をそのまま使う**（全量はサーバーが正しい順位を付けている。ローカル再計算をしない） |
| 3 | `Rank` 昇順 → `StoreId` 序数順で整列して `Rows` に格納 |
| 4 | **既存の `Rows` は破棄する**（マージしない。全量の役割は整合性の回復） |

**エッジケース**

| ケース | 扱い |
|---|---|
| `Entries` が空／`null` | `Rows` を**空にしない**。空の全量は「情報なし」と解釈し、`state` を変えずに返す。（99店ぶんが必ず来る契約であり、空はサーバー不整合。ここで表を消すと観戦画面が一瞬全消えする） |
| 未知の `storeId` が混ざる | 行として追加する。`DisplayName` は空文字。**捨てない**（描画側で「???」等にフォールバックできる） |
| `MatchStart` 前に届いた | `Dispatcher` の phase ゲートで落ちる（§5） |

### 3.3 `RankingDeltaAction`（差分）

```csharp
public sealed class RankingDeltaAction : IAction
{
    public IReadOnlyList<RankingChange> Entries { get; init; } = System.Array.Empty<RankingChange>();
}
```

`RankingChange` は `{ storeId, score, alive }` のみで、**`rank` を持たない**。

Reducer：

| # | 処理 |
|---|---|
| 1 | `Entries` が空／`null` なら `state` をそのまま返す（無駄な再ソートをしない） |
| 2 | 各 `storeId` に対応する行の `Score` / `Alive` を**上書き**する |
| 3 | 表に無い `storeId` は**新しい行として追加**する（`Rank = 0` / `DisplayName` は辞書から解決） |
| 4 | §3.4 のルールで `Rank` を振り直し、整列し直す |

### 3.4 ★ローカル順位の振り直しルール

差分は `rank` を運ばないため、クライアントが表示用に決める。**この規則は決定的でなければならない**（同じ入力から常に同じ並びになること。行入れ替えアニメーションがちらつくため）。

```
1. 脱落済みの行（Alive == false）は Rank を変えない。
   ├ 脱落時の確定順位は以後不変（30_通信シーケンス 4-G）
   └ Rank == 0 のまま脱落している行（差分だけで脱落を知った行）は、
     暫定的に「生存店の数 + 1」を入れる。次の RankingSnapshot で正しい値に直る

2. 生存中の行（Alive == true）を Score 降順、同点なら StoreId の序数昇順で並べ、
   先頭から 1, 2, 3, … を振る

3. 全行を Rank 昇順 → StoreId 序数昇順で整列して Rows に格納する
```

> **なぜ同点のタイブレークを `StoreId` にするか**：`Score` は整数で同点が頻出する（特に開始直後は全店0点）。安定した基準を持たないと、差分が来るたびに同点集団の並びが入れ替わり、UI が意味なく踊る。

> **なぜ脱落店の `Rank` を触らないか**：脱落した店の順位は「そのステージ時点で下から何番目だったか」の確定値であり、以後のスコア変動と無関係。ここを再計算すると、脱落者の順位が試合中に動いてしまう。

**エッジケース**

| ケース | 扱い |
|---|---|
| 差分で `Alive` が `true → false` になった | その行の `Rank` を**触らない**。確定順位は `StoreEliminatedBatch`（[03](./03-cull-warning.md)）が入れるのが正規経路であり、差分が先に来ても壊さない |
| 差分で `Alive` が `false → true` になった | 起こらない契約だが、届いたら生存扱いで再ランクする（不整合を state に持ち込まない） |
| 自店の行が差分で更新された | 反映する。ただし**描画は `EvaluationUpdate` 由来の `state.Rank` / `state.Score` を優先する**（§1） |

## 4. 自店の値の使い分け（★実装者が最も間違えやすい点）

| 用途 | 読む場所 |
|---|---|
| 自分の順位の大表示 | `state.Rank`（`EvaluationUpdate`） |
| 自分のスコアの補助表示 | `state.Score`（`EvaluationUpdate`） |
| 上位10名リストの中の自分の行 | `state.Ranking` の該当行（**リストの整合性のため。ここだけ一瞬ずれても構わない**） |
| 生存数 | `state.AliveCount`（`EvaluationUpdate`）。`Ranking` の `Alive == true` の数を数えない |

## 5. `Dispatcher` の phase ゲート

| MessageType | 受け付ける `ClientPhase` |
|---|---|
| `RankingSnapshot` | `InMatch` / `Spectating` / `Result` |
| `RankingDelta` | `InMatch` / `Spectating` |

`Result` で `RankingSnapshot` を受け付けるのは、**120秒の配信順序が `StoreEliminatedBatch` → `PersonalResult` → `RankingSnapshot` → `MatchEnd`** であり、最後のスナップショット（＝全店の最終順位）をリザルト画面が使うため。`MatchEnd` 到達後に遅れて届いた全量も取りこぼさない。

## 6. 依存関係

- 依存するモジュール：[contract/01](../contract/01-proto-v0.8.0-migration.md)、[01-score-and-self-rank.md](./01-score-and-self-rank.md)（`DisplayNames`）
- 依存されるモジュール：[03-cull-warning.md](./03-cull-warning.md)（脱落確定順位の書き込み先）、Unity `ranking-view/`

## 7. テスト観点

| # | 観点 |
|---|---|
| 1 | `MatchStart`(99店) → `Ranking.Rows.Count == 99`、`Rows[0].Rank == 1` |
| 2 | `RankingSnapshot` で全行が置き換わる（前の表に居て新しい全量に居ない店が消える） |
| 3 | **空の `RankingSnapshot` を受けても `Rows` が消えない** |
| 4 | `RankingDelta` で1店の `Score` を上げると、その店の `Rank` が上がり他店が押し下がる |
| 5 | 同点2店の並びが、差分を何度流しても `StoreId` 順で安定する |
| 6 | 脱落済み（`Alive == false`・`Rank == 40`）の行に差分が届いても `Rank` が 40 のまま |
| 7 | 未知 `storeId` の差分で行が追加され、`DisplayName` が空文字になる |
| 8 | `DisplayNames` に載っている `storeId` の差分で、`DisplayName` が解決される |

## 8. 未確定事項

- 表示件数（上位N・下位N）の決定は Unity 側 [ranking-view/01-ranking-panel.md](../../../Unity/docs/.sdd/ranking-view/01-ranking-panel.md)。**`pureC#` は常に全99行を保持する**（絞り込みは描画側の責務）
