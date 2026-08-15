# 03-優勝者に脱落モーダルを出さない（`EliminationResultView` の分岐）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`StoreEliminatedBatch` / `PersonalResult` / `MatchEnd`）／[30_通信シーケンス §5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)／既存 [02-result-rank-tier.md](./02-result-rank-tier.md)。矛盾したら上流優先。

## 0. このファイルが解こうとしている問題

**サーバー担当からの指摘（2026-08-14）**：

> 120秒で10人全員に StoreEliminated が飛ぶ。素直に「脱落しました」を出すと優勝者に敗北演出が流れるので、finalRank で分岐をお願い

本選では**1位も脱落イベントを受け取る**。予選の「優勝者には脱落が来ない」という前提は消えた（[pureC# result/01](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)）。

**サーバー担当に確認済みの事実（2026-08-15 回答）**：

| # | 確認事項 | 回答 |
|---|---|---|
| 1 | 最終ステージの配信順 | `StoreEliminatedBatch` → `PersonalResult` → `RankingSnapshot` → `MatchEnd` |
| 2 | 送信間隔 | **0ms（同一 tick 内で連続送信）** |
| 3 | 最終バッチの段階番号 | `stageIndex = 6` / `stageTotal = 6` |
| 4 | 優勝者への `PersonalResult` | **届く**（`MatchEnd` の2メッセージ前） |

**つまり優勝者にも、120秒に `StoreEliminatedBatch`（`finalRank = 1`）が必ず届く。** モーダルの表示契機がこのイベントに紐づいている限り、優勝者にもモーダルが出る。

## 1. 現状の実装と、何が足りないか

`MainGame` シーンの `ResultCanvas`（[EliminationResultView.cs](../../../Assets/Scripts/View/EliminationResultView.cs)）は、**脱落モーダルと試合終了モーダルを1つのコンポーネントで兼ねている**。表示契機は2つある。

| # | 契機 | 呼び出し | 対象 |
|---|---|---|---|
| A | 自店が `StoreEliminatedBatch` に含まれた | `Renderer.OnStoreEliminatedBatch` → `Show(finalRank)` | 途中で脱落した店 |
| B | `MatchEnd` を受信した | `Renderer.OnMatchEnd` / `HandleStateChanged` → `ShowIfHidden(finalRank)` | **全店（優勝者を含む）** |

**A は対応済み。** [Renderer.cs:347](../../../Assets/Scripts/View/Renderer.cs:347) に `finalRank == 1` なら出さない分岐を入れた（`MatchEnd` の到着を待って抑止する形だと、A→B の 0ms の間だけ優勝者に「1位」と書かれた脱落モーダルが見える）。

**B が未対応。** `ShowIfHidden` は順位を問わず全店に出すため、**優勝者は結局このモーダルを見る**。A の分岐だけでは要件を満たしていない。これがこの仕様書で埋める穴。

> **モーダルの中身自体には「脱落」という文字は無い**（順位の数字＋上位10店＋「次へ」ボタン。[MainGame.unity](../../../Assets/Scenes/MainGame.unity) の `ResultCanvas`）。それでも出してはいけないのは、**このモーダルが「試合から降りた人の画面」として設計されており、優勝の演出が1つも無いまま「次へ」を押させることになる**ため。優勝の演出は `Result` シーンの `Champion` 段階（[02-result-rank-tier.md](./02-result-rank-tier.md)）が持っている。

## 2. 責務

**する**

- 自店の `FinalRank` が 1 のとき、`MainGame` の `ResultCanvas` を**一度も表示しない**
- 優勝者を、モーダルを経由せず `Result` シーンへ進める
- 上記の判定を、**メッセージの到着順に依存しない形**で行う

**しない**

- 「優勝したか」をクライアントで計算しない（生存数・スコア・`stageIndex` から導出しない）。**判定材料は `FinalRank` ただ1つ**（[02-result-rank-tier.md](./02-result-rank-tier.md) と同じ原則）
- モーダルの文言・見た目を順位で切り替えることで解決しない（優勝演出は `Result` シーンの責務。`MainGame` に2つ目のリザルト画面を作らない）
- `Renderer` に「優勝者かどうか」の状態を持たせない（`selfEliminated` のようなフラグを増やさない）

## 3. ふるまいの詳細

### 3.1 判定の単一ルール

```csharp
// 優勝者か。判定材料は FinalRank ただ1つ。
private static bool IsChampion(int finalRank) => finalRank == 1;
```

`FinalRank` の供給源は2つあり、契機ごとに使い分ける。**どちらも同じ値になる**（サーバー権威）。

| 契機 | 供給源 | 理由 |
|---|---|---|
| A（`StoreEliminatedBatch`） | `entries` 内の自店の `FinalRank` | この時点で `PersonalResult` はまだ届いていない（配信順は batch が先） |
| B（`MatchEnd`／state 駆動） | `state.PersonalResult.FinalRank` | この時点で `PersonalResult` は必ず届いている（`MatchEnd` の2メッセージ前） |

### 3.2 契機 A：`Renderer.OnStoreEliminatedBatch`

**実装済み**（[Renderer.cs:347](../../../Assets/Scripts/View/Renderer.cs:347)）。この仕様書は既存実装を追認する。

```
自店が entries に含まれる
  ├ 一斉脱落演出は流す（massElim.Play）        ← 優勝者にも流す。全店が脱落する事実の演出であり、敗北の演出ではない
  ├ 行列・お題をクリアする                      ← 優勝者も打鍵は終わる
  └ finalRank == 1 → モーダルを出さずに return
     finalRank != 1 → resultView.Show(finalRank)
```

### 3.3 契機 B：`MatchEnd` 受信時（★この仕様書の本体）

`Renderer` には `MatchEnd` 起点の表示経路が2つある（[Renderer.cs:140](../../../Assets/Scripts/View/Renderer.cs:140) の state 駆動と、[Renderer.cs:367](../../../Assets/Scripts/View/Renderer.cs:367) の `OnMatchEnd` コールバック）。**この二重化は意図的なもので、維持する**（コールバック経路が例外で落ちても state 駆動側が拾う、という予選のバグ対策）。したがって**分岐は両方に入れる**。

```
MatchEnd を受信（= state.MatchEnded == true）
  ├ rank = state.PersonalResult?.FinalRank ?? 0
  ├ rank == 1 → モーダルを出さない。GoToResult() で Result シーンへ直行する
  └ rank != 1 → resultView.ShowIfHidden(rank)（従来どおり）
```

**分岐を `EliminationResultView` 側（`Show` / `ShowIfHidden` の内部）に置かない。** 表示するかどうかを決めるのは `Renderer` の責務であり、View は「出せと言われたら出す」に留める（既存の `IsShown` による冪等性を壊さないため）。

### 3.4 優勝者の遷移

他の店は「モーダル →『次へ』ボタン → `GoToResult()`」で `Result` シーンへ進む（[EliminationResultView.cs:184](../../../Assets/Scripts/View/EliminationResultView.cs:184)）。**優勝者はこのボタンを踏む機会が無い**ため、`Renderer` が代わりに遷移させる。

- 遷移は `Bootstrap.GameBootstrapper.Instance.GoToResult()`（[GameBootstrapper.cs:248](../../../Assets/Scripts/Bootstrap/GameBootstrapper.cs:248)）
- **`MatchEnd` の受信から遷移までに 1〜2 秒の余韻を置く**。0ms で `Result` シーンへ飛ぶと、120秒の一斉脱落演出（`massElim`）が始まった瞬間に画面ごと消える。演出の尺は [elimination/01-mass-elimination-effect.md](../elimination/01-mass-elimination-effect.md) に合わせる
- **遷移は1回だけ**。state 駆動側は `MatchEnded` の間ずっと呼ばれ続けるため、`Renderer` に「遷移済み」の真偽値を1つ持たせて二重ロードを防ぐ（`LoadScene` の多重呼び出しは実害が出る）

> **`PersonalResult` が未着のまま `MatchEnd` が来た場合**（`rank == 0`）は、**従来どおりモーダルを出す**。優勝者扱いにしない。「試合が終わったのに画面から出られない」を作らないことが最優先（[pureC# result/01 §3.1](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)）。サーバー確認により通常は起こらないが、取りこぼし時のフォールバックとして残す。

## 4. 依存関係

- 依存するモジュール：`ClientState.PersonalResult`（pureC# [result/01](../../../../pureC%23/docs/.sdd/result/01-personal-result.md)）、`GameBootstrapper.GoToResult()`
- 依存されるモジュール：`Result` シーン（[01-personal-result-view.md](./01-personal-result-view.md) / [02-result-rank-tier.md](./02-result-rank-tier.md)）
- **変更するファイル**：[Renderer.cs](../../../Assets/Scripts/View/Renderer.cs) のみ。`EliminationResultView` と Prefab は触らない

## 5. テスト観点

| # | 観点 | 期待 |
|---|---|---|
| 1 | `finalRank = 1` の自店を含む `StoreEliminatedBatch` を流す | モーダルが表示されない（`IsShown == false`） |
| 2 | 続けて `PersonalResult(finalRank=1)` → `MatchEnd` を流す | モーダルが最後まで表示されず、`Result` シーンへ遷移する |
| 3 | `finalRank = 7` で同じ流れを通す | 契機 A でモーダルが出て、`MatchEnd` でも二重に出ない（順位が上書きされない） |
| 4 | `PersonalResult` を送らずに `MatchEnd` だけ流す | `rank = 0` でモーダルが出る（優勝者扱いしない） |
| 5 | `MatchEnd` 後に state 変化を10回起こす | `GoToResult()` が1回しか呼ばれない |
| 6 | 実測どおり4メッセージを 0ms 間隔で連続投入する | 1〜3 の結果が変わらない（到着順・間隔に依存しない） |

シナリオでの再現は `pureC#/testdata/scenarios/self-eliminated.json` を雛形にし、**優勝ケース（`finalRank = 1`）のシナリオを1本追加する**。

## 6. 未確定事項

- 余韻の秒数（1秒か2秒か）。一斉脱落演出の尺が決まり次第あわせる
- 優勝者に `MainGame` 上で専用の一言（「優勝！」等）を出すかどうか。**この仕様書では出さない**方針だが、演出上ほしくなった場合は `Result` シーン側ではなくここに追記する
