# 06-SubStoreTileState

> 参照する上流：[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md)（`StoreSummaryState`）、用語集5章（`Credit` / `CreditLife`）・9章（`StoreEliminated`）。矛盾したら上流優先。

## 1. 責務

- 99店ミニ盤面の1マス（他店1店舗）の見た目区分を、**信用ライフ（`CreditLife`）と生存状態**から導出する
- 「脱落直後（一定時間、`life0` の見た目を出す）」と「完全脱落（見た目を消して順位を出す）」を**別の区分**として持つ
- **しない**こと：脱落の判定（`StoreEliminated` がサーバー権威）／順位の算出（サーバーが `StoreSummary.finalRank` で配信する。[SV-15](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-15) 参照）

> [01-store-visual-state.md](./01-store-visual-state.md) の `StoreVisualState`（評価3段階）とは**別の指標**である。ミニ盤面のタイル画像 `minitile_booth_life*` は**信用ライフ**を表す。評価3段階はセルの色分け・主画面のアラートに使う。両者を混同しないこと（[03-決定ログ.md](../../../../docs/server-sync/03-決定ログ.md) の D-06）。

## 2. データ定義

```csharp
public enum SubStoreTileState
{
    Life3,           // CreditLife >= 3
    Life2,           // CreditLife == 2
    Life1,           // CreditLife == 1
    JustEliminated,  // Alive == false になった直後（既定 3.0 秒）。life0 の見た目
    Eliminated,      // 上記の経過後。屋台の見た目を消し、順位を表示する
}
```

- `CreditLife == 0` かつ `Alive == true` という状態は、区分上 `JustEliminated` と同じ `life0` の見た目にする（サーバーが `StoreEliminated` を送るまでの空白を埋めるため）
- `initialLife` が3以外になった場合、`Life3` は「上限ライフ」を意味する区分として扱う（画像は3枚しか無いため、4以上は `Life3` にクランプする）

## 3. 変換処理

入力：`StoreSummaryState`（`CreditLife: int`, `Alive: bool`, `FinalRank: int?`）と、Viewローカルの経過時間

```
if (Alive):
    CreditLife >= 3 → Life3
    CreditLife == 2 → Life2
    CreditLife == 1 → Life1
    CreditLife <= 0 → JustEliminated（life0 の見た目。脱落確定待ち）
else:
    Alive が false へ変化してからの経過時間 < EliminationRevealDelay(3.0秒) → JustEliminated
    それ以降                                                              → Eliminated
```

- **経過時間の計測は純粋関数ではない**ため、変換関数自体は `(CreditLife, Alive, elapsedSinceEliminatedSec)` の3値を受け取る純粋関数とし、経過時間の保持は View 側（`SubStoreTileView`）が行う（[README §1](./README.md) の「Viewローカルの一時演出状態はここに含めない」に従う）
- `EliminationRevealDelay` の既定値は **3.0 秒**。View 側の Inspector で差し替え可能にする

### 順位テキストの入力（`FinalRank`）

`Eliminated` で表示する順位は **`StoreSummary.finalRank`（Proto v0.3.0）** をそのまま使う。`StoreEliminated.finalRank` でも同じ値が届くため、どちらから来ても結果は一致する。

> **`FinalRank` は `int?` であり、欠落を 0 として扱ってはいけない。** 順位0は存在しない。Proto 側は3言語とも「キーごと出さない」実装で揃えてあり、`null` は「まだ脱落していない」を意味する。0 に潰すと「0位の店」が盤面に出て表示が壊れる。
>
> `Eliminated` かつ `FinalRank == null` は本来起き得ない組み合わせだが、`StoreListUpdate` の取りこぼし等で一時的に発生し得る。その場合は**順位テキストを空にして屋台だけ消す**（推測で埋めない）。

## 4. Unity構成

- `SubStorePanel` Prefab の `SubStore`（`Image`）が `Life3`/`Life2`/`Life1`/`JustEliminated` に対応する `minitile_booth_life3/2/1/0` を表示する
- `Eliminated` では `SubStore` の `Image` を非表示にし、`SubStorePanel/SubStoreRankPanel`（既定で非アクティブ）を有効化して順位テキストを表示する
- スプライト4枚は View 側の `[SerializeField]` で持ち、`Resources.Load` 等のパス直書きは行わない

## 5. 未確定な演出との境界

- ここまで：5区分と、それぞれがいつ成立するか。`EliminationRevealDelay` の既定値
- ここから先（未確定）：脱落時のアニメーション（潰れる・煙が出る等）、順位テキストの出現演出

## 6. テスト観点

- `CreditLife` を 3→2→1→0 と変化させたとき、対応するスプライトへ切り替わるか
- `Alive` が `false` になってから 3 秒後に `Eliminated` へ遷移し、屋台が消えて順位が出るか
- `Alive == false` かつ `CreditLife > 0`（サーバーが下位淘汰で落とした場合）でも `JustEliminated` から始まるか
- 一度 `Eliminated` になった店に `StoreListUpdate` が来ても `Life*` へ戻らないか
- `FinalRank == null` の店の順位テキストが空になり、**0 と表示されない**こと
- `FinalRank` が受信値のまま表示され、受信順等から再構成されていないこと

## 7. 未確定事項

- ~~他店の脱落順位の入手手段~~ → **Proto v0.3.0 で解決**（`StoreSummary.finalRank`）。サンプル値限定の制約は解除済み（[SV-15](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-15)）
- `initialLife` が3以外の場合のタイル画像（[SV-22](../../../../docs/server-sync/02-パラメータと閾値.md#sv-22) と同根）
