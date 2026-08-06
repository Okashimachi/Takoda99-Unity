# 11-MainGameViewSampleDriver

> 参照する上流：なし（**開発用の足場**であり、上流の仕様を実装するモジュールではない）。ただし [01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md) の「クライアントに経営ロジックを持たせない」に触れないこと。

`WebGLNetworkClient` / `Dispatcher` / `Store` の配線が済むまでの間、主画面・小画面のViewを**サンプルデータで駆動して見た目を確認する**ための開発用コンポーネント。

## 1. 責務

- あらかじめ用意した固定のサンプル値を View（`MainStoreView` / `TakoyakiStandView` / `SubStoreBoardView`）へ流し込む
- Inspector とキーボードから値を手動で動かし、状態遷移を目視確認できるようにする
- **しない**こと：
  - サーバーの挙動を模した**シミュレーション**（評価の計算・客の分配・脱落判定は書かない。これは経営ロジックであり禁止）
  - リリースビルドへの混入。**このコンポーネントは本番シーンに残さない**

> サンプル値はあくまで「Viewに与える入力」であって、ゲームルールの再現ではない。値の遷移は**時間で自動に進めず、手動操作で進める**ことで、ロジックを持ち込まないことを構造的に担保する。

## 2. 公開インターフェース

```csharp
namespace Takoda99.View.Sample
{
    /// <summary>開発用。サンプル値で主画面・小画面のViewを駆動する。本番シーンには置かない。</summary>
    public sealed class MainGameViewSampleDriver : MonoBehaviour
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private TakoyakiStandView takoyakiStand;
        [SerializeField] private SubStoreBoardView subStoreBoard;

        [Header("自店のサンプル値")]
        [SerializeField, Range(0, 3)] private int creditLife = 3;
        [SerializeField, Range(0f, 1f)] private float evalNormalized = 0.5f;
        [SerializeField] private bool alive = true;
        [SerializeField] private string sampleHiragana = "たこやき";
        [SerializeField] private string sampleRoma = "takoyaki";
        [SerializeField] private int typedHiraganaLength;
        [SerializeField] private int typedRomaLength;
        [SerializeField] private int typedWordCount;

        [Header("他店のサンプル値")]
        [SerializeField] private string selfStoreId = "50";
        [SerializeField] private int eliminateStoreCount;

        private void OnValidate();   // Inspector で値を変えたら即座に View へ反映する
    }
}
```

## 3. Unity構成

- `root` 直下に空のGameObjectを作り、本コンポーネントをアタッチする。`MainStore` / `Takoyakis` / `SubStoreCanvas` を Inspector で参照する
- `Start`：
  - `selfStoreId` を除く `"1".."99"` の98件を `SubStoreBoardView.Bind` に渡す
  - 自店のサンプル値を全 View へ一度流す
- `Update`：キーボード（Input System）で以下を操作する

| キー | 動作 |
|---|---|
| `1` / `2` | `creditLife` を −1 / +1 |
| `3` / `4` | `evalNormalized` を −0.1 / +0.1 |
| `5` / `6` | `typedWordCount` を −1 / +1 |
| `7` / `8` | `typedRomaLength` を −1 / +1（`typedHiraganaLength` は比率で連動させる） |
| `9` | 他店を1つ脱落させる（`StoreId` の小さい順） |
| `0` | 全サンプル値を初期状態に戻す |

- `OnValidate` でも同じ反映処理を呼び、Play中にInspectorのスライダーを動かして確認できるようにする

## 4. ふるまいの詳細

- 他店の初期値は「全店 `creditLife = 3` / `alive = true`」。`9` キーで押した回数ぶんだけ `StoreId` 昇順に `alive = false` を送り、脱落演出（`life0` → 3秒 → 順位表示）を確認する
- 脱落時に渡す順位は「98 − これまでの脱落数 + 1」という**サンプル専用の仮値**。[SV-15](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-15) が確定するまでの表示確認用であり、この計算式を本番コードへ持ち込まない
- `typedHiraganaLength` / `typedRomaLength` はクランプのみ行う（打鍵判定はしない）
- サンプル値は Inspector 上の値がそのまま View へ渡る。View 側でのクランプ・エッジケース処理が働くことを確認する目的も兼ねる

## 5. 依存関係

- 依存する `pureC#` モジュール：なし
- 依存するUnity側モジュール：`MainStoreView`（[08](./02-main-store-view.md)）／`TakoyakiStandView`（[09](./03-takoyaki-stand-view.md)）／`SubStoreBoardView`（[10](./04-sub-store-board-view.md)）
- 依存されるモジュール：なし

## 6. テスト・確認観点

- 各仕様書の「テスト・確認観点」を、このドライバのキー操作だけで一通り再現できるか
- 本コンポーネントを外しても、View 側が例外を出さずに既定表示のまま動くか

## 7. 未確定事項

- 実データ配線（`Renderer`／[01-renderer.md](./01-renderer.md) 未作成）が入った時点で本コンポーネントを削除するか、デバッグパネル（[03-debug-panel.md](../platform/03-debug-panel.md) 未作成）へ統合するか
