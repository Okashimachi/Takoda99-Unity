# 05-RankBarViewState / EvalDeltaDisplayState

> 参照する上流：[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md)（`StoreState.EvalNormalized`, `Rank`）、[pureC#/docs/.sdd/value-objects/01-match-state.md](../../../../pureC%23/docs/.sdd/value-objects/01-match-state.md)（`AliveCount`）、[用語集 5章「評価システム」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 画面上部の順位バー（自店の相対位置▲マーカー・生存数ラベル）の表示用状態を提供する
- **しない**こと：評価そのものの算出（`EvalRaw`/`EvalNormalized` の計算はサーバー権威）
- **しない**こと：評価の増減量・増減方向を**クライアント側で差分計算しない**（後述 §4 の決定事項）

## 2. データ定義

```csharp
public readonly record struct RankBarViewState(
    float SelfPositionRatio, // 0(最下位側)..1(1位側)
    int AliveCount,
    int MaxStores
);
```

`EvalDeltaDisplayState` は §4 の通り**保留**のため、この仕様書では型を確定させない。

## 3. 変換処理（RankBarViewState）

```
SelfPositionRatio = StoreState.EvalNormalized   // 用語集定義通り、生存店内パーセンタイルがそのままバー位置に対応
AliveCount = MatchState.AliveCount
MaxStores  = MatchState.MaxStores
```

- `EvalNormalized` が 0..1 で正規化済み（用語集5章）のため、追加の計算なくバー上の位置比率としてそのまま使える
- [01-store-visual-state.md](./01-store-visual-state.md) の `EvalLevel`（緑/黄/赤）と入力が同一のため、バー位置と店の色分けは常に整合する

## 4. 評価の増減表示（保留）

**決定事項：**

- 増減表示のトリガーは「評価が変わったタイミング」ではなく、**一定のタイミング**とする
- 表示するのは増減**量**ではなく、**評価が上がったか下がったか**という方向
- この判定は**サーバーがイベントとして発行する**。クライアントは受け取って表示するだけで、`EvalNormalized` 等の差分をクライアント側で計算しない

**保留の理由：**

該当するS2Cイベントが `Takoda99-Proto` にまだ定義されていない。契約の追加は上流（Proto側・人間承認）の作業であり、このリポジトリでは行えない（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）。イベントの形（発行間隔・ペイロード）が上流で確定してから、`EvalDeltaDisplayState` の型定義と変換処理を本仕様書に追記する。

**この決定により解消された論点：**

初版では「表示中の評価値の差分をクライアントで取る」案を検討していたが、評価は EMA・JKバズ加点・クレーマー減点・他店変動によるパーセンタイル再計算が合流した値であり、**クライアントに増減の因果を正しく帰属させる手段がない**。サーバーがイベントで方向を通知する方式にしたことで、クライアントは一切の評価計算を持たずに済み、サーバー権威の原則とも完全に整合する。

## 5. Unity構成

- 順位バーコンポーネントが `RankBarViewState` を購読し、▲マーカー位置と生存数ラベルを更新する
- 評価増減の表示コンポーネントは、上記イベントが上流で確定するまで実装しない

## 6. 未確定な演出との境界

- ここまで：`SelfPositionRatio` の算出式、生存数ラベルの入力
- ここから先（未確定）：▲マーカーの移動アニメーション、順位変動時の強調演出、評価増減表示の見た目全般

## 7. テスト観点

- `EvalNormalized` の更新が `SelfPositionRatio` に正しく反映されるか
- `AliveCount` / `MaxStores` が 0 のときに 0 除算等を起こさないか（表示比率計算に使う場合）
- 脱落後（`Alive = false`）に▲マーカーをどう扱うかが決まった際、その挙動

## 8. 未確定事項

- **（上流待ち）評価増減イベントの定義**：発行間隔、ペイロード（方向のみか、量も含むか）、対象（自店のみか全店か）。Proto側で確定次第 §4 を本文化する
- 画面上の評価数値（「3.4」）が何の値のどのスケールか（`EvalRaw` か、星換算の独自スケールか）。星表示 `starDisplay` への変換式もこれに従属するため未確定
- ▲マーカーの移動を瞬間移動にするかアニメーションにするか
