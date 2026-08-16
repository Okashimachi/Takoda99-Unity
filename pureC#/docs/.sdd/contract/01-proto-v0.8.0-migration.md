# 01-Proto v0.8.0（本選）への移行

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs` **v0.8.0**（契約の正典）／[Takoda99-Docs 00_本選差分/10_差分_プロト.md](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/10_差分_プロト.md)／既存 [01-contract.md](../01-contract.md)。矛盾したら上流優先。

**このリポジトリの vendor ミラーは v0.5.0 で止まっている。** 本選の実装は、まずこれを v0.8.0 へ差し替えるところから始まる。他のすべての本選仕様書はこのファイルの完了を前提にしている。

## 1. 責務

**する**

- `pureC#/vendor/Takoda99.Proto/Messages.cs` を Proto v0.8.0 の内容へ差し替える
- `vendor/Takoda99.Proto/VERSION.md` を v0.8.0 の記録に更新する
- 増えたメッセージ／消えたメッセージを、`Dispatcher` が扱う `MessageType` の一覧に反映する（振り分けの中身は [../result/02-lifecycle-and-renderer.md](../result/02-lifecycle-and-renderer.md) と各 `match-state/` の仕様書）
- **Obsolete フィールドを読まない**というルールをコード上で徹底する

**しない**

- `Messages.cs` の**中身を編集しない**。手ミラーは1文字も変えず丸ごとコピーする（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)：契約はこのリポジトリで変更しない）
- `EnvelopeCodec` / `RequiredFieldValidator` / `EnumFallbackSanitizer` の設計を変えない（[01-contract.md](../01-contract.md) が引き続き正典）

## 2. 作業手順

| # | 作業 |
|---|---|
| 1 | `../Takoda99-Proto/csharp/Takoda99.Proto/Messages.cs`（tag `v0.8.0`）を `pureC#/vendor/Takoda99.Proto/Messages.cs` へ**上書きコピー** |
| 2 | `pureC#/vendor/Takoda99.Proto/VERSION.md` の「固定バージョン」を `v0.8.0`、取得日を作業日に更新し、v0.5.0 → v0.8.0 の差分表（§3）を追記 |
| 3 | `dotnet build` が通るまで、消えた／変わったフィールドの参照を直す（§4 のコンパイルエラー一覧） |
| 4 | `dotnet test` が通るまでテストを直す |

> **`Takoda99.Client.csproj` の `<Compile Include="..\..\vendor\Takoda99.Proto\Messages.cs" />` はそのまま。** 取り込み方式は変えない。

## 3. v0.5.0 → v0.8.0 の契約差分

### 3.1 増えた型・メッセージ

| 種別 | 名前 | 用途 |
|---|---|---|
| DTO | `CullStageView` | 段階的足切りの1ステージ（`atMs` / `targetAliveCount`） |
| DTO | `RankingEntry` | 全量ランキングの1行（`storeId` / `rank` / `score` / `alive`） |
| DTO | `RankingChange` | 差分ランキングの1行（`storeId` / `score` / `alive`。**`rank` を持たない**） |
| S2C | `RankingSnapshot` | 全店の順位の全量配信（低頻度・整合性の回復） |
| S2C | `RankingDelta` | 変化した店のみの差分配信（高頻度・取りこぼし可） |
| S2C | `StoreEliminatedBatch` | 1回の足切りで脱落した店を**まとめて**配信（`stageIndex` / `entries`） |
| S2C | `PersonalResult` | 自店の脱落確定と同時に届く個人成績 |

### 3.2 中身が変わったメッセージ

| メッセージ | 変更 |
|---|---|
| `StoreSummary` | `score`（累積の絶対値）が追加。`evalNormalized` / `creditLife` は Obsolete |
| `EvaluationUpdate` | `score` が追加。`evalRaw` / `normalized` / `starRating` / `starDelta` は Obsolete |
| `ForcedEliminationWarning` | `untilMs` / `stageIndex` / `stageTotal` / `cutLineRank` / `cutStoreIds` / `selfAtRisk` が追加。`untilTick` / `thresholdPct` は Obsolete |
| `GameParametersPublicSubset` | `cullSchedule` / `scoreWeightTakoyaki` / `scoreWeightMiss` / `finalStageAliveThreshold` / `finalRushAliveThreshold` が追加。`initialLife` / `stormThresholdPct` / `patienceLateMul` / `patienceAlertMs` は Obsolete |
| **`MatchEnd`** | **ペイロードを持たない空クラスになった**（v0.5.0 は `finalRank` / `stats` / `reason` / `matchElapsedMs` / `creditLeft` / `evalRaw` / `evalNormalized` を持っていた）。個人成績は `PersonalResult` から取る |
| `PersonalResult` | `score` / `takoyakiCount` / `survivedMs` を持つ。`reason` / `creditLeft` / `evalRaw` / `evalNormalized` は Obsolete |

> **`MatchEnd` が空になったのが最大の破壊的変更。** 既存の `MatchEndAction` / `MatchResult` / `Renderer.OnMatchEnd(int, MatchStats)` はすべてこの1点で成立しなくなる。対応は [../result/01-personal-result.md](../result/01-personal-result.md)。

### 3.3 サーバーが送らなくなったメッセージ

| メッセージ | 理由 |
|---|---|
| `CustomerLeft` | 客が逃げなくなった（我慢ゲージ・離脱の廃止） |
| `CreditUpdate` | 信用（体力）制の廃止 |
| `StoreListUpdate` | ランキングは `RankingSnapshot` / `RankingDelta` が担う |
| `StoreEliminated`（単体） | `StoreEliminatedBatch` に集約された。**型自体は `StoreEliminatedBatch.Entries` の要素として残る** |

**型は Proto に残っているが、クライアントはこれらを受信しない前提で実装する。** 万一届いた場合の扱いは §5。

## 4. `MessageType` の扱い

`MessageType` は Proto 側の定数クラスであり、**v0.8.0 でも旧メッセージ名の定数は残っている**（`CustomerLeft` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated`）。定数が存在することと、クライアントが処理することは別の話。

`Dispatcher` が**処理する** `MessageType`（v0.8.0 の完全な一覧）：

| MessageType | 分類 | 扱う仕様書 |
|---|---|---|
| `MatchmakingStatus` | 定期更新 | 変更なし（既存 [05-dispatcher.md](../05-dispatcher.md)） |
| `MatchStart` | 全量 | [../match-state/01-score-and-self-rank.md](../match-state/01-score-and-self-rank.md) |
| `CustomerArrived` | イベント | 変更なし |
| `EvaluationUpdate` | 定期更新 | [../match-state/01-score-and-self-rank.md](../match-state/01-score-and-self-rank.md) |
| `DifficultyUpdate` | 定期更新 | 変更なし |
| `PhaseChange` | イベント | 変更なし |
| `RankingSnapshot` | 全量 | [../match-state/02-ranking-store.md](../match-state/02-ranking-store.md) |
| `RankingDelta` | 定期更新 | [../match-state/02-ranking-store.md](../match-state/02-ranking-store.md) |
| `ForcedEliminationWarning` | 定期更新 | [../match-state/03-cull-warning.md](../match-state/03-cull-warning.md) |
| `StoreEliminatedBatch` | イベント | [../match-state/03-cull-warning.md](../match-state/03-cull-warning.md) |
| `PersonalResult` | イベント | [../result/01-personal-result.md](../result/01-personal-result.md) |
| `MatchEnd` | イベント | [../result/01-personal-result.md](../result/01-personal-result.md) |

`Dispatcher` が**処理しない** `MessageType`：`CustomerLeft` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated`（単体）。

## 5. Obsolete フィールドの扱い（★徹底事項）

Proto v0.8.0 は Obsolete フィールドを**型から消していない**。JSON にはキーが存在し、**ゼロ値が届く**。

| フィールド | 届く値 | 誤って読むと |
|---|---|---|
| `GameParametersPublicSubset.InitialLife` | `0` | ライフゲージの最大値が0になる |
| `EvaluationUpdate.Normalized` / `StarRating` | `0` | 評価バー・星が常に0で描かれる |
| `StoreSummary.EvalNormalized` / `CreditLife` | `0` | 他店タイルが全部「瀕死」に見える |
| `ForcedEliminationWarning.ThresholdPct` | `0` | カットラインが常に0% |
| `CustomerView.PatienceMaxMs` | `0` | 我慢ゲージが即0＝即離脱扱い |

**ルール**

1. Obsolete フィールドを**新しいコードから参照しない**。
2. 既存コードで参照している箇所は、参照ごと削除する（値を0で握り潰すのではなく、その表示自体を消す。[../cleanup/01-removed-features.md](../cleanup/01-removed-features.md)）。
3. `null` で届き得るコレクション（`GameParametersPublicSubset.CullSchedule` / `RankingSnapshot.Entries` / `RankingDelta.Entries` / `StoreEliminatedBatch.Entries` / `ForcedEliminationWarning.CutStoreIds`）は、**Reducer に渡す前に空リストへ正規化する**。正規化は `Dispatcher` の Decode で行い、`ClientState` に `null` を入れない。

```csharp
// Dispatcher の Decode 内で使う共通ヘルパー（Net/Dispatcher.cs のプライベート static）
private static IReadOnlyList<T> OrEmpty<T>(List<T>? source)
    => source ?? (IReadOnlyList<T>)System.Array.Empty<T>();
```

## 6. 受信しないはずのメッセージが届いた場合

サーバーの移行途中や、予選挙動へのフォールバック運用（[20_廃止・非使用リスト §5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)）で、廃止済みメッセージが届く可能性は残る。

**`Dispatcher` は既存の未知メッセージ経路で捨てる。**`AcceptedPhases` に載っていない型は `OnUnknownMessage("<type>", "unknown-type")` を発火して `state` を変えない（既存 [05-dispatcher.md](../05-dispatcher.md) の挙動そのまま）。**例外を投げない・落ちない**ことが要件。

> 「知らないメッセージが来たら黙って捨てる」が既に実装されているため、本選対応で新たに書くコードはない。**捨てた事実がデバッグパネルに出ることだけ確認する。**

## 7. 依存関係

- 依存するモジュール：なし（このリポジトリの本選対応の起点）
- 依存されるモジュール：`match-state/` `result/` `cleanup/` の全仕様書、および Unity 側すべて

## 8. テスト観点

| # | 観点 |
|---|---|
| 1 | `dotnet build` が警告なしで通る |
| 2 | `EnvelopeCodec` が `RankingSnapshot` / `RankingDelta` / `StoreEliminatedBatch` / `PersonalResult` を往復（encode → decode）できる |
| 3 | `entries` キーごと欠落した `RankingSnapshot` を decode すると、`Entries` が空リストとして扱われる（`null` 参照例外を出さない） |
| 4 | `MatchEnd` のペイロードが `{}` でも decode が成功する |
| 5 | `CustomerLeft` / `CreditUpdate` / `StoreListUpdate` の Envelope を流しても `state` が変わらず、`OnUnknownMessage` が1回発火する |

## 9. 未確定事項

- なし（Proto v0.8.0 は確定済み・タグ発行済み）
