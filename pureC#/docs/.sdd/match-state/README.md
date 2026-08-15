# `match-state/` — 試合中の状態（本選 v0.8.0）

本選で `ClientState` / `Actions` / `Reducer` がどう変わるかを、**意味の単位で3本に分けた**仕様書。

| # | ファイル | 扱う受信 | 中心となる状態 |
|---|---|---|---|
| 01 | [01-score-and-self-rank.md](./01-score-and-self-rank.md) | `MatchStart` / `EvaluationUpdate` | 自店のスコア・順位・生存数・表示名キャッシュ |
| 02 | [02-ranking-store.md](./02-ranking-store.md) | `RankingSnapshot` / `RankingDelta` | 全99店のランキング表 |
| 03 | [03-cull-warning.md](./03-cull-warning.md) | `ForcedEliminationWarning` / `StoreEliminatedBatch` | 足切りの秒読みと一斉脱落 |
| 04 | [04-unresolved-store-id.md](./04-unresolved-store-id.md) | 上記すべて（storeId を運ぶ受信） | 表示名キャッシュで解決できない storeId の検知（状態は持たない） |

## 既存仕様書との関係（★実装者へ）

このディレクトリは既存の [04-store-reducer.md](../04-store-reducer.md) と [value-objects/](../value-objects/README.md) を**本選向けに上書きする差分**。

> **矛盾したらこのディレクトリが優先。** 既存の 04 は予選版の記述であり、本選実装が完了したら 04 側を本ディレクトリの内容に合わせて更新する（[README §4 運用ルール3](../README.md)）。

## 依存

```
contract/01（Proto v0.8.0 の取り込み）
   ↓
match-state/01 ─→ match-state/02 ─→ match-state/03
                （表示名キャッシュを使う）（ランキング表を書き換える）
```

01 → 02 → 03 の順に実装する。02 は 01 が用意する `DisplayNames` に、03 は 02 が用意する `Ranking` に依存する。
