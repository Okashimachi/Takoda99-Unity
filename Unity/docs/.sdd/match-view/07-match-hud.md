# 07-試合画面のHUD（注文カウンタ／注文吹き出し／星評価／屋号）

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto)（`EvaluationUpdate.starRating` / `CustomerView.orderCount` / `StoreSummary.displayName`）／[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)（`Evaluation` / `Customer` / `Store`）／[01-renderer.md](./01-renderer.md)（振り分け）／[05-rank-bar-and-eval-delta-view-state.md](../value-objects/05-rank-bar-and-eval-delta-view-state.md)（星の定義）。矛盾したら上流優先。

試合画面に後から足した4つのHUD。いずれも**受信値を描くだけ**で、値の決定・推定はしない。

## 1. 置き場所（どのCanvasか）

| HUD | GameObject | スクリプト | Canvasを分けた理由 |
|---|---|---|---|
| 注文カウンタ（x/N） | `MainStoreCanvas/Main/MainStore/OrderCounter` | `MainStoreView`（既存に統合） | お題単語のすぐ横。駆動元が `SetWord` と同じ `typingJudge.CurrentView` 系統 |
| 注文吹き出し | `CustomerCanvas/Order` | `CustomerOrderBubbleView` | 客に属する表示。客Prefabと同じCanvasに置く |
| 星評価 | `MainStoreCanvas/EvalCanvas` | `StarRatingView` | `MainStoreCanvas` は打鍵1回ごとにお題・注文カウンタ・たこ焼き台が再描画される Canvas。星は打鍵より低頻度（評価更新時のみ）なので、同じ Canvas に置くと打鍵のたびに星部分までメッシュが再構築される。入れ子Canvasで切り離す（`SubStoreCanvas` とは別の Canvas であることに注意） |
| 屋号 | `MainStoreCanvas/Main/MainStore/PlayerName` | `MainStoreView`（既存に統合） | 屋台の見た目の一部。更新は表示名確定時の1回だけ |

`MainStoreView` に統合した2つは、[02-main-store-view.md](./02-main-store-view.md) §1「責務を分割しない理由」に従う（`Image`/`Text` を状態で差し替えるだけで、個別に MonoBehaviour を立てる情報量が無い）。

## 2. 注文カウンタ（`OrderCounter`）

```
MainStore/OrderCounter
├── BG
├── NumeratorText    (TMP) ← 準備できたたこ焼きの数
├── BarText          (TMP) ← 「/」。固定文字なので触らない
└── DenominatorText  (TMP) ← 注文個数
```

`MainStoreView.SetOrderProgress(int preparedCount, int orderCount)`。

- **分子＝打ち終えた単語数**（`ClientState.CurrentOrder.WordIndex`）。用語集4章の不変条件「注文個数 = タイプする単語数」により、1単語打ち切る＝たこ焼き1個ぶんが準備できたことになる
- **分母＝注文個数**（`CurrentOrder.OrderCount`。`CustomerView.orderCount` の写し）
- 分子は `0..orderCount` にクランプする。対応中の注文が無いときは `0/0`
- `Renderer` は state 変化のたびに呼ぶため、値が変わらないフレームは `ToString` ごと省く

## 3. 注文吹き出し（`CustomerCanvas/Order`）

```
CustomerCanvas/Order      ← CustomerOrderBubbleView（既定で非表示）
├── BG
└── Text (TMP)            ← 注文文句
```

`Show(string customerId, int orderCount, string orderText = null)` / `Hide()`。

- **行列の先頭が入れ替わった瞬間**に表示し、`visibleDurationSec`（既定2秒）で自分から引っ込める。起動は `Renderer.ApplyServingCustomer`（先頭客IDの変化検知）に相乗りする——`PatienceTimer.Begin` と同じ地点
- **1つを使い回す。** 客ごとに生成せず、次の客が先頭に来たらテキストだけ差し替えて出し直す。同じ `customerId` で繰り返し呼ばれても出し直さない（`Renderer` は state 変化のたびに呼ぶため）
- 文面は `orderText` が来ていればそのまま出す。無ければ `fallbackFormat`（既定 `"{0}こください"`）に注文個数を埋める
- **現時点では常にフォールバック側を通る。** 注文文句を運ぶフィールドは契約（`CustomerView`）に存在しない（§6）。契約に増えたら `Renderer` から第3引数に渡すだけでよい形にしてある
- 行列が空になったとき・自店脱落時・試合終了時は `Hide()`

## 4. 星評価（`EvalCanvas`）

```
MainStoreCanvas/EvalCanvas   ← StarRatingView（入れ子Canvas）
└── Stars
    ├── BG
    └── Star ×5              ← Assets/Prefabs/Others/Star.prefab
        └── Panel (Image)
```

`StarRatingView.SetRating(double starRating)`。

- **`starRating` は `EvaluationUpdate.starRating` の受信値そのまま**（`ClientState.StarRating`）。クライアントで算出も差分計算もしない（[05](../value-objects/05-rank-bar-and-eval-delta-view-state.md) §4 / [D-03](../../../../docs/server-sync/03-決定ログ.md)）
- 小数を表現するため、星ごとの塗り比率は [`StarRatingFill`](../value-objects/README.md) が割る。先頭の星から順に埋め、**端数は境目の星1つだけ**を部分的に塗る（例 2.5 → `1, 1, 0.5, 0, 0`）
- 塗りは `Image.Type.Filled` で行う。`fillMethod` / `fillOrigin` は `StarRatingView` の Inspector 値を `Awake` で全星へ適用する。**Star Prefab 側の設定に依存させない**
- **スクリプトを持つのは `EvalCanvas` だけ。** Star Prefab にはコンポーネントを足さない。`stars` 未設定時は `starsRoot` の子のうち名前が `Star` で始まるものから `Image` を拾う（`BG` は名前で弾かれる）

### `Normalized` と混同しないこと

`starRating` の母集団は99店全体で、`EvalNormalized`（生存店内パーセンタイル）とは別物。順位バー（`RankBarView`）は `Normalized` 側を使い続ける。星に描画以外の意味を持たせない。

## 5. 屋号（`PlayerName`）

```
MainStore/PlayerName
├── LeftText   (TMP)
├── MiddleText (TMP)
└── RightText  (TMP)
```

`MainStoreView.SetPlayerName(string displayName)`。分割は [`PlayerNameLayout`](../value-objects/README.md) の純粋関数が行う（Viewは判定を持たない）。

表示名は6文字固定の想定（`MatchmakingScreenView.DisplayNameInputLimit = 6`）だが、短い名前でも縦看板が崩れないよう全長で場合分けする。**3の倍数でなければ後ろに「屋」を1文字足してから割る。**

| 文字数 | 屋 | 実長 | 割り方 | 例 |
|---|---|---|---|---|
| 1 | 付けない | 1 | 中央のみ | `た` → ` / た / ` |
| 2 | 付ける | 3 | 1/1/1 | `たこ` → `た / こ / 屋` |
| 3 | 付けない | 3 | 1/1/1 | |
| 4 | 付ける | 5 | 2/2/1 | `たこ焼き` → `たこ / 焼き / 屋` |
| 5 | 付ける | 6 | 2/2/2 | |
| 6 | 付けない | 6 | 2/2/2 | |

余りの寄せ方は「余1は中央へ、余2は左と中央へ1文字ずつ」で固定する。余2で右を細くするのは、**右端に「屋」1文字だけが残って屋号として読める**ため。**1文字だけは屋を付けない**——`あ屋` にすると1文字名だけ2枠を使い、他の長さと見た目が揃わないため。

表示名の出どころは `state.Stores` の自店エントリ（`StoreSummary.DisplayName`）。`StoreListUpdate` が届くまでは空。

## 6. 依存関係

- 依存する `pureC#` モジュール：`ClientState`（`StarRating` / `CurrentOrder` / `Queue` / `Stores`）
- 依存するUnity側モジュール：`Takoda99.View.ValueObjects`（`StarRatingFill` / `PlayerNameLayout`）
- 依存されるモジュール：`Renderer`（唯一の入口。各Viewは `Store` を直接参照しない）

### `ClientState` への追加（本仕様書に含む）

`EvaluationUpdate.starRating` / `starDelta` は Proto にはあったが `ClientState` に載っていなかった。**受信値をそのまま保持するだけ**の追加として `ClientState.StarRating` / `StarDelta`・`EvaluationUpdateAction`・`Dispatcher`・`Reducer` に通す（算出は一切しない）。

## 7. Inspector 配線チェックリスト

| コンポーネント | フィールド | 割り当て |
|---|---|---|
| `MainStore` の `MainStoreView` | `orderNumeratorText` / `orderDenominatorText` | `OrderCounter/NumeratorText` / `OrderCounter/DenominatorText` |
| 同上 | `playerNameLeftText` / `playerNameMiddleText` / `playerNameRightText` | `PlayerName/LeftText` / `MiddleText` / `RightText` |
| `CustomerCanvas/Order` の `CustomerOrderBubbleView` | `text` | `Order/Text (TMP)`（未設定なら子から自動解決） |
| `EvalCanvas` の `StarRatingView` | `starsRoot` | `EvalCanvas/Stars`（`stars` は未設定でよい） |
| `Render ` の `Renderer` | `orderBubble` / `starRating` | `CustomerCanvas/Order` / `MainStoreCanvas/EvalCanvas` |
| 同上 | `takoyakiStand` | `MainStoreCanvas/Main/MainStore/Takoyakis`。未結線だと `ApplyOrderCounter` の分子（打ち終えた単語数）がたこ焼き台に伝わらず、鉄板が評価3段階の切り替わりでしか動かない |
| `SubStorePanel` Prefab の `SubStoreTileView` | `nameText` | `SubStorePanel/Text (TMP)`（未設定なら直下から自動解決） |

## 8. テスト・確認観点

判定ロジック（`StarRatingFill` / `PlayerNameLayout`）は `Unity/tests/Takoda99.View.Tests` の xUnit で検証する。View 本体は Unity Editor 実行で確認する。

- 1単語打ち切るたびに分子が1増え、注文が変わると `0/N` に戻るか
- 客が先頭に来た瞬間に吹き出しが出て、2秒で消えるか。次の客で**同じオブジェクトの**テキストが差し替わるか
- `starRating` を 0→5 まで動かしたとき、端数の星だけが部分的に塗られるか（2つ以上が同時に半端にならないか）
- 表示名を1〜6文字で変えたとき、3枠の割り方が §5 の表どおりか
- 他店タイルに `StoreSummary.DisplayName` が出るか。脱落して順位表示に変わった後も名前が残るか

## 9. 未確定事項

- **注文文句のテキストが契約に無い。** `CustomerView` に文面フィールドが増えるまでは `"{N}こください"` 固定で、「12こたべたいねん」のような属性ごとの言い回しは出せない。属性（`CustomerAttribute`）ごとの文面をクライアント側の辞書で持つ案もあるが、**文面が客属性の演出仕様に属するのか上流の配信物なのかが未確定**のため、本仕様書では持たせない
- 吹き出しの2秒という値（`visibleDurationSec`）。行列が速く進むときに前の客の吹き出しが残る/切れる問題は未検証
- `starDelta`（「★+0.2」のポップ）の見た目。`ClientState` までは通したが表示は未実装
- 屋号の「屋」以外の接尾辞（テーマ変更時）。`PlayerNameLayout.From` の引数で差し替えられるようにはしてある
