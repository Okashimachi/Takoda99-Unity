# `cleanup/` — 本選で使われなくなる表示の撤去（Unity）

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-removed-views.md](./01-removed-views.md) | 信用ゲージ・我慢ゲージ・星・99店ミニ盤面・劣化演出の撤去 |

## 上流

[20_廃止・非使用リスト §3・§4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)

## 位置づけ

**独立したPRにしない。** `hud/` `ranking-view/` `result-view/` の各PRの中で同時に行われる。本ファイルは**撤去漏れを防ぐチェックリスト**。

> **重要**：客キャラクター・属性の見た目・行列・背景は**画面から消えない**。内部でゲームに効かなくなるだけ（[本選企画書 3.2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。アート素材が無駄になることはない。
