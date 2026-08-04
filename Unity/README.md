# Unity — Unity依存のクライアント実装

`takoda99-unity` リポジトリの中で、**Unityエディタ／Unityランタイムに依存する実装**を置く領域。Unityプロジェクト本体（`Assets/` `Packages/` `ProjectSettings/`）はこのディレクトリ配下にある。

対象は [Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) で「プラットフォーム依存＝あり」とされたモジュール一式：

- `Renderer`（受信 state を描く。Prefab/UI/エフェクト。**全面Unity依存**）
- `InputSource`（Input System → `OnCharKey`（文字キー）へ正規化）
- `NetworkClient` の実体（`WebGLNetworkClient`。WebGL対応WebSocketライブラリで `/ws` に接続）
- `PatienceTimer`（我慢ゲージの**表示専用**カウントダウン）
- デバッグパネル（送受信 `Envelope` の生JSON表示）

**`Contract` / `Dispatcher` / `Store`+`Reducer` / `TypingJudge` / `RomajiTable` / `MatchClientController` はここに含めない**（Unity非依存のため [`pureC#/`](../pureC%23/README.md) 側で実装し、ここからは参照するだけ。二重実装しない。[docs/rules/01-責務と絶対原則.md](../docs/rules/01-責務と絶対原則.md)）。

---

## 1. ディレクトリ構成

```
Unity/
  docs/
    .sdd/           # 仕様書駆動開発（Spec-Driven Development）の仕様書一式
  Assets/           # .sdd の仕様書に基づいて実装するUnityプロジェクト本体
  Packages/
  ProjectSettings/
  tests/            # Unityを起動せずに実行する単体テスト（Assets外・下記 §2.1）
```

`docs/` と `tests/` は `Assets/` の外に置く（Unityのアセットインポート対象に含めないため）。仕様書の書き方・一覧は [docs/.sdd/README.md](./docs/.sdd/README.md) を参照。

### Assets/ 配下の構成

```
Assets/Scripts/
  Net/        WebGLNetworkClient（実体）
  State/      （pureC#/src の Store/Reducer を参照）
  Typing/     （pureC#/src の TypingJudge を参照）
  Lifecycle/  （pureC#/src の MatchClientController を参照）
  Timer/      PatienceTimer.cs
  View/       Prefab/MonoBehaviour（描画）
  Input/      UnityInputSource.cs
```

## 2. 開発の進め方（仕様書駆動開発）

`pureC#/` と同じく、**実装より先に仕様書を書く**。

1. 実装したいモジュール（例：`PatienceTimer`）について、まず `docs/.sdd/` に仕様書を書く（[docs/.sdd/README.md](./docs/.sdd/README.md) のテンプレに従う）。
2. 仕様書はTakoda99-Client-Docs／Takoda99-Docs `03_Unity仕様書.md` の該当箇所を出発点にし、実装に必要な粒度（クラス構造・MonoBehaviourのライフサイクル・Prefab構成・エッジケース）まで具体化する。
3. 仕様書のレビュー・合意後、`Assets/` に実装する。
4. 実装が仕様書と食い違ったら、**まず仕様書を直してから**コードを直す。

> `pureC#/` との違いは「Unity固有の要素（MonoBehaviour・Prefab・シーン・Input System・WebGL制約）を仕様書に書いてよい／書くべき」点のみ。進め方のルールは共通。

### 2.1 View用派生状態のテスト

`Assets/Scripts/View/ValueObjects/`（[docs/.sdd/value-objects/](./docs/.sdd/value-objects/README.md) の実装）は**純粋関数のみ**で `UnityEngine` に依存しないため、Unityエディタを起動せずに単体テストできる。`tests/Takoda99.View.Tests` が同ソースを**リンク参照**（コピーではない）してテストする。

```bash
dotnet test "Unity/tests/Takoda99.View.Tests/Takoda99.View.Tests.csproj"
```

`Assets/` 側に `MonoBehaviour` 等のUnity依存コードを足すときは、このテストプロジェクトのリンク対象を `ValueObjects/` に限ったままにする（Unity依存コードのテストは Unity Test Framework 側で行う）。

### 2.2 pureC# の参照方法（未確定）

`pureC#/src` を `Assets/` からどう参照するか（DLL参照 or ソース直接取り込み）は**まだ決まっていない**。決まるまでの暫定として、`View/ValueObjects` の変換関数は `pureC#` の値オブジェクト型ではなく**素の値（`evalNormalized` / `creditLife` / `orderCount` 等）を引数に取る**。参照方法が決まった時点で、`pureC#` の型を直接受けるオーバーロードを追加し、本節を更新する。

## 3. Unity側で守る制約

- **描画は受信 state 経由のみ。** 勝敗に関わる数値をUnity側で自前算出しない（サーバー権威）。
- **`PatienceTimer` は表示専用。** 離脱の確定はしない（サーバーの `CustomerLeft` を待つ）。
- **接続先URLをコードに直書きしない。** ビルド設定／ScriptableObject 等で切り替える。
- **WebGL制約**：`System.Net.WebSockets` は使えない。Thread／一部 Task にも制約があるため、非同期はコールバック／更新ループ駆動で書く（Takoda99-Docs `03_Unity仕様書` §3）。
- **`pureC#/src` のモジュールをUnity内で再実装しない。** 参照して使う。

## 4. 上流との関係

上流は [Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs)（モジュール定義・言語非依存インターフェース）、[Takoda99-Docs `04_クライアント仕様/03_Unity仕様書.md`](https://github.com/Okashimachi/Takoda99-Docs)（Unity固有の実装方針・WebGL制約）、[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)（DTO/メッセージ契約）。矛盾したら上流優先で `.sdd` 仕様書側を直す。
