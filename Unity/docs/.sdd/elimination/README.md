# `elimination/` — 一斉脱落の演出（本選 v0.8.0・★新規）

**1ステージで最大49店が同時に脱落する。** 予選の「1件ずつ再生する店じまい演出」をそのまま使うと詰まる。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-mass-elimination-effect.md](./01-mass-elimination-effect.md) | `StoreEliminatedBatch` の集約演出と、自店脱落時のモーダル |

## 上流

- [12_差分_クライアント §5.2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)
- [30_通信シーケンス §4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)（4-B / 4-C / 4-D）
- [20_廃止・非使用リスト §4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)（他店脱落音の都度再生版は使えない）

## 供給データ

`IRenderer.OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf)`
（`pureC#` [result/02 §3](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)）
