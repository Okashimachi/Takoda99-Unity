# 05-RankBarViewState / EvalDeltaDisplayState

> 参照する上流：[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md)（`StoreState.EvalNormalized`, `Rank`）、[pureC#/docs/.sdd/value-objects/01-match-state.md](../../../../pureC%23/docs/.sdd/value-objects/01-match-state.md)（`AliveCount`）、[用語集 5章「評価システム」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 画面上部の順位バー（自店の相対位置▲マーカー・生存数ラベル）の表示用状態を提供する
- **しない**こと：評価そのものの算出（`EvalRaw`/`EvalNormalized` の計算はサーバー権威）
- **しない**こと：評価の増減量・増減方向を**クライアント側で差分計算しない**（後述 §4 の決定事項）

## 2. データ定義

```csharp
public readonly record struct RankBarViewState(
    float SelfPositionRatio,  // 0(最下位側)..1(1位側)
    int AliveCount,
    int MaxStores,
    float StormThresholdPct,  // 淘汰圏の帯を描く位置（表示専用）
    bool SelfAtRisk           // 自店が淘汰圏内か。サーバー判定
);

public readonly record struct EvalDeltaDisplayState(
    float StarRating,  // 0..5。表示専用
    float StarDelta    // 前ティックからの増減。0 なら表示しない
);
```

## 3. 変換処理（RankBarViewState）

```
SelfPositionRatio = StoreState.EvalNormalized   // 用語集定義通り、生存店内パーセンタイルがそのままバー位置に対応
AliveCount        = MatchState.AliveCount
MaxStores         = MatchState.MaxStores
StormThresholdPct = MatchState.StormThresholdPct
SelfAtRisk        = 直近の ForcedEliminationWarning.selfAtRisk（未受信なら false）
```

- `EvalNormalized` が 0..1 で正規化済み（用語集5章）のため、追加の計算なくバー上の位置比率としてそのまま使える
- [01-store-visual-state.md](./01-store-visual-state.md) の `EvalLevel`（緑/黄/赤）と入力が同一のため、バー位置と店の色分けは常に整合する

### `StormThresholdPct` と `SelfAtRisk` の使い分け

**`StormThresholdPct` は帯を描くためだけに使い、危険かどうかの判定に使ってはいけない。** 淘汰圏内かの判定はサーバーが `ForcedEliminationWarning.selfAtRisk` で配信する（Proto v0.3.0）。`SelfPositionRatio < StormThresholdPct` のような比較をクライアントで行うと、サーバーの判定基準とズレて誤警告になる（[SV-05](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-05) / [D-08](../../../../docs/server-sync/03-決定ログ.md#d-08--proto-v030-への追従)）。

## 3.1 変換処理（EvalDeltaDisplayState）

```
StarRating = EvaluationUpdate.starRating   // そのまま。クライアントで算出しない
StarDelta  = EvaluationUpdate.starDelta    // そのまま
```

`starRating` の定義は `5 * (maxStores - rank) / (maxStores - 1)` で、**母集団は生存店ではなく99店全体**（脱落店は下位に積む）。そのため他店が脱落しても自店の星は下がらない。`EvalNormalized`（生存店内パーセンタイル）とは別物であり、**分配重み・下位淘汰はサーバーが `EvalNormalized` を使い続ける**。`StarRating` に描画以外の意味を持たせないこと。

## 4. 評価の増減表示

**Proto v0.3.0 で確定した（保留を解除）。** 専用イベントは追加されず、`EvaluationUpdate` に `starRating` / `starDelta` が相乗りする形で解決した（[SV-06](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-06) / [SV-21](../../../../docs/server-sync/02-パラメータと閾値.md#sv-21)）。

**決定事項：**

- 増減表示のトリガーは `EvaluationUpdate` の受信時。`starDelta` は「前ティックからの増減」
- 表示するのは方向だけでなく**増減量そのもの**（`starDelta`）。画面案の「★+0.2」がそのまま対応する
- 対象は**自店のみ**。`EvaluationUpdate` は自店専用メッセージのため、他店の増減は届かない（[SV-01](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-01)）
- **クライアント側で `EvalNormalized` 等の差分を計算しない。** この点は当初の決定（[D-03](../../../../docs/server-sync/03-決定ログ.md#d-03--評価の増減表示はサーバーイベント方式としクライアントで差分計算しない)）から変わっていない

> 当初 D-03 は「表示するのは方向のみ」としていたが、`starDelta` が量を運ぶため方向に限定する理由が無くなった。D-03 の本質（差分計算をクライアントに持たせない）は満たされているため、D-03 は撤回せず、この点のみ [D-08](../../../../docs/server-sync/03-決定ログ.md#d-08--proto-v030-への追従) で上書きする。

**この決定により解消された論点：**

初版では「表示中の評価値の差分をクライアントで取る」案を検討していたが、評価は EMA・JKバズ加点・クレーマー減点・他店変動によるパーセンタイル再計算が合流した値であり、**クライアントに増減の因果を正しく帰属させる手段がない**。サーバーがイベントで方向を通知する方式にしたことで、クライアントは一切の評価計算を持たずに済み、サーバー権威の原則とも完全に整合する。

## 5. Unity構成

- 順位バーコンポーネントが `RankBarViewState` を購読し、▲マーカー位置・生存数ラベル・淘汰圏の帯を更新する
- 評価増減の表示コンポーネントが `EvalDeltaDisplayState` を購読し、星の数値と「★+0.2」相当のポップを更新する

## 6. 未確定な演出との境界

- ここまで：`SelfPositionRatio` の算出式、生存数ラベルの入力
- ここから先（未確定）：▲マーカーの移動アニメーション、順位変動時の強調演出、評価増減表示の見た目全般

## 7. テスト観点

- `EvalNormalized` の更新が `SelfPositionRatio` に正しく反映されるか
- **`Rank` が 0（`EvaluationUpdate` 未受信）のとき、▲マーカーが最下位側（左端）に立つか。** 順位軸のクランプに任せると 0 が 1位 に丸められ、試合開始と同時に1位の位置へ立ってしまう
- `AliveCount` / `MaxStores` が 0 のときに 0 除算等を起こさないか（表示比率計算に使う場合）
- 脱落後（`Alive = false`）に▲マーカーをどう扱うかが決まった際、その挙動
- `StarRating` / `StarDelta` が受信値のまま保持され、**クライアント側で再計算されていない**こと
- `SelfAtRisk` が `ForcedEliminationWarning` 由来のみで決まり、`SelfPositionRatio` と `StormThresholdPct` の比較から導出されていないこと
- `StarDelta == 0` のとき増減ポップを出さないこと

## 8. 未確定事項

- ~~（上流待ち）評価増減イベントの定義~~ → **Proto v0.3.0 で確定**（`starDelta`）
- ~~画面上の評価数値（「3.4」）が何の値のどのスケールか~~ → **Proto v0.3.0 で確定**（`starRating` = `5*(maxStores-rank)/(maxStores-1)`、母集団は99店全体）
- ▲マーカーの移動を瞬間移動にするかアニメーションにするか
- 星の丸め方（0.5刻みで表示するか、小数第1位まで出すか）。`starRating` は連続値で届くため表示側の裁量
- `SelfAtRisk` が true の間の見せ方（帯の点滅・全画面アラート）。淘汰予告UIの仕様書（未作成）で決める
