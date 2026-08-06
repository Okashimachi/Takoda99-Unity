# .sdd — 仕様書駆動開発（Spec-Driven Development）の仕様書一式

`Unity/Assets/` に実装するUnity依存モジュールの仕様書を置く。**実装より必ず仕様書が先。** 仕様書が無いモジュールを `Assets/` にいきなり実装しない（[01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md)）。

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
Unity/docs/.sdd/**/*.md（↑を Unity 各モジュールの実装粒度まで具体化）  ← ここ
   ↓
Unity/Assets/Scripts/**/*.cs（仕様書の実装）
```

## 2. ディレクトリ構成

**意味の単位でディレクトリを切り、番号はディレクトリ内で完結させる。** ディレクトリをまたいで通し番号を振らない（`value-objects/` に 01〜06 があるところへ直下に 07〜11 を足してしまい、番号が何を表すのか読めなくなった経緯がある）。

| ディレクトリ | 何が入るか | いつ読むか |
|---|---|---|
| [`foundation/`](./foundation/README.md) | Unity と `pureC#` の接続、シーン全体の組み立て | **最初に読む** |
| [`platform/`](./platform/README.md) | `pureC#` 側インターフェースのUnity実体（通信・入力・デバッグ） | 実装前 |
| [`matchmaking/`](./matchmaking/README.md) | 試合が始まる**前**の画面と通信 | 試合前の実装時 |
| [`match-view/`](./match-view/README.md) | 試合**中**の描画（`Renderer` と下位View） | 試合中の実装時 |
| [`value-objects/`](./value-objects/README.md) | `Store` から導出する表示用の派生状態と変換 | 描画モジュールを書く前 |

```
.sdd/
├── README.md            ← このファイル（全体の索引）
├── _template.md         ← 新規仕様書のひな形
├── foundation/          01 pureC#参照(DLL) / 02 シーン構成
├── platform/            01 通信 / 02 入力 / 03 デバッグパネル
├── matchmaking/         01 マッチング進行 / 02 表示名
├── match-view/          01 Renderer / 02 主画面 / 03 たこ焼き台 / 04 小画面 / 05 我慢ゲージ / 06 サンプル駆動
└── value-objects/       01〜06 派生状態
```

## 3. 全仕様書の一覧

### [`foundation/`](./foundation/README.md) — 土台

| # | ファイル | モジュール | 状態 |
|---|---|---|---|
| 01 | [01-purecs-dll-reference.md](./foundation/01-purecs-dll-reference.md) | `pureC#` の参照方法（DLL連携・ビルド自動コピー） | ✅ |
| 02 | [02-scene-composition.md](./foundation/02-scene-composition.md) | シーン構成と結線・`GameBootstrapper` | ✅（方針のみ） |

### [`platform/`](./platform/README.md) — プラットフォーム実体

| # | ファイル | モジュール | 状態 |
|---|---|---|---|
| 01 | [01-network-client.md](./platform/01-network-client.md) | `WebGLNetworkClient`（`INetworkClient` の実体） | ✅ |
| 02 | [02-input-source.md](./platform/02-input-source.md) | `UnityInputSource`（Input System → `OnCharKey`） | ✅ |
| 03 | [03-debug-panel.md](./platform/03-debug-panel.md) | `DebugPanel`（送受信 `Envelope` の生JSON表示） | ✅ |

### [`matchmaking/`](./matchmaking/README.md) — 試合前

| # | ファイル | 内容 | 状態 |
|---|---|---|---|
| 01 | [01-matchmaking-flow.md](./matchmaking/01-matchmaking-flow.md) | 接続 → `MatchmakingJoin` → 待機 → `MatchStart` | ✅ |
| 02 | [02-display-name.md](./matchmaking/02-display-name.md) | 表示名の入力・送信と他店名の取得 | ✅ |

### [`match-view/`](./match-view/README.md) — 試合中の描画

| # | ファイル | モジュール | 状態 |
|---|---|---|---|
| 01 | [01-renderer.md](./match-view/01-renderer.md) | `Renderer`（受信 state → 下位Viewへ振り分け） | ✅ |
| 02 | [02-main-store-view.md](./match-view/02-main-store-view.md) | `MainStoreView`（暖簾・屋台土台・お題単語・提灯・鉄板） | ✅ |
| 03 | [03-takoyaki-stand-view.md](./match-view/03-takoyaki-stand-view.md) | `TakoyakiStandView` / `TakoyakiSlotView`（24穴） | ✅ |
| 04 | [04-sub-store-board-view.md](./match-view/04-sub-store-board-view.md) | `SubStoreBoardView` / `SubStoreTileView`（他店98店） | ✅ |
| 05 | [05-patience-timer.md](./match-view/05-patience-timer.md) | `PatienceTimer`（我慢ゲージの表示専用カウントダウン） | ✅ |
| 06 | [06-view-sample-data.md](./match-view/06-view-sample-data.md) | `MainGameViewSampleDriver`（開発用サンプルデータ駆動） | ✅ |

### [`value-objects/`](./value-objects/README.md) — 派生状態

`StoreVisualState` / `CustomerMoodState` / `TakoyakiStandState` / `CreditLifeLanternState` / `RankBarViewState` / `SubStoreTileState` / `MatchmakingViewState`。一覧は [README.md](./value-objects/README.md)。

## 4. 仕様書の書式

新規の仕様書は [`_template.md`](./_template.md) をコピーして作成する。最低限、以下を含める：

- **参照する上流**（Client-Docsの章番号・Takoda99-Docs `03_Unity仕様書.md` の節・Protoのメッセージ名）
- **責務**（このモジュールが何をする/しないか。[01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md) の「持たないもの」に触れない）
- **公開インターフェース**（クラス/メソッドのシグネチャ。C#の具体的な型で書く。`pureC#` 側のIFを実装する場合はその対応を明記）
- **Unity構成**（MonoBehaviourのライフサイクル・必要なPrefab/シーン要素・Inspector公開値・使用するUnityパッケージ）
- **ふるまいの詳細**（正常系・エッジケース・エラー時の挙動）
- **依存関係**（他のどのモジュール／`pureC#` のどのモジュールに依存するか。依存方向は Client-Docs 第3章の図に従う）
- **未確定事項**（このモジュール固有で、実装しながら決めることになりそうな点）

## 5. 運用ルール

1. 仕様書は**宣言的に書く**（議事録ではなく「何がどういう形か」）。Takoda99-Docs/Client-Docsの文体に揃える。
2. 仕様書のレビューを経てから実装に着手する。仕様書と実装を同一PRで出してもよいが、**先に仕様書のdiffが読めるコミットを分ける**（[03-Git運用.md](../../../docs/rules/03-Git運用.md) §3 の `spec:` コミット）。
3. 実装中に仕様と異なる判断をしたら、その場でコードだけ直さず**仕様書を更新してから**コードを直す。
4. **`pureC#` 側のモジュールの仕様をここに書かない。** `Dispatcher`/`Store`/`TypingJudge` 等の仕様は [`pureC#/docs/.sdd/`](../../../pureC%23/docs/.sdd/README.md) が正典。ここには「それをUnityからどう使うか」だけを書く。
5. **WebGL制約に反する設計を書かない**（`System.Net.WebSockets` の使用、Thread前提の非同期など。Takoda99-Docs `03_Unity仕様書` §3）。
6. 各仕様書の「未確定事項」が解消されたら、該当箇所を仕様書から削除し、決定内容を本文に反映する。
7. **新しい仕様書は、まず §2 のどのディレクトリに属するかを決めてから作る。** どれにも当てはまらないならディレクトリを追加し、§2 と §3 に行を足す。**直下に置かない。**

## 6. 実装コードとの対応

| 仕様書 | 実装 |
|---|---|
| `foundation/` | `Assets/Scripts/Bootstrap/`、`Assets/Plugins/Takoda99/` |
| `platform/` | `Assets/Scripts/Net/`、`Assets/Scripts/Input/`、`Assets/Scripts/Debug/` |
| `matchmaking/` | `Assets/Scripts/View/MatchmakingScreenView.cs` |
| `match-view/` | `Assets/Scripts/View/`、`Assets/Scripts/Timer/` |
| `value-objects/` | `Assets/Scripts/View/ValueObjects/`（テストは `Unity/tests/Takoda99.View.Tests/`） |

各 `.cs` の先頭には対応する仕様書のパスをコメントで書く（`// 仕様書: Unity/docs/.sdd/...`）。**仕様書を移動したらこのコメントも直す。**
