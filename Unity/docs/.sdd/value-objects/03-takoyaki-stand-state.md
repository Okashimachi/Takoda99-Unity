# 03-TakoyakiStandState / TakoyakiSlotState

> 参照する上流：[pureC#/docs/.sdd/value-objects/04-order-progress-state.md](../../../../pureC%23/docs/.sdd/value-objects/04-order-progress-state.md)（`OrderProgressState`）、[pureC#/docs/.sdd/value-objects/03-customer-state.md](../../../../pureC%23/docs/.sdd/value-objects/03-customer-state.md)（`CustomerState.OrderCount`）。矛盾したら上流優先。

## 1. 責務

- たこ焼き台の各穴が「なにもない／生地（未クリア）／焼けた（クリア済み・提供待ち）」のどれかを表す表示用状態を提供する
- 台の物理的な穴数（24）・**評価に応じて生地を流しておく穴数**・いま対応中の客のノルマ進捗、の3つを対応付ける
- 台の穴数と注文個数（`OrderCount`）は**別概念**であり、生地マス数は注文個数に連動しない
- **しない**こと：提供（`Serve`）の確定処理そのもの（`OrderProgressState` 側の責務。ここは表示するだけ）

## 2. データ定義

```csharp
public enum TakoyakiSlotState { Empty, Batter, Cooked }

public readonly record struct TakoyakiStandState(
    IReadOnlyList<TakoyakiSlotState> Slots // 長さ = StandCapacity(24)。index = row * StandColumns + col（行優先・左上原点）
)
{
    public const int StandColumns = 6; // 横
    public const int StandRows    = 4; // 縦
    public const int StandCapacity = StandColumns * StandRows; // 24

    // 生地を流しておく穴の数（評価3段階に対応）。いずれも StandColumns の倍数
    public const int BatterCountLow  = 12;
    public const int BatterCountMid  = 18;
    public const int BatterCountHigh = StandCapacity; // 24
}
```

- 台は **6列×4行＝24穴の横長グリッド**（設計決定）
- `Slots` は1次元の配列として持ち、行優先（左上原点）で2次元グリッドへ写像する。グリッド形状をViewに直接持たせず、この値オブジェクト側で形を定義することで、View差し替え時にも穴の対応がズレない

## 3. 変換処理

入力：`StoreVisualState.EvalLevel`（[01-store-visual-state.md](./01-store-visual-state.md)）と `OrderProgressState`（`TypedWordCount`）

```
occupiedCount =                       // 生地を流しておく穴の数。評価（＝繁盛具合）で決まる
    EvalLevel == Low  → 12            // 1〜2行目
    EvalLevel == Mid  → 18            // 1〜3行目
    EvalLevel == High → 24            // 全マス
cookedCount   = min(TypedWordCount, occupiedCount)  // タイプ完了済みぶん

for i in 0..StandCapacity-1:
    Slots[i] =
        i < cookedCount    → Cooked   // タイプ完了済み。提供待ち
        i < occupiedCount  → Batter   // 生地は流してあるが未クリア
        else               → Empty    // 生地を流していない穴
```

- **`occupiedCount` は注文個数（`OrderCount`）ではなく評価から決まる**（[03-決定ログ.md](../../../../docs/server-sync/03-決定ログ.md) の D-05。D-02 の該当部分を撤回）。「評価が上がると客が増え、台に常時流している生地の量が増える」という**繁盛具合の表現**であり、いま対応中の客の注文個数とは独立している
- 生地マス数 12 / 18 / 24 は `TakoyakiStandState.BatterCountLow / Mid / High` として定数で持つ。View 側で数値を直書きしない
- 「焼ける」のは**いま対応中の客のノルマのうち入力を終えた語数**（`TypedWordCount`）ぶんで、`occupiedCount` を超えない。提供（`OrderServed`）が成立して次の客に切り替わったら `TypedWordCount` が 0 に戻り、`Cooked` の穴は `Batter` へ戻る
- 穴が埋まる順序は `Slots` の index 昇順（左上から行優先）を既定とする。ランダム配置等にするかは演出詳細
- 12 / 18 / 24 という区切りは、`StandColumns`(6) の倍数にして**行単位で見た目が変わる**ようにしたもの。3段階が判別できることを優先した暫定値であり、演出確定時に見直す

## 4. Unity構成

- たこ焼き台Prefab（画面下部のグリッド）が `TakoyakiStandState.Slots` を購読し、各穴の見た目（何もない土台／生地色／焼き色）を切り替える
- グリッドのレイアウトは6列×4行の横長。列数・行数は `StandColumns` / `StandRows` を参照し、View側で数値を直書きしない
- ミス発生時の見た目劣化演出（`OrderProgressState.MissCount` に応じた質感変化）はこの値オブジェクトに含めない。必要なら `TakoyakiSlotState` に区分を追加するか、演出側だけで完結させる（未確定事項）

## 5. 未確定な演出との境界

- ここまで：`Empty`/`Batter`/`Cooked` の3区分、グリッド形状（6×4）、`EvalLevel`・`TypedWordCount` からの導出規則
- ここから先（未確定）：焼き加減のグラデーション演出、ミスによる質感劣化の見た目、穴が埋まる順序をindex順以外にするか

## 6. テスト観点

- `EvalLevel` を Low→Mid→High と変えたとき、生地マスが 12→18→24 と**行単位で**増えるか（3段階が目視で判別できるか）
- `EvalLevel` が Low / Mid のとき、生地を流していない穴が `Empty` のままか
- `TypedWordCount` の増加に伴い、該当穴が `Batter` → `Cooked` へ index 順に切り替わるか
- `Slots.Count` が常に `StandCapacity`(24) と一致するか（グリッド描画側で配列外参照が起きないか）
- 提供（客の繰り上がり）で `TypedWordCount` が 0 に戻ったとき、`Cooked` の穴が `Batter` へ戻り、`Empty` には戻らないか
- `TypedWordCount > occupiedCount` という不整合値が渡された場合に `cookedCount` がクランプされるか

## 7. 未確定事項

- ミスによる質感劣化を `TakoyakiSlotState` の追加区分にするか、演出側だけで完結させるか
- 生地マス数 12 / 18 / 24 の実値（演出確定時に見直す暫定値）
- `TypedWordCount` が生地マス数（12）を超えた場合の表示（現状はクランプして「全部焼けている」ように見える）。注文個数12は `BatterCountLow` と同数のため、評価が低いときは満杯になり得る
