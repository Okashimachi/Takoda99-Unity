# match-view — 試合中の描画

`MatchStart` を受けてから `MatchEnd` までの**試合画面**（`MainGame` シーン）の描画を扱う。

## 1. このディレクトリの位置づけ

試合**前**（接続・待機・名前入力）は [`matchmaking/`](../matchmaking/README.md) の担当。**扱うメッセージも画面も別物**のため分けている。

```
Store（pureC#・サーバー権威の値）
   ↓ 購読
01 Renderer … 受信 state と離散イベントを下位Viewへ振り分ける（このディレクトリの入口）
   ↓
02 主画面 / 03 たこ焼き台 / 04 小画面 / 05 我慢ゲージ … 実際に描く
   ↑
value-objects/ … 「どの区分になるか」の判定はここ（Viewは判定を持たない）
```

**`Renderer` が唯一の入口。** 下位Viewは `Store` や `Dispatcher` を直接参照しない（[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md) の依存方向）。

## 2. ファイル一覧

| # | ファイル | モジュール | 実装 | 状態 |
|---|---|---|---|---|
| 01 | `01-renderer.md` | `Renderer`（`IRenderer` の実体・振り分け） | — | **未作成** |
| 02 | [02-main-store-view.md](./02-main-store-view.md) | `MainStoreView`（暖簾・屋台土台・お題単語・提灯・鉄板） | `View/MainStoreView.cs` | ✅ |
| 03 | [03-takoyaki-stand-view.md](./03-takoyaki-stand-view.md) | `TakoyakiStandView` / `TakoyakiSlotView`（24穴） | `View/TakoyakiStandView.cs` ほか | ✅ |
| 04 | [04-sub-store-board-view.md](./04-sub-store-board-view.md) | `SubStoreBoardView` / `SubStoreTileView`（他店98店） | `View/SubStoreBoardView.cs` ほか | ✅ |
| 05 | `05-patience-timer.md` | `PatienceTimer`（我慢ゲージの表示専用カウントダウン） | — | **未作成** |
| 06 | [06-view-sample-data.md](./06-view-sample-data.md) | `MainGameViewSampleDriver`（開発用サンプルデータ駆動） | `View/Sample/` | ✅ |

**01 から読む。** 02〜05 は 01 が呼ぶ下位Viewであり、単体では駆動しない。

## 3. このディレクトリ共通の原則

### 3.1 View は判定を持たない

「評価が高いか低いか」「客が怒っているか」といった**区分の判定は [`value-objects/`](../value-objects/README.md) の純粋関数**が行う。View は結果を受け取ってスプライトやテキストを差し替えるだけ。

**理由**：判定を View に書くと `UnityEngine` 依存になり、xUnit でテストできなくなる。`value-objects/` 側に置けば `Unity/tests/Takoda99.View.Tests` で検証できる。

### 3.2 サーバー権威の値を再計算しない

信用ライフ・評価・順位・お題単語はすべてサーバーが決めた値をそのまま描く。**閾値による表示区分の分類だけが許される加工**であり、値そのものを補正・推定・先読みしない（[01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）。

我慢ゲージ（05）だけは例外的にクライアントがカウントダウンするが、**離脱の確定は `CustomerLeft` の受信**であり、ゲージが 0 になったことを根拠に客を離脱させない。

### 3.3 06 は実データではない

`MainGameViewSampleDriver` は `Renderer` が無い状態で見た目を確認するための開発用。**実データ駆動（01）に切り替えたら無効化する**（両方が同時に動くと表示が競合する）。

## 4. テスト

View 本体（MonoBehaviour）は `UnityEngine` 依存のため xUnit で検証できない。各仕様書の「テスト・確認観点」は Unity Editor 実行での確認手順として書く。**判定ロジックのテストは [`value-objects/`](../value-objects/README.md) 側**にある。
