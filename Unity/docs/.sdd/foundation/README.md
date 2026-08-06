# foundation — 土台（pureC# の参照とシーンの組み立て）

**個々の機能ではなく、「どのモジュールもこの上に乗る」土台**を置く。他のディレクトリの仕様書を読む前に、まずここを読む。

## 1. このディレクトリの位置づけ

`platform/` `matchmaking/` `match-view/` はいずれも「特定の機能」の仕様だが、ここに入る2本は**それらすべてに共通する前提**を定める。

- **01（DLL参照）** — `pureC#` の型をUnityから使えるようにする仕組み。これが無いと `Store` も `Dispatcher` も参照できない
- **02（シーン構成）** — 画面がどのシーンに分かれ、誰が誰を結線し、いつシーンが切り替わるか。個々のViewは「自分がどう呼ばれるか」をここで知る

## 2. ファイル一覧

| # | ファイル | 内容 | 状態 |
|---|---|---|---|
| 01 | [01-purecs-dll-reference.md](./01-purecs-dll-reference.md) | `pureC#` を DLL としてUnityから参照する方法・ビルド時の自動コピー・`Assets/` で使える C# のバージョン制約 | ✅ |
| 02 | [02-scene-composition.md](./02-scene-composition.md) | 5シーン構成・`GameBootstrapper` による結線とシーン遷移・Inspector 配線チェックリスト | ✅（方針のみ。`.unity`/`.prefab` アセットの作成は対象外） |

## 3. 読む順序

```
01（DLL参照）… pureC# の型が使える状態にする
   ↓
02（シーン構成）… その型を誰が生成し、どの画面へ配るか
   ↓
platform/ matchmaking/ match-view/ … 個々のモジュール
```

**01 は `pureC#` の型を使うすべてのモジュールの前提。** `Assets/` 配下で `record` / `init` が使えない（C# 9 まで）といった制約も 01 に書いてある。**知らずに書くとコンパイルが通らない**ため、実装前に必ず読む。

## 4. 注意

- **02 は `.unity` / `.prefab` アセットそのものを作らない。** シーンに何を置き何を結線するかを文書で定めるだけで、アセットの作成はUnityエディタでの人手作業。
- シーン遷移のロジックは `GameBootstrapper`（`Assets/Scripts/Bootstrap/`）に集約する。個々のView側に `SceneManager.LoadScene` を書かない。
