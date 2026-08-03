# 02 Unity実装ルール

takoda99-unity の実装コードを書くときのルール（AI に書かせる前提）。責務・原則は [01-責務と絶対原則.md](./01-責務と絶対原則.md) を前提とする。

---

## 1. 2つの領域とその境界

このリポジトリは `Unity/`（Unity依存。実体は `Unity/Assets/`）と `pureC#/`（Unity非依存）の2領域からなる。**どちらに書くべきかを最初に判断する。**

| モジュール | 領域 | 判断基準 |
|---|---|---|
| `Contract`（Proto参照）/ `Dispatcher` / `Store`+`Reducer` / `TypingJudge` / `RomajiTable` / `MatchClientController` | `pureC#/src/` | `UnityEngine` 型を1つも使わずに書ける／書くべき（Takoda99-Client-Docs 第3章） |
| `NetworkClient` の実体（WebGL通信） | `Unity/Assets/` | WebGLビルドは `System.Net.WebSockets` が使えず、Unity依存ライブラリ（NativeWebSocket等）が必要（Takoda99-Docs `03_Unity仕様書` §3） |
| `PatienceTimer` | `Unity/Assets/` | 我慢ゲージの表示専用カウントダウン。Unity側で実装する（合意済み） |
| `Renderer` / `InputSource` | `Unity/Assets/` | Prefab/UI/Input System 全面依存 |

迷ったら「`UnityEngine` を書かずに実装できるか」で判断する。書けるなら `pureC#/src/` に置く。

## 2. pureC#（仕様書駆動開発）

`pureC#/` 配下は **仕様書駆動開発（Spec-Driven Development）** で進める。詳細ルールは [pureC#/README.md](../../pureC%23/README.md) および [pureC#/docs/.sdd/README.md](../../pureC%23/docs/.sdd/README.md) を参照。

- **実装より先に仕様書（`pureC#/docs/.sdd/*.md`）を書く。** 仕様書が無いモジュールを直接 `src/` に実装しない。
- 仕様書はTakoda99-Client-Docsの該当章（モジュール定義・IF定義）を出発点にし、実装に必要な粒度まで具体化したもの。矛盾したらTakoda99-Client-Docsが上流。
- 仕様書とコードの対応が崩れたら、まず仕様書を直してからコードを直す（コードだけ直して仕様書を放置しない）。

## 3. Unity/Assets/ 側のディレクトリ構成

```
Unity/Assets/Scripts/
  Net/        WebGLNetworkClient（実体）
  State/      （pureC#/src の Store/Reducer を参照）
  Typing/     （pureC#/src の TypingJudge を参照）
  Lifecycle/  （pureC#/src の MatchClientController を参照）
  Timer/      PatienceTimer.cs
  View/       Prefab/MonoBehaviour（描画）
  Input/      UnityInputSource.cs
```

- `View/` `Input/` `Net/`（WebGL実装）`Timer/` 以外は、**`pureC#/src` の実装をそのまま参照する**（Unity内で再実装・二重管理しない）。
- 参照方法（DLL参照 or ソース直接取り込み）は導入時に確定し、本節に追記する。

## 4. デバッグパネルを最初に作る

- 送受信した `Envelope` の生JSONをそのまま整形表示するデバッグパネルを最初に用意する（Textro99-WebFrontの `RawStateDebugPane` が実装の参考になる）。
- AI 生成コードにバグがあっても正データをここで常に確認でき、「サーバー(ロジック)のバグか表示のバグか」を即座に切り分けられる。

## 5. 状態管理

- **状態管理ライブラリを入れない。** `Store`/`Reducer`（`pureC#/src`）のみで完結させる。状態の実体はサーバー側にあり、クライアントは受信 state を写すだけ。

## 6. 接続先URLはビルド設定で切替

- 接続先 WebSocket URL は**ビルド設定 / ScriptableObject 等で切替**（ローカル / デプロイ版→本番サーバー）。**コードに直書きしない。**
- 本番URL・トークン等の秘密情報はコミットしない（→ [03-Git運用.md](./03-Git運用.md)）。

## 7. proto はバージョンを固定して参照

- 契約は [Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)（C#: NuGet/GitHub Packages or ソース手ミラー）を**バージョン固定**で参照し、任意のタイミングで上げる（勝手に最新を追わない）。互換表は Proto の README。
- **型の同期は人間が責任を持つ。** proto の DTO が変わったら同じタイミングで参照バージョンを上げ、影響箇所（`pureC#/src` と `Unity/Assets/` 両方）を直す。AI への指示時は必ず proto の型をコンテキストに含める。

## 8. 開発用起動モード（サーバー側）

- サーバー（Go）は `--mode solo`（1クライアントでロジック単体デバッグ）/ `--mode match`（本番相当の同期）で起動できる。Unityはどちらにも同一エンドポイントで接続する。

## 9. AI にロジックを"親切に"実装させない

- AI が気を利かせて経営ロジックを書いてしまう事故を、コード規約コメントとレビューで防ぐ。**レビュー観点は「打鍵判定以外のロジックが混入していないか」に絞る。**
