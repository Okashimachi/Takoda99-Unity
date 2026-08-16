# 07-オーディエンスパネル（11〜99位の一覧グリッド）

> 参照する上流：[本選企画書 3.3・3.5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `RankingEntry`／`pureC#` [match-state/02-ranking-store.md](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)。矛盾したら上流優先。

自店が脱落したとき（`MainGame/ResultCanvas`）に、**上位10店を除く 11〜99 位の89店**を一覧で見せるグリッド。
上位10店は既存の `Result/RankList`（[../result-view/01](../result-view/01-personal-result-view.md)）と画面左のランキングパネルが持つので、**このパネルは11位以降だけを引き受ける**。

## 1. 責務

**する**

- 11〜99位の89店を、**9列×10行**のグリッドに並べる
- 各セルに順位・屋号（・スコア）を出す
- 行 Prefab（`LostRanker`）をセルの寸法へ詰めて描く
- 自店の行を強調する（`RankingRowView` の `HighLight` に任せる）

**しない**

- 順位・スコアを計算しない（サーバー権威。並びも `Rank` 昇順のまま**再ソートしない**）
- 上位10位を描かない（重複させない）
- 「次へ」ボタンの挙動を持たない（§5.5。空けたセルに置くだけで、押したときの遷移は既存の `EliminationResultView` の責務）
- 入れ替えアニメーションを持たない（§5.4）

## 2. なぜ作るか

脱落した瞬間に見えるのは「自分の順位」と「上位10店」だけで、**99人中89人が入る中間層が画面から消える**。
「自分の周りに誰がいたか」が分からないと、順位という数字が体験に結び付かない。

99人という規模はこのゲームの中心的な体験であり、**それを一望できる画面を最後に一度だけ出す**。

## 3. Unity構成

### 3.1 現在の階層（★実装済み。この上に組む）

```
MainGame/ResultCanvas
├─ BG                                 [Image]      … 現在 非アクティブ
├─ Result           300×440 @ x=0                  … 現在 非アクティブ。自分の順位＋上位10＋次へ
└─ AudiencePanel    550×440 @ x=+125  ★このファイルの対象
   ├─ Panel         [Image] 全面ストレッチ          … 背景
   └─ RowsRoot                                     … 実行時に LostRanker が89個生成される
```

`AudiencePanel` に `AudiencePanelView` を付ける。

### 3.2 位置が意図どおりであることの確認

| 要素 | 横方向の範囲 |
|---|---|
| 画面左の上位ランキングパネル（`RankingCanvas/TopRankers` 250幅 @ x=-270） | **-395 〜 -145** |
| 中央（`Main` 290幅 @ x=0） | -145 〜 +145 |
| 画面右の下位ランキングパネル（250幅 @ x=+270） | +145 〜 +395 |
| **`AudiencePanel`（550幅 @ x=+125）** | **-150 〜 +400** |

`AudiencePanel` は上位ランキングパネルの右端（-145）のすぐ手前から始まり、中央と右をすべて覆う。
**上位10位だけが左に残り、その右隣に11位以降が広がる**という画になる。

### 3.3 ★`RowsRoot` は中央へ直すこと

現在 `RowsRoot` は `anchoredPosition = (145, 0)` に置かれている。
§5.1 の座標式は `RowsRoot` の中心を基準にするため、**`(0, 0)` へ直す**（`sizeDelta` は使わないので何でもよい）。
直さないとグリッド全体が右へ145pxずれて画面外へはみ出す。

### 3.4 行 Prefab

`Assets/Prefabs/MainGame/LostRanker.prefab`（guid `8aef811940d15584194f363fdb9c046c`）を使う。
中身は `TopRanker` と同じ構成で、`RankingRowView` が付いている。

```
LostRanker  [RankingRowView] 230×44
├─ HighLight  [Image]  … 自分の行だけ点く
├─ Panel      [Image]
├─ RankText   [TMP] fs16  anchor(0, 0.5)    ← 左端
├─ NameText   [TMP] fs20  anchor(0.5, 0.5)  ← 中央
└─ ScoreText  [TMP] fs16  anchor(1, 0.5)    ← 右端
```

> **★このままでは入らない。** Prefab は 230×44 の**横長**で、3つのテキストが横一列に並んでいる。
> セルは 61×44 の**ほぼ正方形**（§5.2）なので、横並びのままでは3つとも溢れて重なる。
> §5.3 のとおり**縦積みへ組み替える**。

## 4. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/AudiencePanelView.cs
namespace Takoda99.View.Ranking
{
    /// <summary>11〜99位の89店を 9列×10行 のグリッドで一覧するパネル。</summary>
    public sealed class AudiencePanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;   // LostRanker
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private RankingRowPalette palette;

        /// <summary>先頭から除外する件数。上位パネルの表示件数と揃える（既定10＝1〜10位を除く）。</summary>
        [SerializeField] private int skipCount = 10;

        /// <summary>並べる件数。99 - skipCount（既定89）。</summary>
        [SerializeField] private int visibleCount = 89;

        [SerializeField] private int columnCount = 9;
        [SerializeField] private int rowsPerColumn = 10;

        /// <summary>グリッド全体の寸法。ここから1マスの寸法を割り出す（既定は AudiencePanel と同じ 550×440）。</summary>
        [SerializeField] private Vector2 gridSize = new Vector2(550f, 440f);

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
    /// 先頭 skipCount 件を飛ばして count 件。並びは Rank 昇順のまま（再ソートしない）。
    /// 足りなければあるだけ返す。
    /// </summary>
    public static IReadOnlyList<RankingRowViewState> BuildRange(
        RankingTable ranking,
        string selfStoreId,
        int authoritativeRank,
        int authoritativeScore,
        int skipCount,
        int count);
}
```

## 5. ふるまいの詳細

### 5.1 ★グリッドの並び順（中核）

**列優先**（上から下へ埋め、埋まったら次の列へ）。

```
index = 10 * col + row          （0..88）
rank  = 11 + index = 11 + 10 * col + row
```

9列×10行＝**90マス**に**89店**なので、**ちょうど1マス余る**。余るのは最後のマス（`col 8, row 9`）＝**右下角**。

|  | c0 | c1 | c2 | c3 | c4 | c5 | c6 | c7 | c8 |
|---|---|---|---|---|---|---|---|---|---|
| **r0** | **11** | 21 | 31 | 41 | 51 | 61 | 71 | 81 | **91** |
| r1 | 12 | 22 | 32 | 42 | 52 | 62 | 72 | 82 | 92 |
| r2 | 13 | 23 | 33 | 43 | 53 | 63 | 73 | 83 | 93 |
| r3 | 14 | 24 | 34 | 44 | 54 | 64 | 74 | 84 | 94 |
| r4 | 15 | 25 | 35 | 45 | 55 | 65 | 75 | 85 | 95 |
| r5 | 16 | 26 | 36 | 46 | 56 | 66 | 76 | 86 | 96 |
| r6 | 17 | 27 | 37 | 47 | 57 | 67 | 77 | 87 | 97 |
| r7 | 18 | 28 | 38 | 48 | 58 | 68 | 78 | 88 | 98 |
| r8 | 19 | 29 | 39 | 49 | 59 | 69 | 79 | 89 | **99** |
| r9 | 20 | 30 | 40 | 50 | 60 | 70 | 80 | 90 | **■空白** |

| 位置 | 順位 |
|---|---|
| 左上 | **11位** |
| 右上 | **91位** |
| 右下から1つ上 | **99位** |
| **右下角** | **空白**（→「次へ」ボタン。§5.5） |

> **列優先である理由：** 1列下がると +1、1列右へ動くと +10 になる。
> 「左上が11位・右上が91位」という配置は、**縦10・横9の列優先でしか成立しない**
> （横10・縦9の列優先だと右上が92位になり、行優先だと右上が20位になる）。

**空白マスのために分岐を書かない。** 89件を index 0..88 に置くだけで、90マス目は自然に使われない。
`Ranking.Rows` が99件に満たない場合も、あるだけ置いて残りが空くだけでよい（例外を出さない）。

### 5.2 1マスの寸法

```
cellWidth  = gridSize.x / columnCount   = 550 / 9  = 61.11
cellHeight = gridSize.y / rowsPerColumn = 440 / 10 = 44
```

座標は `RowsRoot` の中心を原点として：

```
x = (col - (columnCount   - 1) / 2) * cellWidth    // col 0 → -244.4、col 8 → +244.4
y = ((rowsPerColumn - 1) / 2 - row) * cellHeight   // row 0 → +198、 row 9 → -198
```

この式でグリッドは `AudiencePanel` にちょうど収まる（左端 -244.4 - 30.6 = **-275** ＝ パネル左端、
上端 198 + 22 = **220** ＝ パネル上端）。

> **高さ 44 は `LostRanker` の元の高さと一致する。** 詰めるのは幅だけ（230 → 61.11）。
> 「パネルの大きさに合うように縦横比を変更する」とは、具体的には**横方向に約 0.27 倍へ潰す**こと。
> `RankingRowStyle.Size` を `(61.11, 44)` にすれば `RankingRowView.SetStyle` が `sizeDelta` を合わせる。

### 5.3 セルの中身（★暫定。実機を見てから詰める）

61×44 に横並びは入らないので**縦積み**にする。

```
┌───────────┐
│   11th.   │  RankText   fs11
│  たこ屋    │  NameText   fs10
│   9999    │  ScoreText  fs10
└───────────┘
   61 × 44
```

| 要素 | offset | size | fontSize |
|---|---|---|---|
| `RankText` | (0, +14) | (58, 14) | 11 |
| `NameText` | (0, 0) | (58, 14) | 10 |
| `ScoreText` | (0, -14) | (58, 14) | 10 |

`RankingRowStyle.ForAudienceCell(cellSize, tone)` を追加して返す
（[../value-objects/12](../value-objects/12-ranking-row-style.md) §3.2 の表に1行足す）。
寸法・フォントサイズ・テキスト配置の適用は `RankingRowView.SetStyle` が既に行う。

> **★`LostRanker.prefab` 側で直すこと：** `SetStyle` は `anchoredPosition` と `sizeDelta` は動かすが、
> **アンカーは動かさない**。現在3つのテキストは左・中央・右とバラバラのアンカーを持つため、
> 縦積みにするには**3つとも中央 (0.5, 0.5) へ揃える**必要がある。

> **スコアを出すかは未確定（§8）。** 幅58pxで屋号は5文字程度しか入らない。
> スコアを捨てて2段にすれば屋号に8文字ほど使える。**実機で並べてから決める。**
> どちらでも切り替えられるよう、スコアの表示可否は Inspector の bool にしておく。

### 5.4 入れ替えアニメーションを持たない

[06-rank-swap-animation.md](./06-rank-swap-animation.md) の演出は**適用しない**。

- 89行が同時に動くと、何が起きたか分からないうえ WebGL で重い
- リザルトは「結果を読む」画面であり、動かす必要がない

`RankingRowLayout.Apply` を使う場合は `moveDuration = 0`・`emphasisScale = 1` にする
（下位パネルが `emphasisScale = 1` だけ落としているのと同じ考え方を、移動にも広げる）。

行のプールは既存の `RankingRowPool` をそのまま使う（storeId キー）。

### 5.5 右下角の空白と「次へ」ボタン

余った1マス（`col 8, row 9`、中心座標 `(+244.4, -198)`）には**このパネルは何も置かない**。
「次へ」ボタンはシーン上で手置きし、`AudiencePanel` の子として**この位置に重ねる**。

**ボタンの挙動はこのパネルの責務ではない。** 遷移は既存の `EliminationResultView.nextButton` が持つ。
このパネルは「そこを空けておく」ことだけを保証する。

### 5.6 表示・非表示

| 状況 | 扱い |
|---|---|
| `ResultCanvas` が閉じている（試合中） | パネルごと非表示 |
| 自店が脱落（`ResultCanvas` 表示） | **描く**。これがこのパネルの本番 |
| `Ranking.Rows` が空 | パネルごと非表示 |
| 観戦中に順位が動く | **描き続ける**（[03](./03-spectator-ranking-view.md)・[05](./05-bottom-ranking-panel.md) §5.5 と同じ） |

> **★脱落した時点では、自分より上の順位はまだ確定していない。**
> サーバーは生存店に現在順位、脱落店に確定順位を入れて送り続ける（[README](./README.md) 前提5）。
> 早い段階で脱落すると、グリッドの大半は**まだ動いている現在順位**になる。
> これは `EliminationResultView` が既に持っている性質（「未確定分はリアルタイムの現在順位」）と同じで、**バグではない**。
> 凍結すべきかどうかは §8。

## 6. 依存関係

- 依存する：`ClientState.Ranking` / `ClientState.Rank` / `ClientState.Score` / `ClientState.SelfStoreId`、既存の `RankingRowPool` / `RankingRowView` / `RankingRowPalette`、[../value-objects/08](../value-objects/08-ranking-row-view-state.md)、[../value-objects/12](../value-objects/12-ranking-row-style.md)
- 依存される：なし
- **`Renderer` への追加**：`[SerializeField] private Ranking.AudiencePanelView audiencePanel;` を足し、`rankingPanel` と同じ箇所で `Apply(state)` を呼ぶ

## 7. テスト観点

`BuildRange` とグリッドの座標式は EditMode で完結する。

| # | 観点 | 方法 |
|---|---|---|
| 1 | 99件の表から11〜99位の89件が返る | EditMode |
| 2 | 先頭10件（1〜10位）が含まれない | EditMode |
| 3 | 並びが `Rank` 昇順のまま（再ソートしていない） | EditMode |
| 4 | `Rows` が89件未満でも例外が出ず、あるだけ返る | EditMode |
| 5 | `Rows` が空で空リストが返る（パネル非表示の合図） | EditMode |
| 6 | 自分が範囲に入ると `state.Rank` / `state.Score` で上書きされる | EditMode |
| 7 | index 0 が (col0, row0)、index 89 が存在しない（90マス目は空く） | EditMode |
| 8 | 座標式：col0 の左端がパネル左端、row0 の上端がパネル上端に一致する | EditMode |
| 9 | 11位が左上、91位が右上、99位が右下の1つ上に出る | 手動 |
| 10 | 右下角が空いていて「次へ」ボタンが収まる | 手動 |
| 11 | 屋号がセル幅で見切れず読める（§5.3 の暫定値の妥当性） | 手動 |
| 12 | 自分の行の `HighLight` が点く | 手動 |

## 8. 未確定事項

- **スコアを出すか**（§5.3）。幅58pxで屋号5文字 vs スコアを捨てて屋号8文字。実機で並べてから決める
- セルのフォントサイズ（fs10〜11 は暫定。`LostRanker` の元値 16/20/16 からは大幅に落とす必要がある）
- 脱落時点でグリッドを**凍結するか**、観戦中も更新し続けるか（§5.6）。更新し続けると、読んでいる最中に順位が動く
- 空白マスを右下角に固定してよいか（「次へ」ボタンの最終的な置き場所が変わったら §5.1 の並び順ごと見直しになる）
- 帯（`RankingRowTone`）を付けるか。リザルトでは全員脱落済みなので、全行 `Dead` にすると全部暗くなる。`Normal` 固定でよいか
