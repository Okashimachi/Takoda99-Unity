# `ranking-view/` — ランキングと足切り秒読みの表示（本選 v0.8.0・★新規）

**予選の99店ミニ盤面（テト99風）を置き換える、本選のUIの中核。**「上位を見せる」「下位を急かす」の2方向で行動指針を作る（[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-ranking-panel.md](./01-ranking-panel.md) | 試合中のランキング（上位N＋自分）と行入れ替えアニメーション |
| 02 | [02-cull-countdown-panel.md](./02-cull-countdown-panel.md) | 次に脱落する人＋秒読み（常設UI・ローカル補間） |
| 03 | [03-spectator-ranking-view.md](./03-spectator-ranking-view.md) | 観戦中の全99人順位一覧 |
| 04 | [04-top-ranking-slots.md](./04-top-ranking-slots.md) | 上位10行のスロット化と `TopRanker` 系3 Prefab の統合（★次に実装） |
| 05 | [05-bottom-ranking-panel.md](./05-bottom-ranking-panel.md) | 下位30行の常時表示と足切りの帯（脱落確定／警告／通常） |
| 06 | [06-rank-swap-animation.md](./06-rank-swap-animation.md) | 順位入れ替えの演出（動いた行だけを強調する） |
| 07 | [07-audience-panel.md](./07-audience-panel.md) | 脱落時に11〜99位の89店を 9列×10行 で一覧するグリッド（★次に実装） |

> **04〜06 は 01 を置き換えない。** 01 が定めた表示行の組み立て（§3）・表示件数の下限（§4）・
> プール方針（§5 A1〜A5）はそのまま生き、04〜06 はその上に「見た目が順位に従属する」層を積む。

## 供給データ

すべて `pureC#` の [match-state/02-ranking-store.md](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)（`ClientState.Ranking`）と [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)（`ClientState.Cull`）から取る。**新しい通信は増えない。**

## 押さえるべき前提

| # | 前提 |
|---|---|
| 1 | **自分の順位は `state.Rank`（`EvaluationUpdate`）から取る。** ランキング表は差分の取りこぼしでズレ得る |
| 2 | **表示件数は10件を下回らない。** 100秒以降、上位10名＝生存者全員になる（決勝がそのまま画面に収束する） |
| 3 | **秒読みはローカル補間。** サーバーは1秒ごとの正確な配信を保証しない |
| 4 | 観戦中は**全99人**が見える。正確性は低くてよい |
| 5 | **サーバーは99店を最後まで送り続ける。** `RankingEntry.Rank` は生存店が現在順位、脱落店が確定順位。下位パネルを脱落者で埋められるのはこれが理由（[05](./05-bottom-ranking-panel.md) §5.1） |
| 6 | **順位と `CutLineRank` をクライアントで比較しない。** 脱落圏内かは `CutStoreIds` / `SelfAtRisk` に従う（勝敗に関わる推測をさせない原則） |

## 足切りスケジュール（`Takoda99-Server/internal/game/params.go`）

20秒等間隔×6段階。**目標生存数**で定義されており、淘汰人数は差分。

| 段階 | 時刻 | 目標生存数 | 淘汰数 |
|---|---|---|---|
| 1 | 20秒 | 75 | 24 |
| 2 | 40秒 | 55 | 20 |
| 3 | 60秒 | 35 | 20 |
| 4 | 80秒 | 20 | 15 |
| 5 | 100秒 | 10 | 10 |
| 6 | 120秒 | **0** | 10 |

中間段階（2〜4）の目標生存数は当日調整され得る。**生存数を決め打ちせず `AliveCount` を見ること。**
段階6で優勝者を含む全店が脱落する（[../result-view/03-champion-modal-skip.md](../result-view/03-champion-modal-skip.md)）。
