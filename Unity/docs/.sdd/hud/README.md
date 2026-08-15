# `hud/` — 試合画面HUDの刷新（本選 v0.8.0）

**画面の枠組み（縦画面・レイアウト方針）は予選のまま。変えるのはHUDの中身だけ。**（[本選企画書 3.5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)：向きを変えると全画面の作り直しになり、残り期間で吸収できない）

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-hud-composition.md](./01-hud-composition.md) | 何を消し何を出すか、`Renderer` の振り分け、自分の順位の大表示とスコア |
| 02 | [02-order-word-emphasis.md](./02-order-word-emphasis.md) | お題の大型化と枠外飛び出し演出 |

## 既存仕様書との関係

[`match-view/`](../match-view/README.md) の 01〜08 は予選版。本ディレクトリと矛盾したら**本ディレクトリが優先**。実装完了後に `match-view/` 側を更新する。

| 既存 | 本選での扱い |
|---|---|
| `match-view/01-renderer.md` | `Renderer` の振り分け先が変わる → 本ディレクトリ 01 |
| `match-view/02-main-store-view.md` | 提灯（信用）表示を撤去 → [../cleanup/01-removed-views.md](../cleanup/01-removed-views.md) |
| `match-view/04-sub-store-board-view.md` | 99店ミニ盤面ごと撤去 → [../ranking-view/](../ranking-view/README.md) へ役割移譲 |
| `match-view/05-patience-timer.md` | 我慢ゲージ撤去 → [../cleanup/01-removed-views.md](../cleanup/01-removed-views.md) |
| `match-view/07-match-hud.md` | 星評価を撤去、注文カウンタ／注文吹き出し／屋号は維持 |

## 前提

`pureC#` 側 [result/02-lifecycle-and-renderer.md](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md) の実装（`IRenderer` の新しい形）が**先に必要**。
