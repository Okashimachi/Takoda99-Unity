# 09-TakoyakiStandView / TakoyakiSlotView

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`Renderer`）／[用語集](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)（`Order` / `Serve` / `Evaluation`）。値の形は [value-objects/03-takoyaki-stand-state.md](./value-objects/03-takoyaki-stand-state.md) が正典。

主画面のたこ焼き台（`root/MainStoreCanvas/Main/MainStore/Takoyakis`）の描画。

## 1. 責務

### `TakoyakiSlotView`（`Takoyaki` Prefab）

- 穴1つぶんの3状態（`Empty` / `Batter` / `Cooked`）を、Prefab内の2枚のパネルの表示・非表示に変換する
- **しない**こと：自分がどの状態になるべきかの判断（`TakoyakiStandView` が決める）

### `TakoyakiStandView`（`Takoyakis`）

- 24個の `TakoyakiSlotView` を行単位でまとめて保持し、`TakoyakiStandState` を各穴へ配る
- `MainStoreView.EvalLevelChanged` を**購読**して評価3段階を取得し、生地を流すマス数を切り替える
- **しない**こと：評価そのものの保持（`MainStoreView` を介して参照する）／提供の確定

## 2. 公開インターフェース

```csharp
namespace Takoda99.View
{
    /// <summary>たこ焼き1個ぶんの穴の見た目。Assets/Prefabs/MainStoreCanvas/Takoyaki.prefab にアタッチする。</summary>
    public sealed class TakoyakiSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject raw;   // TakoyakiRaw（生地）
        [SerializeField] private GameObject done;  // TakoyakiDone（焼き）

        public TakoyakiSlotState State { get; private set; }

        public void SetState(TakoyakiSlotState state);
    }

    /// <summary>
    /// たこ焼き台全体（6列×4行＝24穴）。root/.../MainStore/Takoyakis にアタッチする。
    /// <c>slots</c> / <c>mainStore</c> は Inspector で手動配線しない。Awake で自身の子階層（行オブジェクト×4、
    /// 各6個の TakoyakiSlotView）と親階層の MainStoreView を自動収集する。Takoyakis への参照だけで
    /// 全24穴を操作できるようにするための設計（手動での24要素配列アタッチを避ける）。
    /// </summary>
    public sealed class TakoyakiStandView : MonoBehaviour
    {
        /// <summary>いま対応中の客のノルマのうち、入力を終えた語数。</summary>
        public void SetTypedWordCount(int typedWordCount);
    }
}
```

## 3. Unity構成

### 3.1 シーン階層

```
Takoyakis                 ← TakoyakiStandView
├── FirstLine             ← Takoyaki ×6（index 0..5）
├── SecondLine            ← Takoyaki ×6（index 6..11）
├── ThirdLine             ← Takoyaki ×6（index 12..17）
└── FourthLine            ← Takoyaki ×6（index 18..23）   ※ ForceLine から改名
```

- 行を分けているのは座標・サイズを行単位で調整しやすくするため。**論理上は index 0..23 の1次元配列**として扱い、`slots` には行順（1行目左から）に24個をアタッチする
- `Takoyaki` Prefab は `TakoyakiRaw`（生地）と `TakoyakiDone`（焼き）の2パネルを持つ。`TakoyakiSlotView` は Prefab 側にアタッチし、シーンの24インスタンスへ自動的に行き渡らせる

### 3.2 MonoBehaviour のライフサイクル

`TakoyakiSlotView`
- `Awake`：`raw` / `done` の null チェック
- `Start`：`SetState(Empty)` で全非表示に初期化する

`TakoyakiStandView`
- `Awake`：`transform` の直接の子（行オブジェクト、順不同で数は問わない）を上から順に走査し、各行の子に付いた `TakoyakiSlotView` を左から順に集めて `slots` を構築する。`GetComponentInParent<MainStoreView>()` で `mainStore` を取得する。`slots.Length != TakoyakiStandState.StandCapacity`(24) や `mainStore == null` の場合は `Debug.LogError`
- `OnEnable`：`mainStore.EvalLevelChanged += OnEvalLevelChanged` を登録し、`mainStore.EvalLevel` で初期化する
- `OnDisable`：購読を解除する
- `Update`：**使わない**

### 3.3 Inspector 公開値

- `TakoyakiSlotView`：`raw` / `done`
- `TakoyakiStandView`：**なし**。`slots` は `Takoyakis` の子階層から、`mainStore` は親階層から実行時に自動収集するため、Inspector での手動配線は不要（シーン構成が本仕様書 §3.1 の階層と一致していれば `Takoyakis` に本コンポーネントをアタッチするだけでよい）

## 4. ふるまいの詳細

### 4.1 穴の見た目

| `TakoyakiSlotState` | `TakoyakiRaw` | `TakoyakiDone` |
|---|---|---|
| `Empty` | 非表示 | 非表示 |
| `Batter` | 表示 | 非表示 |
| `Cooked` | **非表示** | 表示 |

`Cooked` で生地パネルを消してから焼きパネルを出す（重ね表示にしない）。切り替えは `SetActive` で行う。

### 4.2 状態の配り方

`TakoyakiStandView` は、評価3段階と `typedWordCount` から `TakoyakiStandState.From(evalLevel, typedWordCount)` を作り、`slots[i].SetState(state.Slots[i])` を24個ぶん適用する。

- 生地マス数：`Low` → 12（1〜2行目）／`Mid` → 18（1〜3行目）／`High` → 24（全マス）
- 焼きマス数：`min(typedWordCount, 生地マス数)`。**「客のノルマ数を入力し終えて提供するまでの間に、入力完了した分を焼く」**という要求を、先頭からの累積で表す
- 提供が成立して次の客に移ったら、上位から `SetTypedWordCount(0)` が呼ばれ、焼きマスが `Batter` へ戻る（`Empty` には戻らない）
- `EvalLevelChanged` と `SetTypedWordCount` のどちらの経路でも、**24穴すべてを再適用**する（差分更新はしない。24個なのでコストは無視できる）
- エッジケース：`typedWordCount` が負 → 0 にクランプ。生地マス数を超える → 生地マス数にクランプ

## 5. 依存関係

- 依存する `pureC#` モジュール：なし
- 依存するUnity側モジュール：`MainStoreView`（[08](./08-main-store-view.md)）、`Takoda99.View.ValueObjects.TakoyakiStandState`
- 依存されるモジュール：`Renderer`（未作成）、`MainGameViewSampleDriver`（[11](./11-view-sample-data.md)）

## 6. テスト・確認観点

- 評価を Low→Mid→High と動かしたとき、生地が **2行→3行→4行** と行単位で増えるか（3段階が目視で判別できるか）
- 評価を下げたとき、はみ出した行が `Empty`（生地も焼きも非表示）に戻るか
- `typedWordCount` を増やすと、左上から順に生地→焼きへ切り替わるか
- `typedWordCount` を 0 に戻したとき、焼きマスが `Batter` に戻り `Empty` にならないか
- `Takoyaki` Prefab を編集したとき、24インスタンス全てに反映されるか（Prefab側にスクリプトを置けているか）

## 7. 未確定事項

- 生地マス数 12 / 18 / 24 の実値（[value-objects/03](./value-objects/03-takoyaki-stand-state.md) §7）
- 焼き上がりのアニメーション・提供時の消失演出
