# 09-`CullCountdownState`（本選 v0.8.0・★新規）

> 参照する上流：`pureC#` [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)（`CullWarning`）／[ranking-view/02](../ranking-view/02-cull-countdown-panel.md)。矛盾したら上流優先。

**予選の [07-patience-gauge-state.md](./07-patience-gauge-state.md) と同じ立ち位置**（受信値＋ローカル経過から表示値を作る純関数）。我慢ゲージが消え、代わりに足切りの秒読みがこの役割を担う。

## 1. 責務

**する**：`CullWarning` と現在時刻から、パネルが描く文字列と警告の強さを作る
**しない**：時刻を自分で取得しない（`nowLocalMs` を引数で受ける。テスト可能にするため）

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/ValueObjects/CullCountdownState.cs
namespace Takoda99.View.ValueObjects
{
    public readonly struct CullCountdownState : System.IEquatable<CullCountdownState>
    {
        /// <summary>表示する残り秒（切り上げ）。0 以上。</summary>
        public int RemainingSeconds { get; }

        /// <summary>"15" 等。RemainingSeconds の文字列（構築時に1回だけ ToString する）。</summary>
        public string RemainingText { get; }

        /// <summary>"3 / 6"。</summary>
        public string StageText { get; }

        /// <summary>"12位以下が脱落"。</summary>
        public string CutLineText { get; }

        /// <summary>自店が対象圏内か（サーバー値そのまま）。</summary>
        public bool SelfAtRisk { get; }

        /// <summary>警告の強さ 0..1。SelfAtRisk かつ残りが少ないほど 1 に近づく。</summary>
        public float AlertIntensity { get; }

        /// <summary>warning が null なら false。パネルの表示可否。</summary>
        public bool HasWarning { get; }

        public static CullCountdownState From(CullWarning? warning, long nowLocalMs);

        public bool Equals(CullCountdownState other);
    }
}
```

## 3. 変換規則

| # | 規則 |
|---|---|
| C1 | `warning == null` → `HasWarning = false`、他はすべて既定値。**パネルを非表示にする合図**（0秒と区別する） |
| C2 | `RemainingSeconds = ceil(warning.RemainingMsAt(nowLocalMs) / 1000)` |
| C3 | `RemainingMsAt` が既に 0 でクランプ済みなので、**負にならない** |
| C4 | 残り 1ms → `1`、残り 0ms → `0`（切り上げの境界） |
| C5 | `StageText = $"{StageIndex} / {StageTotal}"`。`StageIndex > StageTotal` ならそのまま出す（クランプしない。異常が見えるほうがよい） |
| C6 | `CutLineText`：`CutLineRank <= 0` なら空文字。それ以外は `$"{CutLineRank}位以下が脱落"` |
| C7 | `AlertIntensity`：`SelfAtRisk == false` なら 0。true なら `1 - clamp01(残りms / alertWindowMs)`（`alertWindowMs` は既定 10000） |

### 3.1 最終ステージについて

最終ステージでは `CutLineRank == 2` が届く（Proto コメント。処理上は1位も脱落するが、表示は「1位以外が脱落対象」とするのが企画意図）。

**`CullCountdownState` は特別扱いをしない。** C6 の規則をそのまま適用して「2位以下が脱落」と出す。

## 4. `Equals` を実装する理由

パネルは `Update()` で毎フレーム `From` を呼ぶ。**前回と等しければ `TMP.text` への代入を丸ごと省く**（[ranking-view/02 §3 C2](../ranking-view/02-cull-countdown-panel.md)）。秒が変わるのは1秒に1回なので、59/60 フレームで代入が消える。

`AlertIntensity` は毎フレーム変わり得るため、**`Equals` の比較対象に含めない**（文字列の更新可否だけを判定する。強度は別途毎フレーム適用してよい）。

## 5. 依存関係

- 依存する：`pureC#` `Takoda99.Client.State.CullWarning`
- 依存される：[ranking-view/02](../ranking-view/02-cull-countdown-panel.md)

## 6. テスト観点（`Unity/tests/Takoda99.View.Tests/`）

| # | 観点 |
|---|---|
| 1 | `null` → `HasWarning == false` |
| 2 | `UntilMs=20000` / 受信時刻 `t` → `From(w, t + 5000).RemainingSeconds == 15` |
| 3 | `From(w, t + 25000).RemainingSeconds == 0`（負にならない） |
| 4 | 残り 1ms で `1`、0ms で `0`（切り上げ境界） |
| 5 | `StageText == "3 / 6"` |
| 6 | `CutLineRank = 0` → `CutLineText == ""` |
| 7 | `CutLineRank = 2`（最終ステージ）→ `"2位以下が脱落"` |
| 8 | `SelfAtRisk == false` → `AlertIntensity == 0` |
| 9 | `SelfAtRisk == true` かつ残り 0ms → `AlertIntensity == 1` |
| 10 | 同一秒内の2つの `From` 結果が `Equals` で等しい（`AlertIntensity` が違っても） |

## 7. 未確定事項

- `alertWindowMs` の値（既定 10000。実機で詰める）
