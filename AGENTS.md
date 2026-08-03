# AGENTS.md — takoda99-unity

このファイルは、AIコーディングエージェント（Claude Code / Codex / Cursor 等）が **takoda99-unity**（Unity本番クライアント実装リポジトリ）で作業する際に毎セッション最初に読むインデックス。ツールに依存しない共通の入口として、ここに一本化する。

> Claude Code は `CLAUDE.md` からこのファイルを参照している。どのAIツールを使っても、実体はこの `AGENTS.md` を読むこと。
> **このファイルは索引と最小限の説明だけを持つ。具体的なルール本体は [`docs/rules/`](./docs/rules/) に分冊している。作業前に該当ルールを必ず読むこと。**

## 0. このリポジトリの構成（2つの領域）

このリポジトリは性質の異なる2つのコードを同居させている。

| 領域 | 内容 | Unity依存 | 領域のREADME |
|---|---|---|---|
| `Unity/` | Unityプロジェクト本体（`Unity/Assets/` 配下に実装）。描画（Prefab/UI/シーン）・入力（Input System）・我慢ゲージ表示（`PatienceTimer`）・WebGL向けネットワーク実装など | **あり** | [Unity/README.md](./Unity/README.md) |
| `pureC#/` | Unityを起動せずに実装・テストできる純粋なC#ロジック（`Contract`参照・`Dispatcher`・`Store`/`Reducer`・`TypingJudge`・`RomajiTable`・`MatchClientController`） | **なし（禁止）** | [pureC#/README.md](./pureC%23/README.md) |

**どちらの領域も、`docs/.sdd/` に仕様書を書いてから実装する「仕様書駆動開発」で進める。** 実装に着手する前に、担当領域のREADMEと `.sdd` の索引を必ず読むこと。

| 領域 | 仕様書（.sdd）の索引 | 仕様書の対象 |
|---|---|---|
| `Unity/` | [Unity/docs/.sdd/README.md](./Unity/docs/.sdd/README.md) | `WebGLNetworkClient` / `UnityInputSource` / `PatienceTimer` / `Renderer` / デバッグパネル / シーン・Prefab構成 |
| `pureC#/` | [pureC#/docs/.sdd/README.md](./pureC%23/docs/.sdd/README.md) | `Contract` / `Dispatcher` / `Store`+`Reducer` / `TypingJudge` / `RomajiTable` / `MatchClientController` |

> 仕様書は上流（Client-Docs / Docs / Proto）を**実装粒度まで具体化するだけ**で、新しいルールを作る場所ではない。同じモジュールの仕様を両領域に二重に書かない。

## 1. 上流リポジトリへの参照（正典の所在）

このリポジトリの実装は、以下を上流（正典）として前提にする。**矛盾したら上流が優先し、こちらを直す。**

| リポジトリ | 役割 | 関係 |
|---|---|---|
| **[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)** | 全リポジトリ唯一の共有契約（DTO/メッセージ/GameParameters公開サブセット/ローマ字テーブル） | 送受信する型・共有データの正典。**契約は変更しない**（変更は Proto 側で人間承認） |
| **[Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs)** | Unityクライアント設計の正典 | アーキ（MVU）・モジュール分割・状態管理・ディスパッチ・打鍵判定・画面遷移の設計。本リポジトリはこれを実装する |
| **[Takoda99-Docs](https://github.com/Okashimachi/Takoda99-Docs)** | 企画・ゲーム/サーバー仕様の正典 | 本リポの仕様は `04_クライアント仕様/03_Unity仕様書.md`。ゲームルール本体は `02_共通仕様/01_全体仕様.md` |
| **[用語集（ユビキタス言語）](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)** | 日本語 ↔ コード上の名称（英語）の対応正典 | **変数・型・クラス・メソッド名はここに合わせる**（`Customer` / `CustomerId` / `Evaluation` / `Credit` / `Patience` / `TypingJudge` / `RomajiTable` 等）。テーマ変更時も**コード上の名称は変えない**（表示名だけ差し替える） |
| **[Textro99-WebFront](https://github.com/Okashimachi/Textro99-WebFront)** | 別プロジェクト「テキストロ99」の旧構想プロトタイプ | **実装の参考のみ（正典ではない）**。ディレクトリ構成・打鍵判定オートマトンの組み方・reducerの書き方の見本にする。ゲームルール・旧proto型・旧用語（`Daken`/`Combo`/`Attack`/`Stack`/`Ko`/`Zone`等）は流用禁止（[用語集 §13](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)） |

---

## 2. ルール本体（docs/rules/）

具体的な責務・原則・禁止事項・運用ルールは以下に分冊している。**該当する作業を始める前に必ず読むこと。**

| # | ファイル | 内容 | いつ読むか |
|---|---|---|---|
| — | [docs/rules/README.md](./docs/rules/README.md) | ルール分冊の索引 | 迷ったら |
| 1 | [docs/rules/01-責務と絶対原則.md](./docs/rules/01-責務と絶対原則.md) | クライアントの責務境界・サーバー権威・打鍵判定のみ許可・してはいけない設計 | **全作業の前提（必読）** |
| 2 | [docs/rules/02-Unity実装ルール.md](./docs/rules/02-Unity実装ルール.md) | `Unity/`と`pureC#/`の役割分担・仕様書駆動開発（.sdd）・proto版固定・デバッグパネル | 実装コードを書く前 |
| 3 | [docs/rules/03-Git運用.md](./docs/rules/03-Git運用.md) | ブランチ構成・Git ポリシー・コミット規約・禁止コマンド | commit / push の前 |
| 4 | [docs/rules/04-PRとレビュー.md](./docs/rules/04-PRとレビュー.md) | PR の流れ・粒度・レビュー観点・マージ権限 | PR 作成 / レビューの前 |

---

## 3. やってはいけないこと（要点のみ・詳細は各分冊）

- ❌ C# に経営ロジック（客分配/評価/信用/脱落/下位淘汰/フェーズ/火力/お題単語生成）を書く（唯一の例外は打鍵判定）→ [01](./docs/rules/01-責務と絶対原則.md)
- ❌ 契約（メッセージ/型/`GameParametersPublicSubset`）をこのリポジトリで変更・確定する → [01](./docs/rules/01-責務と絶対原則.md)
- ❌ `pureC#/src` に `UnityEngine` 名前空間を持ち込む → [01](./docs/rules/01-責務と絶対原則.md) / [02](./docs/rules/02-Unity実装ルール.md)
- ❌ 廃止された旧構想（テキストロ99）の用語・機構（`Daken`/`Combo`/`Attack`/`Stack`/`Ko`/`Zone`/ターゲティング等）を持ち込む → [01](./docs/rules/01-責務と絶対原則.md)
- ❌ **`develop` / `main` へマージする**（指示があっても不可。人間が行う）→ [03](./docs/rules/03-Git運用.md)
- ❌ `git reset --hard` / `git rebase -i` / `rm -rf` を指示なく実行する → [03](./docs/rules/03-Git運用.md)
- ❌ 秘密情報（本番URL・トークン）をコミットする → [03](./docs/rules/03-Git運用.md)
- ❌ プロダクト名を勝手にリネームする（`takoda99` を維持。旧名 `textro99` / 仮称 `takoyaki99` は復活させない）
