# `result-view/` — 個人成績とリザルト（本選 v0.8.0）

**99人中89人が最初に見る「結果」は個人成績画面**であり、優先度は高い（[本選企画書 3.5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-personal-result-view.md](./01-personal-result-view.md) | 保持データを表示するだけの個人成績画面（★予選のバグ対策） |
| 02 | [02-result-rank-tier.md](./02-result-rank-tier.md) | リザルトの順位別演出分岐（1位／2〜3位／4〜10位／11位以下） |
| 03 | [03-champion-modal-skip.md](./03-champion-modal-skip.md) | 優勝者に `MainGame` の脱落モーダルを出さず、`Result` へ直行させる |

## この2本が解いている問題

| # | 問題 | 解 |
|---|---|---|
| 1 | 予選：脱落モーダル →「次へ」で個人成績画面へ行くと**何も表示されなかった** | 脱落した瞬間に `PersonalResult` を受け取って保持する。画面は保持データを出すだけ（01） |
| 2 | 本選：サーバーは「120秒に全員脱落・順位はスコア順」しか持たない。**勝者の演出はクライアントの責務** | `FinalRank` で4段階に分岐（02） |

## 供給データ

`ClientState.PersonalResult`（`pureC#` [result/01-personal-result.md](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)）。**サーバーへの問い合わせは行わない。**
