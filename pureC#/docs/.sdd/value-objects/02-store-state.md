# 02-StoreState / StoreSummaryState

> 参照する上流：[用語集 2章「店舗・プレイヤー」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 5章「評価システム」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 6章「信用システム」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / `MatchStart` / `EvaluationUpdate` / `CreditUpdate` / `StoreEliminated`（S2C）。矛盾したら上流優先。

## 1. 責務

- **自店舗を含む全店舗**の、サーバー権威データをそのまま保持する（`StoreState`＝自店舗の詳細、`StoreSummaryState`＝99店ミニ盤面用の軽量版）
- **しない**こと：評価の3段階（高中低）判定・脱落演出フラグ等の**表示用派生状態**を持たない（Unity側 `value-objects/01-store-visual-state.md` の責務）。星表示への変換もしない

## 2. データ定義

```csharp
public readonly record struct StoreState(
    string StoreId,
    int CreditLife,
    float EvalRaw,
    float EvalNormalized, // 0..1
    int Rank,
    bool Alive,
    IReadOnlyList<string> StoreQueue // CustomerId の並び。先頭が対応中
);

// 99店ミニ盤面用の軽量サブセット（自店以外の全店に対して保持）
public readonly record struct StoreSummaryState(
    string StoreId,
    float EvalNormalized,
    int Rank,
    int CreditLife,
    bool Alive
);
```

`Store`（`Dictionary<string, StoreSummaryState>` 相当）は `selfStoreId` を1つ保持し、自店舗については `StoreState` として完全な形を、それ以外は `StoreSummaryState` として保持する。

## 3. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `MatchStart` | `selfStoreId` を確定。全店舗ぶんの `StoreSummaryState` を初期値（`Alive = true`, `CreditLife = initialLife` 等）で生成 |
| `EvaluationUpdate` | 対象 `StoreId` の `EvalRaw` / `EvalNormalized` / `Rank` を置換。自店舗なら `StoreState` を、他店なら `StoreSummaryState` を更新 |
| `CreditUpdate` | 対象 `StoreId` の `CreditLife` を、イベントに含まれる確定値 `life` で**置換**する。`delta` / `reason` は演出のトリガー情報として読むだけで、**クライアント側で加減算して値を作らない**（用語集6章：`CreditUpdate` は `life`, `delta`, `reason` を持つ。信用の確定はサーバー権威） |
| `StoreEliminated` | 対象 `StoreId` の `Alive = false` |
| 客分配・提供結果に伴う行列変化（S2Cで行列情報が届く場合） | 自店舗 `StoreState.StoreQueue` を更新。他店の行列詳細はミニ盤面に不要なため `StoreSummaryState` には持たない |

## 4. 不変条件

- `0 <= EvalNormalized <= 1`
- `CreditLife >= 0`（0で `Alive = false` になっているはず。矛盾を検知したらログのみ、クライアント側で勝手に補正しない）
- `Alive == false` の店の `StoreQueue` は空であることを期待するが、サーバーから届いた値をそのまま信頼し、クライアント側で強制クリアしない

## 5. 依存関係

- 依存するモジュール：`Contract`（DTO型）
- 依存されるモジュール：`Store`/`Reducer`、Unity側 `value-objects/01-store-visual-state.md`

## 6. テスト観点

- `MatchStart` で全店舗が初期状態で生成されるか
- `EvaluationUpdate` の部分更新が対象店舗以外に影響しないか
- `StoreEliminated` 後も他フィールド（`EvalNormalized` 等）が保持され続けるか（消去されないか）
- 自店舗と他店舗で `StoreState` / `StoreSummaryState` のどちらに書き込まれるかの分岐が正しいか

## 7. 未確定事項

- 他店の行列詳細（何人並んでいるか等）をミニ盤面演出で使うかどうかは未定。使う場合は `StoreSummaryState` にフィールド追加が必要
