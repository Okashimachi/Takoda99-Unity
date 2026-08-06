# 02-CustomerMoodState

> 参照する上流：[03-customer-state.md](../../../../pureC%23/docs/.sdd/value-objects/03-customer-state.md)（`CustomerState`）、[05-patience-timer.md](../match-view/05-patience-timer.md)（我慢ゲージの表示専用カウントダウン）。矛盾したら上流優先。

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

入力：`CustomerState.PatienceMaxMs` と、`PatienceTimer` が算出する表示用の残量推定 `patienceLeftMsDisplay`（[05-patience-timer.md](../match-view/05-patience-timer.md) 参照）

```
patienceLeftMsDisplay = PatienceMaxMs - (nowServerMsEstimated - CustomerState.PatienceStartedAtServerMs)
```

**この値はサーバーから配信されない。** 我慢ゲージの**残量**を運ぶメッセージは契約に存在せず（[SV-03](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-03)）、上式はクライアントのローカル推定にすぎない。終盤短縮が適用された場合はサーバー実態とズレるため、**この値で離脱を確定させてはならない**（離脱の確定は `CustomerLeft` 受信のみ）。

> **起点は Proto v0.3.0 で `CustomerView.patienceStartedAtServerMs`（サーバー基準の単調時刻）に変わった。** 受信時刻を起点にしていた頃と違い、**受信遅延ぶんの初期ズレが出ない**。`nowServerMsEstimated` はクライアントが推定するサーバー時刻で、サーバー時刻の同期手段は契約に無いため当面はオフセット推定で代用する（[SV-03](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-03) の未回答項目4）。

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
