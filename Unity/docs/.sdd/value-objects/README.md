# value-objects — View用の派生状態と、Storeからの変換処理

`pureC#` 側の [`Store`](../../../../pureC%23/docs/.sdd/value-objects/README.md) が保持する canonical な値（サーバー権威データ＋クライアントローカル進捗）から、**演出・表示のために導出する派生状態**の形と、その変換（selector）処理を定義する。

## 1. この層の位置づけ

```
pureC#/Store が保持する値オブジェクト（../../../../pureC%23/docs/.sdd/value-objects/）
   ↓ 純粋関数（selector）で導出。永続化しない。UnityEngine非依存で書ける部分はpureC#に置いてもよいが、
   ↓ ここでは「Unity側から見てどう使うか」を仕様として書く
Unity側 View用派生状態（本ディレクトリの対象）
   ↓ View（Prefab/UIコンポーネント）が購読して描画するだけ。View自身は判定ロジックを持たない
画面表示
```

- 対象は「`Store` の値からは一意に計算できる、**表示のための量子化・分類・整形**」（評価の3段階化、我慢ゲージのムード分類、たこ焼き台の穴の状態、提灯の点灯数 等）
- ここに含めないもの：`Store`からは導出できない**Viewローカルの一時演出状態**（脱落後に潰れた見た目を出す残り時間、評価増減表示の一過性フラグ等）。これは各Viewコンポーネント自身が持つ実装詳細であり、値オブジェクトとしての仕様化は行わない（演出は後決めとし、状態の骨格のみ先に確定する方針）。ただし「どのタイミングでその一時状態がトリガーされるか」の入力元（＝`Store`の変化イベント）は各仕様書の「4. 変換処理」に明記する
- 変換関数は原則 **純粋関数**（同じ`Store`値を渡せば同じ結果を返す。副作用・内部状態を持たない）とする。`UnityEngine` に依存しない書き方ができる場合は `pureC#` 側に置くことも検討してよいが、初版はUnity側に置き、必要になった時点で移設を検討する

## 2. 仕様書一覧

| # | ファイル | 派生値オブジェクト | 対応する画面要素 |
|---|---|---|---|
| 01 | `01-store-visual-state.md` | `StoreVisualState`（評価3段階＋脱落） | 主画面の画面端アラート、99店ミニ盤面のセル色 |
| 02 | `02-customer-mood-state.md` | `CustomerMoodState`（普通/いらだち/怒り/退転） | 行列の客の表情・ポーズ |
| 03 | `03-takoyaki-stand-state.md` | `TakoyakiStandState` / `TakoyakiSlotState`（なにもない/生地/焼けた） | たこ焼き台のグリッド |
| 04 | `04-credit-life-lantern-state.md` | `CreditLifeLanternState`（提灯の点灯/消灯） | 左上の提灯3つ |
| 05 | `05-rank-bar-and-eval-delta-view-state.md` | `RankBarViewState` / `EvalDeltaDisplayState` | 上部順位バー・淘汰圏の帯・星評価と増減表示 |
| 06 | `06-sub-store-tile-state.md` | `SubStoreTileState`（信用ライフ3段階＋脱落直後／完全脱落） | 99店ミニ盤面の1マスの屋台画像・順位表示 |
| 07 | `07-patience-gauge-state.md` | `PatienceGaugeState`（残量比＋色段階3段階） | 我慢ゲージのバーの長さと色 |

### 2.1 本選（Proto v0.8.0）で追加したもの

| # | ファイル | 派生値オブジェクト | 対応する画面要素 |
|---|---|---|---|
| 08 | [08-ranking-row-view-state.md](./08-ranking-row-view-state.md) | `RankingRowViewState` / `SelfRankViewState` / `RankingRowsBuilder` | ランキングの1行・自店HUD |
| 09 | [09-cull-countdown-state.md](./09-cull-countdown-state.md) | `CullCountdownState`（残り秒・段階・境界・警告強度） | 足切り秒読みパネル |
| 10 | [10-result-tier.md](./10-result-tier.md) | `ResultTier`（1位／2〜3位／4〜10位／11位以下） | リザルトの順位別演出分岐 |
| 11 | [11-rank-ordinal.md](./11-rank-ordinal.md) | `RankOrdinal`（順位 → `1st` / `22nd` / `--`） | ランキング行の順位表記 |
| 12 | [12-ranking-row-style.md](./12-ranking-row-style.md) | `RankingRowStyle` / `RankingRowTone`（寸法・フォント・配色） | 上位の金銀銅と段階的な大きさ、下位の足切り帯 |

> **01・04〜07 は予選版で、本選では使われない**（信用ライフ・我慢ゲージ・相対評価の廃止に伴う）。
> 撤去状況は [../cleanup/01-removed-views.md](../cleanup/01-removed-views.md) を参照。

新しい派生値オブジェクトが増えたら、この表に行を追加してから仕様書ファイルを作る。

> **例外：`StarRatingFill` / `PlayerNameLayout` は本ディレクトリに専用の仕様書を持たない。** どちらも `Assets/Scripts/View/ValueObjects/` にある純粋関数だが、`Store` の値を「区分」へ落とすのではなく、**1つのHUDの中でどう割り振るか**だけを決める配分計算である。仕様は [match-view/07-match-hud.md](../match-view/07-match-hud.md) §4・§5 が正典（`StarRatingFill` の入力となる `starRating` の定義は [05](./05-rank-bar-and-eval-delta-view-state.md)）。テストは他の値オブジェクトと同じ `Unity/tests/Takoda99.View.Tests` にある。

> **例外：`MatchmakingCountdownState` も本ディレクトリに仕様書を持たない。** `MatchmakingViewState` と同じ理由（試合前の画面の話）で、仕様は [01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md) §8.5 が正典。

> **例外：`MatchmakingViewState` は本ディレクトリに仕様書を持たない。** 実装は他の値オブジェクトと同じ `Assets/Scripts/View/ValueObjects/` にあるが、**`Store` から導出する「試合中の表示区分」ではなく、試合前の画面遷移そのもの**であるため、仕様は [01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md) §2〜§3・§8.4 が正典。**同じモジュールの仕様を二重に書かない**という [README.md](../README.md) §5-4 の方針に従う。

## 3. 仕様書の書式

各ファイルは以下を含める：

- **参照する上流**（対応する `pureC#/docs/.sdd/value-objects/` のファイル、用語集の概念）
- **責務**（この派生状態が表す表示区分の範囲）
- **データ定義**（C#の具体的な型）
- **変換処理**（`Store` のどの値から、どの閾値・規則でこの派生状態を計算するか。擬似コードではなく実際の計算式で書く）
- **Unity構成**（この値を消費するView側で最低限必要なもの。MonoBehaviour本体の仕様は各Viewコンポーネントの仕様書側に譲り、ここでは「どんな形の値を渡せば描画できるか」までに留める）
- **未確定な演出との境界**（どこまでがこの値オブジェクトの責務で、どこからが未確定の演出詳細か）
- **テスト観点**
- **未確定事項**

## 4. 運用ルール

1. 変換処理（selector）は**冪等・副作用なし**で書く。`Store`の値を書き換えたり、Viewへ直接命令したりしない。
2. 演出の中身（色・アニメーション曲線・効果音）はここに書かない。ここに書くのは「どの状態区分がいつ成立するか」という**骨格**のみ。
3. サーバー権威の値（`evalNormalized`等）を閾値判定以外の目的で改変しない。
4. 各仕様書の「未確定な演出との境界」で示した演出詳細が決まったら、対応するViewコンポーネントの仕様書（`../match-view/` 等の番号付きファイル）を別途起こす。この`value-objects/`側は骨格の変更（区分の追加等）があった場合のみ更新する。
