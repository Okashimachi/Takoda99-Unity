# 01-MatchState

> 参照する上流：[用語集 1章「試合・進行」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 8章「フェーズ・火力」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / `MatchStart` / `PhaseChange` / `DifficultyUpdate`（S2C）。矛盾したら上流優先。

## 1. 責務

- 試合全体の進行状況（経過時間・フェーズ・生存数・火力）を1つの値として保持する
- **しない**こと：フェーズ移行の判定ロジック自体（生存数・経過時間の閾値判定）は持たない。判定は常にサーバー権威で、`MatchState` は配信された結果を保持するだけ

## 2. データ定義

```csharp
public enum Phase { Early, Mid, Late }

public readonly record struct MatchState(
    long ElapsedMs,
    Phase Phase,
    int AliveCount,
    int MaxStores,
    int HeatLevel
);
```

## 3. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `MatchStart` | `AliveCount = MaxStores`、`Phase = Early`、`ElapsedMs = 0`、`HeatLevel` は`params`内の初期値で初期化 |
| `PhaseChange` | `Phase` を受信値で置換 |
| `DifficultyUpdate` | `HeatLevel` を受信値で置換 |
| `EvaluationUpdate` | 同梱される生存数で `AliveCount` を置換 |
| `StoreEliminated` | `AliveCount` は**ここで減算しない**（`EvaluationUpdate` の確定値のみを信頼する。クライアント側で減算すると `EvaluationUpdate` との二重減算になり得る）。脱落の事実は `StoreState.Alive` 側へ反映し、生存数表示のズレは次の `EvaluationUpdate` で解消される |
| ローカルtick（描画フレーム） | `ElapsedMs` はサーバー確定値の補間目的でクライアント側カウントアップしてよいが、次の `PhaseChange`/`EvaluationUpdate` 受信時にサーバー値で必ず上書きする（クライアント側加算は表示のなめらかさのためだけで、権威にはしない） |

## 4. 不変条件

- `0 <= AliveCount <= MaxStores`
- `HeatLevel >= 0`

## 5. 依存関係

- 依存するモジュール：`Contract`（DTO型）
- 依存されるモジュール：`Store`/`Reducer`、Unity側 `value-objects` の各派生状態（フェーズ・火力を参照する演出がある場合）

## 6. テスト観点

- `MatchStart` 受信直後の初期値が用語集の定義通りか
- `PhaseChange` / `DifficultyUpdate` を連続受信した際に最新値で上書きされるか（古い値が残らないか）
- `AliveCount` がサーバー確定値受信で必ず補正されるか（クライアント側カウントとズレていた場合）

## 7. 未確定事項

- `ElapsedMs` をクライアント側で補間加算するかどうか（サーバーが十分な頻度で送るなら不要）は実装時に決定
