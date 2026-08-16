# 03-観戦画面の全プレイヤー順位一覧

> 参照する上流：[12_差分_クライアント §2.2](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス §6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)／[10_差分_プロト §2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/10_差分_プロト.md)。矛盾したら上流優先。

**99人中89人は、120秒より前に脱落して観戦側にいる。** 観戦画面は「多数派が最も長く見る画面」であり、優先度は低くない。

## 0. なぜ全員分なのか

> 観戦中に一部のプレイヤーしか見えないのは寂しい。脱落後は99人全員の順位が見えるようにする。**ランキングを差分方式にしているのはこの要件のため**（[10_差分_プロト §2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/10_差分_プロト.md)）。

上位N名だけを送る方式ではこの要件を満たせない。差分＋定期全量にしたことで、**全99店を対象にしても帯域が小さい**。

## 1. 責務

**する**

- `ClientState.Ranking.Rows`（全99行）を1本のリストとして描く
- 自分の行を強調し、**開いた瞬間に自分の位置までスクロールする**
- 生存店と脱落店を描き分ける

**しない**

- 精度を追わない（**「眺めるためのもの」であり、正確性は低くてよい**。差分の取りこぼしは許容し、定期的な全量配信でズレが直る）
- 入力を受け付けない（観戦中は `OrderServed` を送らない）
- サーバーへ何も送らない

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/SpectatorRankingView.cs
namespace Takoda99.View.Ranking
{
    public sealed class SpectatorRankingView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;    // 01 と同じ行Prefabを再利用
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;

        /// <summary>画面を開く。自分の行までスクロールする。</summary>
        public void Open(ClientState state);

        /// <summary>state 変化のたびに呼ぶ。開いていなければ何もしない。</summary>
        public void Apply(ClientState state);

        public void Close();
    }
}
```

## 3. 描画の規則

| # | 規則 |
|---|---|
| S1 | `Ranking.Rows` は `Rank` 昇順で保持されている。**そのままの順で描く**（再ソートしない） |
| S2 | 自分の行（`storeId == state.SelfStoreId`）を強調する。順位は `state.Rank` で上書きしてよい（[01 §3.1](./01-ranking-panel.md) と同じ理由） |
| S3 | 脱落済み（`Alive == false`）は減光。**リストから消さない**（確定順位として並び続ける） |
| S4 | 表示名が空の行は `storeId` を出す |
| S5 | `Open` した瞬間、自分の行が画面中央に来る位置へスクロールする |
| S6 | 以降のスクロール位置は**ユーザーの操作を優先**する。`Apply` で勝手に戻さない |

## 4. パフォーマンス（★WebGL で効く）

99行を素直に並べると、`Apply` のたびに99個の `TMP` 更新が走る。1〜2Hz とはいえ WebGL では無視できない。

| # | 対策 |
|---|---|
| P1 | **行 GameObject を `storeId` でプールする。** 生成破棄しない（01 と同じ方針） |
| P2 | **値が変わった行だけ更新する。** 前回の `RankingRowViewState` を保持し、等価なら `TMP.text` への代入ごと省く |
| P3 | 99行を1つの `Canvas` に置く。行が更新されるたびに `Canvas` 全体のメッシュが再構築されるため、**HUDの Canvas とは分離する**（既存 `match-view/07-match-hud.md` §1 と同じ理由） |
| P4 | 画面外の行の更新まで省く仮想スクロールは、**最初は作らない**。P1〜P3 で足りなければ検討する |

## 5. 画面遷移の中での位置づけ

```
試合中 ─脱落─> 脱落モーダル ─┬─> 観戦（★このView）───┐
                            │                      ├─> リザルト
                            └─> 個人成績 ───────────┘
```

| 状況 | 扱い |
|---|---|
| 脱落モーダルから「観戦する」 | `Open(state)` |
| `MatchEnd` 受信（120秒） | `Close()` してリザルトへ |
| 120秒まで生き残った10店 | この画面を通らない（試合中 → リザルト直行）。**それでも実装に例外は不要** |

> **観戦中も接続は維持し、試合中と同じメッセージを受信し続ける**（6-A）。`RankingDelta` / `RankingSnapshot` は `Spectating` でも受理される（`pureC#` [result/02 §2.1](../../../../pureC%23/docs/.sdd/result/02-lifecycle-and-renderer.md)）。

## 6. 依存関係

- 依存する：`pureC#` [match-state/02](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)、[01-ranking-panel.md](./01-ranking-panel.md)（`RankingRowView`）、[../value-objects/08](../value-objects/08-ranking-row-view-state.md)
- 依存される：なし

## 7. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | 99行が `Rank` 昇順で並ぶ | EditMode（行状態の組み立て） |
| 2 | 自分の行が強調され、`state.Rank` で上書きされる | 同上 |
| 3 | 脱落済みの行が減光され、消えない | 手動 |
| 4 | `Open` で自分の行が中央に来る | 手動 |
| 5 | `Apply` を連打してもスクロール位置が戻らない | 手動 |
| 6 | 1店だけスコアが変わったとき、**その1行しか `TMP` が更新されない** | プロファイラで確認 |
| 7 | WebGL 実機で 1〜2Hz の `Apply` がフレーム落ちを起こさない | 実機確認 |

## 8. 未確定事項

- 一覧かスクロールか、どう見せるか（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点8。クライアント担当・アートと相談）。**本書はスクロールを前提に書いているが、ページ送り等でも要件（全員分が見える）を満たせばよい**
