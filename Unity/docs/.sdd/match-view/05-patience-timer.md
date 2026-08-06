# 03-PatienceTimer

> 参照する上流：[02-Unity実装ルール.md](../../../../docs/rules/02-Unity実装ルール.md) §1（`PatienceTimer` はUnity側で実装すると合意済み）／`CustomerView.PatienceMaxMs` / `PatienceStartedAtServerMs`（Proto）。矛盾したら上流優先。

我慢ゲージの**表示専用**カウントダウン。我慢切れの判定（客の離脱）はサーバー権威で、`CustomerLeft`（`LeaveReason`）で通知される。本モジュールは見た目のカウントダウンのみを持つ。

## 1. 責務

- 対応中の客1名ぶんの、残り我慢時間の見た目（ゲージ・残秒数）を表示する
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
        [SerializeField] private Image gauge;                       // fillAmount 0..1
        [SerializeField] private TextMeshProUGUI remainingSecondsText; // 任意。null なら数値表示なし

        /// <summary>対応開始。arrivedAtLocalMs は IClock.MonotonicMs 基準（CustomerEntry.ArrivedAtLocalMs と同じ時刻系）。</summary>
        public void Begin(long arrivedAtLocalMs, int patienceMaxMs);

        /// <summary>対応終了・客の離脱時に呼ぶ。ゲージを空にする。</summary>
        public void Stop();
    }
}
```

## 3. Unity構成

- **シーン**：主画面の客ごとのUIエレメント（行列表示）に1つずつアタッチする想定。行列UI自体の構成は未確定（[02-scene-composition.md](../foundation/02-scene-composition.md) 参照）
- **MonoBehaviour のライフサイクル**
  - `Update`：カウントダウン中のみ、`Time.realtimeSinceStartupAsDouble` から残り時間を再計算しゲージへ反映する
  - `Awake`：参照の null チェック（`gauge` は必須、`remainingSecondsText` は任意）
- **Inspector 公開値**：`gauge` / `remainingSecondsText`

## 4. ふるまいの詳細

### 4.1 時刻の基準

- `arrivedAtLocalMs` は pureC# 側 `CustomerEntry.ArrivedAtLocalMs`（`Dispatcher` が `CustomerArrived` 受信時に `IClock.MonotonicMs` で記録した値）をそのまま渡す
- 締切 = `arrivedAtLocalMs + patienceMaxMs`。以後 `Update` のたびに `締切 - now` を残り時間とする。`now` は Unity 側 `IClock` 実装（[Bootstrap](../foundation/02-scene-composition.md)）と同じ `Time.realtimeSinceStartupAsDouble` 基準を使い、時刻系を揃える
- **サーバー基準時刻 `PatienceStartedAtServerMs` はそのままでは使わない。** クライアント/サーバー間の時刻同期（NTP的な補正）を持たないため、クライアント受信時刻を起点にする。ズレの許容は未確定事項に記す

### 4.2 表示

- `gauge.fillAmount = clamp(remainingMs, 0, patienceMaxMs) / patienceMaxMs`
- `remainingSecondsText` があれば `Ceiling(remainingMs / 1000)` を表示する
- 残り時間が0以下になったら `Update` での再計算を止める（それ以上ゲージは変化しない。空のまま張り付く）。**客の離脱そのものはこの0到達をトリガーにしない**（§1）

### 4.3 `Stop`

- `Begin` を呼んでいない状態で呼んでも安全（ゲージを空にするだけ）
- 対応中の客が入れ替わる場合は、呼び出し側が `Stop()` → `Begin(new)` の順に呼ぶ（本モジュールは客の同一性を追跡しない）

## 5. 依存関係

- 依存する `pureC#` モジュール：なし（`long` / `int` の素の値で受け取る）
- 依存するUnity側モジュール：なし
- 依存されるモジュール：`Renderer`（`OnCustomerArrived` / `OnCustomerLeft` を受けて `Begin`/`Stop` を呼ぶ想定。[01-renderer.md](./01-renderer.md)）

## 6. テスト・確認観点

`UnityEngine.Time` 依存のため xUnit では検証できない。Unity Editor 実行で確認する。

- `Begin` 直後は `fillAmount = 1`、時間経過とともに単調減少するか
- `patienceMaxMs` 経過後は `fillAmount = 0` に張り付き、負値にならないか
- `Stop` を呼ぶと即座にゲージが空になるか

## 7. 未確定事項

- クライアント/サーバー間の時刻同期を導入するか（現状はクライアント受信時刻起点で、ネットワーク遅延ぶんだけ実際の締切より長く見える可能性がある）
- ゲージが0になった後も客が離脱しない（サーバー側の余裕時間がある）場合の見た目（点滅・警告色等）
