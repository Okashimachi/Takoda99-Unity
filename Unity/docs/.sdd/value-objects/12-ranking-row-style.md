# 12-ランキング行の見た目（順位・帯から決まる）

> 参照する上流：[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `ForcedEliminationWarning`。矛盾したら上流優先。

## 1. 責務

**する**

- **順位**から、その行が取るべき寸法・フォントサイズ・配色を決める
- **足切りの帯**（脱落確定／警告／通常）から、その行が取るべき配色を決める
- 上記を `RankingRowView` がそのまま適用できる値の束（構造体）にして返す

**しない**

- 順位を計算しない。帯の判定に自分で順位比較をしない（§4.2）
- `RectTransform` や `Image` を触らない（適用は View の仕事）
- 座標を決めない（座標は [../ranking-view/04-top-ranking-slots.md](../ranking-view/04-top-ranking-slots.md) のスロットが持つ）

## 2. なぜ必要か

**「見た目は順位に従属する」**という企画方針（1位は大きく金色、7位は小さく地味）と、
**「行の GameObject は店に固定する」**という既存のプール方針（[../ranking-view/01-ranking-panel.md](../ranking-view/01-ranking-panel.md) A1）は、
そのままでは両立しない。

```
4位の店が3位に上がったとき

  プール方針を守る  → その店の GameObject を 130×66 から 230×44 へ育てて金色にする
  Prefab 3種を守る  → GameObject を別 Prefab に差し替える＝同一性が切れ、移動アニメが成立しない
```

**採るのは前者。** 入れ替えアニメーション（[../ranking-view/06-rank-swap-animation.md](../ranking-view/06-rank-swap-animation.md)）は
「2枚が位置を交換しながら大きさと色を変える」演出であり、GameObject の同一性が保たれることが前提のため。

結果として **`TopRanker` 系3種の Prefab は1種へ統合**され、
3種が持っていた寸法・フォントサイズは**このVOの表（§3.2）へ移る**。

## 3. 公開インターフェース

```csharp
// Assets/Scripts/View/ValueObjects/RankingRowStyle.cs
namespace Takoda99.View.ValueObjects
{
    /// <summary>行の配色区分。Panel の色をこれで引く。</summary>
    public enum RankingRowTone
    {
        Gold,      // 1位
        Silver,    // 2位
        Bronze,    // 3位
        Upper,     // 4〜10位
        Normal,    // 帯なしの下位
        AtRisk,    // 警告帯（次の足切りで落ちる可能性がある）
        Doomed,    // 脱落確定帯（CutStoreIds に入っている）
        Dead,      // 脱落済み
    }

    /// <summary>1行が取るべき見た目。座標は含まない（スロットが持つ）。</summary>
    public readonly struct RankingRowStyle : IEquatable<RankingRowStyle>
    {
        public Vector2 Size { get; }          // RectTransform.sizeDelta
        public float RankFontSize { get; }
        public float NameFontSize { get; }
        public float ScoreFontSize { get; }
        public RankingRowTone Tone { get; }

        /// <summary>上位パネル用。順位だけで決まる。</summary>
        public static RankingRowStyle ForTopRank(int rank);

        /// <summary>下位パネル用。寸法は固定で、色だけが帯で変わる。</summary>
        public static RankingRowStyle ForBottomBand(RankingRowTone tone);

        public bool Equals(RankingRowStyle other);
    }
}
```

`UnityEngine.Vector2` を使うため、このVOだけは `UnityEngine` に依存してよい
（`Assets/Scripts/View/ValueObjects/` 配下であり `pureC#/src` ではない。[docs/rules/01](../../../../docs/rules/01-責務と絶対原則.md) の禁止対象外）。

### 3.1 配色は ScriptableObject で外に出す

`RankingRowTone` → `Color` の対応は**コードに直書きしない**。
金銀銅はアートの調整対象であり、色を変えるたびにコードを触る形にしない。

```csharp
// Assets/Scripts/View/Ranking/RankingRowPalette.cs
[CreateAssetMenu(menuName = "Takoda99/Ranking Row Palette")]
public sealed class RankingRowPalette : ScriptableObject
{
    [SerializeField] private Color gold   = new Color(1f, 0.84f, 0.20f);
    [SerializeField] private Color silver = new Color(0.78f, 0.80f, 0.84f);
    [SerializeField] private Color bronze = new Color(0.80f, 0.52f, 0.25f);
    [SerializeField] private Color upper;
    [SerializeField] private Color normal;
    [SerializeField] private Color atRisk;   // 警告（暖色）
    [SerializeField] private Color doomed;   // 脱落確定（強い警告色）
    [SerializeField] private Color dead;     // 脱落済み

    public Color Of(RankingRowTone tone);
}
```

アセットの置き場所は `Assets/Settings/RankingRowPalette.asset`。
既存のテーマ系 ScriptableObject（`Assets/Scripts/View/Typography/`）と同じ扱いにする。

### 3.2 順位 → 寸法・フォントサイズ（★実測値）

**現行3 Prefab から採寸した確定値。** 統合後の1 Prefab はこの表を再現できればよい。

| 順位 | `Size` | RankText | NameText | ScoreText | `Tone` | 由来 Prefab |
|---|---|---|---|---|---|---|
| 1 | (230, 44) | 20 | 24 | 20 | `Gold` | `TopRanker` |
| 2 | (230, 44) | 20 | 24 | 20 | `Silver` | `TopRanker` |
| 3 | (230, 44) | 20 | 24 | 20 | `Bronze` | `TopRanker` |
| 4〜6 | (130, 66) | 16 | 20 | 14 | `Upper` | `TopRanker4-6` |
| 7〜10 | (100, 50) | 12 | 16 | 12 | `Upper` | `TopRanker7-10` |
| 11以上・不明 | (100, 50) | 12 | 16 | 12 | `Upper` | （7〜10 と同じ。上位パネルに出ることは通常ない） |

下位パネル（[../ranking-view/05-bottom-ranking-panel.md](../ranking-view/05-bottom-ranking-panel.md)）は**順位で寸法を変えない**。

| 用途 | `Size` | RankText | NameText | ScoreText | 由来 Prefab |
|---|---|---|---|---|---|
| 下位30行すべて | (120, 29) | 12 | 18 | 非表示 | `BottomRanker` |

> 1〜3位の寸法が同じで色だけ違うのは意図通り。**金銀銅は「大きさ」ではなく「色」で差を付ける**。
> 大きさまで3段にすると1位だけが極端になり、2位・3位が4〜6位と見分けにくくなる。

## 4. ふるまいの詳細

### 4.1 `ForTopRank` の境界

| 入力 | 結果 |
|---|---|
| `1` / `2` / `3` | 表の1〜3行目 |
| `4` `5` `6` | 4〜6 の行 |
| `7` 〜 `10` | 7〜10 の行 |
| `11` 以上 | 7〜10 と同じ（`Upper`） |
| `0` 以下 | 7〜10 と同じ（`Upper`）。**例外を投げない** |

順位不明（`0`）はサーバー未受信時に起こり得る。**落とさず、最も地味な見た目で描く。**

### 4.2 帯（`Tone`）の決め方 ★重要

**クライアントは順位と `CutLineRank` を比較しない。**
[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go` に
「rank と cutLineRank の比較をクライアントにさせない（勝敗に関わる推測をさせない原則）」と明記されている。

判定は上から順に、**最初に当たったもので確定**する。

| 優先 | 条件 | `Tone` |
|---|---|---|
| 1 | `RankingRow.Alive == false` | `Dead` |
| 2 | `ForcedEliminationWarning.CutStoreIds` に `storeId` が含まれる | `Doomed` |
| 3 | 下位パネルに表示されており、上の2つに当たらない | `AtRisk` |
| 4 | それ以外 | `Normal` |

`AtRisk` が「下位30人のうち脱落確定でない残り」になる。
これが企画意図そのもの——**「今は圏外だが、スコアの急変動で落ち得る人」を事前に警告する**——を表す。

> ⚠ `ForcedEliminationWarning.CutStoreIds` には
> 「右パネルの表示件数ぶんに上限を切ってよい」とサーバー側の許可が書かれている。
> **`CutStoreIds` が淘汰対象の全員を含むとは限らない。**
> 含まれていないからといって `Doomed` でないとは言えないため、`AtRisk` は
> 「確定ではないが危ない」という意味に留める（これも企画意図と一致する）。

自店だけは `ForcedEliminationWarning.SelfAtRisk` が別途来るので、
画面全体アラート（[../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md)）はそちらを使う。**行の色付けには使わない**（二重表現になる）。

### 4.3 `Dead` と `deadAlpha` の関係

`RankingRowView` は既に `IsAlive == false` で `CanvasGroup.alpha = deadAlpha (0.4)` にする。
`Dead` トーンはそれに**加えて** Panel の色を変える。減光だけだと「色が薄い生存者」と区別が付きにくいため。

### 4.4 `Equals` の意味

`RankingRowView` が「変わっていなければ何もしない」ための比較。
`RankingRowViewState.Equals` と同じ役割で、**毎フレームの `DOTween` 張り直しを防ぐのが目的**。
全フィールドを比較してよい（`Vector2` / `float` の完全一致でよい。表から引いた定数なので誤差は出ない）。

## 5. 依存関係

- 依存する：`UnityEngine`（`Vector2` / `Color`）、`ClientState.Cull`（帯の判定に `CutStoreIds`）
- 依存される：[../ranking-view/04-top-ranking-slots.md](../ranking-view/04-top-ranking-slots.md)、[../ranking-view/05-bottom-ranking-panel.md](../ranking-view/05-bottom-ranking-panel.md)、[../ranking-view/06-rank-swap-animation.md](../ranking-view/06-rank-swap-animation.md)

## 6. テスト観点

`ForTopRank` は EditMode で完結する。パレットは手動確認。

| # | 観点 | 方法 |
|---|---|---|
| 1 | 1・2・3位で `Size` が同じ、`Tone` だけ Gold/Silver/Bronze になる | EditMode |
| 2 | 4位と6位が同じ `Size` (130,66)、7位と10位が同じ `Size` (100,50) | EditMode |
| 3 | 3位と4位で `Size` が変わる（境界） | EditMode |
| 4 | `0` / `-1` / `999` で例外が出ず `Upper` になる | EditMode |
| 5 | `Alive == false` が `CutStoreIds` より優先される | EditMode |
| 6 | `CutStoreIds` に居る生存店が `Doomed` になる | EditMode |
| 7 | `Equals` が同じ順位同士で `true`、隣の段と `false` | EditMode |
| 8 | パレットの色を変えると再生中の見た目が変わる | 手動 |

## 7. 未確定事項

- 金銀銅の実際の色値。§3.1 の既定値は仮。アート担当が `RankingRowPalette.asset` で調整する
- `Upper`（4〜10位）と `Normal` を色で分けるか、同色にするか。上位パネルと下位パネルは画面上で離れているので同色でも混乱しない可能性がある
