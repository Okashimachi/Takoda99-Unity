# Takoda99.Proto（ソース手ミラー・バージョン固定）

[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) の C# 契約を、`pureC#` からビルド・テストできるようにするための**手ミラー**（[docs/rules/02-Unity実装ルール.md](../../../docs/rules/02-Unity実装ルール.md) §7 が認める参照方法）。

| 項目 | 値 |
|---|---|
| 取得元 | `Okashimachi/Takoda99-Proto` `csharp/Takoda99.Proto/` |
| 固定バージョン | `0.1.0` |
| 固定コミット | `d567a98fc4d0b7ce3094c7c7c6f53064063d8401`（`main`） |
| 取得日 | 2026-08-04 |

## ルール

- **このディレクトリのファイルを本リポジトリで編集しない。** 契約の変更は Proto 側の人間承認フローで行う（[docs/rules/01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md) 絶対原則7）。
- 上流の更新に追従するときは、上表のコミットを更新したうえでファイル全体を差し替え、影響箇所（`pureC#/src` と `Unity/Assets/`）を同じタイミングで直す（勝手に最新を追わない）。
- NuGet / GitHub Packages での配布が整った時点で、このミラーは `PackageReference` に置き換える。
