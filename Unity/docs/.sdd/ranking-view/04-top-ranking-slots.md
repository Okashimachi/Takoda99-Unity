# 04-上位ランキングのスロット化とPrefab統合

> 参照する上流：[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)。前提となる仕様書：[01-ranking-panel.md](./01-ranking-panel.md)（この文書は 01 の §5・§10 を具体化する差分仕様）／[../value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md)。

## 1. 責務

**する**

- 上位10行の**座標を、シーンに手で置いたスロットから読む**
- `TopRanker` 系3 Prefab を**1種に統合**する
- 行の寸法・フォントサイズ・配色を、順位から決めて適用する

**しない**

- 表示行の組み立て規則を変えない（[01-ranking-panel.md](./01-ranking-panel.md) §3 のまま）
- 入れ替えの演出を定義しない（[06-rank-swap-animation.md](./06-rank-swap-animation.md) が担う）
- 下位30行を扱わない（[05-bottom-ranking-panel.md](./05-bottom-ranking-panel.md) が担う）

## 2. 現状と、変える理由

### 2.1 いまシーンにあるもの

```
RankingCanvas
└─ TopRankers
   ├─ BG   [Image]
   ├─ 1st  … TopRanker.prefab      (230×44)   ← 手で座標を置いてある
   ├─ 2nd  … TopRanker.prefab      (230×44)
   ├─ 3rd  … TopRanker.prefab      (230×44)
   ├─ 4th  … TopRanker4-6.prefab   (130×66)
   ├─ 5th  … TopRanker4-6.prefab   (130×66)
   ├─ 6th  … TopRanker4-6.prefab   (130×66)
   ├─ 7th  … TopRanker7-10.prefab  (100×50)
   ├─ 8th  … TopRanker7-10.prefab  (100×50)
   ├─ 9th  … TopRanker7-10.prefab  (100×50)
   └─ 10th … TopRanker7-10.prefab  (100×50)
```

### 2.2 2つの食い違い

| # | 食い違い |
|---|---|
| 1 | **この10個は `RankingPanelView` に一切繋がっていない。** `RankingPanelView` は `rowPrefab` を `rowsRoot` へ `Instantiate` する作りで、手置きのインスタンスを使わない。このままだと実行時に11個目以降が生成され、手置きの10個は動かないまま残る |
| 2 | **座標が `-rowHeight * i` で決まらない。** `RankingRowLayout.Apply` は等間隔の縦積みを前提にしているが、上位は 44/66/50 と高さが違い、手で調整した配置になっている |

### 2.3 解決方針

**手で置いた10個を「スロット（座標と順序だけを持つ空の目印）」に変え、行はそこへ飛ばす。**

こうすると、

- 手で詰めたレイアウトが**そのまま活きる**（座標を数式に起こし直さない）
- レイアウト調整が**エディタでの移動だけ**で完結する（コード変更にならない）
- 行の GameObject は店に固定されたままなので、[06](./06-rank-swap-animation.md) の入れ替え演出が成立する

## 3. Unity構成

### 3.1 目標構成

```
RankingCanvas
└─ TopRankers          [TopRankingSlots]  ★このコンポーネントを新設
   ├─ BG      [Image]
   ├─ Slots                                … 空の RectTransform（全面ストレッチ）
   │  ├─ Slot01 … 空の RectTransform。座標と sizeDelta だけを持つ
   │  ├─ Slot02
   │  │   …
   │  └─ Slot10
   └─ RowsRoot                             … 実行時に行が生成される親（初期状態は空）
```

**`Slot01`〜`Slot10` は、いまの `1st`〜`10th` の `anchoredPosition` と `sizeDelta` をそのまま引き継ぐ。**
アンカーとピボットも現行どおり `AnchorMin = AnchorMax = Pivot = (0.5, 0.5)` に揃える
（座標の基準が行と一致していないとスロットへ飛ばせないため）。

`Slots` と `RowsRoot` を分けるのは、スロットが**描画されない目印**であることを構造で示すため。
`Slot*` に `Image` を付けない。

### 3.2 移行手順（エディタ作業）

1. `1st`〜`10th` の `anchoredPosition` / `sizeDelta` を控える
2. `TopRankers` の下に空の `Slots` と `RowsRoot` を作る
3. `Slots` の下に空の GameObject を10個作り、`Slot01`〜`Slot10` と名付けて 1 の値を入れる
4. `1st`〜`10th`（Prefab インスタンス）を**削除する**
5. `TopRankers` に `TopRankingSlots` をアタッチし、`Slot01`〜`Slot10` を順に配線する

> `Slot01` が1位。**配線の順序が順位そのもの**なので、要素の並び順を間違えないこと。

### 3.3 Prefab の統合

`TopRanker.prefab` を残し、**`TopRanker4-6.prefab` と `TopRanker7-10.prefab` を削除する。**

3種は子の構成が完全に同じで、違いは寸法とフォントサイズだけだった。
その値は [../value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md) §3.2 の表へ移してあるので、**Prefab に残す必要がない**。

残す `TopRanker.prefab` の中身（[c28ae36](https://github.com/Okashimachi/Takoda99-Unity/pull/61) で配線済み・変更不要）：

```
TopRanker  [RankingRowView] [CanvasGroup]
├─ HighLight  [Image]  … Panel の背面。ストレッチ + sizeDelta(4,4) で縁だけ見せる
├─ Panel      [Image]  … 不透明。★ここに RankingRowStyle.Tone の色を乗せる
├─ RankText   [TMP]
├─ NameText   [TMP]
└─ ScoreText  [TMP]
```

> Prefab 名は `TopRanker` のまま変えない。GUID を維持したいので**新規作成し直さない**
> （シーンの参照が切れる）。

## 4. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/TopRankingSlots.cs
namespace Takoda99.View.Ranking
{
    /// <summary>
    /// 上位N行の配置先。座標と寸法をシーンに持たせ、コードから数式で決めない。
    /// </summary>
    public sealed class TopRankingSlots : MonoBehaviour
    {
        /// <summary>1位から順に並べる。要素数が上位表示件数になる。</summary>
        [SerializeField] private RectTransform[] slots;

        /// <summary>スロット数。RankingPanelView の visibleCount より優先する。</summary>
        public int Count { get; }

        /// <summary>index は 0 始まり（0 = 1位）。範囲外は null。</summary>
        public RectTransform Slot(int index);

        /// <summary>index 番目のスロットの座標。</summary>
        public Vector2 PositionOf(int index);
    }
}
```

```csharp
// Assets/Scripts/View/Ranking/RankingPanelView.cs（既存を変更）
public sealed class RankingPanelView : MonoBehaviour
{
    [SerializeField] private RankingRowView rowPrefab;
    [SerializeField] private RectTransform rowsRoot;

    [Header("配置")]
    [SerializeField] private TopRankingSlots slots;      // ★追加
    [SerializeField] private RankingRowPalette palette;  // ★追加

    [SerializeField] private int visibleCount = 10;
    [SerializeField] private float rowMoveDuration = 0.25f;

    // ★削除する: rowHeight（スロットが座標を持つため不要）

    public void Apply(ClientState state);
}
```

```csharp
// Assets/Scripts/View/Ranking/RankingRowView.cs（既存に追加）
public sealed class RankingRowView : MonoBehaviour
{
    public void SetState(RankingRowViewState state);   // 既存・変更なし

    /// <summary>見た目を適用する。duration = 0 で即時。</summary>
    public void SetStyle(RankingRowStyle style, RankingRowPalette palette, float duration);
}
```

## 5. ふるまいの詳細

### 5.1 `visibleCount` とスロット数

**スロットの要素数を正とする。** `visibleCount` は残すが、`Awake` で次のように解決する。

```
1. slots が未配線     → 従来どおり visibleCount を使い、警告を出す（縦積みへフォールバック）
2. slots.Count < 10   → 警告して 10 にクランプ（01-ranking-panel.md §4 の要件は維持）
3. それ以外           → visibleCount = slots.Count
```

スロットを10個置いたのに `visibleCount` が 8 のまま、といった食い違いを**エディタ側の値で黙って上書きしない**。
数を変えたいならスロットを増減する、という一本道にする。

### 5.2 行の配置

`RankingRowLayout.Apply` の座標計算を差し替える。

```
変更前:  target = new Vector2(rect.anchoredPosition.x, -rowHeight * i)
変更後:  target = slots.PositionOf(i)
```

`SetSiblingIndex(i)` / `DOKill()` してから張り直す規則（[01](./01-ranking-panel.md) A2・A5）は**そのまま維持**する。

### 5.3 見た目の適用タイミング ★

| 要素 | タイミング | 理由 |
|---|---|---|
| 順位・名前・スコアの**文字** | **即時**（`SetState`） | [01](./01-ranking-panel.md) A4「読めない時間を作らない」を維持 |
| 寸法・フォントサイズ・**色** | `rowMoveDuration` 秒かけて補間 | 移動と同時に変化させる。到着時に順位相当の見た目になる |

**文字だけ即時、見た目は補間**が結論。
「入れ替え後に順位に応じた見た目になる」という企画意図は、移動と同じ長さのトゥイーンで満たす。
順位の数字まで補間すると A4 に反する。

### 5.4 `SetStyle` の実装規則

| # | 規則 |
|---|---|
| S1 | `duration <= 0` なら即時代入。初回描画・`RankingSnapshot` による全置換はこちらを使う |
| S2 | 前回と同じ `RankingRowStyle` なら**何もしない**（`RankingRowStyle.Equals`）。毎フレームの Tween 張り直しを防ぐ |
| S3 | Tween は `RectTransform` / `Image` / 各 `TMP_Text` ごとに `DOKill()` してから張る（[01](./01-ranking-panel.md) A5 と同じ理由） |
| S4 | フォントサイズの補間は `DOTween.To` で `TMP_Text.fontSize` を動かす。**Auto Size は使わない**（[../hud/02-order-word-emphasis.md](../hud/02-order-word-emphasis.md) と同じ方針） |
| S5 | `CanvasGroup.alpha` はここで触らない。生死の減光は `SetState` の責務 |

### 5.4.1 Tween の総数

10行 × (位置 + 寸法 + 色 + フォント3つ) = 最大60本が同時に走り得る。
1〜2Hz の更新では実測上問題にならないが、**S2 の早期リターンが効いていないと毎フレーム張り直しになる**。
テスト観点 §7-6 で必ず確認すること。

### 5.5 脱落済みが上位10件に入った場合

[01](./01-ranking-panel.md) §6 のとおり**リストから消さない**。
`Tone = Dead` と `deadAlpha` の両方が乗る（[../value-objects/12](../value-objects/12-ranking-row-style.md) §4.3）。

最終段階（120秒）では**全店が脱落する**ため、上位10行がすべて `Dead` になる瞬間がある。
これは異常ではない。この直後に `Result` へ遷移する。

## 6. 依存関係

- 依存する：[../value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md)、[../value-objects/11-rank-ordinal.md](../value-objects/11-rank-ordinal.md)、[01-ranking-panel.md](./01-ranking-panel.md)
- 依存される：[06-rank-swap-animation.md](./06-rank-swap-animation.md)
- **影響が及ぶ**：[03-spectator-ranking-view.md](./03-spectator-ranking-view.md) が同じ `RankingRowView` を使う。`SetStyle` を呼ばなければ Prefab の既定値のまま描かれるので、**観戦画面は変更しなくてよい**（99行に順位別の寸法を適用するとスクロールの行高が揃わなくなるため、呼ばないのが正しい）

## 7. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | 10スロットに10行が1位から順に収まる | `MainGameViewSampleDriver` |
| 2 | 手でスロットを動かすと、再生中の行の着地点が変わる | 手動 |
| 3 | 1位が金、2位が銀、3位が銅になる | 手動 |
| 4 | 4位の店が3位に上がると、行が (130,66) から (230,44) へ補間される | サンプル駆動で順位を入れ替える |
| 5 | 順位の数字だけは補間されず即座に変わる | 手動（目視） |
| 6 | 順位が変わらない行で Tween が新規に張られない | `DOTween.TotalPlayingTweens()` をログ、または S2 にブレークポイント |
| 7 | `slots` 未配線でも例外を出さず、従来の縦積みで描かれる | 手動 |
| 8 | スロットを8個にすると警告が出て10にクランプされる | 手動 |
| 9 | 99店を10秒間ランダムに入れ替えても行が生成破棄されない | サンプル駆動のストレスケース（既存） |

## 8. 未確定事項

- スロットを10個より増やすか（企画上10で確定しているが、構造としては可変）
- 1〜3位のスロットだけ別の並び（横並び等）にするか。スロット化したので**コード変更なしで試せる**
