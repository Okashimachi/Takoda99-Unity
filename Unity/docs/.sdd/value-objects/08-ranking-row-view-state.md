# 08-`RankingRowViewState` / `SelfRankViewState`（本選 v0.8.0・★新規）

> 参照する上流：`pureC#` [match-state/02-ranking-store.md](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)（`RankingRow` / `RankingTable`）／[ranking-view/01](../ranking-view/01-ranking-panel.md)。矛盾したら上流優先。

`Store` から導出する表示用の派生状態。**Unity 非依存の純粋な struct/class として書き、EditMode テストで検証する**（既存 `value-objects/` の方針どおり）。

## 1. 責務

**する**：`ClientState` と `RankingRow` から、View がそのまま描ける形へ変換する
**しない**：順位・スコアの計算、`TMP` への代入、`GameObject` の操作

## 2. `RankingRowViewState`

```csharp
// Assets/Scripts/View/ValueObjects/RankingRowViewState.cs
namespace Takoda99.View.ValueObjects
{
    public readonly struct RankingRowViewState : System.IEquatable<RankingRowViewState>
    {
        public string StoreId { get; }
        public string RankText { get; }     // "1" / "--"
        public string NameText { get; }     // 表示名。空なら storeId
        public string ScoreText { get; }    // "1200" / "-30"
        public bool IsSelf { get; }
        public bool IsAlive { get; }

        /// <summary>ランキング表の1行から作る（他人の行）。</summary>
        public static RankingRowViewState From(RankingRow row, bool isSelf);

        /// <summary>自分の行。順位・スコアは EvaluationUpdate 由来の権威値で上書きする。</summary>
        public static RankingRowViewState FromSelf(RankingRow row, int authoritativeRank, int authoritativeScore);

        public bool Equals(RankingRowViewState other);
    }
}
```

### 2.1 変換規則

| 入力 | 出力 |
|---|---|
| `Rank <= 0` | `RankText = "--"`（0位は存在しない。順位未確定の意） |
| `Rank >= 1` | `RankText = Rank.ToString()` |
| `DisplayName` が空 | `NameText = StoreId`（空欄にしない） |
| `Score` が負 | `ScoreText = "-30"` のようにそのまま。**0でクランプしない** |

### 2.2 `IEquatable` を実装する理由

99行のリストで **「値が変わった行だけ `TMP` を更新する」** ため（[ranking-view/03 §4 P2](../ranking-view/03-spectator-ranking-view.md)）。前回の状態と比較して等しければ、`TMP.text` への代入ごと省く。

`string` の比較になるので、`RankText` / `ScoreText` は**構築時に1回だけ `ToString` する**（毎フレーム作り直さない）。

## 3. `SelfRankViewState`

```csharp
public readonly struct SelfRankViewState : System.IEquatable<SelfRankViewState>
{
    public string RankText { get; }        // "12" / "--"
    public string ScoreText { get; }       // "1200" / "-30"
    public string AliveCountText { get; }  // "残り 55 店"

    public static SelfRankViewState From(int rank, int score, int aliveCount);
    public bool Equals(SelfRankViewState other);
}
```

| 入力 | 出力 |
|---|---|
| `rank <= 0` | `RankText = "--"` |
| `aliveCount <= 0` | `AliveCountText = ""`（0店は表示しない） |
| `score` | そのまま。負値可 |

## 4. 表示行の組み立て（純関数）

`RankingPanelView.Apply` の中身を、テストできる形に切り出す。

```csharp
public static class RankingRowsBuilder
{
    /// <summary>
    /// 上位 visibleCount 件を取り、自分が含まれていなければ末尾に自分を足す。
    /// 自分の行は authoritativeRank / authoritativeScore で上書きする。
    /// </summary>
    public static IReadOnlyList<RankingRowViewState> Build(
        RankingTable ranking,
        string selfStoreId,
        int authoritativeRank,
        int authoritativeScore,
        int visibleCount);

    /// <summary>観戦画面用。全行をそのまま変換する（自分だけ上書き）。</summary>
    public static IReadOnlyList<RankingRowViewState> BuildAll(
        RankingTable ranking,
        string selfStoreId,
        int authoritativeRank,
        int authoritativeScore);
}
```

| 規則 | 内容 |
|---|---|
| B1 | `visibleCount` は 10 未満なら 10 にクランプする（[ranking-view/01 §4](../ranking-view/01-ranking-panel.md)） |
| B2 | 自分が上位 `visibleCount` に含まれるなら**足さない**（重複させない） |
| B3 | `ranking.Rows` が空なら空リストを返す |
| B4 | 自分が `ranking` に居ない場合、`storeId` と権威値だけで行を作って足す |
| B5 | 並び順は `ranking.Rows` の順を保つ（再ソートしない） |

## 5. 依存関係

- 依存する：`pureC#` `Takoda99.Client.State`（`RankingRow` / `RankingTable` / `ClientState`）
- 依存される：[ranking-view/01](../ranking-view/01-ranking-panel.md)、[ranking-view/03](../ranking-view/03-spectator-ranking-view.md)、[hud/01](../hud/01-hud-composition.md)

## 6. テスト観点（`Unity/tests/Takoda99.View.Tests/`）

| # | 観点 |
|---|---|
| 1 | `Rank = 0` → `RankText == "--"` |
| 2 | `DisplayName` が空 → `NameText == StoreId` |
| 3 | `Score = -30` → `ScoreText == "-30"` |
| 4 | 同じ値から作った2つの `RankingRowViewState` が `Equals` で等しい |
| 5 | `Build`：自分が50位 → 11行（上位10＋自分） |
| 6 | `Build`：自分が3位 → 10行（重複なし） |
| 7 | `Build`：`visibleCount = 5` → 10行に増える |
| 8 | `Build`：自分の行の `RankText` が権威値になる（`ranking` 側が古くても） |
| 9 | `Build`：空の `RankingTable` → 空リスト |
| 10 | `Build`：自分が `ranking` に居ない → 権威値だけの行が末尾に足される |
| 11 | `BuildAll`：99行が `ranking` の順のまま返る |

## 7. 未確定事項

- なし
