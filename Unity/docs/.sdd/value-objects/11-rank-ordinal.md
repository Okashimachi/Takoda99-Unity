# 11-順位の序数表記

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `RankingEntry.Rank` / `EvaluationUpdate.Rank`。矛盾したら上流優先。

## 1. 責務

**する**

- 順位の整数（1〜99）を `1st` / `2nd` / `3rd` / `4th` … の**表示文字列**へ変換する
- 順位が未確定（0以下）のときの表記を1箇所に集める

**しない**

- 順位を計算・補正しない（サーバー権威）
- ロケール切り替えをしない（英語序数の1形式のみ）

## 2. なぜ必要か

Prefab のダミーテキストは `1st.` `99th.` で作られているが、
[08-ranking-row-view-state.md](./08-ranking-row-view-state.md) の `RankingRowViewState.Create` は
`rank.ToString()` で `"1"` を作るため、**実行した瞬間に序数の接尾辞が消える**。

序数を残す方法は3つあるが、採るのは3つめ。

| 案 | 判断 |
|---|---|
| Prefab で接尾辞を別ラベルに分ける | ❌ 4 Prefab × 配線が増え、レイアウトも崩れる |
| `RankingRowView` 側で接尾辞を足す | ❌ 表示文字列の組み立てが VO と View に分散する |
| **VO の文字列生成を序数にする** | ✅ Prefab を一切触らない。既存の差分検出（`Equals`）もそのまま効く |

## 3. 公開インターフェース

```csharp
// Assets/Scripts/View/ValueObjects/RankOrdinal.cs
namespace Takoda99.View.ValueObjects
{
    /// <summary>順位 → 序数表記。1〜99 を事前計算した表から引くだけ（実行時に文字列を作らない）。</summary>
    public static class RankOrdinal
    {
        /// <summary>順位が未確定（0以下）／範囲外のときの表記。</summary>
        public const string Unknown = RankingRowViewState.UnknownRankText; // "--"

        /// <summary>最大順位。99店固定（GameParametersPublicSubset.MaxStores）。</summary>
        public const int MaxRank = 99;

        /// <summary>"1st" / "22nd" / "--"。</summary>
        public static string Of(int rank);
    }
}
```

## 4. ふるまいの詳細

### 4.1 生成規則

**起動時に `static readonly string[]` を1本作り、以後はそれを引くだけ。**
`RankingRowView.SetState` は 30〜99 行ぶん毎回呼ばれるため、実行時の文字列生成と GC を発生させない。

| 条件 | 接尾辞 |
|---|---|
| 下2桁が 11 / 12 / 13 | `th`（`11th` `12th` `13th`) |
| 上記以外で下1桁が 1 | `st` |
| 上記以外で下1桁が 2 | `nd` |
| 上記以外で下1桁が 3 | `rd` |
| それ以外 | `th` |

99店なので実際に効く例外は `11th` `12th` `13th` の3つだけだが、**規則として書く**（人数が変わったときに壊れないため）。

### 4.2 境界値

| 入力 | 出力 |
|---|---|
| `1` | `1st` |
| `2` | `2nd` |
| `3` | `3rd` |
| `4` | `4th` |
| `11` `12` `13` | `11th` `12th` `13th` |
| `21` `22` `23` | `21st` `22nd` `23rd` |
| `99` | `99th` |
| `0` / 負値 / `100` 以上 | `--` |

### 4.3 接尾辞を小さく見せる（任意）

TMP のリッチテキストで、**ラベルを分けずに**数字と接尾辞のサイズを変えられる。

```
"1<size=60%>st</size>"
```

採用するかは見た目の判断。採用する場合も表を作る場所は同じ1箇所で、View 側の変更は要らない。
条件は TMP の `Rich Text` が ON であること（既定値 ON）。

> ⚠ 採用すると `RankingRowViewState.Equals` が比較する文字列にタグが混ざるが、
> 同じ順位なら同じ文字列なので差分検出は正しく働く。

## 5. 呼び出し側の変更

[08-ranking-row-view-state.md](./08-ranking-row-view-state.md) の `RankingRowViewState.Create` 内の

```csharp
rank >= 1 ? rank.ToString() : UnknownRankText
```

を `RankOrdinal.Of(rank)` に置き換える。**変更はこの1行のみ。**

`SelfRankViewState`（自店HUDの順位大表示）は**変更しない**。
あちらは画面で最も大きい数字であり、接尾辞を付けると桁が読みにくくなる（[../hud/01-hud-composition.md](../hud/01-hud-composition.md) R2）。

## 6. 依存関係

- 依存する：なし（純粋な静的クラス）
- 依存される：[08-ranking-row-view-state.md](./08-ranking-row-view-state.md)

### 6.1 なぜ `pureC#` 側に置かないか

**このVOは `UnityEngine` に一切依存しないため、`pureC#/src` へ置くこともできる。それでも Unity 側に置く。**

| # | 理由 |
|---|---|
| 1 | [README.md](./README.md) §1 が「`UnityEngine` に依存しない書き方ができる場合も、**初版はUnity側に置き、必要になった時点で移設を検討する**」と定めている |
| 2 | [`pureC#/docs/.sdd`](../../../../pureC%23/docs/.sdd/README.md) §2 の索引は [Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) のモジュール一覧と1対1で対応している。表示用の文字列整形はそのモジュール一覧に無い |
| 3 | 唯一の呼び出し元が [08](./08-ranking-row-view-state.md)（Unity側）の1行だけ。移すと呼ばれる側だけが DLL の向こうになる。`Takoda99.Client.dll` は各自がビルドする運用（[../foundation/01-purecs-dll-reference.md](../foundation/01-purecs-dll-reference.md)）なので、表を1文字直すたびにDLLリビルドが要る |
| 4 | [09](./09-cull-countdown-state.md) / [10](./10-result-tier.md) も同じ性格（純粋な整形処理）で Unity 側にある。並びが揃う |

**移設すべきになる条件：`pureC#` 側が序数表記を必要としたとき**（例：`PersonalResult` の成績テキストを `pureC#` で組み立てる設計に変えた場合）。
依存が下向き1本しかないので、そのとき本ファイルだけを移せばよい。

## 7. テスト観点

EditMode で完結する。

| # | 観点 |
|---|---|
| 1 | `1` → `1st`、`2` → `2nd`、`3` → `3rd`、`4` → `4th` |
| 2 | `11` `12` `13` が `th`（`1st` `2nd` `3rd` にならない） |
| 3 | `21` `22` `23` が `st` `nd` `rd` |
| 4 | `0` / `-1` / `100` が `--` |
| 5 | `Of` を2回呼んで**同一のインスタンスが返る**（表から引いており毎回生成していないこと） |

## 8. 未確定事項

- リッチテキストで接尾辞を小さくするか（§4.3）。実機で見て決める
