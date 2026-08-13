# 01-撤去する表示（Unity）

> 参照する上流：[20_廃止・非使用リスト §3・§4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)／[12_差分_クライアント §9](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)。

## 0. 方針

**Obsolete フィールドは0で届く。** 表示を残すと「ライフ0」「星0」「我慢0ms＝即離脱」で描かれる。無効化ではなく**撤去**する。

| 撤去する | 撤去しない（★重要） |
|---|---|
| 信用ゲージ・提灯・店の体力表現 | **客キャラクター（属性別の見た目）** |
| 我慢ゲージ | **客の行列** |
| 星評価 | **背景・世界観** |
| たこ焼きの劣化演出 | **注文カウンタ `x/N`・注文吹き出し・屋号** |
| 99店ミニ盤面 | |

## 1. スクリプトの撤去

| ファイル | 対応 |
|---|---|
| `Scripts/View/StarRatingView.cs` | **削除** |
| `Scripts/View/ValueObjects/StarRatingFill.cs` | **削除** |
| `Scripts/View/ValueObjects/CreditLifeLanternState.cs` | **削除** |
| `Scripts/View/SubStoreBoardView.cs` | **削除**（役割は `ranking-view/` へ移譲） |
| `Scripts/View/SubStoreTileView.cs` | **削除** |
| `Scripts/View/ValueObjects/SubStoreTileState.cs` | **削除** |
| `Scripts/Timer/PatienceTimer.cs` | **削除**（見た目だけ残す判断をする場合は、`PatienceMaxMs` に依存しないローカル演出として作り直す） |
| `Scripts/Timer/PatienceGaugePalette.cs` | 同上 |
| `Scripts/View/ValueObjects/PatienceGaugeState.cs` | 同上 |
| `Scripts/View/RankBarView.cs` | **削除**（`StormThresholdPct` に依存していた。役割は `SelfRankView` へ） |
| `Scripts/View/ValueObjects/RankBarViewState.cs` | **削除** |
| `Scripts/View/TakoyakiAppearance.cs` | 劣化（味/見た目）の段階を持つ部分を削除。焼け具合の表現自体は残してよい |

対応する `.meta` と、Prefab／シーン上の参照も併せて外す。

## 2. `MainStoreView` の撤去箇所

| メソッド | 対応 |
|---|---|
| `SetCreditLife(int)` | **削除**（提灯・体力表現） |
| `SetEvaluation(double, bool)` | **削除**（相対評価の廃止） |
| `SetWord` / `SetTypedProgress` / `SetOrderProgress` / `SetPlayerName` | **維持** |

## 3. `Renderer.cs` の撤去箇所

`Assets/Scripts/View/Renderer.cs` は本選対応で最も手が入る。詳細は [../hud/01-hud-composition.md](../hud/01-hud-composition.md) §4。ここでは**消す対象**だけ列挙する。

| 対象 | 行の目印 |
|---|---|
| `SerializeField`：`subStoreBoard` / `patienceTimer` / `starRating` / `rankBar` | 宣言・`WarnIfMissing`・参照すべて |
| `mainStore.SetCreditLife(state.CreditLife)` | — |
| `mainStore.SetEvaluation(state.Normalized, state.Alive)` | — |
| `starRating.SetRating(state.StarRating)` | — |
| `subStoreBoard` のブロックまるごと | `subStoreBoardBound` フラグごと |
| `rankBar.SetState(RankBarViewState.From(...))` | — |
| `patienceTimer?.Begin(nowMs, front.View.PatienceMaxMs)` | `ApplyServingCustomer` 内 |
| `FindSelfDisplayName(state)`（`state.Stores` の線形探索） | `state.DisplayNames` を引く形へ置き換え |
| `OnCustomerLeft(string, LeaveReason)` の実装 | `IRenderer` から消える |
| `OnForcedEliminationWarning(int, double)` の実装 | `OnCullWarning(CullWarning)` へ |
| `OnStoreEliminated(string, EliminationReason, int)` の実装 | `OnStoreEliminatedBatch(...)` へ |
| `OnMatchEnd(int, MatchStats)` の実装 | `OnMatchEnd()` へ |

### 3.1 ★消す「予選の特殊ケース対応」

以下は**長いコメント付きで実在する**分岐。前提そのものが消えるため、**コメントごと削除する**。

| 対象 | 予選での理由 | 本選で消える理由 |
|---|---|---|
| `OnMatchEnd` の「最後まで生き残った店（1位）には `OnStoreEliminated` が来ない。そのため自店を順位一覧へ載せられるのはここだけ」の分岐（`if (!selfEliminated) { … RecordElimination(selfStoreId, finalRank); }`） | 優勝者だけ脱落イベントが来なかった | **120秒に全店が脱落する。** 優勝者も `StoreEliminatedBatch` を受け取る |
| `HandleStateChangedCore` 冒頭の `state.Result != null` によるモーダル表示 | `MatchEnd` が順位を運んでいた | `state.MatchEnded` + `state.PersonalResult?.FinalRank` へ置き換え（[../hud/01 §4.5](../hud/01-hud-composition.md)）。**「state 駆動を唯一の契機にする」という方針自体は維持する** |
| `MatchResult.Reason` の空文字で優勝を判定する `var won = string.IsNullOrEmpty(result.Reason);` | `MatchEnd.reason` が優勝の唯一の手がかりだった | `MatchEnd` が空になった。優勝は `FinalRank == 1` |
| `OnCustomerLeft` の「怒り → 退店」 | 客が我慢切れで帰った | **客は逃げない** |

> **`customerQueue.MarkLeft(customerId)` の呼び出し元が消える。** `CustomerQueueView.MarkLeft` 自体を消すかは任意（`MarkServed` と対になっており、残しても害はない）。

## 4. シーン・Prefab の撤去

| 対象 | 場所 |
|---|---|
| `MainStoreCanvas/EvalCanvas`（星評価） | MainGame シーン |
| 提灯（`CreditLifeLantern` 系） | `MainStore` Prefab |
| 我慢ゲージのゲージUI | MainGame シーン |
| `SubStoreCanvas`（99店ミニ盤面）まるごと | MainGame シーン |
| ランクバー | MainGame シーン |

**空いた領域に `ranking-view/` の3パネルと `SelfRankView` を置く**（[../hud/01 §3](../hud/01-hud-composition.md)）。

## 5. サウンドの撤去

| 対象 | 理由 |
|---|---|
| 客離脱音 | 離脱が発生しない |
| **他店脱落音（都度再生版）** | **そのまま使うと足切り時に24〜49回同時に鳴る。** 集約版へ置き換える（[../elimination/01](../elimination/01-mass-elimination-effect.md)） |
| 信用減少音 | 信用制の廃止 |

## 6. テストの撤去・書き換え

| テスト | 対応 |
|---|---|
| `Takoda99.View.Tests` の `StarRatingFill` / `CreditLifeLanternState` / `SubStoreTileState` / `PatienceGaugeState` / `RankBarViewState` | **削除** |
| 同 `RankingRowViewState` / `CullCountdownState` / `ResultTier` | **新規追加**（[../value-objects/08](../value-objects/08-ranking-row-view-state.md)〜[10](../value-objects/10-result-tier.md)） |
| `MainGameViewSampleDriver` / `ResultSampleData` | 本選のサンプル（ランキング99行・足切り予告・一斉脱落・順位別リザルト）へ差し替え |

## 7. 撤去の確認方法

| # | 確認 |
|---|---|
| 1 | Unity がコンパイルエラーなしで通る |
| 2 | `Assets/Scripts` を `CreditLife` `StarRating` `StarDelta` `PatienceMaxMs` `Normalized` `EvalRaw` `EvalNormalized` `StormThresholdPct` `InitialLife` `OnCustomerLeft` で grep して **0件** |
| 3 | MainGame シーンを再生して、撤去した要素が画面に出ないこと |
| 4 | Prefab／シーンに `Missing (Mono Script)` が残っていないこと |
| 5 | **客キャラクター・行列・背景が今までどおり出ること**（消しすぎていないことの確認） |

## 8. 未確定事項

- 我慢ゲージを見た目だけの演出として残すか（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点2。クライアント担当・アートと相談）。**残す場合もサーバー値に依存しないローカル演出として作り直すこと**（`PatienceMaxMs` は 0 で届く）
