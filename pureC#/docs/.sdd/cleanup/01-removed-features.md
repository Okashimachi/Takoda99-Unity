# 01-撤去する機能（`pureC#`）

> 参照する上流：[20_廃止・非使用リスト §3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)／[本選企画書 3.2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)。

## 0. 方針：**残して使わない、ではなく消す**

Proto 側は互換のため定義を残しているが、**このリポジトリのコードからは消す**。

| 理由 | 内容 |
|---|---|
| Obsolete フィールドは**ゼロ値で届く** | 残すと「ライフ0」「星0」「我慢0ms」で描かれる。無効化より誤作動のほうが厄介 |
| 実装者がSonnet/人間を問わず迷わない | 参照が残っていると「これは使うのか」の判断が毎回発生する |
| 予選挙動へ戻す退避弁は**サーバー側**にある | `GameParameters` はサーバー配信であり、クライアントを戻す必要はない（[20_廃止・非使用リスト §5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)） |

## 1. 撤去一覧（`pureC#/src/Takoda99.Client/`）

### 1.1 信用（ライフ）制

| 対象 | ファイル | 実施する仕様書 |
|---|---|---|
| `ClientState.CreditLife` | `State/ClientState.cs` | [match-state/01 §2.1](../match-state/01-score-and-self-rank.md) |
| `ClientState.With(creditLife:)` 引数 | 同上 | 同上 |
| `CreditUpdateAction` クラス | `State/Actions.cs` | [result/02 §2.2](../result/02-lifecycle-and-renderer.md) |
| `Reducer` の `CreditUpdateAction` 分岐 | `State/Reducer.cs` | 同上 |
| `Dispatcher` の `CreditUpdate` 受理・Decode | `Net/Dispatcher.cs` | 同上 |
| `ApplyMatchStart` の `creditLife: a.Params.InitialLife` | `State/Reducer.cs` | **★これを消し忘れるとライフ0で初期化される** |
| `StoreSummary.CreditLife` の参照 | `State/Reducer.cs`（`CloneWithAlive`） | `CloneWithAlive` ごと廃止（`RankingRow` へ移行） |

### 1.2 我慢ゲージ・客の離脱

| 対象 | ファイル | 実施する仕様書 |
|---|---|---|
| `CustomerLeftAction` クラス | `State/Actions.cs` | [result/02 §5.1](../result/02-lifecycle-and-renderer.md) |
| `Reducer.ApplyCustomerLeft` | `State/Reducer.cs` | 同上 |
| `Dispatcher` の `CustomerLeft` 受理・Decode | `Net/Dispatcher.cs` | 同上 |
| `MatchClientController` の `case CustomerLeftAction` | `Lifecycle/MatchClientController.cs` | 同上 |
| `IRenderer.OnCustomerLeft` | `Lifecycle/IMatchClientController.cs` | [result/02 §3](../result/02-lifecycle-and-renderer.md) |
| `CustomerView.PatienceMaxMs` の参照 | （`pureC#` には無し。Unity 側 `Renderer` / `PatienceTimer`） | Unity `cleanup/` |

> **`Reducer.FindIndex` は `ApplyCustomerLeft` からしか呼ばれていない。** 併せて削除する。

### 1.3 相対評価・星

| 対象 | ファイル |
|---|---|
| `ClientState.EvalRaw` / `Normalized` / `StarRating` / `StarDelta` | `State/ClientState.cs` |
| `With(evalRaw: / normalized: / starRating: / starDelta:)` 引数 | 同上 |
| `EvaluationUpdateAction` の同4フィールド | `State/Actions.cs` |
| `Dispatcher` の `EvaluationUpdate` Decode の同4行 | `Net/Dispatcher.cs` |

### 1.4 予選版の99店概況・結果

| 対象 | ファイル | 置き換え先 |
|---|---|---|
| `ClientState.Stores`（`IReadOnlyList<StoreSummary>`） | `State/ClientState.cs` | `ClientState.Ranking`（[match-state/02](../match-state/02-ranking-store.md)） |
| `StoreListUpdateAction` | `State/Actions.cs` | `RankingSnapshotAction` / `RankingDeltaAction` |
| `Dispatcher` の `StoreListUpdate` 受理・Decode | `Net/Dispatcher.cs` | 同上 |
| `StoreEliminatedAction`（単体） | `State/Actions.cs` | `StoreEliminatedBatchAction`（[match-state/03](../match-state/03-cull-warning.md)） |
| `Reducer.ApplyStoreEliminated` / `CloneWithAlive` | `State/Reducer.cs` | 同上 |
| `StormWarning` クラス / `ClientState.Storm` | `State/ClientState.cs` | `CullWarning` / `ClientState.Cull` |
| `MatchResult` クラス / `ClientState.Result` | `State/ClientState.cs` | `PersonalResultState` / `ClientState.PersonalResult`（[result/01](../result/01-personal-result.md)） |

## 2. 撤去に伴って**消える分岐**（★見落としやすい）

以下は「予選の特殊ケースに対処するためのコード」で、本選では前提そのものが消える。**動くから残す、をしない。**

| 消える分岐 | 予選での理由 | 本選で消える理由 |
|---|---|---|
| 「優勝者には `StoreEliminated` が来ない」ための特別扱い | 最後の1店だけ脱落しないため、順位一覧に自分を載せる契機が `MatchEnd` しか無かった | **120秒に全店が脱落する。** 優勝者も `StoreEliminatedBatch` + `PersonalResult` を受け取る（[30_通信シーケンス 5-B](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)） |
| 「対応中の客が離脱した」ときの `AbortOrder` + 状態巻き戻し | 打鍵中に客が消える割り込みがあった | **客は逃げない。** 一度出たお題は必ず打ち切られる |
| `MatchResult.Reason` の空文字で優勝を判定する処理 | `MatchEnd.reason` が優勝の唯一の手がかりだった | **`MatchEnd` が空になった。** 順位は `PersonalResult.FinalRank` で分岐する |

> Unity 側 `Renderer.cs` に上記1・3に該当する長いコメント付きの分岐が実在する。**コメントごと削除する**（[Unity cleanup/01](../../../../Unity/docs/.sdd/cleanup/01-removed-views.md)）。

## 3. 撤去の確認方法

| 種別 | 確認 |
|---|---|
| 型・フィールド | `dotnet build` が通る（参照が残っていればコンパイルエラーになる） |
| 通信 | `Dispatcher` の `AcceptedPhases` が [result/02 §2.1](../result/02-lifecycle-and-renderer.md) の12行と一致する |
| Obsolete の読み取り | `pureC#/src` を `EvalNormalized` `CreditLife` `StarRating` `StarDelta` `PatienceMaxMs` `Normalized` `EvalRaw` `UntilTick` `ThresholdPct` `InitialLife` で grep して**0件**であること |
| テスト | `dotnet test` が通る。予選前提のテストは**削除ではなく本選の期待値へ書き換える**（テストが消えると退行に気づけない） |

## 4. テストの扱い

| テストファイル | 対応 |
|---|---|
| `State/ReducerTests.cs` | `CreditUpdate` / `CustomerLeft` / `StoreListUpdate` / `StoreEliminated`（単体）/ `MatchEnd`（旧形）のケースを、本選の Action の期待値へ**書き換える** |
| `State/TestMessages.cs` | v0.8.0 のメッセージ生成ヘルパーを追加（`RankingSnapshot` / `RankingDelta` / `StoreEliminatedBatch` / `PersonalResult`） |
| `Lifecycle/FakeRenderer.cs` | `IRenderer` の新しい形に合わせる（[result/02 §3](../result/02-lifecycle-and-renderer.md)） |
| `Lifecycle/MatchClientControllerTests.cs` | 離脱による中断のテストを削除し、**自店脱落による中断**のテストへ差し替える |
| `Contract/EnvelopeCodecTests.cs` | 新メッセージ4種の往復テストを追加 |
| `Testing/Scenario*` | シナリオ JSON に新メッセージを足す。旧メッセージを含むサンプルは本選の内容へ更新 |

## 5. 未確定事項

- なし
