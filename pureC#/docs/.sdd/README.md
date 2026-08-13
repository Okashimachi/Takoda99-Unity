# .sdd — 仕様書駆動開発（Spec-Driven Development）の仕様書一式

`pureC#/src/` に実装するモジュールの仕様書を置く。**実装より必ず仕様書が先。** 仕様書が無いモジュールを `src/` にいきなり実装しない（[docs/rules/01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md)）。

## 1. 仕様書の対象と正典との関係

ここに書く仕様書は「新しいルールを作る」場所ではない。上流の設計・契約を**実装可能な粒度まで具体化するだけ**。矛盾したら常に上流が優先。

```
Takoda99-Docs（企画・ゲーム仕様の正典）
   ↓
Takoda99-Proto（DTO/メッセージ契約の正典）
   ↓
Takoda99-Client-Docs（モジュール定義・言語非依存インターフェースの正典）
   ↓
pureC#/docs/.sdd/*.md（↑を pureC# 各モジュールの実装粒度まで具体化）  ← ここ
   ↓
pureC#/src/*.cs（仕様書の実装）
```

## 2. 仕様書一覧（＝実装の単位・依存順）

[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) のモジュール一覧に対応させる。ファイルはモジュール1つにつき1本。

**番号は依存順（＝実装順）。** 上から順に実装すれば、常に「依存先が実装済み」の状態で進められる。

| # | ファイル | モジュール | 依存先 | 仕様書 | 実装 |
|---|---|---|---|---|---|
| 01 | [01-contract.md](./01-contract.md) | `Contract`（Proto参照・Envelope コーデック） | なし | ✅ | ✅ |
| 02 | [02-romaji-table.md](./02-romaji-table.md) | `RomajiTable`（テーブル・かな分割） | なし | ✅ | ✅ |
| 03 | [03-typing-judge.md](./03-typing-judge.md) | `TypingJudge`（打鍵判定） | 02 | ✅ | ✅ |
| 04 | [04-store-reducer.md](./04-store-reducer.md) | `Store` / `Reducer`（状態管理） | 01 | ✅ | ✅ |
| 05 | [05-dispatcher.md](./05-dispatcher.md) | `Dispatcher`（振り分け・送信キュー） | 01, 04 | ✅ | ✅ |
| 06 | [06-match-client-controller.md](./06-match-client-controller.md) | `MatchClientController`（統括・ライフサイクル） | 01, 03, 04, 05 | ✅ | ✅ |
| 07 | [07-scenario-player.md](./07-scenario-player.md) | `ScenarioPlayer`（サンプルデータ再生・テスト専用） | 01 | ✅ | ✅ |

新しいモジュールが増えたら、この表に行を追加してから仕様書ファイルを作る。実装が完了したら「実装」列を ✅ にする。

> **⚠ 01〜07 は予選版の記述。** 本選（Proto v0.8.0）の実装は §2.1 のディレクトリが正典であり、矛盾したらそちらが優先する。

### 2.1 本選（v0.8.0）差分の仕様書 ★いま実装するのはこちら

本選向けの変更は**意味の単位でディレクトリに分けている**。番号はディレクトリ内で完結させ、ディレクトリをまたいだ通し番号にしない。

| ディレクトリ | 何が入るか | いつ読むか |
|---|---|---|
| [`contract/`](./contract/README.md) | Proto v0.8.0 の取り込みと Obsolete の扱い | **最初に読む・最初に実装する** |
| [`match-state/`](./match-state/README.md) | 試合中の state（スコア／ランキング／足切り） | contract の後 |
| [`result/`](./result/README.md) | 個人成績・試合終了・`IRenderer`・ライフサイクル | match-state の後 |
| [`cleanup/`](./cleanup/README.md) | 撤去チェックリスト（単独PRにしない） | 全部終わったあとの確認 |

**実装順（＝依存順。1本＝1ブランチ＝1PR）**

| # | 仕様書 | 依存先 | 実装 |
|---|---|---|---|
| 1 | [contract/01-proto-v0.8.0-migration.md](./contract/01-proto-v0.8.0-migration.md) | なし | ✅ |
| 2 | [match-state/01-score-and-self-rank.md](./match-state/01-score-and-self-rank.md) | 1 | ✅ |
| 3 | [match-state/02-ranking-store.md](./match-state/02-ranking-store.md) | 2 | ✅ |
| 4 | [match-state/03-cull-warning.md](./match-state/03-cull-warning.md) | 3 | ✅ |
| 5 | [result/01-personal-result.md](./result/01-personal-result.md) | 4 | ✅ |
| 6 | [result/02-lifecycle-and-renderer.md](./result/02-lifecycle-and-renderer.md) | 1〜5 | ✅ |
| — | [cleanup/01-removed-features.md](./cleanup/01-removed-features.md) | 1〜6 の各PRに内包 | ✅ |

> **6 が終わるまで Unity 側の描画は書けない**（`IRenderer` の形が変わるため）。逆に 1〜6 は Unity を一度も開かずに `dotnet test` だけで検証できる。

**本選の上流（正典）**

| リポジトリ | ファイル |
|---|---|
| Takoda99-Docs | [00_本選差分/](https://github.com/Okashimachi/Takoda99-Docs/tree/main/00_本選差分) 一式。特に [30_通信シーケンス](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md) は送受信の順序と周期の正典 |
| Takoda99-Proto | `csharp/Takoda99.Proto/Messages.cs` **v0.8.0**。各メッセージのコメントに用途・タイミング・分類が書かれている |

> `07-scenario-player.md` はテスト専用モジュール。**サーバーのロジックは一切再現せず、サーバー権威の値はシナリオに書かれたものをそのまま流す。** サーバー未接続でクライアントの状態遷移・表示分岐を検証するために使う。

### 1仕様書 ＝ 1ブランチ ＝ 1PR

各仕様書は**単独のブランチ／PRで実装しきれる粒度**にしてある（[docs/rules/03-Git運用.md](../../../docs/rules/03-Git運用.md) §6 の「1 Issue ＝ 1ブランチ ＝ 1PR」に対応）。

- ブランチ名は仕様書が分かる形にする（例：`feature/03-typing-judge`）。
- 依存先がまだ `develop` にマージされていない状態で次に進む場合は、**統合用ブランチ `integ/xxx`**（ローカル限定・push しない）を使う（[03-Git運用.md](../../../docs/rules/03-Git運用.md) §7）。
- 02 と 04 は依存が競合しないため**並行して進められる**（01 のマージ後）。

`03-store-reducer.md` が保持する値オブジェクトの形（データ定義そのもの）は [`value-objects/`](./value-objects/README.md) に分冊している。`Store`/`Reducer` のふるまいを書く前に参照すること。

## 3. 仕様書の書式

新規の仕様書は [`_template.md`](./_template.md) をコピーして作成する。最低限、以下を含める：

- **参照する上流**（Client-Docsの章番号・Protoのメッセージ名）
- **責務**（このモジュールが何をする/しないか。1で示した「持たないもの」に触れない）
- **公開インターフェース**（クラス/メソッドのシグネチャ。C#の具体的な型で書く）
- **ふるまいの詳細**（正常系・エッジケース・エラー時の挙動）
- **依存関係**（他のどのモジュールに依存するか。依存方向は Client-Docs 第3章の図に従う）
- **未確定事項**（このモジュール固有で、実装しながら決めることになりそうな点）

## 4. 運用ルール

1. 仕様書は**宣言的に書く**（議事録ではなく「何がどういう形か」）。Takoda99-Docs/Client-Docsの文体に揃える。
2. 仕様書のレビューを経てから実装に着手する。仕様書と実装を同一PRで出してもよいが、**先に仕様書のdiffが読めるコミットを分ける**（[docs/rules/03-Git運用.md](../../../docs/rules/03-Git運用.md) §3 の `spec:` コミット）。
3. 実装中に仕様と異なる判断をしたら、その場でコードだけ直さず**仕様書を更新してから**コードを直す。
4. `UnityEngine` に依存する概念（`MonoBehaviour`のライフサイクル、`Time.deltaTime`等）を仕様書内のシグネチャに持ち込まない（`pureC#` はUnity非依存が前提）。
5. 各仕様書の「未確定事項」が解消されたら、該当箇所を仕様書から削除し、決定内容を本文に反映する。
