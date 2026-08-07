# 07-PatienceGaugeState

> 参照する上流：[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_%E4%BC%81%E7%94%BB/00_%E7%94%A8%E8%AA%9E%E9%9B%86.md)（`Patience`）／`CustomerView.PatienceMaxMs`（Proto）／[05-patience-timer.md](../match-view/05-patience-timer.md)。矛盾したら上流優先。

我慢ゲージの**残量比**と、そこから決まる**色の段階（3段階固定）**を表す派生状態。バーの伸縮と着色に必要な量だけを持つ。

## 1. 責務

- 残り我慢時間 / 我慢時間の総量 から、0..1 の残量比を求める
- 残量比を **Safe / Caution / Danger の3段階**に分類する（段階数は固定。増減させない）
- バーの左端を置くべきアンカー位置（`1 - 残量比`）を与える。**右端は常に固定**という描画規則をここに閉じ込める
- **しない**こと：
  - 色そのものを持つ（色は演出であり ScriptableObject `PatienceGaugePalette` の担当。[README.md](./README.md) §4-2）
  - 我慢切れの判定・客の離脱決定（サーバー権威。[05-patience-timer.md](../match-view/05-patience-timer.md) §1）

### `CustomerMoodState` との違い

[02-customer-mood-state.md](./02-customer-mood-state.md) の `CustomerMoodState` と入力は似ているが、**別の表示区分**であり統合しない。

| | `CustomerMoodState` | `PatienceGaugeState` |
|---|---|---|
| 対象 | 行列の客の表情・ポーズ | 我慢ゲージのバー本体 |
| 区分 | 4区分（普通/いらだち/怒り/退転） | 3段階（Safe/Caution/Danger） |
| 時刻の起点 | `MatchState.ElapsedMs`（サーバー由来の経過） | `PatienceTimer` のローカル単調時計 |
| 閾値の既定 | 2/3・1/3 | 1/2・1/4 |

## 2. データ定義

```csharp
namespace Takoda99.View.ValueObjects
{
    /// <summary>我慢ゲージの色段階。3段階で固定する。</summary>
    public enum PatienceGaugeStage { Safe = 0, Caution = 1, Danger = 2 }

    public readonly struct PatienceGaugeState
    {
        /// <summary>残量比 0..1（クランプ済み）。</summary>
        public double RemainingRatio { get; }

        public PatienceGaugeStage Stage { get; }

        /// <summary>バー左端のアンカー位置 = 1 - RemainingRatio。右端は固定なのでここだけ動かす。</summary>
        public double LeftEdgeAnchorX { get; }

        public static PatienceGaugeState From(long remainingMs, long totalMs, PatienceGaugeThresholds thresholds);

        public static PatienceGaugeStage StageOf(double remainingRatio, PatienceGaugeThresholds thresholds);
    }

    /// <summary>段階の境界（残量比 0..1）。</summary>
    public readonly struct PatienceGaugeThresholds
    {
        public double Caution { get; }
        public double Danger { get; }

        /// <summary>残 50% 以上で Safe、25% 以上で Caution、それ未満で Danger。</summary>
        public static PatienceGaugeThresholds Default { get; }
    }
}
```

## 3. 変換処理

```
totalMs <= 0            → RemainingRatio = 0, Stage = Danger（0除算を避ける）
RemainingRatio          = clamp(remainingMs, 0, totalMs) / totalMs
LeftEdgeAnchorX         = 1 - RemainingRatio

Stage:
  RemainingRatio >= thresholds.Caution → Safe
  RemainingRatio >= thresholds.Danger  → Caution
  それ以外                              → Danger
```

**境界値はいずれも上位側（余裕がある側）の段階に入る。** 残量比ちょうど 0.5 は Safe、ちょうど 0.25 は Caution。`CustomerMoodState` と同じ規則に揃える。

`thresholds` は呼び出し側から渡す。実運用では `PatienceGaugePalette` が保持する値（既定 0.5 / 0.25）が渡る。

## 4. Unity構成

この値を消費するのは `PatienceTimer` のみ（[05-patience-timer.md](../match-view/05-patience-timer.md) §4.2）。本ディレクトリのファイルは `Unity/tests/Takoda99.View.Tests` にリンク参照されるため、**`UnityEngine` へ依存させない**（`Color` を持たせない理由でもある）。

## 5. 未確定な演出との境界

- 3段階それぞれの**実際の色**・グラデーション・点滅は `PatienceGaugePalette`（ScriptableObject）側の演出。ここは段階の成立条件だけを持つ
- 段階が切り替わる瞬間のトランジション（色を補間するか、パキッと切り替えるか）は未確定。初版は補間なしの即時切り替え

## 6. テスト観点

`Unity/tests/Takoda99.View.Tests/PatienceGaugeStateTests.cs`。

- 残量比ちょうど 0.5 が Safe、0.25 が Caution になるか（境界値は上位側）
- `remainingMs` が負・`totalMs` 超過でも残量比が 0..1 にクランプされるか
- `totalMs <= 0` で 0除算せず Danger になるか
- `LeftEdgeAnchorX + RemainingRatio == 1`（右端固定の規則が崩れていないか）

## 7. 未確定事項

- 閾値 0.5 / 0.25 はゲーム側の正典から降りてきた値ではなく、見た目の分かりやすさで置いた値。企画側で我慢ゲージの警告タイミングが定義されたら差し替える
- ゲージが0に張り付いた後（`CustomerLeft` 待ち）の見た目を Danger のままにするか、専用の段階を足すか（[05-patience-timer.md](../match-view/05-patience-timer.md) §7 と同じ論点）
