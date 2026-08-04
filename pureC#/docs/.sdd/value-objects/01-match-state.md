# 01-MatchState

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`（`MatchStart` / `PhaseChange` / `DifficultyUpdate` / `EvaluationUpdate` / `StoreListUpdate`）/ [用語集 1章「試合・進行」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 8章「フェーズ・火力」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 試合全体の進行状況（フェーズ・生存数・火力・制限時間）を1つの値として保持する
- **しない**こと：フェーズ移行の判定ロジック自体（生存数・経過時間の閾値判定）は持たない。判定は常にサーバー権威で、`MatchState` は配信された結果を保持するだけ

## 2. データ定義

```csharp
public enum Phase { Early, Mid, Late } // Proto の Takoda99.Proto.Phase と同一

public readonly record struct MatchState(
    string MatchId,
    Phase Phase,
    int AliveCount,
    int MaxStores,
    int MatchTimeLimitMs,
    int HeatLevel,
    long StartedAtLocalMs, // MatchStart を受信したクライアントローカル時刻
    long ElapsedMs         // クライアントのローカル計測値。サーバー由来ではない（§3・§5参照）
);
```

## 3. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `MatchStart` | `MatchId` / `Phase` を受信値で設定。`MaxStores` = `params.maxStores`、`MatchTimeLimitMs` = `params.matchTimeLimitMs`。`AliveCount` は `stores` のうち `alive == true` の件数を数えて設定する。`StartedAtLocalMs` に受信時刻を記録し `ElapsedMs = 0` |
| `PhaseChange` | `Phase` を受信値で置換 |
| `DifficultyUpdate` | `HeatLevel` を受信値で置換 |
| `EvaluationUpdate` | `aliveCount` を含むため `AliveCount` を置換（このメッセージは自店専用だが `aliveCount` は試合全体の値） |
| `StoreListUpdate` | `aliveCount` を含むため `AliveCount` を置換 |
| ローカルtick | `ElapsedMs = 現在時刻 - StartedAtLocalMs` |

### `HeatLevel` の初期値について

`GameParametersPublicSubset` は `matchTimeLimitMs` / `initialLife` / `maxStores` の**3項目のみ**であり、**火力の初期値は配信されない**。`MatchStart` にも `heatLevel` は含まれない。

したがって `HeatLevel` は **最初の `DifficultyUpdate` を受信するまで 0** とする。火力を表示や演出に使う場合は、未受信状態（0）を「不明」として扱えるようにしておくこと。

### `ElapsedMs` がサーバー由来でないことについて

**現契約に試合経過時間を運ぶメッセージは存在しない。** `MatchStart` にも `elapsedMs` は含まれない。そのため経過時間は `MatchStart` の受信時刻を起点としたクライアントのローカル計測でしかない。

- サーバー確定値による補正の経路が無いため、**ズレても検知・補正できない**
- タイマー表示は `MatchTimeLimitMs - ElapsedMs` で算出する
- この制約は [docs/server-sync SV-07](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-07) として調整中

## 4. 不変条件

- `0 <= AliveCount <= MaxStores`
- `HeatLevel >= 0`（0 は「`DifficultyUpdate` 未受信」を含む）
- `ElapsedMs >= 0`

## 5. 依存関係

- 依存するモジュール：`Contract`（Proto の DTO 型）
- 依存されるモジュール：`Store`/`Reducer`、Unity側 `value-objects` の各派生状態

## 6. テスト観点

- `MatchStart` 受信直後、`AliveCount` が `stores` の `alive == true` の件数と一致するか（`maxStores` をそのまま入れていないか）
- `MatchStart` の `phase` が `Early` 以外だった場合でも、その値がそのまま反映されるか（途中参加を想定しない場合でも決め打ちしない）
- `DifficultyUpdate` 未受信の間 `HeatLevel` が 0 のままか
- `EvaluationUpdate` と `StoreListUpdate` の双方から `AliveCount` が更新されること、および両者の値が食い違った場合に後着が勝つこと

## 7. 未確定事項

- `MatchId` の用途（[SV-12](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-12)）
- `AliveCount` について `EvaluationUpdate` と `StoreListUpdate` の値がズレた場合、どちらを優先するか（[SV-02](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-02)）
