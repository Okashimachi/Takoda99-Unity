# 01-たこ焼き調理アニメーション

> 上流：企画「たこ焼き調理アニメーション仕様書」。本書はそれを実装粒度へ落としたもので、新しいゲームルールは作らない。
> **演出はすべて表示専用。** 打鍵判定（`TypingJudge`）の結果を写すだけで、評価・信用・順位に一切影響しない（[rules/01](../../../docs/rules/01-責務と絶対原則.md) 原則3）。

## 1. 企画仕様からの確定差分

| 項目 | 企画書 | 本実装 | 理由 |
|---|---|---|---|
| 使用する穴 | 中央縦2列＝8穴のみ | **24穴すべて。打鍵速度で使う穴数が増減する。使う穴自体は1つめ→2つめ→…→occupiedCount個目→また1つめと巡回する** | 企画判断（速い人ほど多く仕込める・毎回同じ穴だけが動くのを避ける） |
| 焼け段階 | `raw` → `half` → `done` の3段階 | **`raw` → `done` の2段階**（`half` 廃止） | 企画判断。既存 `TakoyakiSlotState` を変えずに済む |
| 遷移点 | 40% で half、80% で done | **単語を打ち切った瞬間に done のみ**（進捗率での早期遷移は行わない） | 上に同じ |
| 注文個数 | 8個固定 | **`CustomerView.OrderCount`（サーバー値）** | 契約は本リポジトリで変更しない（[rules/01](../../../docs/rules/01-責務と絶対原則.md) 原則7） |
| 舟皿への移動 | 1単語打ち切るたびに1個ずつ | **注文ぶん打ち終えた瞬間に全個数を一斉に盛る** | 企画判断 |
| 不格好（7番） | 窪みの玉ごとにミス回数で変化 | **玉は変化しない。舟皿の盛り付けが打鍵ミス率で3段階に変わる** | 企画判断（素材が皿単位の1枚絵のため） |
| 手の待機（呼吸） | 実装コストが許せば | **実装しない** | 企画判断 |
| 調整値 | 本文に直書き | **すべて `CookingAnimationSettings`（ScriptableObject）** | 企画判断（後から調整するため） |
| 手の演出 | 打鍵ごとの縦揺れ・ミス時の横揺れのみ | **単語を打ち切った瞬間、手がその穴まで動いてひっくり返す演出を追加** | 企画判断（本実装の追加分） |
| 使う穴数の初期値 | 明記なし | **試合開始・客の入れ替わり直後は常に下限（8）から始まる** | 打鍵速度の計測窓が満ちる前の異常値で穴が誤って全開放されるのを防ぐ |

### 1.1 「使う穴数」と「注文個数」の関係

`TakoyakiStandState.From(orderCount, typedWordCount)` は v0.8.0 で注文個数を使う設計だった。本改訂では
**生地を流す穴数（`occupiedCount`）を打鍵速度から決める**。したがって「台に並ぶ生地の数」と注文カウンタ `x/N` は
一致しなくなり、生地は「先行して仕込んでいる分」を表す。

打鍵速度（KPM）はサーバーから来ないため **クライアントローカルで算出する**。これは経営ロジックではなく
見た目の段階決定であり、送信もしない（[rules/01](../../../docs/rules/01-責務と絶対原則.md) の禁止事項に触れない）。

## 2. 責務分担

| クラス | 置き場 | 責務 | しないこと |
|---|---|---|---|
| `CookingAnimationSettings` | `View/Cooking/` | 全アニメーションの尺・振れ幅・しきい値を保持する ScriptableObject | 演出そのもの |
| `TypingSpeedMeter` | `View/Cooking/` | 直近 N 打の間隔から KPM を算出する（純クラス） | 段階の決定 |
| `TypingSpeedTierRule` | `View/Cooking/` | KPM → 使用穴数への変換（純関数）。`CookingAnimationSettings.SpeedTier`（UnityEngine 依存）を引数に取るため、UnityEngine 非依存が前提の `View/ValueObjects/` には置かない（`tests/Takoda99.View.Tests` 等がその前提でビルドされる） | KPM の計測 |
| `TakoyakiQualityRule` | `View/ValueObjects/` | ミス率 → 盛り付け3段階への変換（純関数） | ミスの計数 |
| `TakoyakiSlotView` | `View/` | 穴1つの見た目。生地投入・焼き上がり・玉の取り外し | どの穴が対象かの判断 |
| `TakoyakiStandView` | `View/` | 24穴の統括。打鍵イベントを受けて対象の穴を進める。完成玉を `FlyingTakoyakiAnimator` に渡す | 打鍵の判定・品質の計算式 |
| `FlyingTakoyakiAnimator` | `View/Cooking/` | 窪み→舟皿の弧移動（8番） | 玉の生成元・着地先の決定 |
| `TrayView` | `View/Cooking/` | 舟皿。着地した玉の保持と提供演出（9番） | 提供の確定 |
| `HandView` | `View/Cooking/` | 手の打鍵反応・ミス反応（2, 3番） | 打鍵の判定 |
| `Renderer` | `View/` | `ITypingJudge` / `IStore` の変化を上記へ配る | 演出の中身 |

## 3. イベントの流れ

```
UnityInputSource → TypingJudge → MatchClientController
                                        │
                                        ├─ IRenderer.OnKeyFeedback(KeyResult)
                                        │        │
                                        │        ├─ Correct      → HandView.PlayKeyReaction()
                                        │        │                  TakoyakiStandView.OnKeyTyped(miss:false)
                                        │        ├─ Miss         → HandView.PlayMissReaction()   ※2番より優先
                                        │        │                  TakoyakiStandView.OnKeyTyped(miss:true)
                                        │        └─ WordCleared  → TakoyakiStandView.OnWordCleared()
                                        │                            （玉は鉄板に残す。焼く穴を次へ進めるだけ）
                                        └─ IRenderer.OnOrderServed(customerId)
                                                 → TakoyakiStandView.OnWordCleared()（最終単語ぶん）
                                                 → TakoyakiStandView.OnOrderServed()
                                                      → FlyingTakoyakiAnimator.Fly() ×注文個数（一斉）
                                                      → TrayView.Serve(quality)
```

- `OrderCleared` は `OnKeyFeedback` に来ない（`MatchClientController` が `OnOrderServed` を呼ぶ）。
  最終単語の玉が飛ばなくなるため、**`OnOrderServed` でも `OnWordCleared()` を呼ぶ**。既存の打鍵SEと同じ扱い。
- 新しい単語の1文字目（5番の生地投入）は、`TakoyakiStandView` が「対象の穴がまだ `Empty` なら投入」で自律的に判断する。
  単語の開始を別イベントで通知しない（`WordCleared` の直後が次の単語であるため）。

## 4. ふるまい

### 4.1 使う穴数・使う穴の巡回（D）

**試合開始・客の入れ替わりの直後は、常に下限（`speedTiers` 先頭の穴数。既定8）から始める。**
`TypingSpeedMeter` が `speedWindowKeys` 打ぶんのリングバッファを満たす（`HasFullWindow`）までは、
KPM を計算しても段階の判定には使わない。数打だけで KPM を出すと、たまたま連打が詰まっただけで
異常に高い値が出ることがあり、試合が始まった瞬間に穴が全開放される事故になるため。

窓が満ちたら `TypingSpeedTierRule.ResolveSlotCount` が `CookingAnimationSettings.speedTiers`
（`minKpm` 昇順の表）から穴数を引く。

- 段階が**上がる**のは即時、**下がる**のは `speedTierDropHoldMs` 継続してから（打鍵の揺らぎで台がちらつかない）
- 穴数が減ったとき、はみ出した穴は `Empty` に戻す（調理中の玉があっても捨てる。先行仕込みの取り消しという意味づけ）
- 客が入れ替わるたびに `TypingSpeedMeter` はリセットされ、段階も下限へ戻る（`ResetSpeedTier`）。
  そこから新しい客の打鍵速度に合わせて再びランプアップする

**どの物理的な穴を使うかは、occupiedCount 個の範囲を巡回する。** 1単語ごとに
`activeSlotIndex` が1つ進み、occupiedCount 個目を超えたら1つめへ戻る
（`TakoyakiStandView.AdvanceActiveSlot`）。**客をまたいでもこの巡回位置は引き継ぐ**
（`typedWordCount` や `orderCorrectCount` 等の注文単位のカウンタと違い、`BeginOrder` では
戻さない）。短い注文が連続しても毎回同じ左上の穴だけが動き続けることのないようにするため。

occupiedCount ぶんの穴を一巡しても提供待ち（`Cooked`）の穴に行き当たった場合はそこを飛ばし、
次の空いている穴を探す（長い注文が生地の数を超えて一巡したとき、まだ舟皿へ運んでいない
完成品を新しい生地で上書きしないため）。

### 4.2 焼け具合（E）

**Raw→Done の切り替えは、単語を打ち切った瞬間にだけ起きる。** 打鍵の途中経過（進捗率）では起こさない。
`Batter` → `Cooked` へ `cookedFadeMs` でクロスフェードし、同時にたこ焼き自身が
`takoyakiFlipRotationDegrees` ぶん回転して元の向きに戻る（ひっくり返す動き。§4.4 参照）。

過去に検討した「単語の進捗80%で早めに焼き上げる」案は採用しない。
タイプの完了タイミングと見た目の切り替わりが一致しないと、プレイヤーが自分の打鍵を信用できなくなるため。

### 4.3 盛り付けの出来（7番の読み替え）

**鉄板の上の玉は出来で変化しない。** `Empty` / `Batter` / `Cooked` の3状態だけで、ミスしても見た目は変わらない。

出来が現れるのは舟皿だけ。1注文ぶんの打鍵で数えたミス率
（`missCount / (correctCount + missCount)`）を `TakoyakiQualityRule` に通し、
`trayClean` / `trayNormal` / `trayDirty` のどれで盛り付けるかを決める。

- ミス率 <= `trayCleanMaxMissRatio` → きれい
- ミス率 <= `trayNormalMaxMissRatio` → ふつう
- それより上 → 汚い

回数ではなく**率**で見るのは、注文個数（＝単語数）も単語の長さもサーバーが決めるため、
回数のしきい値だと注文ごとに難易度が変わってしまうから。既存の打鍵SE（`TypingWordSoundRule`）と同じ考え方。

### 4.4 手のひっくり返し演出（本実装の追加分）

**単語を打ち切った瞬間、手が打ち終えた穴まで移動してひっくり返し、続けて次のお題が使う穴まで動く。**
手の「左下角」が対象の穴の位置に重なるよう動く（`HandView.PlayFlipReaction`。手のサイズの半分だけ
右上へオフセットして、中心ではなく角を合わせる）。尺は `handFlipDurationMs`（2区間で折半）。

**手の定位置（休んでいるときに戻る場所）は固定ではない。** 移動の最後にいた「次のお題の穴」の位置が、
以後の定位置として上書きされる（`HandView.restPosition` が可変）。使う穴が§4.1のとおり毎回変わる
ため、手を毎回同じ場所へ戻すと、次のお題の穴から離れた位置で打鍵ごとの反応（2番）だけが起き続けて
不自然になる。お題が変わるたびに手の居場所そのものを更新することで、常に「いま打っている穴の近く」
で反応するようにする。

打鍵ごとの反応（2番）・ミス反応（3番）より**後に発火するため、これらを上書きする**
（`HandView.Play` が実行中のコルーチンを止める既存の規則をそのまま使う）。

### 4.35 一斉盛り付け（8番の読み替え）

1単語を打ち切っても玉は鉄板に残る（`Cooked` のまま）。焼く穴が次へ進むだけ。

**注文ぶんを打ち終えた瞬間に、`Cooked` の玉すべてが一斉に舟皿へ飛ぶ。**
同時に出発すると1個の塊に見えるため、`flyStaggerMs` だけ出発を後ろへずらす。
全部が着地したら盛り付け済みの絵へ差し替え、提供演出（9番）へ進む。

### 4.4 提供（9番）

`TrayView.AddBall` が `OrderCount` 個目を受けたら `serveDelayMs` 後に提供演出。
**提供演出中も打鍵は止めない。** 新しい皿は `serveMs - trayCrossOverlapMs` 経過時点でフェードインを始める。

## 5. 調整パラメータ（`CookingAnimationSettings`）

企画書の数値を既定値とする。実機で詰める前提で全て Inspector 公開。

| フィールド | 既定 | 企画書の該当 |
|---|---|---|
| `handKeyDurationMs` / `handKeyOffsetY` | 50 / -12 | 2番 |
| `handMissDurationMs` / `handMissOffsetX` | 70 / 10 | 3番 |
| `batterFallMs` / `batterSpreadMs` | 60 / 40 | 5番（計100ms） |
| `cookedFadeMs` | 90 | 6番（打ち切った瞬間に切り替え） |
| `handFlipDurationMs` | 160 | 手のひっくり返し（§4.4） |
| `takoyakiFlipRotationDegrees` | 360 | たこ焼きの回転（§4.4） |
| `trayCleanMaxMissRatio` / `trayNormalMaxMissRatio` | 0.0 / 0.15 | 7番（率に読み替え） |
| `trayServedFadeMs` | 80 | 7番（盛り付け絵の出現） |
| `flyRiseMs` / `flyArcMs` / `flyLandMs` / `flyApexHeightScale` | 90 / 80 / 50 / 1.4 | 8番（計220ms） |
| `flyStaggerMs` | 40 | 8番（一斉盛り付けの出発ずらし） |
| `serveDelayMs` / `serveMs` / `trayFadeInMs` / `trayCrossOverlapMs` | 180 / 380 / 220 / 50 | 9番 |
| `speedTiers` | 0kpm→8穴 / 120→12 / 200→18 / 280→24 | D（新規） |
| `speedWindowKeys` / `speedTierDropHoldMs` | 20 / 1500 | D（新規） |

## 6. Unity構成

[match-view/03-takoyaki-stand-view.md](../match-view/03-takoyaki-stand-view.md) §3.1 の階層に以下を追加する。

```
root/MainStoreCanvas/Main/MainStore
├── Takoyakis                ← TakoyakiStandView
├── TrayRoot                 ← TrayView（CanvasGroup 必須）
│   ├── TrayEmpty / TrayServed
│   └── TrayBalls
├── FlyLayer                 ← FlyingTakoyakiAnimator
└── HandRoot / HandPivot / Hand   ← HandView
```

描画順（Hierarchy 順）は `Stand → Takoyakis → TrayRoot → FlyLayer → HandRoot`。

## 7. テスト・確認観点

- 打鍵速度を上げ下げして、生地の穴数が段階的に増減するか。減る側が即座にちらつかないか
- 単語を打ち切った**瞬間に** Raw→Done が切り替わるか（打鍵の途中で先に焼き上がらないか）
- 鉄板は基本 Raw で、Done になるのはタイプが終わって提供待ちの穴だけか
  （Prefab の `raw`/`done` 参照が入れ替わっていないか。過去に取り違えていた事故があった）
- 単語を打ち切るたびに使う穴が1つめ→2つめ→…と変わり、occupiedCount 個目の次は1つめに戻るか
- 単語を打ち切った瞬間、手がその穴まで動いてひっくり返し、続けて次のお題の穴まで動いて止まるか
  （毎回同じ場所に戻ってしまっていないか）
- 単語を打ち切っても玉が鉄板に残り、焼く穴だけが次へ進むか
- 注文ぶん打ち終えた瞬間に全個数が一斉に舟皿へ飛び、盛り付け → 提供 → 新しい皿、と流れるか。その間も打鍵が通るか
- ミスを多く混ぜたとき、盛り付けの絵が「汚い」に変わるか
- 試合開始直後・客が入れ替わった直後、鉄板の生地が8個から始まり、24個から始まらないか
- 高速で打ち続けたとき、`speedWindowKeys` ぶん打ってから穴数が増え始めるか（それより前は増えないか）
- ミスで手が横に揺れ、通常反応（縦）が同時に出ないか
- `CookingAnimationSettings` の値を変えると挙動が変わるか（コード側に直値が残っていないか）

## 8. 未確定事項

- 空の舟皿スプライトが未入稿（アート担当へ依頼済み）。玉単位の不格好スプライトは仕様変更で不要になった
- `speedTiers` の KPM しきい値は仮値。実打鍵テストで調整する
