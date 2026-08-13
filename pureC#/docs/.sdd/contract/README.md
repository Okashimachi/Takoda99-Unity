# `contract/` — 契約（Proto）の取り込みと本選 v0.8.0 への移行

本選（Takoda99-Proto **v0.8.0**）の契約をこのリポジトリへ取り込むための仕様書。**本選対応の最初の1本であり、他のすべてのディレクトリの依存先**。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-proto-v0.8.0-migration.md](./01-proto-v0.8.0-migration.md) | vendor ミラーの v0.5.0 → v0.8.0 更新、Obsolete フィールドの扱い、`MessageType` の増減 |

## 上流

- [Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`（**v0.8.0**・正典）
- [Takoda99-Docs 00_本選差分/10_差分_プロト.md](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/10_差分_プロト.md)
- 既存 [01-contract.md](../01-contract.md)（`EnvelopeCodec` の仕様。**本ディレクトリで置き換えない**。追加するのは扱うメッセージの種類だけ）

## 実装順の位置づけ

```
contract/01 （必ず最初）
   ↓
match-state/01,02,03  ─┐
result/01,02          ─┴→ Unity/docs/.sdd/（描画側）
```
