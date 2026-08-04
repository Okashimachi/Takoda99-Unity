# pureC# — Unity非依存のクライアントロジック

`takoda99-unity` リポジトリの中で、**Unityを起動せずに実装・ビルド・単体テストできる純粋な C# コード**を置く領域。

対象は [Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) で「プラットフォーム依存＝なし」とされたモジュール一式：

- `Contract`（Takoda99-Proto の C# DTO を参照するだけ。ここでは定義しない）
- `Dispatcher`（`Envelope.type` 振り分け → Action化 → Store へ）
- `Store` / `Reducer`（`ClientState` の唯一の保持者）
- `TypingJudge`（打鍵判定。クライアント唯一のローカルドメイン）
- `RomajiTable`（Proto共有データの差し替え口）
- `MatchClientController`（ライフサイクル状態機械の駆動・各モジュールの結線）

**`PatienceTimer` / `Renderer` / `InputSource` / WebGL向け `NetworkClient` はここに含めない**（Unity依存のため `Unity/Assets/` 側で実装する。[docs/rules/01-責務と絶対原則.md](../docs/rules/01-責務と絶対原則.md)）。

---

## 1. ディレクトリ構成

```
pureC#/
  docs/
    .sdd/           # 仕様書駆動開発（Spec-Driven Development）の仕様書一式
  src/              # .sdd の仕様書に基づいて実装するコード本体
```

仕様書とソースの対応関係・書き方のルールは [docs/.sdd/README.md](./docs/.sdd/README.md) を参照。

## 2. 開発の進め方（仕様書駆動開発）

1. 実装したいモジュール（例：`TypingJudge`）について、まず `docs/.sdd/` に仕様書を書く（[docs/.sdd/README.md](./docs/.sdd/README.md) のテンプレに従う）。
2. 仕様書はTakoda99-Client-Docsの該当章を出発点にし、実装に必要な粒度（クラス構造・メソッドのふるまい・エッジケース）まで具体化する。
3. 仕様書のレビュー・合意後、`src/` に実装する。
4. 実装が仕様書と食い違ったら、**まず仕様書を直してから**コードを直す（コードだけ直して仕様書を放置しない。仕様書とコードの対応が崩れたら次にこのモジュールを触る人が損をする）。

## 3. プロジェクト構成

```
pureC#/
  vendor/Takoda99.Proto/    # Takoda99-Proto のソース手ミラー（VERSION.md に取得元・固定版を明記）
  src/
    Takoda99.Client/            # netstandard2.1 クラスライブラリ本体（Contract/Typing/State/Net/Lifecycle）
    Takoda99.Client.Tests/      # net8.0 + xUnit のテストプロジェクト
```

- `src/Takoda99.Client` は Unity非依存の `.NET` クラスライブラリ（`netstandard2.1`。UnityのC#バージョンと合わせる）としてビルドできる（`dotnet build`）。
- テストは `src/Takoda99.Client.Tests`（xUnit、`dotnet test`）。Unity非依存のテストランナーで実行する。
- `UnityEngine` への参照・`using UnityEngine` を `src/` に一切書かない（CIやレビューでチェックする）。
- Takoda99-Proto の C# DTO は `vendor/Takoda99.Proto/Messages.cs` をソース手ミラーとして取り込み、`Takoda99.Client.csproj` から `<Compile Include>` で参照する（NuGet/GitHub Packages はこの開発環境から認証済み解決ができないため採用しなかった。経緯は `vendor/Takoda99.Proto/VERSION.md`）。
- Unity側（`Unity/Assets/`）からの参照方法（DLL参照 or ソース直接取り込み）は、Unity側の実装開始時にここへ追記する（未確定のまま）。

## 4. 上流との関係

上流は [Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs)（モジュール定義・言語非依存インターフェース）と [Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)（DTO/メッセージ契約）。矛盾したら上流優先で `.sdd` 仕様書側を直す。
