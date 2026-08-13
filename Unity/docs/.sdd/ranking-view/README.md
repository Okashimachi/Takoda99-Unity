# `ranking-view/` — ランキングと足切り秒読みの表示（本選 v0.8.0・★新規）

**予選の99店ミニ盤面（テト99風）を置き換える、本選のUIの中核。**「上位を見せる」「下位を急かす」の2方向で行動指針を作る（[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-ranking-panel.md](./01-ranking-panel.md) | 試合中のランキング（上位N＋自分）と行入れ替えアニメーション |
| 02 | [02-cull-countdown-panel.md](./02-cull-countdown-panel.md) | 次に脱落する人＋秒読み（常設UI・ローカル補間） |
| 03 | [03-spectator-ranking-view.md](./03-spectator-ranking-view.md) | 観戦中の全99人順位一覧 |

## 供給データ

すべて `pureC#` の [match-state/02-ranking-store.md](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)（`ClientState.Ranking`）と [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)（`ClientState.Cull`）から取る。**新しい通信は増えない。**

## 押さえるべき前提

| # | 前提 |
|---|---|
| 1 | **自分の順位は `state.Rank`（`EvaluationUpdate`）から取る。** ランキング表は差分の取りこぼしでズレ得る |
| 2 | **表示件数は10件を下回らない。** 100秒以降、上位10名＝生存者全員になる（決勝がそのまま画面に収束する） |
| 3 | **秒読みはローカル補間。** サーバーは1秒ごとの正確な配信を保証しない |
| 4 | 観戦中は**全99人**が見える。正確性は低くてよい |
