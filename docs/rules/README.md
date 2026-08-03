# docs/rules — takoda99-unity ルール分冊

[AGENTS.md](../../AGENTS.md) から参照されるルール本体。AGENTS.md は索引と最小限の説明だけを持ち、具体的な指示はここに置く。

| # | ファイル | 内容 | いつ読むか |
|---|---|---|---|
| 1 | [01-責務と絶対原則.md](./01-責務と絶対原則.md) | クライアントの責務境界・サーバー権威・打鍵判定のみ許可・してはいけない設計 | **全作業の前提（必読）** |
| 2 | [02-Unity実装ルール.md](./02-Unity実装ルール.md) | `Unity/`と`pureC#/`の役割分担・仕様書駆動開発（.sdd）・proto版固定・デバッグパネル | 実装コードを書く前 |
| 3 | [03-Git運用.md](./03-Git運用.md) | ブランチ構成・Git ポリシー・コミット規約・禁止コマンド | commit / push の前 |
| 4 | [04-PRとレビュー.md](./04-PRとレビュー.md) | PR の流れ・粒度・レビュー観点・マージ権限 | PR 作成 / レビューの前 |

## 運用方針

- ルールを更新するときは、このディレクトリ内の該当ファイルを編集する（AGENTS.md の索引は必要時のみ追従）。
- 上流（[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) / [Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs) / [Takoda99-Docs](https://github.com/Okashimachi/Takoda99-Docs)）と矛盾する内容をここに残さない。矛盾したら上流優先で直す。
- `pureC#/` の仕様書（`pureC#/docs/.sdd/`）と本ディレクトリの関係：本ディレクトリは「このリポジトリでの作業ルール」、`pureC#/docs/.sdd/` は「pureC#配下の各モジュールの仕様書（何を実装するか）」。役割が異なるため混在させない。
