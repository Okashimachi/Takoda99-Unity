# .sdd — 仕様書駆動開発（Spec-Driven Development）の仕様書一式

`Unity/Assets/` に実装するUnity依存モジュールの仕様書を置く。**実装より必ず仕様書が先。** 仕様書が無いモジュールを `Assets/` にいきなり実装しない（[docs/rules/01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md)）。

> `pureC#/docs/.sdd/` と対をなす。あちらはUnity非依存ロジック、こちらはUnity依存の描画・入力・通信実体・タイマー。**進め方のルールは共通**で、違いは「Unity固有の要素（MonoBehaviour・Prefab・シーン・Input System・WebGL制約）を仕様書に書いてよい／書くべき」点のみ。

## 1. 仕様書の対象と正典との関係

ここに書く仕様書は「新しいルールを作る」場所ではない。上流の設計・契約を**実装可能な粒度まで具体化するだけ**。矛盾したら常に上流が優先。

```
Takoda99-Docs（企画・ゲーム仕様の正典／03_Unity仕様書.md がUnity固有方針）
   ↓
Takoda99-Proto（DTO/メッセージ契約の正典）
   ↓
Takoda99-Client-Docs（モジュール定義・言語非依存インターフェースの正典）
   ↓
Unity/docs/.sdd/*.md（↑を Unity 各モジュールの実装粒度まで具体化）  ← ここ
   ↓
Unity/Assets/Scripts/**/*.cs（仕様書の実装）
```

## 2. 仕様書一覧（モジュール対応）

[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) の「プラットフォーム依存＝あり」のモジュールに対応させる。ファイルはモジュール1つにつき1本。

| # | ファイル | モジュール | 状態 |
|---|---|---|---|
| 01 | `01-network-client.md` | `WebGLNetworkClient`（`INetworkClient` の実体） | 未作成 |
| 02 | `02-input-source.md` | `UnityInputSource`（Input System → `OnCharKey`） | 未作成 |
| 03 | `03-patience-timer.md` | `PatienceTimer`（我慢ゲージの表示専用カウントダウン） | 未作成 |
| 04 | `04-renderer.md` | `Renderer`（受信 state → Prefab/UI/エフェクト） | 未作成 |
| 05 | `05-debug-panel.md` | デバッグパネル（送受信 `Envelope` の生JSON表示） | 未作成 |
| 06 | `06-scene-prefab.md` | シーン・Prefab構成方針 | 未作成 |

新しいモジュールが増えたら、この表に行を追加してから仕様書ファイルを作る。

`Store`から導出するView用の派生値オブジェクト（評価3段階・客のムード・たこ焼き台の状態・提灯の点灯数・順位バー等）とその変換処理は [`value-objects/`](./value-objects/README.md) に分冊している。`Renderer`等の描画モジュールを書く前に参照すること。

## 3. 仕様書の書式

新規の仕様書は [`_template.md`](./_template.md) をコピーして作成する。最低限、以下を含める：

- **参照する上流**（Client-Docsの章番号・Takoda99-Docs `03_Unity仕様書.md` の節・Protoのメッセージ名）
- **責務**（このモジュールが何をする/しないか。[docs/rules/01](../../../docs/rules/01-責務と絶対原則.md) の「持たないもの」に触れない）
- **公開インターフェース**（クラス/メソッドのシグネチャ。C#の具体的な型で書く。`pureC#` 側のIFを実装する場合はその対応を明記）
- **Unity構成**（MonoBehaviourのライフサイクル・必要なPrefab/シーン要素・Inspector公開値・使用するUnityパッケージ）
- **ふるまいの詳細**（正常系・エッジケース・エラー時の挙動）
- **依存関係**（他のどのモジュール／`pureC#` のどのモジュールに依存するか。依存方向は Client-Docs 第3章の図に従う）
- **未確定事項**（このモジュール固有で、実装しながら決めることになりそうな点）

## 4. 運用ルール

1. 仕様書は**宣言的に書く**（議事録ではなく「何がどういう形か」）。Takoda99-Docs/Client-Docsの文体に揃える。
2. 仕様書のレビューを経てから実装に着手する。仕様書と実装を同一PRで出してもよいが、**先に仕様書のdiffが読めるコミットを分ける**（[docs/rules/03-Git運用.md](../../../docs/rules/03-Git運用.md) §3 の `spec:` コミット）。
3. 実装中に仕様と異なる判断をしたら、その場でコードだけ直さず**仕様書を更新してから**コードを直す。
4. **`pureC#` 側のモジュールの仕様をここに書かない。** `Dispatcher`/`Store`/`TypingJudge` 等の仕様は [`pureC#/docs/.sdd/`](../../../pureC%23/docs/.sdd/README.md) が正典。ここには「それをUnityからどう使うか」だけを書く。
5. **WebGL制約に反する設計を書かない**（`System.Net.WebSockets` の使用、Thread前提の非同期など。Takoda99-Docs `03_Unity仕様書` §3）。
6. 各仕様書の「未確定事項」が解消されたら、該当箇所を仕様書から削除し、決定内容を本文に反映する。
