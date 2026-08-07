# 08-GameBeforeView（試合開始前の待機）

> 参照する上流：[01-matchmaking-flow.md](../matchmaking/01-matchmaking-flow.md) §6（`MatchStart`）／[02-scene-composition.md](../foundation/02-scene-composition.md) §3（`InMatch` → `MainGame`）／[01-renderer.md](./01-renderer.md)（振り分け）。矛盾したら上流優先。

`MainGame` シーンへ移ってから実際に打ち始めるまでの数秒を、画面上で明示するための待機表示。

## 1. 責務

- カウントダウンの数値を `GameBefore/CountDownPanel/CountText` に出す
- 待機が明けるまで**お題と客の行列を出させない**（`Renderer` に待機中であることを知らせる）
- **しない**こと：
  - 試合開始の**決定**（サーバー権威。`MatchStart` 受信＝`ClientPhase.InMatch` 到達が唯一の合図）
  - 我慢ゲージの停止・客の到着の抑止。**サーバー側の進行は待たない**（§5）

## 2. 公開インターフェース

```csharp
namespace Takoda99.View
{
    public sealed class GameBeforeView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countText;      // CountDownPanel/CountText
        [SerializeField] private float countdownSeconds = 5f;

        /// <summary>まだ待機中か。true の間、お題と客の行列は出さない。</summary>
        public bool IsHolding { get; }

        /// <summary>待機が明けた瞬間に1度だけ発火する。</summary>
        public event Action Finished;

        /// <summary>カウントダウンを開始する（Renderer の結線時）。</summary>
        public void Begin();

        /// <summary>サーバーの試合開始（InMatch 到達）を伝える。</summary>
        public void SetMatchStarted(bool started);
    }
}
```

## 3. Unity構成

```
GameBefore                    ← GameBeforeView（ルートのCanvas）
└── CountDownPanel
    ├── Panel
    ├── Text (TMP)            （「まもなく開店」等の固定文言。触らない）
    └── CountText             ← カウントダウンの数値
```

- `Begin()` が `gameObject.SetActive(true)` を行うため、シーン上で非アクティブに置いてあってもよい
- 畳むときは `gameObject.SetActive(false)`。以降 `Update` は回らない

## 4. ふるまいの詳細

**カウントダウンが 0 になっただけでは畳まない。サーバーの合図と AND を取る。**

| ローカルの残り | `MatchStart` 受信 | 表示 |
|---|---|---|
| > 0 | 未 / 済 | 待機画面（数値をカウントダウン） |
| ≦ 0 | 未 | 待機画面（0 のまま留まる） |
| ≦ 0 | 済 | 畳む → `Finished` 発火 |

実際には `MainGame` シーンは `ClientPhase.InMatch` への遷移で初めてロードされる（[02-scene-composition.md](../foundation/02-scene-composition.md) §3）ため、通常は**シーンが出た時点で既にサーバーの合図は済んでいる**。それでも AND を取るのは、**シーンから直接再生した場合や、観戦・再接続でこの画面に入った場合に、合図が無いまま打ち始める画面を作らない**ため。片方だけの条件にすると、この事故が黙って通る。

`Finished` は `Renderer` が購読する。**数え終わる瞬間は `Store` の変化と一致しない**（サーバーは何も送らない）ため、これを起点に現在の `state` をそのまま描き直す。

## 5. 待機中に止めるもの・止めないもの

| | 待機中 |
|---|---|
| お題単語（`MainStoreView.SetWord`） | 空にする |
| 注文カウンタ | `0/0` |
| 客の行列（`CustomerQueueView`） | 描かない |
| 注文吹き出し・我慢ゲージ | 出さない（`ApplyServingCustomer` ごと止める） |
| 信用ライフ・評価・星・順位バー・他店盤面・屋号 | **通常どおり描く**（試合前から見えていてよい） |

**`ClientState` 側は止めない。** サーバーが待たずに `CustomerArrived` を送ってきても `state.Queue` には溜まり続け、明けた瞬間に `Finished` → 描き直しで一気に現れる。

> ⚠ この場合、待機中に届いた客の我慢ゲージは**その客の到着時刻を起点に**開始する（すでに目減りした状態で現れる）。サーバー側の我慢は待機に関係なく進んでいるため、ここで起点をずらして「満タンから」に見せるのは実態と食い違う。**見た目を合わせるためにサーバー権威の値を補正しない**（[01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）。

## 6. 依存関係

- 依存する `pureC#` モジュール：なし（`Renderer` から `bool` を受けるだけ）
- 依存されるモジュール：`Renderer`（`Begin` / `SetMatchStarted` / `Finished`）

## 7. Inspector 配線チェックリスト

| コンポーネント | フィールド | 割り当て |
|---|---|---|
| `GameBefore` の `GameBeforeView` | `countText` | `GameBefore/CountDownPanel/CountText` |
| 同上 | `countdownSeconds` | 5 |
| `Render ` の `Renderer` | `gameBefore` | `GameBefore` |

## 8. テスト・確認観点

`UnityEngine` 依存のため xUnit では検証できない。Unity Editor 実行で確認する。

- シーンに入ってから 5 → 1 と数え、0 で待機画面が畳まれるか
- 畳まれた瞬間にお題が出て、待機中に届いていた客が行列に現れるか
- 待機中はお題・行列・我慢ゲージが出ず、他店盤面や順位バーは動いているか
- `gameBefore` を未割り当てにしたとき、待機なしで従来どおり動くか（結線漏れで画面が止まらないこと）

## 9. 未確定事項

- 待機の 5 秒という値。サーバー側に「開始までの猶予」を配る契約は無く、クライアントの見た目の都合で決めている。サーバーが実際に客を送り始めるタイミングとズレた場合の擦り合わせは未検証
- 待機中に届いた客の我慢ゲージが目減りして見える件（§5 の ⚠）。サーバーが猶予を持っているなら実害は無いが、確認できていない
- 観戦・再接続でこの画面に入ったときに待機を出すべきか（現状は出る）
