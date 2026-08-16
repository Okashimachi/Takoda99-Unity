# `result/` — 個人成績・試合終了・ライフサイクル（本選 v0.8.0）

**予選で実際に踏んだバグ（脱落モーダル → 個人成績画面で何も表示されない）への構造的な対策**を含む。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-personal-result.md](./01-personal-result.md) | `PersonalResult` の保持と破棄、空になった `MatchEnd` の扱い |
| 02 | [02-lifecycle-and-renderer.md](./02-lifecycle-and-renderer.md) | `IRenderer` の新しい形、`MatchClientController` / `Dispatcher` の差分、`ClientPhase` |

## 上流

- [12_差分_クライアント §6・§7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)
- [30_通信シーケンス §4.3・§5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)

## 依存

`contract/01` → `match-state/01,02,03` → **`result/01` → `result/02`**

02 は本選 `pureC#` 対応の**最後の1本**。ここまで終われば Unity 側の描画実装に入れる。
