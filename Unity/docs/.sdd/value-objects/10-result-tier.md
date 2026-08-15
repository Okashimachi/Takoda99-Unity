# 10-`ResultTier`（本選 v0.8.0・★新規）

> 参照する上流：[本選企画書 3.7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)（D10）／[result-view/02](../result-view/02-result-rank-tier.md)。矛盾したら上流優先。

**リザルト演出の分岐を、たった1つの純関数に閉じ込める。**

## 1. 責務

**する**：`FinalRank` から演出の段階を返す
**しない**：演出そのものを持たない（Prefab の選択は View 側）

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/ValueObjects/ResultTier.cs
namespace Takoda99.View.ValueObjects
{
    public enum ResultTier
    {
        /// <summary>1位。チャンピオン専用の特別演出（最も豪華に）。</summary>
        Champion,

        /// <summary>2〜3位。表彰台の演出。</summary>
        Podium,

        /// <summary>4〜10位。決勝進出者としての演出。</summary>
        Finalist,

        /// <summary>11位以下、および順位不明。通常のリザルト。</summary>
        Standard,
    }

    public static class ResultTierRule
    {
        /// <summary>
        /// 最終順位から演出の段階を決める。**分岐の基準はこの値だけ**
        /// （途中の StoreEliminatedBatch を使わない）。
        /// finalRank が 0 以下（PersonalResult 未受信）なら Standard。
        /// </summary>
        public static ResultTier From(int finalRank);
    }
}
```

## 3. 判定規則

```
finalRank <= 0   → Standard   （PersonalResult 未受信。0位は存在しない）
finalRank == 1   → Champion
finalRank 2..3   → Podium
finalRank 4..10  → Finalist
finalRank >= 11  → Standard
```

### 3.1 境界の根拠

| 境界 | 根拠 |
|---|---|
| 10 | **100秒時点の生存数が10人**＝決勝進出ライン（[本選企画書 3.6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。11位以下は決勝に残れなかった側 |
| 3 | 表彰台 |
| 1 | 120秒時点でスコア1位＝チャンピオン |

**ハードコードでよい。** `cullSchedule` から導出しない：`GameParametersPublicSubset.CullSchedule` は届くが、演出の段階は企画が決めた固定値であり、スケジュールが調整されても4段階の意味は変わらない（中間ステージの `targetAliveCount` だけが調整対象で、10人という決勝人数は「動かしてはいけない」側）。

## 4. 依存関係

- 依存する：なし（`int` を受けて `enum` を返すだけ）
- 依存される：[result-view/02](../result-view/02-result-rank-tier.md)、[result-view/01](../result-view/01-personal-result-view.md)

## 5. テスト観点（`Unity/tests/Takoda99.View.Tests/`）

| 入力 | 期待 |
|---|---|
| `-1` / `0` | `Standard` |
| `1` | `Champion` |
| `2` / `3` | `Podium` |
| `4` / `7` / `10` | `Finalist` |
| `11` / `50` / `99` | `Standard` |
| `100`（範囲外） | `Standard`（例外を投げない） |

## 6. 未確定事項

- なし
