# Takoda99-Proto ソース手ミラー

`pureC#/docs/.sdd/01-contract.md` §6 未確定事項「Proto の C# 配布方法」の暫定解として、
NuGet/GitHub Packages ではなく**ソース手ミラー**を採用する（[Takoda99-Proto README](https://github.com/Okashimachi/Takoda99-Proto) が認める配布方式の一つ）。
理由：この開発環境から GitHub Packages（要認証）を解決できないため、`dotnet build` が素の状態で通ることを優先した。

- 取得元: https://github.com/Okashimachi/Takoda99-Proto
- ファイル: `csharp/Takoda99.Proto/Messages.cs`
- 固定バージョン: タグ **`v0.8.0`**（`main` の同ファイルと一致）
- 取得日: 2026-08-13

### v0.5.0 → v0.8.0 の差分（本選対応。[pureC#/docs/.sdd/contract/01-proto-v0.8.0-migration.md](../../docs/.sdd/contract/01-proto-v0.8.0-migration.md)）

**増えた型・メッセージ**

| 種別 | 名前 | 用途 |
|---|---|---|
| DTO | `CullStageView` | 段階的足切りの1ステージ（`atMs` / `targetAliveCount`） |
| DTO | `RankingEntry` | 全量ランキングの1行（`storeId` / `rank` / `score` / `alive`） |
| DTO | `RankingChange` | 差分ランキングの1行（`rank` を持たない） |
| S2C | `RankingSnapshot` | 全店の順位の全量配信（低頻度・整合性の回復） |
| S2C | `RankingDelta` | 変化した店のみの差分配信（高頻度・取りこぼし可） |
| S2C | `StoreEliminatedBatch` | 1回の足切りで脱落した店をまとめて配信 |
| S2C | `PersonalResult` | 自店の脱落確定と同時に届く個人成績 |

**中身が変わったメッセージ**

| メッセージ | 変更 |
|---|---|
| `StoreSummary` | `score` 追加。`evalNormalized` / `creditLife` は Obsolete。`finalRank` が `int?` へ |
| `EvaluationUpdate` | `score` 追加。`evalRaw` / `normalized` / `starRating` / `starDelta` は Obsolete |
| `ForcedEliminationWarning` | `untilMs` / `stageIndex` / `stageTotal` / `cutLineRank` / `cutStoreIds` / `selfAtRisk` 追加。`untilTick` / `thresholdPct` は Obsolete |
| `GameParametersPublicSubset` | `cullSchedule` / `scoreWeightTakoyaki` / `scoreWeightMiss` 追加。`initialLife` / `stormThresholdPct` / `patienceLateMul` / `patienceAlertMs` は Obsolete |
| **`MatchEnd`** | **ペイロードを持たない空クラスになった**（破壊的）。個人成績は `PersonalResult` から取る |
| `CustomerView` | `patienceMaxMs` / `patienceStartedAtServerMs` は Obsolete（我慢ゲージの廃止） |
| `MatchmakingParticipant` | `isBot` 追加 |

**サーバーが送らなくなったメッセージ**（型は残るが受信しない前提）

`CustomerLeft` / `CreditUpdate` / `StoreListUpdate` / `StoreEliminated`（単体・`StoreEliminatedBatch.entries` の要素としては残る）

> **Obsolete フィールドはゼロ値で届く。** 読むと「ライフ0」「星0」「我慢0ms」で描かれるため、
> このリポジトリのコードからは参照ごと削除する（[cleanup/01-removed-features.md](../../docs/.sdd/cleanup/01-removed-features.md)）。

### v0.4.0 → v0.5.0 の差分（[Proto PR #7](https://github.com/Okashimachi/Takoda99-Proto/pull/7)、Unity REQ-03対応）

| 種別 | 内容 |
|---|---|
| 追加 | `MatchmakingParticipant`（`storeId` / `displayName`）を新設 |
| 追加 | `MatchmakingStatus.selfStoreId`（受信者自身の識別子。マッチング画面で自分を強調表示するために配る） |
| 追加 | `MatchmakingStatus.participants`（`List<MatchmakingParticipant>`。待機中の参加者一覧、Botは含まない。並び順は`MatchStart.stores[]`の先頭部分と一致） |
| 変更 | `MatchmakingStatus.countdownMs`から`JsonIgnore(WhenWritingNull)`が外れ、無条件シリアライズに統一（Waiting中は値自体が省略されない点に注意） |

### v0.3.1 → v0.4.0 の差分（[Proto PR #6](https://github.com/Okashimachi/Takoda99-Proto/pull/6)）

| 種別 | 内容 |
|---|---|
| 追加 | `AttributeTally`（`served` / `left`）を新設、`MatchStats`に`normal`/`bonus`/`claimer`/`buzz`の属性別内訳を追加 |
| 追加 | `MatchStats.leftCount` / `totalKeystrokes` / `totalMisses` / `fastestMs` / `slowestMs` |
| 追加 | `GameParametersPublicSubset.patienceLateMul` / `patienceAlertMs` |
| 追加 | `MatchmakingJoin.displayName`（盤面表示名、任意） |
| 追加 | `MatchEnd.reason` / `matchElapsedMs` / `creditLeft` / `evalRaw` / `evalNormalized` |

### v0.2.0 → v0.3.0 の差分（[Proto PR #4](https://github.com/Okashimachi/Takoda99-Proto/pull/4)）

| 種別 | 内容 |
|---|---|
| **削除（破壊的）** | `GameParametersPublicSubset.matchTimeLimitMs`。試合の終了条件が「生存店=1」のみになり、制限時間という概念が無くなったため |
| 追加 | `GameParametersPublicSubset.stormThresholdPct` / `finalStageAliveThreshold` / `finalRushAliveThreshold` |
| 追加 | `EvaluationUpdate.starRating` / `starDelta`（表示専用の星0..5。母集団は生存店ではなく99店全体） |
| 追加 | `ForcedEliminationWarning.selfAtRisk`（自店が淘汰圏内か。閾値比較をクライアントにさせない） |
| 追加 | `StoreSummary.finalRank`（`int?`・脱落店のみ。**欠落を 0 と読まないこと**） |
| 追加 | `CustomerView.patienceStartedAtServerMs`（我慢ゲージの起点・サーバー基準の単調時刻） |
- 同期方法: Proto 側でバージョンが上がったら、このディレクトリのファイルを手動で置き換え、`pureC#/docs/.sdd/01-contract.md` のテスト観点を再実行する（[02-Unity実装ルール.md](../../../docs/rules/02-Unity実装ルール.md) §7）。

このディレクトリは Takoda99-Proto の正典をそのまま複製したものであり、**このリポジトリ側で内容を変更しない**（絶対原則7）。
