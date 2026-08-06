# 04-Renderer

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`Renderer` / `IRenderer`）／[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md)（`IRenderer` の呼ばれ方）／[08](./02-main-store-view.md)〜[10](./04-sub-store-board-view.md)（下位View）。矛盾したら上流優先。

`Takoda99.Client.Lifecycle.IRenderer` の Unity 実体。`MatchClientController` からの離散イベント通知と、`IStore` の連続的な状態変化の両方を受け、既存の下位View（`MainStoreView` / `TakoyakiStandView` / `SubStoreBoardView`）へ振り分ける。[06-view-sample-data.md](./06-view-sample-data.md) の `MainGameViewSampleDriver` が担っていた「サンプルデータを流す」役割を、実データ（`ClientState`）に置き換える。

## 1. 責務

- `IRenderer` を実装し、`MatchClientController` からの離散イベント（客の到着/離脱・打鍵フィードバック・脱落・試合終了 等）を下位Viewへ振り分ける
- `IStore.Subscribe` で `ClientState` の変化を購読し、連続値（信用ライフ・評価・お題単語・他店一覧）を下位Viewへ反映する
- **しない**こと：
  - 表示の**決定**（値の分類・閾値判定は `Takoda99.View.ValueObjects` の各値オブジェクトの責務。`Renderer` は値を右から左へ渡すだけ）
  - `Store`/`Dispatcher` への書き込み（`Renderer` は読み取り専用の購読者）
  - 打鍵判定（`TypingJudge` の責務。`ITypingJudge.CurrentView` を読むだけ）

## 2. 公開インターフェース

```csharp
namespace Takoda99.View
{
    /// <summary>IRenderer の Unity 実体（01-renderer.md）。</summary>
    public sealed class Renderer : MonoBehaviour, IRenderer
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private SubStoreBoardView subStoreBoard;
        [SerializeField] private PatienceTimer patienceTimer; // 対応中の客1名ぶん（未確定事項参照）

        /// <summary>Bootstrap から IStore / ITypingJudge を注入する（MonoBehaviour のため Inspector 配線ではなくコード経由）。</summary>
        public void Bind(IStore store, ITypingJudge typingJudge);

        // IRenderer 実装
        public void OnCustomerArrived(CustomerView customer);
        public void OnCustomerLeft(string customerId, LeaveReason reason);
        public void OnKeyFeedback(KeyResult result);
        public void OnOrderServed(string customerId);
        public void OnPhaseChanged(Phase phase);
        public void OnForcedEliminationWarning(int untilTick, double thresholdPct);
        public void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank);
        public void OnMatchEnd(int finalRank, MatchStats stats);
        public void OnLifecycleChanged(ClientPhase from, ClientPhase to);
        public void OnConnectionTrouble(string kind);
    }
}
```

## 3. Unity構成

- **MonoBehaviour のライフサイクル**
  - `OnEnable`：`GameBootstrapper.Instance`（別シーンに永続する。[02-scene-composition.md](../foundation/02-scene-composition.md) §4）から `Store`/`ITypingJudge` を取得して `Bind` し、`GameBootstrapper.AttachRenderer(this)` で `MatchClientController` の `IRenderer` 転送先として自己登録する
  - `OnDisable`：`GameBootstrapper.DetachRenderer(this)` を呼び、購読解除する
  - `Bind(store, typingJudge)`：`store.Subscribe(HandleStateChanged)` を行い、以後は購読で駆動する。通常は `OnEnable` が自動で呼ぶが、テスト等で明示的に呼んでもよい公開メソッドとして残す
  - `Update`：使わない（`PatienceTimer` 自身が `Update` を持つ）
- **Inspector 公開値**：`mainStore` / `subStoreBoard` / `patienceTimer`（シーン内の既存Viewへの参照）
- `TakoyakiStandView` へは直接触らない。`MainStoreView.EvalLevelChanged` を自分で購読して連動する既存の仕組み（03-takoyaki-stand-view.md）をそのまま使う

## 4. ふるまいの詳細

### 4.1 連続値（`HandleStateChanged(ClientState state)`）

`store.Subscribe` のコールバックで、変化のたびに以下を無条件に再適用する（差分検知はしない。下位Viewが冪等な `Set*` メソッドを持つ前提。[08](./02-main-store-view.md) 参照）。

- `mainStore.SetCreditLife(state.CreditLife)`
- `mainStore.SetEvaluation(state.Normalized, state.Alive)`
- `state.CurrentOrder` が非null：`typingJudge.CurrentView` から `mainStore.SetWord(CurrentWord, "")` ・ `mainStore.SetTypedProgress(TypedKanaLength, 0)`。**ローマ字表示は未対応**（`TypingView` にローマ字の確定文字数が無いため。[08 未確定事項](./02-main-store-view.md#7-未確定事項)と同じ未解決事項）
- `state.CurrentOrder` が null：`mainStore.SetWord("", "")`
- `subStoreBoard.SetSummary(storeId, creditLife, alive)` を `state.Stores` の自店以外全件に適用。`SetRank` は `StoreSummary.FinalRank.HasValue` のときのみ呼ぶ（`null` を 0 として渡さない。[10 テスト観点](./04-sub-store-board-view.md#6-テスト確認観点)）

### 4.2 離散イベント（`IRenderer` 実装）

| メソッド | 振り分け先 |
|---|---|
| `OnCustomerArrived` | `store.State.Queue` の先頭がこの客になった時点（`HandleStateChanged` 内で先頭客IDの変化を検知）で `patienceTimer.Begin(arrivedAtLocalMs, customer.PatienceMaxMs)` を呼ぶ。**`OnCustomerArrived` の引数だけでは `ArrivedAtLocalMs` が取れない**（`CustomerView` に含まれない）ため、実際の起動は `HandleStateChanged` 側の「対応中客が変わった」検知に委ねる。本メソッド自体は何もしない（将来の演出フック用に残す） |
| `OnCustomerLeft` | 離脱した客が対応中客だった場合のみ `patienceTimer.Stop()` |
| `OnKeyFeedback` | 未使用（打鍵の即時エフェクトは未確定。[02-scene-composition.md](../foundation/02-scene-composition.md)） |
| `OnOrderServed` | 未使用（提供演出は未確定） |
| `OnPhaseChanged` | 未使用（フェーズ別演出は未確定） |
| `OnForcedEliminationWarning` | 未使用（下位淘汰警告演出は未確定） |
| `OnStoreEliminated` | 未使用（脱落演出は `SubStoreTileView` が `StoreSummary.Alive` の変化から自律的に行う。[10](./04-sub-store-board-view.md) 参照） |
| `OnMatchEnd` | 未使用（リザルト画面は未着手） |
| `OnLifecycleChanged` | 未使用（シーン切り替えは `Bootstrap` の責務） |
| `OnConnectionTrouble` | `Debug.LogWarning` のみ（デバッグパネルで詳細確認する前提。[03-debug-panel.md](../platform/03-debug-panel.md)） |

### 4.3 対応中客の追跡

`HandleStateChanged` の中で、直前フレームの `state.Queue` 先頭 `CustomerId` と今回を比較する：

- 先頭が変わった（新しい客になった）→ `patienceTimer.Begin(newFront.ArrivedAtLocalMs, newFront.View.PatienceMaxMs)`
- 先頭が居なくなった（行列が空になった）→ `patienceTimer.Stop()`

## 5. 依存関係

- 依存する `pureC#` モジュール：`Takoda99.Client.Lifecycle.IRenderer`、`Takoda99.Client.State.IStore` / `ClientState`、`Takoda99.Client.Typing.ITypingJudge`
- 依存するUnity側モジュール：`MainStoreView`（[08](./02-main-store-view.md)）、`SubStoreBoardView`（[10](./04-sub-store-board-view.md)）、`PatienceTimer`（[03](./05-patience-timer.md)）
- 依存されるモジュール：`MatchClientController`（`IRenderer` として注入される）、`Bootstrap`（`Bind` を呼ぶ）
- `Renderer` に依存してよいモジュールは無い（Client-Docs 第3章の依存方向図の終端）

## 6. テスト・確認観点

`UnityEngine` 依存のため xUnit では検証できない。Unity Editor 実行で確認する。

- `CustomerArrived` → 行列先頭になった瞬間に `PatienceTimer` が開始するか
- 対応中客が `CustomerLeft` した瞬間に `PatienceTimer` が止まるか（離脱理由を問わない）
- `CreditUpdate` / `EvaluationUpdate` 相当の `Store` 変化で `MainStoreView` が即座に追従するか
- 他店の `StoreListUpdate` で `SubStoreBoardView` の該当タイルが更新されるか

## 7. 未確定事項

- ローマ字の入力進捗表示（[08 未確定事項](./02-main-store-view.md#7-未確定事項)と同一）。`TypingJudge` にローマ字確定文字数を返すAPIが増えたら、`typingJudge.CurrentView` の拡張に合わせて本モジュールも更新する
- `OnKeyFeedback` / `OnOrderServed` / `OnPhaseChanged` 等、現状「未使用」としたイベントの演出内容（[02-scene-composition.md](../foundation/02-scene-composition.md)の未確定事項と共通）
- 行列に複数の客が並ぶ場合の、対応中客以外（2番手以降）の表示（現状 `PatienceTimer` は1名ぶんのみ）
