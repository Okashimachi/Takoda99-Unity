# Takoda99-Proto ソース手ミラー

`pureC#/docs/.sdd/01-contract.md` §6 未確定事項「Proto の C# 配布方法」の暫定解として、
NuGet/GitHub Packages ではなく**ソース手ミラー**を採用する（[Takoda99-Proto README](https://github.com/Okashimachi/Takoda99-Proto) が認める配布方式の一つ）。
理由：この開発環境から GitHub Packages（要認証）を解決できないため、`dotnet build` が素の状態で通ることを優先した。

- 取得元: https://github.com/Okashimachi/Takoda99-Proto
- ファイル: `csharp/Takoda99.Proto/Messages.cs`
- 固定バージョン: タグ **`v0.3.0`**（`main` の同ファイルと一致）
- 取得日: 2026-08-06

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
