# 01-個人成績画面

> 参照する上流：[12_差分_クライアント §6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス §4.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)／`pureC#` [result/01-personal-result.md](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)。矛盾したら上流優先。

## 0. ★この画面が守る一線

**サーバーへ問い合わせない。保持しているデータを表示するだけ。**

予選のバグは「画面遷移のタイミングとデータ受信のタイミングが結びついていた」ことが原因だった。プレイヤーがボタンを押す速さに、データの有無が依存していた。

本選では `PersonalResult` が脱落した瞬間に届き、`ClientState.PersonalResult` に保持されている。**この画面はいつ開いても壊れない。**

## 1. 責務

**する**

- `ClientState.PersonalResult` の値を表示する
- 未受信（`null`）でも**画面として成立させる**

**しない**

- **サーバーへ何も送らない**（個人成績を要求する C2S メッセージは契約に存在しない）
- 値を計算・補正しない（`Score` も `FinalRank` もサーバー権威）
- データの到着を待たない（**ローディング表示を作らない**。待つ設計こそが予選のバグ）

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/Result/PersonalResultView.cs
namespace Takoda99.View.Result
{
    public sealed class PersonalResultView : MonoBehaviour
    {
        /// <summary>保持データを表示する。result が null でも例外を出さず、空欄で成立させる。</summary>
        public void Show(PersonalResultState result);

        public void Close();
    }
}
```

呼び出し側は `GameBootstrapper.Store.State.PersonalResult` をそのまま渡す。

## 3. 表示項目

| 表示 | 供給元 | 備考 |
|---|---|---|
| **最終順位** | `FinalRank` | 「99人中 ◯位」。最も大きく |
| **スコア** | `Score` | **試合中は補助だったが、ここでは大きく出す。** 具体的な数字が達成感になる（企画書 3.8）。負値もそのまま |
| **作ったたこ焼き数** | `TakoyakiCount` | ★`Stats.ServedCount`（提供した**客**の数）と混同しない |
| **総ミス数** | `Stats.TotalMisses` | `PersonalResult` 直下には無い |
| 総打鍵数 | `Stats.TotalKeystrokes` | 精度の分母 |
| 平均精度 | `Stats.AvgAccuracy` | 0..1。パーセント表記にする |
| 提供した客の数 | `Stats.ServedCount` | |
| 生存時間 | `SurvivedMs` | 「◯秒生き残った」 |
| 最速／最遅の提供 | `Stats.FastestMs` / `SlowestMs` | 提供0なら 0 が届く。**0のときは出さない** |
| 客の属性別内訳 | `Stats.Normal` / `Bonus` / `Claimer` / `Buzz` の `Served` | 「ヒョウ柄おばちゃんを12人さばいた」という**成績の彩り**。属性がスコアに影響しなくなっても、この数字は出せる |

### 3.1 出してはいけない値（Obsolete）

| 値 | 届く値 |
|---|---|
| `Stats.LeftCount` / 各 `AttributeTally.Left` | **常に 0**（客が逃げない） |
| `PersonalResult.CreditLeft` | 0（信用制の廃止） |
| `PersonalResult.EvalRaw` / `EvalNormalized` | 0（相対評価の廃止） |
| `PersonalResult.Reason` | `null`（脱落経路が1本） |

**「取りこぼし0人」と表示しない。** 逃げる客がいないので当たり前の数字であり、意味のない情報になる。

### 3.2 最大連続成功数について

**サーバーからは返らない。** サーバーは打鍵列を受け取らず（`OrderServed` は客1人ぶんの `elapsedMs` / `missCount` のみ）、連続無ミス数を知る手段がない（Proto `MatchStats` のコメント）。

出したい場合は**クライアント側で自前に数える**。その場合：

| # | 要件 |
|---|---|
| 1 | `MatchClientController` ではなく、**View 層でカウントしない**（ミス判定は `ITypingJudge` の `KeyResult` に出る） |
| 2 | 数える場所を1つに決める（`Renderer.OnKeyFeedback` が最も自然） |
| 3 | **`LocalMatchReset` と同じタイミングでリセットする**（次の試合に持ち越さない） |
| 4 | サーバー値と混ぜて表示しない（自前値であることが分かる置き方にする） |

**優先度は低い。まず届く値だけで画面を作る。**

## 4. `PersonalResult == null` の場合

サーバーの不整合や取りこぼしで届かない可能性は残る。**そのとき画面を出さないのではなく、空欄で出す。**

| 項目 | 表示 |
|---|---|
| 最終順位 | `--` |
| スコア・各統計 | `--` または `0` |
| 画面から出る導線 | **必ず生きている** |

> 予選の教訓は「データが無いと何も表示されない」だった。**本選では「データが無いことが分かる画面が出る」**。どちらにせよプレイヤーは次に進める。

## 5. 画面遷移

```
脱落モーダル ──「成績を見る」──> 個人成績（★いつ押してもよい）
                                    ↓
                              観戦へ戻る / MatchEnd を待つ
                                    ↓
リザルト ──「成績を見る」──────> 個人成績
```

| # | 要件 |
|---|---|
| T1 | **どのタイミングで遷移してもデータは揃っている**（4-I） |
| T2 | 何度開いても同じ内容が出る（`Show` は冪等） |
| T3 | この画面にいる間も受信は続いている。`MatchEnd` が届いたら**リザルトへ進める導線を出す**（勝手に飛ばさなくてよい） |
| T4 | 保持データの破棄は**この画面の責務ではない**（`LocalMatchReset` が1箇所で行う。`pureC#` [result/01 §4](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)） |

**T4 が重要**：既存の「画面を離れるときにデータを捨てる」発想を持ち込まない。この画面は**読むだけ**。

## 6. 依存関係

- 依存する：`pureC#` [result/01](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)、[../value-objects/10](../value-objects/10-result-tier.md)（順位の表記）
- 依存される：[02-result-rank-tier.md](./02-result-rank-tier.md)（リザルトからの導線）

## 7. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | `PersonalResult` を渡すと全項目が出る | `ResultSampleData` にケースを追加 |
| 2 | **`null` を渡しても例外が出ず、画面から出られる** | 同上 |
| 3 | `Score` が負値でそのまま出る | 同上 |
| 4 | `TakoyakiCount` と `Stats.ServedCount` が別々の欄に出る | 目視 |
| 5 | `FastestMs == 0`（提供0）のとき、その欄が出ない | EditMode |
| 6 | `LeftCount` / `CreditLeft` / `EvalRaw` が画面のどこにも出ない | `Assets/Scripts/View/Result` を grep して0件 |
| 7 | 脱落直後に開いても、120秒後に開いても同じ内容 | シナリオ再生 |
| 8 | 2試合目の同画面に1試合目の値が残らない | シナリオ再生（`Rematch` を挟む） |

## 8. 未確定事項

- 表示項目の最終決定（企画・アートと合意。[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点6）。**`pureC#` は全項目を保持しているので、増減しても `pureC#` の変更は発生しない**
- 最大連続成功数を自前で数えるか（§3.2。優先度低）
