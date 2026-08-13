# 01-一斉脱落の集約演出

> 参照する上流：[12_差分_クライアント §5.2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス 4-B・4-C・4-D](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)／[本選企画書 3.4・3.6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／`pureC#` [match-state/03-cull-warning.md](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)。矛盾したら上流優先。

## 0. 前提の数字

| ステージ | 時刻 | 切る数 |
|---|---|---|
| 1 | 20秒 | 24 |
| 2 | 40秒 | 20 |
| 3 | 60秒 | 20 |
| 4 | 80秒 | 15 |
| 5 | 100秒 | 10 |
| 6 | 120秒 | **10（全員）** |

**6回再生される演出**であり、かつ**1回あたり最大24件（予選想定の49件はサーバー実装の上限側の値）が同時**。1件ずつ再生する設計は成立しない。

予選で好評だった「人が減っていく画面」は維持・強化する。時間区切りにしたことで**「まとめて減る」瞬間**が作れるようになった。

## 1. 責務

**する**

- 1回の `OnStoreEliminatedBatch` を**1つの演出**として再生する
- SEを**1回だけ**鳴らす
- 自店が含まれる場合、演出のあとに脱落モーダルを出す
- ステージが進むほど演出を**段階的に強くする**

**しない**

- 脱落を判定しない（サーバーが確定済み）
- 自店が含まれるかを自分で判定しない（`includesSelf` が渡ってくる）
- 演出中に入力を止めない（自店が含まれない場合、プレイヤーは打鍵を続けている）

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/Elimination/MassEliminationEffect.cs
namespace Takoda99.View.Elimination
{
    public sealed class MassEliminationEffect : MonoBehaviour
    {
        [SerializeField] private AudioSource se;
        [SerializeField] private AudioClip cullClip;

        /// <summary>1ステージぶんの一斉脱落を1つの演出として再生する。</summary>
        /// <param name="stageIndex">第何段階か（1始まり）。演出の強度に使う。</param>
        /// <param name="count">脱落した店の数。演出の規模に使う。</param>
        /// <param name="includesSelf">自店が含まれるか。</param>
        public void Play(int stageIndex, int count, bool includesSelf);
    }
}
```

`Renderer` からの呼び出し：

```csharp
public void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf)
{
    if (entries.Count == 0) return;

    massElim?.Play(stageIndex, entries.Count, includesSelf);

    if (includesSelf)
    {
        selfEliminated = true;
        customerQueue?.ClearAll();
        orderBubble?.Hide();
        // モーダルは演出の完了後（§5）
    }
}
```

> **`entries` を1件ずつループして演出を呼ばない。** 件数だけを渡す。個々の `storeId` が要るのはランキング表示側であり、そちらは state 経由で既に更新されている。

## 3. 演出の規則

| # | 規則 |
|---|---|
| E1 | **SEは1回。** 予選の「他店脱落音（都度再生版）」をそのまま使うと24〜49回同時に鳴る。**1回の大きな音**に置き換える（[20_廃止・非使用リスト §4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/20_廃止・非使用リスト.md)） |
| E2 | 演出時間は**2秒以内**。6回再生され、そのたびに試合が止まって見えると尺を食う |
| E3 | `stageIndex` が進むほど強くする（1回目は控えめ、6回目が最大） |
| E4 | `count` を演出に反映してよい（多いほど派手に）。ただし**件数に比例した数のパーティクル／オブジェクトを出さない**（WebGL） |
| E5 | 自店が含まれない場合、**打鍵の視認性を妨げない**。プレイヤーは演出中も打っている |
| E6 | 演出とランキングの更新は**別々に走ってよい**。ランキングは state 駆動で即座に反映される |

### 3.1 「まとめて減る」を見せる

演出の中身はアート側と決めるが、**伝えるべきことは1つ**：

```
「今、◯店が一斉に閉店した」
```

数字（`count`）を出すのが最も確実で、LTのプレゼン中に「今20人減りました」と言える（[本選企画書 3.6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)）。

## 4. ステージ表示との連動

`stageIndex` / `stageTotal` は [../ranking-view/02-cull-countdown-panel.md](../ranking-view/02-cull-countdown-panel.md) が常設で出している。演出側で重複して出す必要はない。

演出の直後、秒読みパネルには**次のステージの `ForcedEliminationWarning`（残り20秒）** が届く（配信順序 4-A）。「リセットされて次が始まる」感覚は秒読み側が担う。

## 5. 自店が脱落した場合

```
StoreEliminatedBatch（自店を含む）を受信
  ↓
Reducer: Phase = Spectating / Queue クリア / CurrentOrder = null   … pureC# match-state/03
MatchClientController: ITypingJudge.AbortOrder()                    … pureC# result/02
  ↓
Renderer.OnStoreEliminatedBatch(includesSelf: true)
  ↓
★集約演出を再生（自店ぶんは特に強く）
  ↓
演出の完了後、脱落モーダルを表示
```

| # | 要件 |
|---|---|
| M1 | **この時点でリザルトへ行かない**（4-D）。120秒の `MatchEnd` を待つ |
| M2 | モーダルの選択肢は「観戦する」／「成績を見る」の2つ |
| M3 | **`PersonalResult` は既に手元にある**（配信順序 4-A で `StoreEliminatedBatch` の直後）。どちらを選んでも、いつ押しても壊れない（[../result-view/01](../result-view/01-personal-result-view.md)） |
| M4 | モーダルが出るまでの間、入力は既に無効（`Phase == Spectating`） |
| M5 | 演出が何らかの理由で完了しなくてもモーダルは出る（**タイムアウトで強制的に出す**。試合から出られない状態を作らない） |

> M5 は予選の教訓。既存 `Renderer` の `resultView` が未割り当てだと `LogError` を出しているのも同じ理由（「MainGame から出られなくなる」）。**演出はモーダル表示の前提条件にしない。**

## 6. 120秒（最終ステージ）

最終バッチには `FinalRank == 1`（優勝者）を含む10件が入る。**特別扱いをしない。**

```
120秒 : StoreEliminatedBatch（10件）→ PersonalResult → RankingSnapshot → MatchEnd
```

| 状況 | 扱い |
|---|---|
| 自店が決勝の10店に居た | `includesSelf == true` で通常どおり演出。**ただし直後に `MatchEnd` が来るため、脱落モーダルではなくリザルトへ進む** |
| 自店が既に脱落済み | 観戦中に最後の演出を見て、`MatchEnd` でリザルトへ |

実装：`includesSelf` で脱落モーダルを出す処理に、**「`state.MatchEnded` が立っていたら出さない」**か、**モーダル表示を短い遅延に置き、その間に `MatchEnd` が来たらキャンセルする**のどちらかを入れる。前者を推奨（判定が state だけで閉じる）。

> `MatchEnd` は `StoreEliminatedBatch` の直後に届くため、遅延方式でも実際には問題ないが、**通信の揺れで数百ms空く可能性がある**。state で判定するほうが確実。

## 7. 依存関係

- 依存する：`pureC#` [match-state/03](../../../../pureC%23/docs/.sdd/match-state/03-cull-warning.md)、[result/02](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)、[../hud/01](../hud/01-hud-composition.md)
- 依存される：[../result-view/](../result-view/README.md)

## 8. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | 24件のバッチで `Play` が**1回**、SEが**1回** | `MainGameViewSampleDriver` にケースを追加 |
| 2 | `entries` が空で何も起きない | 同上 |
| 3 | `includesSelf == false` で行列が畳まれない | 同上 |
| 4 | `includesSelf == true` で行列が畳まれ、モーダルが出る | 同上 |
| 5 | 演出をスキップ（`massElim` 未割り当て）してもモーダルが出る | 参照を外して確認 |
| 6 | 最終ステージ（`MatchEnd` が続く）で脱落モーダルが出ずリザルトへ行く | シナリオ再生 |
| 7 | 6ステージ連続で再生してもフレーム落ちしない | WebGL 実機 |

## 9. 未確定事項

- 演出の具体的な絵とSE（アートと相談。段階的に強くするという要件のみ確定）
- 脱落モーダルのボタン構成（「観戦する」「成績を見る」の2つで足りるか）
