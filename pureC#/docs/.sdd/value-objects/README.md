# value-objects — Store が保持する canonical な値オブジェクト群

`03-store-reducer.md`（`Store`/`Reducer` モジュール本体の仕様）から参照される、**`Store` が保持する状態の「形」だけ**を意味単位で切り出した仕様書群。`Store`/`Reducer` のふるまい（`Dispatcher` からどう呼ばれるか等）は `03-store-reducer.md` 側に書き、ここには「どういうデータをどういう形で持つか」だけを書く。

## 1. この層の位置づけ

```
S2C メッセージ（proto DTO。生の受信データ）
   ↓ Reducer が畳み込む（本ディレクトリの各仕様書「加工プロセス」節に記述）
Store が保持する値オブジェクト（本ディレクトリの対象）
   ↓ 純粋関数（selector）で導出。永続化しない
Unity 側の View 用派生状態（../../Unity/docs/.sdd/value-objects/）
```

- 対象は「**サーバーから届いた事実、または `Dispatcher` に流れてくるローカル入力イベントを `Reducer` が畳み込んだ結果として `Store` が保持する値**」。表示演出のための状態（3段階評価・ムード・たこ焼きの見た目等）はここに含めない（それは Unity 側 `value-objects/`）。
- `orderProgress` や `missCount` のようにサーバーへ送らないクライアントローカル値も、`Store` が保持する「ゲームの実際の状態」である以上はここに含める（[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) 4章）。
- **値オブジェクトの形は `Takoda99-Proto` の実体（`csharp/Takoda99.Proto/Messages.cs`）に合わせる。** DTOをそのまま流用するのではなく、クライアントが扱いやすい形へ整えるが、**Protoに存在しない情報を「サーバーから届く」前提で書かない**。
- Proto に無い情報をクライアント側で補う場合（例：`CustomerState.ArrivedAtElapsedMs`）は、**それがクライアントの推定値であることを各仕様書に明記**し、サーバー権威の値と混同させない。
- Proto に対する不足・疑問は [docs/server-sync/](../../../../docs/server-sync/README.md) の台帳に `SV-xx` として起票し、仕様書からはその ID を参照する。契約の変更は本リポジトリでは行わない。

## 2. 仕様書一覧

| # | ファイル | 値オブジェクト | 対応する用語集の概念 |
|---|---|---|---|
| 01 | `01-match-state.md` | `MatchState` | 試合・進行（1章）、フェーズ・火力（8章） |
| 02 | `02-store-state.md` | `StoreState` / `StoreSummaryState` | 店舗・プレイヤー（2章） |
| 03 | `03-customer-state.md` | `CustomerState` | 客・客プール（3章）、我慢ゲージ（7章） |
| 04 | `04-order-progress-state.md` | `OrderProgressState` | 注文・お題・提供（4章） |

新しい値オブジェクトが増えたら、この表に行を追加してから仕様書ファイルを作る。

## 3. 仕様書の書式

各ファイルは以下を含める（[`../_template.md`](../_template.md) をベースに、値オブジェクト用に「2. データ定義」「3. 加工プロセス」を追加した構成）：

- **参照する上流**（Proto契約・Client-Docs・用語集の対応語）
- **責務**（この値オブジェクトが表す事実の範囲。表示用の派生状態を持たない）
- **データ定義**（C# の具体的な型で書く。`record`/`readonly struct` を想定）
- **加工プロセス**（どの S2C メッセージ／ローカルイベントから、どういう規則でこの値オブジェクトが更新されるか）
- **不変条件**（値の取り得る範囲・整合性）
- **依存関係**
- **テスト観点**
- **未確定事項**

## 4. 運用ルール

1. ここに書くのは**データの形と、それを更新する規則**のみ。UI表示・演出のロジックを書かない（Unity側 `value-objects/` の責務）。
2. サーバー権威の値（`evalNormalized` / `patienceLeftMs` / `creditLife` / `alive` 等）を、この層で勝手に補正・上書きしない（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）。
3. `UnityEngine` に依存する型を持ち込まない（`pureC#/docs/.sdd/README.md` 運用ルール4と同じ）。
4. Proto の実体が古い場合でも、ここでは「あるべき値オブジェクトの形」を先に確定してよい。Proto側の追従は別途トラッキングする。
