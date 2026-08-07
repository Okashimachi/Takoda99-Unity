# 03-PatienceTimer

> 参照する上流：[02-Unity実装ルール.md](../../../../docs/rules/02-Unity実装ルール.md) §1（`PatienceTimer` はUnity側で実装すると合意済み）／`CustomerView.PatienceMaxMs` / `PatienceStartedAtServerMs`（Proto）。矛盾したら上流優先。
>
> 段階分類の骨格は [07-patience-gauge-state.md](../value-objects/07-patience-gauge-state.md)（`PatienceGaugeState`）が正典。本仕様書はそれを Unity からどう描くかを扱う。

我慢ゲージの**表示専用**カウントダウン。我慢切れの判定（客の離脱）はサーバー権威で、`CustomerLeft`（`LeaveReason`）で通知される。本モジュールは見た目のカウントダウンのみを持つ。

## 1. 責務

- 対応中の客1名ぶんの、残り我慢時間の見た目（ゲージ・残秒数）を表示する
- ゲージバーの**右端を固定したまま左端を右へ寄せて**残量を表す
- 残量に応じてバーの色を3段階で切り替える（色の実値は `PatienceGaugePalette` から引くだけで、自前では持たない）
- **しない**こと：
  - 我慢切れの判定・客の離脱決定（サーバー権威。`CustomerLeftAction` / `LeaveReason` で通知が来るのを待つだけ）
  - ゼロになった瞬間に自発的に客を離脱させる処理を呼ばない（表示が0になっても、実際の離脱は `CustomerLeft` 受信まで起きない可能性がある。表示と実処理がズレても表示側は追従するだけ）

## 2. 公開インターフェース

```csharp
namespace Takoda99.Timer
{
    /// <summary>我慢ゲージの表示専用カウントダウン（05-patience-timer.md）。</summary>
    public sealed class PatienceTimer : MonoBehaviour
    {
        [SerializeField] private Image gauge;                         // バー本体。RectTransform の左端を動かし、color を段階色で塗る
        [SerializeField] private PatienceGaugePalette palette;        // 3段階の色と閾値
        [SerializeField] private TextMeshProUGUI remainingSecondsText; // 任意。null なら数値表示なし

        /// <summary>対応開始。arrivedAtLocalMs は IClock.MonotonicMs 基準（CustomerEntry.ArrivedAtLocalMs と同じ時刻系）。</summary>
        public void Begin(long arrivedAtLocalMs, int patienceMaxMs);

        /// <summary>対応終了・客の離脱時に呼ぶ。ゲージを空にする。</summary>
        public void Stop();
    }
}
```

### 2.1 `PatienceGaugePalette`（ScriptableObject）

3段階の色をアセットとして差し替え可能にする。**段階の数は3で固定**し、アセット側で増減させない（配列ではなく named field で持つ理由）。

```csharp
namespace Takoda99.Timer
{
    [CreateAssetMenu(fileName = "PatienceGaugePalette", menuName = "Takoda99/Patience Gauge Palette")]
    public sealed class PatienceGaugePalette : ScriptableObject
    {
        [SerializeField] private Color _safe;     // 既定：緑
        [SerializeField] private Color _caution;  // 既定：オレンジ
        [SerializeField] private Color _danger;   // 既定：赤

        [SerializeField, Range(0f, 1f)] private float _cautionThreshold; // 既定 0.5
        [SerializeField, Range(0f, 1f)] private float _dangerThreshold;  // 既定 0.25

        public PatienceGaugeThresholds Thresholds { get; }
        public Color Resolve(PatienceGaugeStage stage);
    }
}
```

- 実体は `Assets/Resources/PatienceGaugePalette.asset`（`CustomerSpriteLibrary` と同じ置き場）
- `OnValidate` で `_dangerThreshold <= _cautionThreshold` を保つ。逆転すると段階が潰れるため

## 3. Unity構成

- **シーン**：`MainStoreCanvas/PatienceGageCanvas/PatientGage/` に配置する。`Gage` がバー本体で、`BG` / `TopFrame` / `BottomFrame` は背景と枠（本モジュールは触らない）
- **`Gage`（`gauge` に割り当てる `Image`）の RectTransform 前提**：右端固定・左端可変を成立させるため、以下を満たすこと。崩れると意図した伸縮にならない
  - `anchorMax.x == 1`（右端は最大アンカーに固定）
  - 左右のオフセットが 0（stretch 配置で `anchoredPosition.x == 0` かつ `sizeDelta.x == 0`）
  - 本モジュールが書き換えるのは `anchorMin.x` と `Image.color` のみ。`Image.type` は問わない（`fillAmount` は使わない）
- **MonoBehaviour のライフサイクル**
  - `Update`：カウントダウン中のみ、`Time.realtimeSinceStartupAsDouble` から残り時間を再計算しゲージへ反映する
  - `Awake`：参照の null チェック（`gauge` / `palette` は必須、`remainingSecondsText` は任意）と、上記 RectTransform 前提の検証
- **Inspector 公開値**：`gauge` / `palette` / `remainingSecondsText`

## 4. ふるまいの詳細

### 4.1 時刻の基準

- `arrivedAtLocalMs` は pureC# 側 `CustomerEntry.ArrivedAtLocalMs`（`Dispatcher` が `CustomerArrived` 受信時に `IClock.MonotonicMs` で記録した値）をそのまま渡す
- 締切 = `arrivedAtLocalMs + patienceMaxMs`。以後 `Update` のたびに `締切 - now` を残り時間とする。`now` は Unity 側 `IClock` 実装（[Bootstrap](../foundation/02-scene-composition.md)）と同じ `Time.realtimeSinceStartupAsDouble` 基準を使い、時刻系を揃える
- **サーバー基準時刻 `PatienceStartedAtServerMs` はそのままでは使わない。** クライアント/サーバー間の時刻同期（NTP的な補正）を持たないため、クライアント受信時刻を起点にする。ズレの許容は未確定事項に記す

### 4.2 表示

残量比と段階の算出は `PatienceGaugeState.From(remainingMs, patienceMaxMs, palette.Thresholds)` に委譲する（[07-patience-gauge-state.md](../value-objects/07-patience-gauge-state.md) §3）。本モジュールはその結果を Unity のプロパティへ写すだけ。

- **伸縮**：`gauge.rectTransform.anchorMin.x = state.LeftEdgeAnchorX`（= `1 - 残量比`）。`anchorMax.x` は触らないので**右端は固定**され、残量が減るほど左端が右端へ寄る
- **着色**：`gauge.color = palette.Resolve(state.Stage)`。3段階の即時切り替えで、段階間の補間はしない
- `remainingSecondsText` があれば `Ceiling(remainingMs / 1000)` を表示する
- 残り時間が0以下になったら `Update` での再計算を止める（それ以上ゲージは変化しない。幅0・Danger色のまま張り付く）。**客の離脱そのものはこの0到達をトリガーにしない**（§1）

> `Image.fillAmount` を使わない理由：`Gage` は Sliced な `Image` で、`fillAmount` を効かせるには `Image.type` を `Filled` に変える必要があり、アセット側の設定に暗黙に依存する。同じ「右端固定・左端可変」を扱う `RankBarView` も `anchorMin.x` 方式で揃っている。

### 4.3 `Stop`

- `Begin` を呼んでいない状態で呼んでも安全（ゲージを空にするだけ）
- 対応中の客が入れ替わる場合は、呼び出し側が `Stop()` → `Begin(new)` の順に呼ぶ（本モジュールは客の同一性を追跡しない）

## 5. 依存関係

- 依存する `pureC#` モジュール：なし（`long` / `int` の素の値で受け取る）
- 依存するUnity側モジュール：`PatienceGaugeState`（[07-patience-gauge-state.md](../value-objects/07-patience-gauge-state.md)）／`PatienceGaugePalette`（§2.1）
- 依存されるモジュール：`Renderer`（`OnCustomerArrived` / `OnCustomerLeft` を受けて `Begin`/`Stop` を呼ぶ想定。[01-renderer.md](./01-renderer.md)）

## 6. テスト・確認観点

段階分類は `UnityEngine` 非依存なので xUnit で検証する（[07-patience-gauge-state.md](../value-objects/07-patience-gauge-state.md) §6）。バーの見た目そのものは `UnityEngine.Time` / RectTransform 依存のため Unity Editor 実行で確認する。

- `Begin` 直後は左端が `Gage` の左いっぱい（`anchorMin.x = 0`）で、時間経過とともに右端へ単調に寄るか
- **右端が動かないか**（`anchorMax.x` が 1 のまま。バーが左右両方から縮んでいないか）
- 残量 50% / 25% を跨いだ瞬間に 緑 → オレンジ → 赤 と切り替わるか
- `patienceMaxMs` 経過後は幅0に張り付き、左端が右端を追い越さないか
- `Stop` を呼ぶと即座にゲージが空になるか
- `palette` を別アセットに差し替えると色だけが変わるか（段階の数は変わらない）

## 7. 未確定事項

- クライアント/サーバー間の時刻同期を導入するか（現状はクライアント受信時刻起点で、ネットワーク遅延ぶんだけ実際の締切より長く見える可能性がある）
- ゲージが0になった後も客が離脱しない（サーバー側の余裕時間がある）場合の見た目（点滅等）。色は Danger のまま張り付く
- 段階が切り替わる瞬間に色を補間するか（初版は即時切り替え）
