# 14-中央カウントダウンの表示状態（残り5秒）

> 参照する上流：`pureC#` [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)（`CullWarning`）／[ranking-view/02](../ranking-view/02-cull-countdown-panel.md) §6。矛盾したら上流優先。
>
> このVOは [../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md) §6 のうち、**「出すか・どの数字か・その数字が出てからどれだけ経ったか」だけ**を、テストできる純関数として切り出したもの。**大きさ・不透明度・イージングは持たない**（View 側の Inspector 値）。

## 1. 責務

**する**

- 中央カウントダウンを出すかを決める（窓＝既定5秒に入っているか）
- 表示する数字（秒の切り上げ）を作る
- いま出している数字が出てからの進み具合（`SecondProgress` 0..1）を返す

**しない**

- **誰に出すかを決めない。** `CullAlertTier` を [13](./13-cull-alert-state.md) からそのまま受ける
  （順位と `CutLineRank` を比較しない。[docs/rules/01](../../../../docs/rules/01-責務と絶対原則.md)）
- 大きさ・不透明度・イージング・色を決めない（View の担当）
- 時刻を自分で取得しない（残り時間を引数で受ける。テスト可能にするため）

## 2. データ定義

```csharp
// Assets/Scripts/View/ValueObjects/CullFinalCountdownState.cs
public readonly struct CullFinalCountdownState : IEquatable<CullFinalCountdownState>
{
    public const int DefaultWindowMs = 5_000;

    public bool Visible { get; }
    public int Seconds { get; }
    public string Text { get; }

    /// <summary>いま出している数字が出てからの進み具合 0..1（1で次の数字へ変わる）。</summary>
    public float SecondProgress { get; }

    public static CullFinalCountdownState From(CullAlertTier tier, long remainingMs);
    public static CullFinalCountdownState From(CullAlertTier tier, long remainingMs, int windowMs);
}
```

## 3. 変換規則

| 入力 | 出力 |
|---|---|
| `tier == None`（安全圏・脱落後・未受信・アラートの窓の外） | `Hidden` |
| `remainingMs > windowMs` | `Hidden`（まだ出さない） |
| `remainingMs <= 0` | `Hidden`。**「0」を出したまま残さない**（淘汰の瞬間は [../elimination/01](../elimination/01-mass-elimination-effect.md) へ譲る） |
| それ以外 | `Visible`。`Seconds = ceil(remainingMs / 1000)` |

`Seconds` の切り上げは [09](./09-cull-countdown-state.md) と同じ規則（残り 4001ms → 5、4000ms → 4）。
パネルの数字と中央の数字が1フレームでもずれると目立つため、**規則を揃えることは要件**。

`SecondProgress` は `(1000 - remainingMs % 1000) % 1000 / 1000`。
残り 4800ms なら「5」が出て 200ms 経過＝ `0.2`。

> `CullAlertState` の窓は 10秒、こちらは 5秒。**アラート（ビネット）が先に出て、その後に数字が出る**という
> 二段構えを意図している。窓を広げるときは両方を見比べて決めること。

## 4. 等値判定

**`SecondProgress` を `Equals` に含めない。** 毎フレーム変わる値であり、含めると
「文字列を差し替えるべきか」の判定に使えなくなる（[09](./09-cull-countdown-state.md) の `AlertIntensity` と同じ扱い）。
View は等しいフレームで `TMP.text` への代入を省き、アニメーションだけを毎フレーム適用する。

## 5. 依存関係

- 依存する：[13-cull-alert-state.md](./13-cull-alert-state.md)（`CullAlertTier`）
- 依存される：[../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md) §6

`CullWarning` を直接は参照しない（残り時間だけを `long` で受ける）。

## 6. テスト観点

`Unity/tests/Takoda99.View.Tests/CullFinalCountdownStateTests.cs`。

| # | 観点 |
|---|---|
| 1 | `None` では出さない |
| 2 | 残り 5001ms では出さず、5000ms から出る |
| 3 | `Caution`（ぎりぎり圏外）にも出す |
| 4 | 残り 0ms 以下で消える（「0」が残らない） |
| 5 | 秒の切り上げ（4001→5・4000→4・1→1） |
| 6 | `SecondProgress` が数字ごとに 0 から始まる |
| 7 | 同じ秒なら `SecondProgress` が違っても等しい／秒が変われば等しくない |
| 8 | `windowMs <= 0` で出さない |
