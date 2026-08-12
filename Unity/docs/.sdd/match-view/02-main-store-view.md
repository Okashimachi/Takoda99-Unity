# 08-MainStoreView

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`Renderer`）／[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)（`Store` / `Credit` / `CreditLife` / `Evaluation` / `Word`）／[Takoda99-Proto]()（`CreditUpdate` / `EvaluationUpdate` / `WordAssigned`）。矛盾したら上流優先。

`01-renderer.md`（未作成）の `Renderer` を構成する下位Viewのうち、**主画面の自店舗（`root/MainStoreCanvas/Main/MainStore`）**を担当する。

## 1. 責務

- 主画面の自店舗まわりの見た目（暖簾・屋台土台・お題単語パネル・提灯・鉄板）を**1つのクラスで一括管理**する
- 上位（`Renderer`／サンプルデータ供給）から受け取った表示用状態を、`Image` のスプライト差し替え・`TextMeshProUGUI` のテキスト差し替えに変換する
- `Takoyakis`（[03-takoyaki-stand-view.md](./03-takoyaki-stand-view.md)）が購読できるよう、**評価3段階（`StoreEvalLevel`）の変化を通知**する
- **しない**こと：
  - 信用ライフ・評価・お題単語の**決定**（すべてサーバー権威。受け取って描くだけ）
  - 打鍵判定（`TypingJudge` の責務。入力済み文字数を受け取るだけ）
  - `Resources.Load` 等によるパス直書きでのスプライト取得（全て `[SerializeField]`）

### 責務を分割しない理由

暖簾・土台・お題・提灯・鉄板はいずれも「1〜数個の `Image`/`Text` を状態で差し替える」だけで、個別に MonoBehaviour を立てるほどの情報量が無い。1クラスに集約し、Inspector で `MainStore` に全参照をアタッチする。

## 2. 公開インターフェース

```csharp
namespace Takoda99.View
{
    /// <summary>主画面の自店舗（root/MainStoreCanvas/Main/MainStore）の表示を一括管理する。</summary>
    public sealed class MainStoreView : MonoBehaviour
    {
        // ---- 信用ライフ（暖簾・屋台土台・提灯） ----
        [SerializeField] private Image noren;
        [SerializeField] private Sprite norenLife1;      // stall_noren_life1
        [SerializeField] private Sprite norenLife2;      // stall_noren_life2
        [SerializeField] private Sprite norenLife3;      // stall_noren_life3

        [SerializeField] private Image stand;
        [SerializeField] private Sprite standLife0;      // stall_booth_life0
        [SerializeField] private Sprite standLife1;
        [SerializeField] private Sprite standLife2;
        [SerializeField] private Sprite standLife3;

        [SerializeField] private Image[] lanterns;       // 添字 0 = Lantern1, 1 = Lantern2, 2 = Lantern3
        [SerializeField] private Sprite lanternOn;       // stall_lantern_on
        [SerializeField] private Sprite lanternOff;      // stall_lantern_off

        // ---- 評価（鉄板） ----
        [SerializeField] private Image griddle;
        [SerializeField] private Sprite griddleNormal;   // stall_griddle_normal
        [SerializeField] private Sprite griddleHot;      // stall_griddle_hot

        // ---- お題単語 ----
        [SerializeField] private TextMeshProUGUI wordHiragana;   // WordPanel/Hiragana
        [SerializeField] private TextMeshProUGUI wordRoma;       // WordPanel/Roma
        [SerializeField, Range(0f, 1f)] private float typedAlpha = 0.35f;

        /// <summary>評価3段階が変化したときに発火する。Takoyakis が購読する。</summary>
        public event Action<StoreEvalLevel> EvalLevelChanged;

        /// <summary>現在の評価3段階。購読開始直後の初期化に使う。</summary>
        public StoreEvalLevel EvalLevel { get; private set; }

        /// <summary>信用ライフ（提灯・暖簾・屋台土台）を反映する。</summary>
        public void SetCreditLife(int creditLife);

        /// <summary>評価を反映する。evalNormalized は 0..1（生存店内パーセンタイル）。</summary>
        public void SetEvaluation(double evalNormalized, bool alive);

        /// <summary>お題単語を差し替える。typedRomaLength = 0 の未入力状態にリセットされる。</summary>
        public void SetWord(string hiragana, string roma);

        /// <summary>入力進捗を反映する。引数はいずれも「確定した先頭からの文字数」。</summary>
        public void SetTypedProgress(int typedHiraganaLength, int typedRomaLength);
    }
}
```

## 3. Unity構成

### 3.1 シーン階層（本仕様書の適用後）

```
root/MainStoreCanvas/Main/MainStore   ← MainStoreView をアタッチ
├── Noren        (Image)
├── Stand        (Image)
├── Griddle      (Image)
├── WordPanel
│   ├── Hiragana (TextMeshProUGUI)
│   └── Roma     (TextMeshProUGUI)
├── Lanterns                          ← Lantans から改名
│   ├── Lantern1 (Image)              ← Lantan1 から改名
│   ├── Lantern2 (Image)
│   └── Lantern3 (Image)
└── Takoyakis                         ← 03-takoyaki-stand-view.md
```

### 3.2 リネーム作業（本仕様書に含む）

| 対象 | 変更前 | 変更後 |
|---|---|---|
| シーンのGameObject | `Lantans` | `Lanterns` |
| シーンのGameObject | `Lantan1` / `Lantan2` / `Lantan3` | `Lantern1` / `Lantern2` / `Lantern3` |
| Prefabアセット | `Assets/Prefabs/MainStoreCanvas/Lantan1.prefab` | `Lantern1.prefab` |
| シーンのGameObject | `Takoyakis/ForceLine` | `Takoyakis/FourthLine` |

Prefabのリネームは `.meta` の GUID を保持したままファイル名と `m_Name` を変更する（参照が切れないこと）。

### 3.3 MonoBehaviour のライフサイクル

- `Awake`：参照の null チェックに続けて、`SetCreditLife(初期ライフ)` / `SetEvaluation(0, true)` / `SetWord("", "")` 相当の既定表示への初期化も**ここで**行う（**`Start` ではない**）。Unity は同フレーム内で全オブジェクトの `Awake` を終えてから `OnEnable` を呼ぶため、`Renderer.OnEnable`（`Bind` → 初回 `HandleStateChanged`）より確実に先に走る。`Start` に置くと、`Renderer.OnEnable` が先に本物の値を描いた後で既定値に上書きしてしまう事故が起こる（実際に発生した不具合）
- `Update`：**使わない**。表示は全て上記の公開メソッド呼び出しで駆動する
- `OnDestroy`：`EvalLevelChanged` の購読解除は購読側（`TakoyakiStandView`）の責務

### 3.4 Inspector 公開値

- 上記 `[SerializeField]` 全て（スプライトは `Assets/Images/stall/` から手動アタッチ）
- `typedAlpha`（入力済み文字の薄さ。既定 0.35）

### 3.5 使用するUnityパッケージ

- TextMeshPro（`Hiragana` / `Roma`）。リッチテキストの `<alpha>` タグを使うため、レガシー `Text` ではなく TMP を使う

## 4. ふるまいの詳細

### 4.1 信用ライフ（`SetCreditLife`）

`creditLife` を `0..initialLife` にクランプしたうえで：

| `creditLife` | 暖簾 | 屋台土台 | 提灯 |
|---|---|---|---|
| 3以上 | `norenLife3` 表示 | `standLife3` | 3つ点灯 |
| 2 | `norenLife2` 表示 | `standLife2` | `Lantern3` のみ消灯 |
| 1 | `norenLife1` 表示 | `standLife1` | `Lantern1` のみ点灯 |
| 0 | **`noren.enabled = false`（非表示）** | `standLife0` | 3つとも消灯 |

- 暖簾に `life0` の画像は存在しないため、ライフ0では `Image` を無効化して非表示にする。1以上に戻ったら再度有効化する（下位淘汰の予告演出等で戻る可能性を残す）
- 提灯は**番号の大きい方から消灯**する。`Lantern{i+1}` は `i < creditLife` のとき `lanternOn`、そうでなければ `lanternOff`。**GameObject の破棄・非アクティブ化は行わない**（[04-credit-life-lantern-state.md](../value-objects/04-credit-life-lantern-state.md) の `CreditLifeLanternState.From` をそのまま使う）
- `lanterns` の要素数が `creditLife` の上限より少ない場合も配列長でループし、配列外参照しない

### 4.2 評価（`SetEvaluation`）

- `StoreVisualState.From(storeId, evalNormalized, alive, StoreEvalThresholds.Default, 直前の値)` で3段階へ分類する（[01-store-visual-state.md](../value-objects/01-store-visual-state.md)）
- 鉄板：`EvalLevel == High` → `griddleHot`、`Mid` / `Low` → `griddleNormal`
- 分類結果が**前回と変わったときだけ** `EvalLevelChanged` を発火する（毎フレーム発火させない）
- `alive == false` のときは `StoreVisualState` の仕様通り `EvalLevel` を凍結する。鉄板の見た目も凍結される

### 4.3 お題単語（`SetWord` / `SetTypedProgress`）

- `SetWord` は `Hiragana` にひらがな、`Roma` にローマ字を設定し、入力進捗を 0 にリセットする
- `SetTypedProgress` は、**先頭から `typed*Length` 文字ぶんを薄く**表示する。実装は TMP のリッチテキストで、文字列を分割して前半だけ `<alpha=#XX>` を付ける：

```
表示文字列 = $"<alpha=#{alphaHex}>{原文.Substring(0, typedLength)}<alpha=#FF>{原文.Substring(typedLength)}"
```

- `alphaHex` は `typedAlpha` を 0..255 の16進2桁へ変換した値
- ひらがなとローマ字は**進捗の粒度が違う**（「し」に対し `si` / `shi` 等の複数受理があるため、ローマ字の確定文字数からひらがなの確定文字数は一意に決まらない）。そのため引数を2つに分け、それぞれの確定済み文字数を `TypingJudge` から受け取る
- エッジケース：
  - `typedLength <= 0` → 全文を通常色で表示（タグを付けない）
  - `typedLength >= 原文.Length` → 全文を薄く表示
  - 原文が `null` / 空 → 空文字を設定し、例外を投げない
- 原文に `<` が含まれる場合、TMP がタグとして解釈しうる。お題単語はひらがな・ローマ字のみの想定だが、防御的に `<` を `<noparse>` で包まず**そのまま渡す**（お題にタグ文字が来た時点で上流の不正データであり、View 側では検知しない）

## 5. 依存関係

- 依存する `pureC#` モジュール：なし（値は素の `int` / `double` / `string` で受け取る。[01-purecs-dll-reference.md](../foundation/01-purecs-dll-reference.md) の解決を待たない）
- 依存するUnity側モジュール：`Takoda99.View.ValueObjects`（`StoreVisualState` / `CreditLifeLanternState`）
- 依存されるモジュール：`TakoyakiStandView`（`EvalLevelChanged` を購読）、`Renderer`（未作成）、`MainGameViewSampleDriver`（[06-view-sample-data.md](./06-view-sample-data.md)）
- `Renderer` に依存してよいモジュールは無い（Client-Docs 第3章）ため、`MainStoreView` から `Store` や `Dispatcher` を直接参照しない

## 6. テスト・確認観点

[06-view-sample-data.md](./06-view-sample-data.md) のサンプル駆動でエディタ実行して確認する。

- 信用ライフ 3→2→1→0 で、暖簾・屋台土台・提灯が同時に切り替わり、ライフ0で暖簾だけが消えるか
- 提灯が**番号の大きい方から**消え、GameObject が破棄されていないか（Hierarchy に残っているか）
- `evalNormalized` を 0→1 へ動かしたとき、鉄板が `High` の帯でだけ `hot` になるか
- 評価が同じ帯に留まる間、`EvalLevelChanged` が発火しないか
- お題単語の入力進捗を 0→全長 まで動かしたとき、ひらがな・ローマ字それぞれの先頭から薄くなるか

## 7. 未確定事項

- 信用ライフの上限（`initialLife`）が3以外のときの提灯・暖簾（[SV-22](../../../../docs/server-sync/02-パラメータと閾値.md#sv-22)）
- 評価3段階の閾値（[SV-20](../../../../docs/server-sync/02-パラメータと閾値.md#sv-20)）。現状は `StoreEvalThresholds.Default`（High: 2/3, Mid: 1/3）
- `TypingJudge` から「ひらがなの確定文字数」を取得する API の有無（無ければ受理済みローマ字列から逆算する必要がある）
- 評価が `Low` の間の画面端アラート演出（[01-store-visual-state.md](../value-objects/01-store-visual-state.md) §5 の未確定演出）。本仕様書には含めない
