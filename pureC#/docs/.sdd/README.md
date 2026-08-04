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

## 2. 仕様書一覧（モジュール対応）

[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) のモジュール一覧に対応させる。ファイルはモジュール1つにつき1本。

| # | ファイル | モジュール | 状態 |
|---|---|---|---|
| 01 | `01-contract.md` | `Contract`（Proto参照の使い方） | 未作成 |
| 02 | `02-dispatcher.md` | `Dispatcher` | 未作成 |
| 03 | `03-store-reducer.md` | `Store` / `Reducer` | 未作成 |
| 04 | `04-typing-judge.md` | `TypingJudge` | 未作成 |
| 05 | `05-romaji-table.md` | `RomajiTable` | 未作成 |
| 06 | `06-match-client-controller.md` | `MatchClientController` | 未作成 |

新しいモジュールが増えたら、この表に行を追加してから仕様書ファイルを作る。

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
