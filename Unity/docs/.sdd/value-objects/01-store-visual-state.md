# 01-StoreVisualState

> 参照する上流：[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md)（`StoreState` / `StoreSummaryState`）。矛盾したら上流優先。

## 1. 責務

- 店の評価を「高中低」の3段階＋「脱落」の計4区分に分類した表示用状態を提供する
- 自店舗（主画面のアラート演出）・他店（99店ミニ盤面の色分け）の**両方**で同じ変換規則を共有する
- **しない**こと：評価の閾値（`EvalNormalized`のどこで区切るか）を独自に決めない。`GameParameters`（外部パラメータ）由来か、演出用のUnity固定値かは未確定事項に記載する。閾値の実値そのものはここでは仮置きし、確定は別途行う

## 2. データ定義

```csharp
public enum StoreEvalLevel { High, Mid, Low }

public readonly record struct StoreVisualState(
    string StoreId,
    StoreEvalLevel EvalLevel,
    bool Eliminated
);
```

## 3. 変換処理

入力：`StoreState` または `StoreSummaryState`（どちらも `EvalNormalized: float(0..1)` と `Alive: bool` を持つ）

```
if (!Alive) → Eliminated = true, EvalLevel は直近生存時点の値を保持（脱落後に再計算しない）
else:
    Eliminated = false
    EvalLevel =
        EvalNormalized >= highThreshold → High
        EvalNormalized >= midThreshold  → Mid
        else                            → Low
```

- **この3段階は「相対順位の表示」である**（設計決定）。`EvalNormalized` は用語集5章の定義通り「生存店内でのパーセンタイル(0..1)」であり、そこに固定閾値を引くため、**全店の絶対的な巧拙に関わらず、常に生存店の約 (1-highThreshold) / (highThreshold-midThreshold) / midThreshold の割合が緑/黄/赤に振り分けられる**。これは意図した挙動で、「赤＝絶対的に下手」ではなく「赤＝今この試合で下位グループにいる」を意味する。下位淘汰（`ForcedElimination`）が正規化評価の下位を刈る仕様（用語集9章）と表示の意味が一致するため、この方式を採る
- `highThreshold` / `midThreshold` は本仕様書内の定数ではなく、演出確定時に決める設定値（未確定事項参照）。**赤帯の下限は `MatchState.StormThresholdPct` を使う**（Proto v0.3.0 で `GameParametersPublicSubset` に追加された。[SV-20](../../../../docs/server-sync/02-パラメータと閾値.md#sv-20)）。これにより「赤＝淘汰圏」という意味がサーバーの実閾値と常に一致し、リモートコンフィグでの調整にも追従する

> **`StormThresholdPct` は帯の描画にのみ使い、危険かどうかの判定に使ってはいけない。** 自店が淘汰圏内かの判定はサーバーが `ForcedEliminationWarning.selfAtRisk` で配信する（Proto v0.3.0）。`EvalNormalized < StormThresholdPct` のような比較で警告を出すと、サーバーの判定基準とズレて誤警告になる（[SV-05](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-05) / [D-08](../../../../docs/server-sync/03-決定ログ.md#d-08--proto-v030-への追従)）。
- `Eliminated` は `StoreState.Alive` / `StoreSummaryState.Alive` の否定をそのまま使う。**脱落直後の「潰れた見た目を一定時間出す」という時限演出はここに含めない**（Viewローカルの一時状態。`Eliminated = true` になった**瞬間**をトリガーとして使うところまでがこの値オブジェクトの責務）

## 4. Unity構成

- 主画面：自店舗の `StoreVisualState.EvalLevel` が `Low` の間、画面端フラッシュを継続表示するアラートコンポーネントが購読する
- 99店ミニ盤面：`StoreSummaryState[]` を毎回 `StoreVisualState[]` に変換し、セルごとに `EvalLevel`→色（緑/黄/赤）のマッピングを適用するコンポーネントが購読する。色そのもの（RGB値）はこの仕様書の対象外

## 5. 未確定な演出との境界

- ここまで：`EvalLevel` の3区分と `Eliminated` フラグが「いつ成立するか」
- ここから先（未確定）：フラッシュの点滅速度・色のRGB値、脱落後に潰れた見た目を出す**継続時間**とその間の`EvalLevel`表示の扱い

## 6. テスト観点

- `EvalNormalized` が閾値ちょうどの境界値のときにどちらの区分になるか（`>=`の向き）
- `Alive = false` に変わった瞬間、`Eliminated` が正しく `true` になるか
- 脱落後に `EvalNormalized` の更新イベントが来ても `EvalLevel` が変化しない（凍結される）仕様が意図通りか

## 7. 未確定事項

- `highThreshold`（緑/黄の境界）の具体的な数値。サーバー配信の対象外のため Unity 側の設定値にする
- ~~`midThreshold`（黄/赤の境界）~~ → **Proto v0.3.0 で解決**。`MatchState.StormThresholdPct` を使う（[SV-20](../../../../docs/server-sync/02-パラメータと閾値.md#sv-20)）
- 脱落直後の演出継続時間と、その間の `EvalLevel` の扱い（表示し続けるか隠すか）
