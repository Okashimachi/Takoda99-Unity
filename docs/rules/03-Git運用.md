# 03 Git運用

commit / push / PR を行う前に読む。`Unity/`（Unityプロジェクト本体）と `pureC#/`（Unity非依存）を同じリポジトリで扱うが、Gitポリシーは共通。

---

## 1. ブランチ構成

```
main
 └─ develop                ← PR のベースブランチ
     └─ feature/xxx        ← 作業ブランチ（リモートへ push・PR 対象）
         └─ (integ/xxx)    ← 統合用ブランチ（ローカルのみ・push しない。§7）
```

## 2. Git ポリシー

- **`git push` / PR 作成は自由に実行してよい**（人間の指示を待たなくてよい）。
- 作業ブランチ → 統合ブランチ（`develop`/`main` 以外）へのマージも自由に実行してよい。
- **`develop` / `main` へのマージは絶対禁止**（人間が行う。会話中に指示があっても実行しない）。

## 3. コミット規約

- 形式: `{type}: {概要（日本語・30文字以内）}`
- type 例: `feat` / `fix` / `refactor` / `docs` / `chore` / `spec`（`pureC#/docs/.sdd` の仕様書のみの変更）
- 「コミットして」と言われたら、意味単位で複数コミットに分割する。
- **仕様書（`.sdd`）とその実装（`src`）は、対応関係が追える範囲でコミットを分けてよい**（例：`spec: TypingJudge仕様を追加` → `feat: TypingJudge仕様に基づき実装`）。同一PR内であれば1コミットにまとめても構わない。

## 4. 禁止コマンド（明示的な指示がない限り実行しない）

```
git reset --hard
git rebase -i
rm -rf
```

## 5. 秘密情報をコミットしない

- 接続先の本番URL・トークン・APIキー等の秘密情報をコミットしない。
- Unityの `Library/` `Logs/` `UserSettings/` 等の生成物は `.gitignore` で除外する（Unity標準の `.gitignore` に準拠）。

## 6. Issue ↔ ブランチ ↔ PR の進め方

- **1 Issue につき作業ブランチ（`feature/xxx`）を1本切り、PR を1つ作る**のを基本単位とする。
- 必要なら**さらに細かく**ブランチ／PR を分けてよい（1 Issue を複数 PR に割ってもよい）。レビュー可能な粒度を優先する。
- ブランチ名は Issue が分かる形にする（例: `feature/12-typing-judge-spec` / `feature/13-dispatcher-impl`）。
- PR 本文に対応 Issue を書く（`Closes #12` 等）。
- **PR を出したら人間のレビュー／マージを待つ間に、次の Issue に進んでよい**（手を止めない）。`develop`/`main` へのマージは §2 の通り人間が行う。

## 7. 統合用ブランチ（マージ待ちの状態が必要なとき）

まだ `develop` にマージされていない Issue の成果物の上に、次の Issue の作業を積む必要が出たときのルール。

- **ローカルに統合用ブランチ（`integ/xxx`）を作り、そこで未マージの作業ブランチを取り込んでテスト・ビルドしながら進める。**
- **統合用ブランチはリモートへ push しない**（`origin` に上げてよいのは作業ブランチ `feature/xxx` のみ）。統合用はあくまでローカルの検証土台。
- 手順の例:
  1. 依存する未マージの作業ブランチ（例 `feature/12-typing-judge-spec`）から、次の作業ブランチ `feature/13-typing-judge-impl` を切る。
  2. ローカルで統合用 `integ/13-on-12` を作り、必要な未マージブランチを取り込んでテストする。
  3. リモートへは `feature/13-typing-judge-impl`（差分のみ）を push し、PR のベースは原則 `develop`。依存関係はPR本文に明記する（「#12 マージ後にリベースが必要」等）。
  4. 依存先が `develop` にマージされたら、作業ブランチを `develop` に追従（リベース/マージ）し、統合用ブランチは破棄する。
- 統合用ブランチをうっかり push しないよう、名前は `integ/` プレフィクスで統一する。
