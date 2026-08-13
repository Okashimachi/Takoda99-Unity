# 01-スコアと自店順位（`MatchStart` / `EvaluationUpdate`）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`MatchStart` / `StoreSummary` / `EvaluationUpdate` / `GameParametersPublicSubset`）／[本選企画書 3.8](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)（スコアの定義）／[30_通信シーケンス 2-A・2-D](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。矛盾したら上流優先。既存 [04-store-reducer.md](../04-store-reducer.md) と矛盾する場合は**本書が優先**。

本選では **`Score`（累積の絶対値）が順位を決める唯一の値**になり、評価（`EvalRaw` / `Normalized`）・星（`StarRating` / `StarDelta`）・信用（`CreditLife`）は消える。

## 1. 責務

**する**

- `MatchStart` から初期状態を組む。**99店の表示名をここでしか受け取らないため、`storeId → displayName` の辞書としてキャッシュする**
- `EvaluationUpdate` から自店の `Score` / `Rank` / `AliveCount` を保持する
- `GameParametersPublicSubset` を保持する（`CullSchedule` / `ScoreWeightTakoyaki` / `ScoreWeightMiss` を後段が使う）

**しない**

- **スコアを自前で計算しない。** `ScoreWeightTakoyaki` / `ScoreWeightMiss` を配られていても、それは加点演出（「+100」の表示）のためだけであり、`Score` の権威は常にサーバー（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）
- 順位を自前で決めない（自店の順位は `EvaluationUpdate.Rank` が権威）
- `DisplayName` を採番・補完しない（サーバーがフォールバック名を割り当てる）

## 2. `ClientState` の差分

### 2.1 削除するフィールド

| 削除 | 理由 |
|---|---|
| `CreditLife` | 信用制の廃止。`Params.InitialLife` は 0 で届く |
| `EvalRaw` | 相対評価の廃止 |
| `Normalized` | 同上 |
| `StarRating` | 同上（星は相対評価前提の表示だった） |
| `StarDelta` | 同上 |

`With(...)` の対応する引数（`creditLife` / `evalRaw` / `normalized` / `starRating` / `starDelta`）も併せて削除する。

### 2.2 追加・変更するフィールド

```csharp
public sealed class ClientState
{
    // ── 追加 ───────────────────────────────────────────────
    /// <summary>自店のスコア（順位を決める累積値）。EvaluationUpdate の受信値そのまま。
    /// 積み上がる絶対値で上限がなく、**負値もあり得る**（ミスが先行した序盤）。</summary>
    public int Score { get; init; }

    /// <summary>storeId → 表示名。MatchStart でのみ構築し、以降は再構築しない（§3.1）。</summary>
    public IReadOnlyDictionary<string, string> DisplayNames { get; init; }
        = new Dictionary<string, string>();

    // ── 既存のまま ─────────────────────────────────────────
    public int Rank { get; init; }        // EvaluationUpdate.Rank（自店の権威）
    public int AliveCount { get; init; }  // EvaluationUpdate.AliveCount
    public GameParametersPublicSubset Params { get; init; } = new();
    public bool Alive { get; init; }
    // …（Connection / Phase / MatchId / SelfStoreId / Queue / CurrentOrder / EventLog 等は変更なし）
}
```

> **`Stores`（`IReadOnlyList<StoreSummary>`）は廃止する。** 他店の状況は [02-ranking-store.md](./02-ranking-store.md) の `Ranking` が持つ。`MatchStart.Stores` は「`DisplayNames` を作る」「`Ranking` の初期値を作る」ための入力としてのみ使い、生の `StoreSummary` を state に残さない（`EvalNormalized` / `CreditLife` という Obsolete 値を誤って読む経路を消すため）。

## 3. Action と Reducer

### 3.1 `MatchStartAction`

```csharp
public sealed class MatchStartAction : IAction
{
    public string MatchId { get; init; } = "";
    public string SelfStoreId { get; init; } = "";
    public GameParametersPublicSubset Params { get; init; } = new();
    public Phase MatchPhase { get; init; }
    public IReadOnlyList<StoreSummary> Stores { get; init; } = System.Array.Empty<StoreSummary>();
    public long StartedAtLocalMs { get; init; }   // ★追加：Dispatcher が IClock.MonotonicMs を入れる
}
```

Reducer のふるまい：

| # | 処理 |
|---|---|
| 1 | `MatchId` / `SelfStoreId` / `Params` / `MatchPhase` を格納 |
| 2 | `StartedAtMs = a.StartedAtLocalMs`（**`MatchStart.StartsAtServerMs` を使わない**。ローカル補間の基準はローカル単調時計で揃える） |
| 3 | `DisplayNames` を `Stores` から構築（`storeId` 重複時は**先勝ち**） |
| 4 | `Ranking` を `Stores` から構築（[02](./02-ranking-store.md) §3.1） |
| 5 | `Score = 0` / `Rank` は `Stores` 中の自店の `Rank` / `AliveCount = Stores.Count` / `Alive = true` |
| 6 | `Phase = ClientPhase.InMatch` / `Queue` を空へ |
| 7 | `PersonalResult = null` / `MatchEnded = false` へリセット（[../result/01-personal-result.md](../result/01-personal-result.md) §4 の保険） |

> **`creditLife: a.Params.InitialLife` の初期化は削除する。** v0.8.0 では 0 が届くため、残すと「ライフ0で開始」になる。

**エッジケース**

| ケース | 扱い |
|---|---|
| `Stores` が空 | `DisplayNames` / `Ranking` は空。`AliveCount = 0`。**例外を投げない**。この状態でも `InMatch` へは進む（サーバー不整合を画面停止にしない） |
| `Stores` に自店が含まれない | `Rank = 0` のまま進む。表示側は `Rank <= 0` を「順位未確定」として扱う |
| `MatchStart` を2回受信 | 2回目で**全部作り直す**（冪等）。`Dispatcher` の phase ゲートで通常は起こらない |

### 3.2 `EvaluationUpdateAction`

```csharp
public sealed class EvaluationUpdateAction : IAction
{
    public int Score { get; init; }
    public int Rank { get; init; }
    public int AliveCount { get; init; }
}
```

`EvalRaw` / `Normalized` / `StarRating` / `StarDelta` は**フィールドごと削除**する。`Dispatcher` の Decode でも読まない。

Reducer：`state.With(score: a.Score, rank: a.Rank, aliveCount: a.AliveCount)`。

**エッジケース**

| ケース | 扱い |
|---|---|
| 自店が脱落済み（`Alive == false`）に届いた | **そのまま反映する。** 観戦中も `AliveCount` は減り続け、画面に出す必要がある。ただし `Rank` は脱落時点の確定順位が届き続ける想定であり、クライアントは値を検査しない |
| `Score` が負 | そのまま保持する。**0でクランプしない**（企画上あり得る値） |
| 取りこぼし | 定期更新なので次で追いつく。差分累積をしない |

## 4. スコア加点演出のための派生値（任意実装）

`Params.ScoreWeightTakoyaki` / `ScoreWeightMiss` は「+100」のようなポップを出すために配られている。**演出のためだけに使う。**

```csharp
// 提供1件ぶんの見込み加点。表示専用であり、state.Score へ足さないこと。
public static int EstimateDeltaScore(GameParametersPublicSubset p, int orderCount, int missCount)
    => (p.ScoreWeightTakoyaki * orderCount) - (p.ScoreWeightMiss * missCount);
```

| 禁止 | 理由 |
|---|---|
| この値を `state.Score` に加算する | サーバーのサニティ検証で棄却された場合に食い違う。**表示が正で内部値が偽**という最悪の状態になる |
| この値から順位を推定する | 順位はサーバー権威 |

## 5. 依存関係

- 依存するモジュール：[contract/01](../contract/01-proto-v0.8.0-migration.md)
- 依存されるモジュール：[02-ranking-store.md](./02-ranking-store.md)（`DisplayNames`）、[03-cull-warning.md](./03-cull-warning.md)、Unity 側 HUD 全般

## 6. テスト観点

| # | 観点 |
|---|---|
| 1 | `MatchStart`（99店）→ `DisplayNames.Count == 99`、自店の `Rank` が入る、`Score == 0` |
| 2 | `MatchStart` で `Params.InitialLife == 0` が届いても、state にライフ相当の値が生まれない（フィールドが存在しないことをコンパイルで担保） |
| 3 | `EvaluationUpdate{Score=-30, Rank=88, AliveCount=75}` → 負値がそのまま保持される |
| 4 | `EvaluationUpdate` を10連続で流しても、`Score` が累積せず**最後の値**になる |
| 5 | `Stores` が空の `MatchStart` で例外が出ず `Phase == InMatch` になる |
| 6 | 自店が `Stores` に居ない `MatchStart` で `Rank == 0` になり例外が出ない |

## 7. 未確定事項

- なし
