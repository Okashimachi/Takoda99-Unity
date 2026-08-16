# 02-リザルトの順位別演出分岐

> 参照する上流：[本選企画書 3.7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)（D10）／[12_差分_クライアント §7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス 5-C](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。矛盾したら上流優先。

## 0. なぜクライアントの責務なのか

**サーバーは「120秒で全員脱落・順位はスコア順」という単純な処理だけを行う。** 勝者の特別扱いを持たない（5-C）。

```
【危険】 10人 → 9人淘汰 → 1人だけ生存状態のまま試合が続いている？終わっている？
【安全】 10人 → 10人全員が同時に脱落 → 試合終了。全員がリザルトへ
```

処理をシンプルにした代わりに、**盛り上がりの演出はすべてクライアントが担う**。役割分担が明確になり、双方の実装が軽くなる。

## 1. 責務

**する**

- `PersonalResult.FinalRank` から演出の段階（Tier）を決める
- 段階に応じてリザルト演出とジングルを分岐させる
- スコア・たこ焼き数・ミス数を**大きく**出す

**しない**

- 途中の `StoreEliminatedBatch` を演出の分岐に使わない（**分岐の基準は `FinalRank` のみ**）
- 優勝者かどうかをサーバーに問い合わせない
- 順位を計算しない

## 2. 演出の4段階

| `FinalRank` | Tier | 演出 |
|---|---|---|
| 1 | `Champion` | **チャンピオン専用の特別演出（最も豪華に）** |
| 2〜3 | `Podium` | 表彰台の演出（1位とは別だが特別感はある） |
| 4〜10 | `Finalist` | 決勝進出者としての演出 |
| 11以上 | `Standard` | 通常のリザルト |

Tier の判定は [../value-objects/10-result-tier.md](../value-objects/10-result-tier.md) の純関数で行う。

### 2.1 分岐が単純である理由

| # | 理由 |
|---|---|
| 1 | **全員が同じ `MatchEnd` から入る。** 優勝者も最下位も経路が1本 |
| 2 | 優勝者だけ別の状態にいる、という特殊ケースが存在しない |
| 3 | 判定に使う値が `FinalRank` の1つだけ |

> 予選の `Renderer` にあった「最後まで生き残った店には `OnStoreEliminated` が来ないため、ここでしか順位一覧に載せられない」という分岐は**不要になる**（[../cleanup/01-removed-views.md](../cleanup/01-removed-views.md)）。

### 2.2 LTでの見え方

会場の全員が**同時に**リザルトに入る。**1位の演出が、会場のどこかで必ず1つだけ再生される。** これがLTで最も見せたい瞬間になるため、`Champion` の演出は他の3つより明確に強くする。

## 3. 公開インターフェース

```csharp
// Assets/Scripts/View/Result/ResultScreenView.cs（既存を改修）
namespace Takoda99.View
{
    public sealed class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private ResultTierPresenter championPresenter;
        [SerializeField] private ResultTierPresenter podiumPresenter;
        [SerializeField] private ResultTierPresenter finalistPresenter;
        [SerializeField] private ResultTierPresenter standardPresenter;

        /// <summary>リザルトを表示する。result が null なら Standard 相当で成立させる。</summary>
        public void Show(PersonalResultState result, RankingTable finalRanking);
    }

    /// <summary>1つの Tier の演出とジングル。Prefab として4つ用意する。</summary>
    public sealed class ResultTierPresenter : MonoBehaviour
    {
        [SerializeField] private AudioSource jingle;
        public void Play(PersonalResultState result);
    }
}
```

**4つの Tier を4つの Prefab として持ち、`Show` はどれを再生するか選ぶだけにする。** `if` の中に演出を書き分けない（アートの差し替えが `Show` の改修になってしまう）。

## 4. 表示する内容（Tier 共通）

| 表示 | 供給元 | 扱い |
|---|---|---|
| 最終順位 | `PersonalResult.FinalRank` | 最も大きく |
| **スコア** | `PersonalResult.Score` | **大きく出す。** 試合中は順位が主役だったが、リザルトでは具体的な数字が達成感になる（企画書 3.8） |
| たこ焼き数 | `PersonalResult.TakoyakiCount` | 内訳として出す |
| ミス数 | `PersonalResult.Stats.TotalMisses` | 同上 |
| 上位陣の顔ぶれ | `finalRanking`（最後の `RankingSnapshot` 由来） | 「誰が優勝したか」。**自分が1位でなくても、誰が勝ったかは見たい** |

`finalRanking` は `ClientState.Ranking` をそのまま渡す。120秒の配信順序が `… → RankingSnapshot → MatchEnd` なので、**`MatchEnd` を受け取った時点で全店の最終順位が入っている**（`RankingSnapshot` は `Result` フェーズでも受理される。`pureC#` [result/02 §2.1](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)）。

## 5. `PersonalResult` が `null` の場合

| # | 扱い |
|---|---|
| 1 | Tier は `Standard` として扱う（`FinalRank = 0` は「11以上」と同じ側へ倒す） |
| 2 | 数字は `--` |
| 3 | **画面から出る導線（再戦・タイトル）は必ず生きている** |

## 6. 画面遷移

```
試合中（決勝の10店）─ MatchEnd ─┐
観戦 ───────────── MatchEnd ─┴─> リザルト ─┬─> 個人成績（01）
                                            ├─> 再戦（接続を張り直す）
                                            └─> タイトル
```

| # | 要件 |
|---|---|
| 1 | **全員がここに来る。** 例外経路がない |
| 2 | 「再戦」は `IMatchClientController.Rematch()` を呼ぶ。**保持データはここで破棄される**（`pureC#` [result/01 §4](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)） |
| 3 | 「タイトル」は `BackToTitle()`。次に `BeginPlay()` を通るときに破棄される |
| 4 | リザルトから個人成績へ行って戻ってこられる |

## 7. 依存関係

- 依存する：`pureC#` [result/01](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)、[match-state/02](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)、[../value-objects/10](../value-objects/10-result-tier.md)、[01-personal-result-view.md](./01-personal-result-view.md)
- 依存される：なし

## 8. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | `FinalRank` 1 / 2 / 3 / 4 / 10 / 11 / 99 で Tier が `Champion` / `Podium` / `Podium` / `Finalist` / `Finalist` / `Standard` / `Standard` になる | EditMode（`ResultTier` の純関数） |
| 2 | `FinalRank == 0` で `Standard` | 同上 |
| 3 | 各 Tier の Prefab が1つだけ再生される（重複しない） | 手動 |
| 4 | `PersonalResult == null` で例外が出ず、再戦・タイトルへ行ける | `ResultSampleData` |
| 5 | 最終 `RankingSnapshot` 由来の上位陣が出る | シナリオ再生 |
| 6 | 「再戦」後、2試合目のリザルトに1試合目の値が残らない | シナリオ再生 |
| 7 | 決勝で生き残った店が、脱落モーダルを経ずにリザルトへ入る | シナリオ再生 |

## 9. 未確定事項

- 各 Tier の演出とジングルの中身（アート・サウンド4パターン。[13_差分_アート](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/13_差分_アート.md)）
- リザルトで上位陣を何位まで見せるか
