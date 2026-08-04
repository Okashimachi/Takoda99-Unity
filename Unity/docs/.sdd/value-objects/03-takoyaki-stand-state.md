# 03-TakoyakiStandState / TakoyakiSlotState

> 参照する上流：[pureC#/docs/.sdd/value-objects/04-order-progress-state.md](../../../../pureC%23/docs/.sdd/value-objects/04-order-progress-state.md)（`OrderProgressState`）、[pureC#/docs/.sdd/value-objects/03-customer-state.md](../../../../pureC%23/docs/.sdd/value-objects/03-customer-state.md)（`CustomerState.OrderCount`）。矛盾したら上流優先。

## 1. 責務

- たこ焼き台の各穴が「なにもない／生地（未クリア）／焼けた（クリア済み・提供待ち）」のどれかを表す表示用状態を提供する
- 台の物理的な穴数（同時に生地を流しておける数）と、注文個数は**別概念**であることを踏まえ、穴数と注文の対応付けを行う
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
}
```

- 台は **6列×4行＝24穴の横長グリッド**（設計決定）
- `Slots` は1次元の配列として持ち、行優先（左上原点）で2次元グリッドへ写像する。グリッド形状をViewに直接持たせず、この値オブジェクト側で形を定義することで、View差し替え時にも穴の対応がズレない

## 3. 変換処理

入力：`OrderProgressState`（`OrderCount`, `TypedWordCount`）

```
occupiedCount = min(OrderCount, StandCapacity)      // 生地を流しておく対象になる穴の数
cookedCount   = min(TypedWordCount, occupiedCount)  // タイプ完了済みぶん

for i in 0..StandCapacity-1:
    Slots[i] =
        i < cookedCount    → Cooked   // タイプ完了済み。提供待ち
        i < occupiedCount  → Batter   // 未クリアだが生地は流してある
        else               → Empty    // 対応する注文がない穴
```

- `occupiedCount`（生地を流してある穴の数）は「注文個数」そのものではなく「注文個数と台の穴数の小さい方」。注文個数によって生地を流す数が変わり、余った穴は `Empty` のまま残る
- 用語集4章の注文個数（4/6/8/12）はいずれも `StandCapacity`(24) 未満のため、**現行のパラメータでは `min` によるクランプは発動しない**。将来 `OrderCount` が24を超え得るようになった場合に備えた防御的な式として残す（その場合の「台に乗り切らない待機分」の表現は改めて決める）
- 穴が埋まる順序は `Slots` の index 昇順（左上から行優先）を既定とする。ランダム配置等にするかは演出詳細

## 4. Unity構成

- たこ焼き台Prefab（画面下部のグリッド）が `TakoyakiStandState.Slots` を購読し、各穴の見た目（何もない土台／生地色／焼き色）を切り替える
- グリッドのレイアウトは6列×4行の横長。列数・行数は `StandColumns` / `StandRows` を参照し、View側で数値を直書きしない
- ミス発生時の見た目劣化演出（`OrderProgressState.MissCount` に応じた質感変化）はこの値オブジェクトに含めない。必要なら `TakoyakiSlotState` に区分を追加するか、演出側だけで完結させる（未確定事項）

## 5. 未確定な演出との境界

- ここまで：`Empty`/`Batter`/`Cooked` の3区分、グリッド形状（6×4）、`OrderCount`・`TypedWordCount` からの導出規則
- ここから先（未確定）：焼き加減のグラデーション演出、ミスによる質感劣化の見た目、穴が埋まる順序をindex順以外にするか

## 6. テスト観点

- `OrderCount < StandCapacity`（通常ケース）で、余った穴が `Empty` になるか
- `TypedWordCount` の増加に伴い、該当穴が `Batter` → `Cooked` へ index 順に切り替わるか
- `Slots.Count` が常に `StandCapacity`(24) と一致するか（グリッド描画側で配列外参照が起きないか）
- 客の繰り上がり（新しい `OrderProgressState` 生成）で `Slots` が全て `Empty` にリセットされてから再構成されるか
- `TypedWordCount > OrderCount` という不整合値が渡された場合に `cookedCount` が `occupiedCount` でクランプされるか

## 7. 未確定事項

- ミスによる質感劣化を `TakoyakiSlotState` の追加区分にするか、演出側だけで完結させるか
