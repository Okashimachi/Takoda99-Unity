# 02-StoreState / StoreSummaryState

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`（`MatchStart` / `EvaluationUpdate` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated` / `StoreSummary`）/ [用語集 2章](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)・[5章](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)・[6章](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 自店舗の詳細（`StoreState`）と、99店ミニ盤面用の全店サマリー（`StoreSummaryState`）を保持する
- **しない**こと：評価の3段階（高中低）判定・脱落演出フラグ等の**表示用派生状態**を持たない（Unity側 `value-objects/01-store-visual-state.md` の責務）
- **しない**こと：星評価の**算出**。`StarRating` / `StarDelta` はサーバーが配信する値をそのまま保持するだけで、`Rank` や `EvalNormalized` から導出しない（Proto v0.3.0・[SV-21](../../../../docs/server-sync/02-パラメータと閾値.md#sv-21)）

## 2. 前提：メッセージのスコープ（重要）

Proto のメッセージは `storeId` の有無で対象が分かれる。**この区別が値オブジェクトの更新経路を決めるため、最初に明示する。**

| スコープ | メッセージ | 更新先 |
|---|---|---|
| **自店専用**（`storeId` を持たない） | `EvaluationUpdate` / `CreditUpdate` | `StoreState` のみ |
| 全店対象（`storeId` を持つ） | `StoreEliminated` | 該当する `StoreSummaryState`（自店なら `StoreState` も） |
| 全店ぶんを一括 | `MatchStart.stores` / `StoreListUpdate.stores` | 全 `StoreSummaryState` |

**他店の評価・信用を個別に通知するメッセージは存在しない。** 他店の情報は `StoreListUpdate` のフルスナップからのみ得られる（[SV-01](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-01)）。

## 3. データ定義

```csharp
// 自店舗の詳細
public readonly record struct StoreState(
    string StoreId,
    string DisplayName,
    int CreditLife,
    double EvalRaw,
    double EvalNormalized, // 0..1。Proto の EvaluationUpdate では `normalized`、StoreSummary では `evalNormalized`
    int Rank,
    double StarRating,     // 0..5。表示専用（Proto v0.3.0）。分配重み・淘汰には使わない
    double StarDelta,      // 前ティックからの星の増減（Proto v0.3.0）
    bool Alive,
    IReadOnlyList<string> StoreQueue // CustomerId の並び。先頭が対応中。クライアントがローカル構築する（§4）
);

// 99店ミニ盤面用。Proto の StoreSummary に対応
public readonly record struct StoreSummaryState(
    string StoreId,
    string DisplayName,
    double EvalNormalized,
    int Rank,
    int CreditLife,
    bool Alive,
    int? FinalRank // 脱落済みの店のみ。null は「まだ脱落していない」（Proto v0.3.0）
);
```

### `StarRating` / `StarDelta`（Proto v0.3.0）

`EvaluationUpdate` が運ぶ**表示専用**の値。定義は `starRating = 5 * (maxStores - rank) / (maxStores - 1)` で、**母集団は生存店ではなく99店全体**（脱落店は下位に積む）。

- **クライアントで算出しない。** 算出させるとプレイヤーごとに違う星が見える（[SV-21](../../../../docs/server-sync/02-パラメータと閾値.md#sv-21)）
- `EvalNormalized` とは別物。**分配重み・下位淘汰はサーバーが `EvalNormalized` を使う**ため、`StarRating` にゲームロジック上の意味を持たせない
- `StarDelta` は「★+0.2」演出のトリガー。クライアントで差分を取らない（[D-03](../../../../docs/server-sync/03-決定ログ.md#d-03--評価の増減表示はサーバーイベント方式としクライアントで差分計算しない) / [D-08](../../../../docs/server-sync/03-決定ログ.md#d-08--proto-v030-への追従)）

### `FinalRank` が `int?` である理由（Proto v0.3.0）

**欠落を 0 として扱ってはいけない。** 順位0は存在しないため、Proto は3言語とも「キーごと出さない」実装で揃えてある（C# は `int?` + `WhenWritingNull`）。`null` は「まだ脱落していない」を意味する。`int` に潰して既定値0を入れると「0位の店」が生まれ、ミニ盤面の表示が壊れる（[SV-15](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-15)）。

`Store` は `selfStoreId` を1つ保持し、自店舗は `StoreState`、全店（自店を含む）は `StoreSummaryState` のコレクションとして持つ。

## 4. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `MatchStart` | `selfStoreId` を確定。`stores`（`List<StoreSummary>`）から全店の `StoreSummaryState` を生成。自店の `StoreState` は、対応する `StoreSummary` の値と `params.initialLife` から初期化し、`StoreQueue` は空 |
| `EvaluationUpdate` | **自店のみ。** `StoreState` の `EvalRaw` / `EvalNormalized`(`normalized`) / `Rank` / `StarRating` / `StarDelta` を置換する。`storeId` を持たないため、他店の更新には使わない |
| `CreditUpdate` | **自店のみ。** `StoreState.CreditLife` を確定値 `life` で**置換**する。`delta` / `reason` は演出のトリガー情報として読むだけで、クライアント側で加減算して値を作らない |
| `StoreListUpdate` | `stores` で全 `StoreSummaryState` を**一括置換**する（フルスナップ）。`finalRank` も受信値のまま保持し、**欠落を 0 にしない**。自店ぶんも含まれるが、自店の `StoreState` はより新しい `EvaluationUpdate` / `CreditUpdate` を持ち得るため、**`StoreState` を上書きしない** |
| `StoreEliminated` | `storeId` に対応する `StoreSummaryState.Alive = false` と `FinalRank = finalRank`。自店なら `StoreState.Alive = false` も設定。次の `StoreListUpdate` でも同じ `finalRank` が届くため、両経路で値は一致する |
| `CustomerArrived` | 自店の `StoreQueue` 末尾に `customerId` を追加（§5） |
| `CustomerLeft` / 提供完了 | 自店の `StoreQueue` から該当 `customerId` を除去（§5） |

## 5. 行列（`StoreQueue`）がローカル構築であることについて

**行列の内容・長さを配信するメッセージは存在しない。** `CustomerArrived` の到着順に積み、`CustomerLeft` と提供完了で取り除く、というクライアント側の積算でしか行列を持てない。

- サーバー側の行列とズレても検知・復旧する手段が無い
- 他店の行列長は `StoreSummary` に含まれないため、ミニ盤面には表示できない
- この制約は [SV-04](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-04) として調整中

## 6. 不変条件

- `0 <= EvalNormalized <= 1`
- `CreditLife >= 0`
- `Alive == false` の店に対して、クライアント側で `StoreQueue` を強制クリアしない（サーバーから届いた事実のみを反映する）

## 7. 依存関係

- 依存するモジュール：`Contract`（Proto の DTO 型）
- 依存されるモジュール：`Store`/`Reducer`、Unity側 `value-objects/01-store-visual-state.md`・`04-credit-life-lantern-state.md`・`05-rank-bar-and-eval-delta-view-state.md`

## 8. テスト観点

- `EvaluationUpdate` 受信時、**自店の `StoreState` のみ**が更新され、`StoreSummaryState` のコレクションが書き換わらないこと
- `StoreListUpdate` 受信時に全 `StoreSummaryState` が置換されること、および**自店の `StoreState` が巻き戻らないこと**
- `CreditUpdate` の `life` が確定値として使われ、`delta` の加減算で値を作っていないこと
- `StoreEliminated` が自店・他店のどちらでも正しい対象に適用されること
- `MatchStart.stores` から生成した `StoreSummaryState` の件数が `params.maxStores` と一致しない場合（欠員あり）でも破綻しないこと
- `finalRank` を持たない（`null`）生存店を取り込んだとき、`FinalRank` が `null` のままで **0 にならない**こと
- `StarRating` / `StarDelta` が受信値のまま保持され、`Rank` や `EvalNormalized` から再計算されていないこと

## 9. 未確定事項

- `StoreListUpdate` の配信頻度（[SV-02](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-02)）。他店表示の更新粒度がこれに完全に依存する
- `DisplayName` の決定主体と制約（[SV-11](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-11)）
- `EvaluationUpdate.normalized` と `StoreSummary.evalNormalized` の命名不統一（[SV-09](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-09)）。値オブジェクト側は `EvalNormalized` に統一している
