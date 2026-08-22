# 08-自店ネオンパネル（下位パネル横に置く大表示）

> 参照する上流：[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md)／[hud/01-hud-composition.md](../hud/01-hud-composition.md) §5。矛盾したら上流優先。

これは [05-bottom-ranking-panel.md](./05-bottom-ranking-panel.md) の変更（横2列×縦15行 → 横3列×縦10行）で
空いた画面右下の領域に置く、自店の順位・名前・スコアの大表示である。

## 1. 責務

**する**

- 自店の順位・表示名・スコアを大きく表示する
- 見た目（色）を [value-objects/12-ranking-row-style.md](../value-objects/12-ranking-row-style.md) の
  `RankingRowTone` に応じて変える（金・銀・銅・警告・脱落確定など）

**しない**

- 順位・スコアを計算しない（`state.Rank` / `state.Score` をそのまま描く）
- 自分が落ちるかどうかを順位比較で推測しない（`CutStoreIds` / 下位パネルの表示範囲に従う。
  `SelfRankView.ResolveTone` と同じ根拠）
- 秒読み・画面全体アラートを持たない（[02-cull-countdown-panel.md](./02-cull-countdown-panel.md) の責務）

## 2. なぜ作るか

下位パネルが3列×10行へレイアウトを変えたことで、右パネルの下側に空きができる。
そこに「自分がいまどこにいるか」を大きく・目立つ色で見せることで、[hud/01](../hud/01-hud-composition.md)
の「順位が本選の画面の主役」という方針を右パネル側でも徹底する。
既存の `SelfRankView`（HUD上部の順位テキスト）と役割は重なるが、こちらは**視認性優先の単独パネル**であり、
`SelfRankView` を置き換えるものではない（両方が同じ state から同じトーンを引いて描くだけ）。

## 3. Unity構成

### 3.1 目標構成

```
RankingCanvas
└─ BottomRankers                          [BottomRankingPanelView] 3列×10行へ変更
└─ SelfRankNeonPanel                      [SelfRankNeonPanelView] ★新規（BottomRankers の右下に配置）
   ├─ Glow      [Image]  … Panel の外側に一回り大きく敷く発光風の縁取り
   ├─ Panel     [Image]  … 帯の色を乗せる本体
   ├─ RankText  [TMP] fs36（「78位」のように大きく）
   ├─ NameText  [TMP] fs16（「じぶん」＝自店の表示名）
   └─ ScoreText [TMP] fs14（「スコア 3400」）
```

`TopRanker.prefab`（`RankText`/`NameText`/`ScoreText`/`Panel`/`CanvasGroup` の構成）を土台にした
レイアウトを流用している。**現状シーンに直接構築してあり、Prefab 化はしていない**
（★未確定事項参照）。

### 3.2 ネオン表現の実装方針

このプロジェクトには発光（ブルーム）専用シェーダー・ポストエフェクトが無いため、
**Panel の外側に一回り大きい半透明 Image（`Glow`）を重ねるだけ**の簡易ネオン表現にする。

**トーンで色が変わるのは `Glow` と `RankText` だけ**で、本体 `Panel` の暗い塗り
（`{0.05, 0.05, 0.1, a 0.85}`）はシーンの authored 値のまま触らない。
`Panel` まで同じトーン色で塗ると、その上に載る `RankText` が背景と同色になって読めなくなる
（`SelfRankView` の「色を変えるのは順位テキストだけ」と同じ理由）。
また `Glow` はパレット色の**色相だけ**を受け取り、authored なアルファ（半透明）は維持する。
不透明色をそのまま代入すると発光のにじみが消えてベタ塗りの矩形になるため。

本格的なブルームにしたい場合は、Editor 側で `Glow` に URP の発光マテリアル／Bloom 対応シェーダーを
差し替えるだけで拡張できる（コンポーネント側は Image.color を差し替えるだけなので変更不要）。

## 4. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/SelfRankNeonPanelView.cs
namespace Takoda99.View.Ranking
{
    public sealed class SelfRankNeonPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Image glowImage;   // Panel は触らない（§3.2）
        [SerializeField] private RankingRowPalette palette;
        [SerializeField] private int bottomRangeCount = SelfRankToneResolver.DefaultBottomRangeCount;

        public void Apply(ClientState state);
        public void SetPanelVisible(bool visible);
    }
}
```

## 5. ふるまいの詳細

### 5.1 表示する値

表示値は `SelfRankViewState.From(Rank, Score, AliveCount)` を経由して作る（`SelfRankView` と共通）。
`RankText` の「順位未確定は `--`」という約束をこのパネルだけ別に書かないため。

| テキスト | 値 |
|---|---|
| `RankText` | `state.Rank >= 1 ? $"{RankText}位" : RankText`（未確定時は `--`。「`--位`」にしない） |
| `NameText` | `state.DisplayNames[state.SelfStoreId]`（`Renderer.ResolveSelfDisplayName` と同じ引き方） |
| `ScoreText` | `$"スコア {ScoreText}"` |

`Apply` は 2〜4Hz で呼ばれ続けるため、**`SelfRankViewState` と表示名が前回と等しい呼び出しでは
文字列の組み立てと TMP への代入をまとめて省く**（`SelfRankView` と同じ理由：メッシュ再構築と
WebGL の GC を避ける）。

### 5.2 トーンの決め方

`SelfRankView` と**同じ関数**を呼ぶ（`SelfRankToneResolver.Resolve`。中身は
[value-objects/12](../value-objects/12-ranking-row-style.md) の `RankingRowStyle.ResolveSelfRankTone`）。
1位は金、2位は銀、3位は銅、`CutStoreIds` に入っていれば `Doomed`（脱落確定＝赤系）、
下位パネルの表示範囲に入っていれば `AtRisk`（警告）。

判定を2箇所に書き写さないのは、**片方だけ直したときに HUD の順位テキストと右下パネルで
自店の色が食い違うため**。色は `RankingRowPalette.Of(tone)` から引き、`Glow` と `RankText` に適用する
（`Panel` は据え置き。§3.2）。**このパネル自身は閾値・脱落条件を一切持たない。**

### 5.3 表示・非表示

`Renderer` は他のランキング系パネルと同じ扱いにする。

| 状況 | 扱い |
|---|---|
| `GameBeforeView` 保持中 | `SetPanelVisible(false)`（`Apply` を呼ばない） |
| 試合中 | `Apply(state)` |
| `MatchEnd` 受信後 | `SetPanelVisible(false)` |

`Apply` は描画の直前に必ず `SetPanelVisible(true)` を呼ぶ。
これが無いと `OnMatchEnd` で畳んだあと次の試合が始まっても開き直せず、パネルが消えたままになる
（`RankingPanelView` / `BottomRankingPanelView` と同じ約束）。

## 6. 依存関係

- 依存する：`ClientState.Rank` / `ClientState.Score` / `ClientState.SelfStoreId` /
  `ClientState.DisplayNames` / `ClientState.Cull`、
  `SelfRankToneResolver`（`SelfRankView` と共通。中身は
  [value-objects/12](../value-objects/12-ranking-row-style.md) の `RankingRowStyle.ResolveSelfRankTone` と
  `RankingRowsBuilder.IsInBottomRange`）、`SelfRankViewState`、`RankingRowPalette`
- 依存される：なし
- **`Renderer` への追加**：`[SerializeField] private Ranking.SelfRankNeonPanelView selfRankNeonPanel;` を足し、
  `selfRank.Apply(state)` の直後に、他パネルと同じ `holding` 分岐で
  `SetPanelVisible(false)` / `Apply(state)` を呼び分ける。`OnMatchEnd` でも `SetPanelVisible(false)` する。

## 7. 未確定事項

- **シーンに直接構築してあり、まだ Prefab 化していない。** 手書き YAML で Prefab Instance の
  override ブロックを起こすのは壊れやすいため、意図的に「シーンが唯一の正」にしてある。
  Prefab として再利用したくなったら、Editor でシーン上の `SelfRankNeonPanel` を
  `Assets/Prefabs/MainGame/` へドラッグして Prefab を作り直すこと
  （Unity が正しい参照を書いてくれる）。**未リンクの Prefab アセットを別途置かないこと**
  ——シーンと二重管理になり、片方だけ直して気付かない事故になる。
- 座標（`AnchoredPosition {270, -150}` / `SizeDelta {230, 130}`）は「3列×10行にした下位パネルの下に
  収まるはず」という計算上の見積もり値。実機のセーフエリア・他パネルとの余白は Editor 上で最終調整が要る。
- ネオンの発光表現は単純な半透明 Image 2枚重ねの簡易実装（§3.2）。本格的なブルームにするかは
  アートディレクション次第。
- `RankText` のフォントサイズ 36 は TopRanker.prefab 系の基準値からの目測。実機で大きすぎ／小さすぎたら
  Inspector 上の値のみで調整可能（コード変更不要）。

## 追記：ネオンの作り方は `InfoPanel` / `TopRanker` に合わせる

当初の `Glow` は**組み込みの `Background` スプライトを半透明で敷いただけ**で、ネオンになっていなかった。
`CullPanel/InfoPanel` と `TopRanker.prefab` と同じ3点セットに揃える。

| 要素 | 中身 |
|---|---|
| `NeonFrame`（旧 `Glow`） | `Images/UI/NeonFrame.png` / `Type = Sliced` / `FillCenter = オフ` / `sizeDelta (12, 12)` のストレッチ |
| `Panel` | 半透明の塗り（白 α0.13）。**色は据え置き**（`SelfRankNeonPanelView` は触らない） |
| `RankText` / `NameText` / `ScoreText` | `ThemedText._materialPreset` に `NotoSansJP-Black SDF - Neon.mat` |

`SelfRankNeonPanelView.glowImage` は引き続き `NeonFrame` を指す。トーンで色相を差し替える挙動と、
authored なアルファを保つ扱いはそのまま（枠のアルファは 1 になったので、パレット色がそのまま乗る）。
