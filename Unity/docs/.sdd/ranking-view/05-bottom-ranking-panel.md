# 05-下位ランキングパネル（足切り警告）

> 参照する上流：[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `RankingEntry` `ForcedEliminationWarning`／`pureC#` [match-state/02](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)・[match-state/03](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)。矛盾したら上流優先。

これは [01-ranking-panel.md](./01-ranking-panel.md) §10 の未確定事項「下位側も同時に見せるか」に対する**決定**である。

## 1. 責務

**する**

- 下位**30行を常に**描く（生存者が30人を切ったら脱落済みで埋める）
- 各行を3つの帯（脱落確定／警告／通常）で塗り分ける
- 「今は圏内でないが、落ち得る人」を**事前に**見せる

**しない**

- 順位・スコアを計算しない
- 自分が落ちるかどうかを順位比較で推測しない（`CutStoreIds` / `SelfAtRisk` に従う）
- 秒読み・画面全体アラートを持たない（[02-cull-countdown-panel.md](./02-cull-countdown-panel.md) の責務）
- スコアを表示しない（§3.2）

## 2. なぜ作るか

足切りは**順位の絶対値**で行われるため、スコアの急変動で「さっきまで圏外だったのに落ちた」が起こる。
これを理不尽に感じさせないために、**圏内・圏外の2値ではなく「圏外だが危ない」を見せる**。

```
淘汰24人の段階                     淘汰20人の段階
┌─ 下位30人 ─────────┐            ┌─ 下位30人 ─────────┐
│  6人  … 警告帯       │            │ 10人  … 警告帯       │
│       「今は圏外だが  │            │       すぐ上の10人   │
│        すぐ落ちる」   │            │                     │
├────────────────────┤ ← CutLine  ├────────────────────┤
│ 24人  … 脱落確定帯   │            │ 20人  … 脱落確定帯   │
└────────────────────┘            └────────────────────┘
```

**表示件数を30に固定するのは、淘汰人数（24/20/20/15/10/10）より常に多い数だから。**
どの段階でも「確定帯 + 警告帯」の2層が必ず画面に出る。

## 3. Unity構成

### 3.1 目標構成

```
RankingCanvas
└─ BottomRankers      [BottomRankingPanelView]   ★現在 "BottomRrankers"（綴り誤り）。改名する
   ├─ BG       [Image]
   └─ RowsRoot                … 実行時に BottomRanker.prefab が30個生成される
```

現在ここに置かれている `BottomRanker` のインスタンス1個は**削除する**。
行はプールから生成されるため、手置きは要らない（上位と違い、下位は等間隔の縦積みなのでスロットも要らない）。

> 綴りの修正（`BottomRrankers` → `BottomRankers`）は、参照している箇所が無いいま行う。

### 3.2 行 Prefab

`BottomRanker.prefab` をそのまま使う（[c28ae36](https://github.com/Okashimachi/Takoda99-Unity/pull/61) で配線済み・変更不要）。

```
BottomRanker  [RankingRowView] [CanvasGroup]
├─ HighLight  [Image]  … 自分の行だけ点く
├─ Panel      [Image]  … ★帯の色を乗せる
├─ RankText   [TMP] fs12
├─ NameText   [TMP] fs18
└─ ScoreText  [TMP] fs12  ← 非アクティブのまま（★意図的）
```

**スコアを出さないのは意図的。** 下位で必要な情報は「自分が何位で、あと何人で切られるか」であり、
スコアの絶対値は行動を変えない。29px の行に3つ数字を詰めると読めなくなる。

`ScoreText` は非アクティブのままでよい。`RankingRowView.SetState` は
`scoreText.text` に代入するだけで `SetActive` を触らないため、副作用は無い。

## 4. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/BottomRankingPanelView.cs
namespace Takoda99.View.Ranking
{
    /// <summary>下位N行（既定30）を常に描き、足切りの帯で塗り分けるパネル。</summary>
    public sealed class BottomRankingPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private RankingRowPalette palette;

        /// <summary>表示件数。淘汰人数の最大(24)より大きい値にする。</summary>
        [SerializeField] private int visibleCount = 30;

        /// <summary>1行の高さ(px)。RankingRowStyle.BottomRowSize.y と一致させる（既定 29）。</summary>
        [SerializeField] private float rowHeight = 29f;

        /// <summary>横の列数。columnCount × rowsPerColumn は visibleCount と一致させる（既定 3）。</summary>
        [SerializeField] private int columnCount = 3;

        /// <summary>1列あたりの行数。columnCount × rowsPerColumn は visibleCount と一致させる（既定 10）。</summary>
        [SerializeField] private int rowsPerColumn = 10;

        /// <summary>列と列の中心間の距離(px)。行の幅より広くする（既定 81.33）。</summary>
        [SerializeField] private float columnSpacing = 81.33f;

        [SerializeField] private float rowMoveDuration = 0.25f;

        public void Apply(ClientState state);

        public void SetPanelVisible(bool visible);
    }
}
```

```csharp
// Assets/Scripts/View/ValueObjects/RankingRowViewState.cs（既存の RankingRowsBuilder に追加）
public static class RankingRowsBuilder
{
    /// <summary>
    /// 下位 count 件。生存者が count 人を切ったら、確定順位を持つ脱落済みの店で埋める。
    /// </summary>
    public static IReadOnlyList<RankingRowViewState> BuildBottom(
        RankingTable ranking,
        string selfStoreId,
        int authoritativeRank,
        int authoritativeScore,
        int aliveCount,
        int count);
}
```

## 5. ふるまいの詳細

### 5.1 表示する30人の選び方 ★中核

`ClientState.Ranking.Rows` は**99店すべて**を `Rank` 昇順で持つ。
サーバーは脱落店にも確定順位を入れて送り続けるため（`Takoda99-Proto/proto/messages.go`
「生存店は現在順位、脱落店は確定順位（以後不変）」）、**埋め合わせ用のデータは常に揃っている**。

```
start = Max(0, aliveCount - count)
表示  = Rows[start .. start + count)      （Rank 昇順のまま。再ソートしない）
```

この1本の式だけで、生存者が多い段階と少ない段階の両方が正しくなる。

| 段階 | 生存 | `start` | 表示される順位 | 内訳 |
|---|---|---|---|---|
| 開始 | 99 | 69 | 70〜99位 | 生存30 |
| 1 終了 | 75 | 45 | 46〜75位 | 生存30 |
| 2 終了 | 55 | 25 | 26〜55位 | 生存30 |
| 3 終了 | 35 | 5 | 6〜35位 | 生存30 |
| 4 終了 | 20 | 0 | 1〜30位 | **生存20 + 脱落10** |
| 5 終了 | 10 | 0 | 1〜30位 | **生存10 + 脱落20** |

生存20を下回ると1位が下位パネルにも映る。これは**仕様**であり、バグではない。
上位パネルと重複するが、最終盤は「残り全員が下位でもある」という状態そのものが正しい。

> `aliveCount` は `ClientState.AliveCount`（`EvaluationUpdate` 由来の権威値）を使う。
> `Rows` を走査して `Alive == true` を数え直さない。差分の取りこぼしで数がズレるため。

### 5.2 帯の決め方

各行の `RankingRowTone` は [../value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md) §4.2 の表に従う。
このパネルに出ている時点で `Normal` にはならない（生存していれば `AtRisk` 以上）。

| 行の状態 | `Tone` |
|---|---|
| `Alive == false` | `Dead` |
| `CutStoreIds` に含まれる | `Doomed` |
| 上記以外（生存・確定圏外） | `AtRisk` |

`ClientState.Cull` が `null`（`ForcedEliminationWarning` 未受信）のときは、
**全行 `AtRisk`** ではなく **全行 `Normal`** にする。
警告が来ていない時点で全員を警告色にすると、警告が意味を失うため。

### 5.3 行の配置

上位と違いスロットは使わない。すべて同じ高さなので数式で並べる。

**縦一列（30行）ではなく、横3列×縦10行のグリッドに並べる。**
1列30行だと画面外まで溢れるため（[c28ae36]の初期配置の反省）、パネル内に収まる3列に分割する
（★変更：旧仕様は横2列×縦15行。画面右上に収める形へレイアウトを変更し、空いた右下領域に
[08-self-rank-neon-panel.md](./08-self-rank-neon-panel.md) の自店ネオンパネルを置く）。
`index` は列優先で埋める（0列目を上から10件埋めたのち、1列目・2列目へ移る＝1〜10位が左列、
11〜20位が中央列、21〜30位が右列）。

```
column = index / rowsPerColumn          // rowsPerColumn = 10
row    = index % rowsPerColumn
x = (column - (columnCount - 1) / 2) * columnSpacing
y = ((rowsPerColumn - 1) / 2 - row) * rowHeight
```

> **★行の寸法は prefab の authored 値ではなく `RankingRowStyle.ForBottomBand` が返す `Size`。**
> 80px では「二回り小さいフォントの全角6文字の屋号」＋「`22nd`」が入らず屋号が隣の列へはみ出したため
> 118px まで広げたが、3列（366px）だと `BottomRrankers`（370×300）に収まりきらなかった。
> そこで **`RankingRowStyle.BottomPanelScale = 2/3` で全体を縮めている**。
>
> | | 縮尺前 | 実際の値（× 2/3） |
> |---|---|---|
> 高さだけは別係数 **`BottomRowHeightScale = 1.5`** を重ねて、横は詰めたまま縦にゆとりを持たせている。
>
> | | 縮尺前 | 実際の値 |
> |---|---|---|
> | 行の寸法 | 118 × 29 | 約 78.7 × 29（幅 × 2/3、高さ × 2/3 × 1.5） |
> | 列間隔 | 122 | 約 81.3 |
> | フォント | 9.72 | 6.48（**幅の縮尺だけが効く**） |
> | グリッド全体 | 366 × 290 | 約 244 × 290 |
>
> **大きさを変えるときは `BottomPanelScale`（横と文字）と `BottomRowHeightScale`（縦）だけを触る。**
> 幅も文字も同じ倍率で縮むので、「全角6文字の屋号が入る」関係
> （[value-objects/12](../value-objects/12-ranking-row-style.md) §3.2.2）は保たれる。
> ただし `rowHeight` / `columnSpacing`（シーンの値）は自動では追従しないので、必ず揃えること。

### 5.3.1 画面内の収まり（縦を伸ばすときの上限）

`BottomRrankers` は `RankingCanvas`（参照解像度 800×600・`MatchWidthOrHeight = 0` ＝幅合わせ）の
中心から (270, 70) にある。**縦を伸ばすと上下から同時に余白が減る。**

| | 位置 | 現在の余白 |
|---|---|---|
| 画面上端（16:9 では y = 225） | グリッド上端 y = 215 | **10px** |
| 自店パネル `SelfRankNeonPanel`（上端 y = -85） | グリッド下端 y = -75 | **10px** |

横は x[148, 392] で画面内（±400）に収まっている（`BottomRrankers` の BG は 370px 幅のため
右へ 55px はみ出すが、行そのものは出ない）。
**`BottomRowHeightScale` をこれ以上上げるなら、先に `BottomRrankers` の位置か自店パネルの位置を動かすこと。**

`GridSlotSource`（`RankingSwapSettings.cs`）がこの計算を持つ。
既存の `RankingRowLayout.Apply` は `IRankingSlotSource` を介して座標を受け取るだけなので変更不要。
（`RankingPanelView` は [04](./04-top-ranking-slots.md) でスロット版へ移るため、
数式配置はこのパネルが引き取る形になる）。

行のプールは既存の `RankingRowPool` をそのまま使う（storeId キー）。
表示範囲から出た店は `ReleaseAllExcept` でプールへ戻る。

### 5.4 自分の行

`IsSelf` の判定と `HighLight` の点灯は `RankingRowView` が既に行う。
自分が下位30人に入っていない（＝上位側にいる）ときは、単に含まれないだけでよい。
**上位パネルのように「自分を末尾に足す」ことはしない**（下位パネルの意味は「危険水域の一覧」であり、
安全圏にいる自分を混ぜると意味が壊れる）。

自分の順位・スコアの権威値上書き（[01](./01-ranking-panel.md) §3.1）は、
自分がこのリストに含まれる場合のみ適用する。

### 5.5 パネルの表示・非表示

| 状況 | 扱い |
|---|---|
| `GameBeforeView` 保持中 | 描かない（[01](./01-ranking-panel.md) §7 と同じ） |
| `Ranking.Rows` が空 | パネルごと非表示 |
| 自店が脱落（`Spectating`） | **描き続ける**。観戦中も足切りは進む |
| 最終段階（生存10） | **畳まない。** §6 のとおり表示内容が自然に変わる |
| `Phase == Result` | 畳んでよい |

## 6. 最終段階でこのパネルが持つ意味

生存10人になると、下位30行は「生存10 + 直近に散った20」になる。
上位パネルと重複するが、**分岐を書かずにそのまま表示し続ける**。

理由は3つ。

1. §5.1 の式が自動的にこの状態を作る（**最終段階のための特別扱いが1行も要らない**）
2. 脱落済みが `Dead` で減光されて並ぶので、「自分がさっきまで居た場所」「直前に散った店」が見える
3. 非表示にすると画面が寂しくなる

**この挙動は段階4以降で連続的に立ち上がるので、最終段階だけ挙動が変わって見えることはない。**

## 7. 依存関係

- 依存する：`ClientState.Ranking` / `ClientState.Cull` / `ClientState.AliveCount`、[../value-objects/12](../value-objects/12-ranking-row-style.md)、[../value-objects/11](../value-objects/11-rank-ordinal.md)、既存の `RankingRowPool`
- 依存される：なし
- **`Renderer` への追加**：`[SerializeField] private Ranking.BottomRankingPanelView bottomRankingPanel;` を足し、`rankingPanel` と同じ箇所で `Apply(state)` を呼ぶ

## 8. テスト観点

`BuildBottom` は EditMode で完結する。ここを固めれば View は描くだけになる。

| # | 観点 | 方法 |
|---|---|---|
| 1 | 生存99で 70〜99位の30行 | EditMode |
| 2 | 生存55で 26〜55位の30行 | EditMode |
| 3 | 生存35で 6〜35位の30行（境界。まだ全員生存） | EditMode |
| 4 | 生存20で 1〜30位＝生存20＋脱落10 | EditMode |
| 5 | 生存10で 1〜30位＝生存10＋脱落20 | EditMode |
| 6 | 生存0（最終段階直後）でも例外が出ない | EditMode |
| 7 | `Rows` が30件未満でも例外が出ず、あるだけ返る | EditMode |
| 8 | 並びが `Rank` 昇順のまま（再ソートしていない） | EditMode |
| 9 | `CutStoreIds` の店が `Doomed`、残る生存が `AtRisk` | EditMode |
| 10 | `Cull == null` で全行 `Normal` | EditMode |
| 11 | 段階が進んでも行が生成破棄されない（プールが効いている） | サンプル駆動 |
| 12 | 自分が下位に入ると `HighLight` が点く | 手動 |

`MainGameViewSampleDriver` に `aliveCount` を段階どおり（99→75→55→35→20→10）動かすケースを足すこと。

## 9. 未確定事項

- `visibleCount = 30` の妥当性。淘汰24人の段階で警告帯が6行しかない。実機で薄いようなら 32〜36 に増やす（コード変更なしで変えられる）
- 警告帯と確定帯の境目に区切り線を入れるか
