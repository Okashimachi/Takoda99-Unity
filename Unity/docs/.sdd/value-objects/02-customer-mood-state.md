# 02-CustomerMoodState

> 参照する上流：[pureC#/docs/.sdd/value-objects/03-customer-state.md](../../../../pureC%23/docs/.sdd/value-objects/03-customer-state.md)（`CustomerState`）、[Unity/docs/.sdd/03-patience-timer.md](../03-patience-timer.md)（我慢ゲージの表示専用カウントダウン）。矛盾したら上流優先。

## 1. 責務

- 客の我慢ゲージ残量を「普通・いらだち・怒り」の3段階＋「退転（離脱確定後の演出状態）」に分類した表示用状態を提供する
- **しない**こと：我慢ゲージの減算処理そのもの（`PatienceTimer` の責務）。離脱の確定判定（サーバー権威、`CustomerLeft` 受信が真実）

## 2. データ定義

```csharp
public enum CustomerMood { Calm, Irritated, Angry, TurnedAway }

public readonly record struct CustomerMoodState(
    string CustomerId,
    CustomerMood Mood
);
```

## 3. 変換処理

入力：`CustomerState.PatienceMaxMs` と、`PatienceTimer` が管理する表示用の現在残量 `patienceLeftMsDisplay`（[03-patience-timer.md](../03-patience-timer.md) 参照。サーバー確定値 `PatienceLeftMs` をクライアント側で滑らかにカウントダウン表示するためのローカル値）

```
ratio = patienceLeftMsDisplay / PatienceMaxMs  // 0..1

Mood =
    ratio >= irritatedThreshold → Calm
    ratio >= angryThreshold     → Irritated
    ratio > 0                   → Angry
    ratio <= 0                  → TurnedAway（離脱確定演出のトリガー地点）
```

- `TurnedAway` は「表示上ゲージが尽きた瞬間」から「`CustomerLeft` を受信して実際に行列から除去されるまで」の間の演出状態として使う想定（用語集の「離脱はサーバー権威」との整合：見た目が先行しても、行列からの実除去は必ずサーバー確定を待つ）
- `CustomerLeft` を受信した時点で、この客の `CustomerMoodState` 自体を破棄する（`CustomerState` の破棄と同じタイミング）

## 4. Unity構成

- 行列の客Prefabが `CustomerMoodState.Mood` を購読し、表情/ポーズを切り替える
- `TurnedAway` になってから実際に `CustomerLeft` が届くまでのタイムラグがある場合、その間のPrefabの見た目（背中を見せたまま留まる等）は演出詳細として未確定

## 5. 未確定な演出との境界

- ここまで：`Mood` の4区分と、切り替わりの入力（`ratio`としきい値）
- ここから先（未確定）：各ムードの具体的な表情・アニメーション、`TurnedAway`中の待機時間上限（`CustomerLeft`がなかなか届かない場合のフォールバック挙動）

## 6. テスト観点

- `ratio`が各閾値の境界値のときの区分
- `patienceLeftMsDisplay`が0に到達した瞬間に`TurnedAway`へ切り替わるか
- `CustomerLeft`受信後に`CustomerMoodState`が確実に破棄され、除去後の客IDに対する参照が残らないか

## 7. 未確定事項

- `irritatedThreshold` / `angryThreshold` の具体値
- `TurnedAway`状態の最大継続時間、および`CustomerLeft`が届く前にタイムアウトした場合のフォールバック要否
