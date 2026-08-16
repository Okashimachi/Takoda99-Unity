# 06-順位入れ替えの演出

> 前提となる仕様書：[01-ranking-panel.md](./01-ranking-panel.md) §5（A1〜A5・**この文書はそれを置き換えない、上に積む**）／[04-top-ranking-slots.md](./04-top-ranking-slots.md)／[../value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md)。

## 1. 責務

**する**

- 順位が変わった行**だけ**を強調しながら、新しいスロットへ移動させる
- サーバーからの配信頻度（全量／差分）に関係なく、見た目を一定の速度に保つ

**しない**

- 順位を計算しない・予測しない
- 演出の都合で表示順を変えない
- 下位パネル（[05](./05-bottom-ranking-panel.md)）には適用しない（§6）

## 2. 何を作るか

```
4位の店が3位へ、3位の店が4位へ入れ替わったとき

  ┌ 3rd ─ 230×44 金銀銅の銅 ┐          ┌ 3rd ─ 230×44 銅 ┐
  │   AAA店                 │  ──╲╱──▶ │   BBB店         │
  └─────────────────────────┘    ╳     └─────────────────┘
  ┌ 4th ─ 130×66 Upper ┐        ╱╲      ┌ 4th ─ 130×66 Upper ┐
  │   BBB店            │  ──────────▶  │   AAA店            │
  └────────────────────┘               └────────────────────┘

  動くのはこの2枚だけ。1・2・5〜10位は静止したまま。
```

**動いた行だけが動く**ことが要件。全行がわずかに動くと、どこで何が起きたか読めない。

## 3. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/RankingRowLayout.cs（既存の internal static class を拡張）
internal static class RankingRowLayout
{
    /// <summary>
    /// 行を並べ替えて配置する。前回から順位が変わった行だけ強調する。
    /// </summary>
    public static void Apply(
        RankingRowPool pool,
        IReadOnlyList<RankingRowViewState> rows,
        HashSet<string> visibleIds,
        IRankingSlotSource slots,        // ★スロット版 or 等間隔版
        RankingRowPalette palette,
        RankingSwapSettings settings);
}

/// <summary>座標の供給元。上位はスロット、下位は等間隔。</summary>
internal interface IRankingSlotSource
{
    int Count { get; }
    Vector2 PositionOf(int index);
}
```

```csharp
// Assets/Scripts/View/Ranking/RankingSwapSettings.cs
namespace Takoda99.View.Ranking
{
    /// <summary>入れ替え演出の調整値。Inspector から触る。</summary>
    [Serializable]
    public struct RankingSwapSettings
    {
        [Tooltip("移動と見た目の補間にかける秒数")]
        public float moveDuration;          // 既定 0.25

        [Tooltip("順位が変わった行の強調の強さ。1 で等倍（強調なし）")]
        public float emphasisScale;         // 既定 1.08

        [Tooltip("強調の往復にかける秒数。moveDuration 以下にする")]
        public float emphasisDuration;      // 既定 0.15

        [Tooltip("同時に強調する行の上限。超えたら強調せず移動だけ行う")]
        public int maxEmphasisRows;         // 既定 4
    }
}
```

## 4. ふるまいの詳細

### 4.1 「順位が変わった行」の判定

`RankingRowLayout` が**前回の配置を覚える**。

```
前回:  Dictionary<string storeId, int slotIndex>
今回:  rows[i].StoreId → i

変化した行 = 前回に存在し、かつ slotIndex が今回と異なる storeId
```

| ケース | 扱い |
|---|---|
| 前回に無い（新しくリスト入り） | 強調しない。フェードインのみ（[01](./01-ranking-panel.md) A3） |
| 前回と同じ index | **何もしない**。Tween を張らない |
| index が変わった | **強調 + 移動** |
| 今回に無い（リストから出た） | フェードアウトしてプールへ（[01](./01-ranking-panel.md) A3） |

> 「2枚がちょうど交換した」ペアを検出しない。
> 99店では3つ以上が同時にずれる連鎖が普通に起きるため、**ペア検出は必ず破綻する**。
> 「index が変わった行を全部強調する」で、見た目の要件は満たせる。

### 4.2 強調の中身

移動と**同時に**、次を行う。

| 要素 | 内容 |
|---|---|
| スケール | `1.0 → emphasisScale → 1.0` の往復（`DOPunchScale` ではなく `DOScale` の往復。Punch は減衰が読みにくい） |
| 描画順 | 強調中だけ `SetAsLastSibling()`。**移動が終わったら §4.4 の順序へ戻す** |
| 色 | `RankingRowStyle` の目標色へ `moveDuration` で補間（[04](./04-top-ranking-slots.md) §5.3） |
| 寸法・フォント | 同上 |

**新しい色・寸法は移動の開始と同時に補間を始める。** 到着してから変えると2段階に見える。

### 4.3 強調の上限（`maxEmphasisRows`）

`RankingSnapshot`（全量）を受けた直後は**10行すべての index が変わり得る**。
このとき10行が同時に拡大すると画面が破綻する。

```
変化した行数 > maxEmphasisRows  →  強調をやめ、移動と色の補間だけ行う
```

サーバーは「足切り直後と試合終了直前には必ず `RankingSnapshot` を流す」ため、
**この分岐は毎試合6回必ず通る。** 例外パスではない。

### 4.4 描画順（`SetSiblingIndex`）

上位パネルはスロットに散らばって配置されるため、行同士が重なり得る。

```
移動中     : 強調している行を最前面（SetAsLastSibling）
移動完了後 : rows の順に SetSiblingIndex(i) を張り直す
             → 1位が最背面、10位が最前面
```

[01](./01-ranking-panel.md) A2 の「`Apply` のたびに `SetSiblingIndex(i)`」は、
**完了後の張り直しへ移す**。移動中に確定順で並べ替えると、動いている行が他の行の裏へ潜る。

### 4.5 連続する `Apply` への追従

[01](./01-ranking-panel.md) A5 の規則を維持する。

| # | 規則 |
|---|---|
| E1 | `Apply` のたびに、その行の全 Tween を `DOKill()` してから張り直す |
| E2 | `DOKill()` は**現在値を保持したまま**止める（`complete: false`）。スケールが 1.08 のまま止まったら、次の Tween が 1.0 へ戻す |
| E3 | 強調中に次の `Apply` が来たら、強調をやり直さず**移動だけ**を新しい目標へ張り替える |

E2 が守られないと、拡大したまま戻らない行が残る。**最も出やすい不具合。**

### 4.6 順位の数字

**補間しない。移動の開始と同時に即座に新しい値へ変わる**（[01](./01-ranking-panel.md) A4）。
「読めない時間を作らない」ため。文字が転がるカウントアップ演出は入れない。

## 5. 配信頻度との関係

| メッセージ | 頻度 | このパネルの挙動 |
|---|---|---|
| `RankingDelta`（差分） | 高頻度 | `Rank` を持たないため、`Score` で並べ替えた結果が順位になる。数行だけ動く |
| `RankingSnapshot`（全量） | 低頻度・足切り直後は必ず | 大量に動く。§4.3 で強調を抑制する |

**クライアントは配信の種類を意識しない。** `ClientState.Ranking` を見て、前回との差だけを演出に使う。
どちらが来たかで分岐を書かない。

## 6. 下位パネルには適用しない

[05-bottom-ranking-panel.md](./05-bottom-ranking-panel.md) では**強調を行わない**（`emphasisScale = 1`）。

理由：下位30行は毎回大きく入れ替わるうえ、行が 29px と小さく、
拡大しても読めるようにならない。下位で伝えるべきは「自分が今どの帯にいるか」であり、
どの行が動いたかではない。

移動の補間（`moveDuration`）は下位でも行う。瞬間移動だと追えないため。

## 7. 依存関係

- 依存する：DOTween、[04-top-ranking-slots.md](./04-top-ranking-slots.md)、[../value-objects/12](../value-objects/12-ranking-row-style.md)
- 依存される：なし
- **影響が及ぶ**：[03-spectator-ranking-view.md](./03-spectator-ranking-view.md)。99行のスクロールで強調は行わない（`maxEmphasisRows` を超えるので自動的に抑制されるが、明示的に `emphasisScale = 1` を設定すること）

## 8. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | 2行だけ入れ替えたとき、その2行だけが動く | サンプル駆動 |
| 2 | 動かなかった行に Tween が張られない | `DOTween.TotalPlayingTweens()` |
| 3 | 全10行が変わると強調が消え、移動だけになる | サンプル駆動（`shuffleStress`） |
| 4 | 強調中に次の `Apply` が来ても、拡大したまま戻らない行が出ない | `shuffleStress` を `moveDuration` より速い周期で回す ★最重要 |
| 5 | 移動完了後、1位が最背面・10位が最前面になっている | 手動（重なる配置で確認） |
| 6 | 順位の数字が移動開始と同時に変わる | 手動（目視） |
| 7 | 10秒間連続で入れ替えても行が生成破棄されない | サンプル駆動 |
| 8 | 色・寸法の補間が移動と同時に始まり、同時に終わる | 手動 |

**#4 が最も壊れやすい。** `MainGameViewSampleDriver` の `shuffleStress` を
`moveDuration` の半分の周期で回すケースを追加すること。

## 9. 未確定事項

- `emphasisScale = 1.08` の値。実機で決める
- 入れ替わった2枚の間に軌跡（線・残像）を出すか。**まず出さずに作る**
- 1位が入れ替わったときだけ別格の演出を足すか（[../elimination/01-mass-elimination-effect.md](../elimination/01-mass-elimination-effect.md) と競合しないこと）
