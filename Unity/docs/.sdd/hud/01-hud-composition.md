# 01-HUDの構成と `Renderer` の振り分け

> 参照する上流：[12_差分_クライアント §2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)（UI表示要素の差分・★中核）／[本選企画書 3.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／`pureC#` [result/02-lifecycle-and-renderer.md](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)。矛盾したら上流優先。既存 [match-view/01-renderer.md](../match-view/01-renderer.md) と矛盾する場合は**本書が優先**。

## 1. 責務

**する**

- `IStore` の `ClientState` を購読し、本選のHUD要素へ値を配る
- `IRenderer` の新しいコールバックを下位Viewへ振り分ける
- 予選の表示要素（信用ゲージ・我慢ゲージ・星・99店ミニ盤面・劣化演出）を**画面から消す**

**しない**

- 値の決定・推定をしない（**受信値を描くだけ**。予選から変わらない原則）
- スコアから順位を計算しない（`state.Rank` が権威）
- `Renderer` に演出のロジックを持たせない（下位Viewへ委譲する既存方針を維持）

## 2. 画面に出るもの／出ないもの

| 要素 | 予選 | 本選 | 供給元 |
|---|---|---|---|
| お題単語 | あり | **維持・大型化・演出強化**（[02](./02-order-word-emphasis.md)） | `typingJudge.CurrentView` |
| 注文進捗 `x/N` | あり | **維持** | `state.CurrentOrder.WordIndex` / 先頭客の `OrderCount` |
| 客の行列 | ゲーム要素 | **演出として維持**（見た目は変えない） | `state.Queue` |
| 屋号（自店の表示名） | あり | **維持** | `state.DisplayNames[state.SelfStoreId]` |
| **自分の現在順位** | 小さく | **★主役。大きく表示** | `state.Rank` |
| **自分のスコア** | なし | **★新規。順位より小さく（補助）** | `state.Score` |
| 生存数 | あり | **維持** | `state.AliveCount` |
| **上位ランキング＋自分** | なし（98人分の体力だった） | **★新規** | [../ranking-view/01](../ranking-view/01-ranking-panel.md) |
| **次に脱落する人＋秒読み** | 予告時のみポップアップ | **★新規・常設UI** | [../ranking-view/02](../ranking-view/02-cull-countdown-panel.md) |
| フェーズ表示 | あり | 維持（優先度低） | `PhaseChange` |
| 火力表示 | あり | 維持（優先度低） | `DifficultyUpdate` |
| 信用（ライフ）ゲージ・提灯 | あり | **撤去** | — |
| 我慢ゲージ | あり | **撤去** | — |
| 星評価 | あり | **撤去**（相対評価の廃止） | — |
| たこ焼きの劣化演出 | あり | **撤去** | — |
| 99店ミニ盤面 | あり | **撤去**（ランキング表示へ役割移譲） | — |

撤去の詳細と手順は [../cleanup/01-removed-views.md](../cleanup/01-removed-views.md)。

## 3. 配置の要件（レイアウトは拘束しない）

**具体的な座標・サイズはクライアント担当が決める。** 満たすべきは以下の「情報が導く行動」だけ（[12_差分_クライアント §2.3](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)）。

| プレイヤーの状況 | 目に入るべき情報 | 導かれる行動 |
|---|---|---|
| 上位 | 上位リストに自分がいる | 余裕がある。正確性でスコアを伸ばす |
| 下位 | 秒読みの対象に自分が入っている | 急ぐ。リスクを取る |
| 中位 | 両方が見える | 上を目指すか逃げ切るかを選ぶ＝**タイミングの判断が発生する** |

| # | 満たすこと |
|---|---|
| R1 | **打鍵中の視線（お題）から、順位・秒読みの両方が周辺視野に入る**こと |
| R2 | 自分の順位が**画面上で最も大きい数字**であること（お題の文字を除く） |
| R3 | スコアは順位より**明確に小さい**こと（主役は順位。企画書 3.8） |
| R4 | 上位リストは**10件を下回らない**こと（100秒以降は上位10名＝生存者全員になる。[12_差分_クライアント §2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)） |
| R5 | 縦画面のまま成立すること |

## 4. `Renderer` の構成

### 4.1 `SerializeField` の増減

```csharp
public sealed class Renderer : MonoBehaviour, IRenderer
{
    [SerializeField] private MainStoreView mainStore;
    [SerializeField] private TakoyakiStandView takoyakiStand;
    [SerializeField] private Customers.CustomerQueueView customerQueue;
    [SerializeField] private Customers.CustomerOrderBubbleView orderBubble;
    [SerializeField] private GameBeforeView gameBefore;
    [SerializeField] private EliminationResultView resultView;

    // ★追加
    [SerializeField] private SelfRankView selfRank;              // 順位の大表示＋スコア＋生存数
    [SerializeField] private RankingPanelView rankingPanel;      // ranking-view/01
    [SerializeField] private CullCountdownPanelView cullPanel;   // ranking-view/02
    [SerializeField] private MassEliminationEffect massElim;     // elimination/01

    // ★削除
    // [SerializeField] private SubStoreBoardView subStoreBoard;
    // [SerializeField] private PatienceTimer patienceTimer;
    // [SerializeField] private StarRatingView starRating;
    // [SerializeField] private RankBarView rankBar;
}
```

`Awake` の `WarnIfMissing` も同じ増減に合わせる。`resultView` の `LogError` 扱い（未割り当てだと試合から出られない）は**維持**。

### 4.2 `HandleStateChangedCore` の差分

**state 駆動で描くもの**（毎回の state 変化で呼ぶ）：

```csharp
// 自店
selfRank?.SetState(SelfRankViewState.From(state.Rank, state.Score, state.AliveCount));
mainStore?.SetPlayerName(ResolveSelfDisplayName(state));

// ランキング（上位N＋自分）
rankingPanel?.Apply(state);

// 足切り予告（秒読みの毎フレーム更新はパネル側の Update が行う。ここは受信値の差し替えのみ）
cullPanel?.SetWarning(state.Cull, state.Rank);
```

**削除する行**：

| 削除 | 理由 |
|---|---|
| `mainStore.SetCreditLife(state.CreditLife)` | フィールドごと消える |
| `mainStore.SetEvaluation(state.Normalized, state.Alive)` | 相対評価の廃止 |
| `starRating.SetRating(state.StarRating)` | 同上 |
| `subStoreBoard.*` のブロックまるごと | 99店ミニ盤面の撤去 |
| `rankBar.SetState(RankBarViewState.From(..., state.Params.StormThresholdPct))` | `StormThresholdPct` は Obsolete（0が届く） |

**維持する行**：`gameBefore.SetMatchStarted(...)` / `holding` による待機中の抑止 / `ApplyWord` / `ApplyOrderCounter` / `customerQueue.Apply(state, nowMs)`。

> **`front`（`state.Queue[0]`）を1回だけ引いて全要素へ配る**という既存の方針は維持する。我慢ゲージが消えるため `ApplyServingCustomer` は注文吹き出しの出し入れだけになる（`patienceTimer.Begin` の行を削除）。

### 4.3 表示名の解決

```csharp
/// <summary>自店の表示名。MatchStart のキャッシュから引く（StoreListUpdate は廃止された）。</summary>
private static string ResolveSelfDisplayName(ClientState state)
    => state.DisplayNames.TryGetValue(state.SelfStoreId, out var name) ? name : string.Empty;
```

既存の `FindSelfDisplayName`（`state.Stores` を線形探索）は削除する。

### 4.4 `IRenderer` の実装

| メソッド | Unity 実装 |
|---|---|
| `OnCustomerArrived` | 空のまま（検知は `HandleStateChanged` 側。既存方針） |
| `OnKeyFeedback` | 空のまま（打鍵演出を足すならここ） |
| `OnOrderServed` | `customerQueue.MarkServed(customerId)`（維持） |
| `OnPhaseChanged` | 背景・BGMの切り替え（優先度低） |
| `OnCullWarning(CullWarning)` | `cullPanel.OnWarningReceived(warning)`。**受信の瞬間だけ必要な演出**（自分が対象圏に入った瞬間のアラート等）に使う。値の描画は state 駆動側 |
| `OnStoreEliminatedBatch(stageIndex, entries, includesSelf)` | [../elimination/01](../elimination/01-mass-elimination-effect.md) |
| `OnPersonalResult(PersonalResultState)` | **何もしない。** 保持は `Store` の責務であり、画面に出すのは個人成績シーン（[../result-view/01](../result-view/01-personal-result-view.md)） |
| `OnMatchEnd()` | 行列・吹き出しを畳んでリザルトへ（[../result-view/02](../result-view/02-result-rank-tier.md)） |
| `OnLifecycleChanged` | 空のまま |
| `OnConnectionTrouble` | `Debug.LogWarning`（維持） |

**削除する実装**：`OnCustomerLeft` / `OnForcedEliminationWarning(int, double)` / `OnStoreEliminated(string, EliminationReason, int)` / `OnMatchEnd(int, MatchStats)`。

### 4.5 リザルトモーダルの表示契機

既存 `Renderer` は「`OnMatchEnd` が例外で呼ばれない事故」に備えて **state 駆動（`state.Phase == Result && state.Result != null`）を唯一の契機**にしている。**この方針は維持する。** 条件だけ差し替える：

```csharp
if (state.MatchEnded && resultView != null)
{
    var rank = state.PersonalResult?.FinalRank ?? 0;
    resultView.ShowIfHidden(rank);
}
```

| 変更点 | 理由 |
|---|---|
| `state.Result != null` → `state.MatchEnded` | `MatchEnd` が空ペイロードになり `Result` が消えたため |
| 順位は `state.PersonalResult?.FinalRank` から | `MatchEnd` が順位を運ばなくなったため |
| `PersonalResult` が `null` でも**モーダルを出す**（`rank = 0`） | **試合が終わったのに画面から出られない状態を作らない**。これが既存コードの `LogError` が守ろうとしている一線 |

> `Renderer.OnMatchEnd()` からも `resultView.ShowIfHidden(rank)` を呼んでよい。`ShowIfHidden` は冪等なので二重表示にならない（既存の設計をそのまま使う）。

## 5. 自店HUDの下位View（`SelfRankView`）

```csharp
// Assets/Scripts/View/SelfRankView.cs
public sealed class SelfRankView : MonoBehaviour
{
    /// <summary>順位・スコア・生存数をまとめて反映する。値が変わらないフレームは ToString ごと省く。</summary>
    public void SetState(SelfRankViewState state);
}
```

`SelfRankViewState` は [value-objects/08-ranking-row-view-state.md](../value-objects/08-ranking-row-view-state.md) に併記する。

| 表示 | 規則 |
|---|---|
| 順位 | `Rank <= 0` は「順位未確定」として `--` を出す（0位は存在しない） |
| スコア | 負値をそのまま出す（`-30` 等）。0でクランプしない |
| 生存数 | `AliveCount` をそのまま。「残り◯店」 |

**Canvas の分離**：`MainStoreCanvas` は打鍵1回ごとにお題・注文カウンタが再描画される。`SelfRankView` は更新頻度が低い（2〜4Hz）ため、**入れ子Canvasで切り離す**（既存 `match-view/07-match-hud.md` §1 で星評価に対して行ったのと同じ理由）。ランキングパネル・秒読みパネルも同様に分ける。

## 6. 依存関係

- 依存する：`pureC#` [match-state/](../../../../pureC%23/docs/.sdd/match-state/README.md) 全3本、[result/02](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)
- 依存される：[../ranking-view/](../ranking-view/README.md)、[../elimination/](../elimination/README.md)、[../result-view/](../result-view/README.md)

## 7. テスト観点

Unity の EditMode テスト（`Unity/tests/Takoda99.View.Tests/`）で検証できるのは値オブジェクトの変換のみ。View 本体は手動確認とする。

| # | 観点 | 方法 |
|---|---|---|
| 1 | `Rank <= 0` で `--` が出る | `SelfRankViewState` のテスト |
| 2 | 負のスコアがそのまま文字列になる | 同上 |
| 3 | 撤去した参照が残っていない | `Assets/Scripts` を `CreditLife` `StarRating` `PatienceMaxMs` `Normalized` `StormThresholdPct` で grep して0件 |
| 4 | `MatchEnd` 受信でモーダルが出る（`PersonalResult` 未受信でも） | `MainGameViewSampleDriver` にケースを追加して手動確認 |
| 5 | 待機中（`GameBefore` 保持中）にランキング・秒読みが出ない | 手動確認 |

## 8. 未確定事項

- HUDの具体的な配置（§3 の要件を満たす範囲でクライアント担当が決定。[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点1）
- 我慢ゲージを**見た目だけ**の演出として残すか（論点2）。**残す場合も `PatienceMaxMs` は 0 で届くため、サーバー値に依存しないローカル演出として作り直すこと**
