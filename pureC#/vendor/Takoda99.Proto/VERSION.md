# Takoda99-Proto ソース手ミラー

`pureC#/docs/.sdd/01-contract.md` §6 未確定事項「Proto の C# 配布方法」の暫定解として、
NuGet/GitHub Packages ではなく**ソース手ミラー**を採用する（[Takoda99-Proto README](https://github.com/Okashimachi/Takoda99-Proto) が認める配布方式の一つ）。
理由：この開発環境から GitHub Packages（要認証）を解決できないため、`dotnet build` が素の状態で通ることを優先した。

- 取得元: https://github.com/Okashimachi/Takoda99-Proto
- ファイル: `csharp/Takoda99.Proto/Messages.cs`
- 固定バージョン: タグ `v0.2.0` 相当（`main` の同ファイルはコメント中の誤字修正のみの差分。`Textro99-Docs` → `Takoda99-Docs` の1行）
- 取得日: 2026-08-04
- 同期方法: Proto 側でバージョンが上がったら、このディレクトリのファイルを手動で置き換え、`pureC#/docs/.sdd/01-contract.md` のテスト観点を再実行する（[docs/rules/02-Unity実装ルール.md](../../docs/rules/02-Unity実装ルール.md) §7）。

このディレクトリは Takoda99-Proto の正典をそのまま複製したものであり、**このリポジトリ側で内容を変更しない**（絶対原則7）。
