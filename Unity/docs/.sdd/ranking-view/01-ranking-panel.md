# 01-ランキングパネル（試合中）

> 参照する上流：[12_差分_クライアント §2.2・§2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[本選企画書 3.3・3.7](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/01_本選企画書.md)／`pureC#` [match-state/02-ranking-store.md](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)。矛盾したら上流優先。

## 1. 責務

**する**

- `ClientState.Ranking` から**上位N名**を取り出して行として描く
- 自分が上位Nに入っていない場合、**自分の行を末尾に追加で見せる**
- 順位が入れ替わったとき、行を**動かして**見せる

**しない**

- 順位・スコアを計算しない（`Ranking` が持っている値を描くだけ）
- 表示名をサーバーへ問い合わせない（`RankingRow.DisplayName` は解決済み）
- 脱落済みの店を勝手に並べ替えない

## 2. 公開インターフェース

```csharp
// Assets/Scripts/View/Ranking/RankingPanelView.cs
namespace Takoda99.View.Ranking
{
    public sealed class RankingPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;

        /// <summary>表示件数。**10 を下回る値を設定しない**（§4）。</summary>
        [SerializeField] private int visibleCount = 10;

        /// <summary>行の移動にかける秒数。0 で即時。</summary>
        [SerializeField] private float rowMoveDuration = 0.25f;

        /// <summary>state から表示行を組み立てて反映する。Renderer が state 変化のたびに呼ぶ。</summary>
        public void Apply(ClientState state);
    }

    /// <summary>1行。順位・名前・スコアの3点セット。</summary>
    public sealed class RankingRowView : MonoBehaviour
    {
        public void SetState(RankingRowViewState state);
    }
}
```

`RankingRowViewState` は [../value-objects/08-ranking-row-view-state.md](../value-objects/08-ranking-row-view-state.md)。

## 3. 表示行の組み立て

```
1. rows = state.Ranking.Top(visibleCount)
2. self = state.Ranking.Find(state.SelfStoreId)
3. rows に self が含まれていなければ、rows の末尾に self を足す
   （このとき「…」等の区切りを1行挟んでよい）
4. 自分の行は IsSelf = true にして強調する
5. 自分の行の順位・スコアは state.Rank / state.Score で上書きする ★重要
```

### 3.1 なぜ自分の行だけ上書きするのか

`Ranking` は `RankingDelta` の取りこぼしでズレ得る。**自分の順位が他人の行より不正確に見えるのは、体験として最悪**（「1位のはずなのに3位と出ている」）。自分の値だけは `EvaluationUpdate` 由来の権威値に差し替える。

| 値 | 出どころ |
|---|---|
| 他人の行の順位・スコア | `RankingRow.Rank` / `RankingRow.Score` |
| **自分の行の順位・スコア** | **`state.Rank` / `state.Score`** |
| 全員の表示名 | `RankingRow.DisplayName` |
| 生死 | `RankingRow.Alive` |

> リスト内の**並び順**は `Ranking` の順のまま（自分だけ順位を差し替えても位置は動かさない）。一瞬のズレは次の `RankingSnapshot` で直る。

## 4. 表示件数（★要件）

**`visibleCount` は 10 を下回らない。**

理由：足切りスケジュールで100秒時点の生存数を10人に決めたため、**上位10名リスト＝生存者全員**になる（[12_差分_クライアント §2.4](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)）。

```
100秒時点で、上位10名リストが、そのまま生存者全員になる
  → 決勝では「リストに映っている全員が敵」という状態が生まれる
  → 逆転がリアルタイムで見える
  → 観戦している脱落者にも何が起きているか完全に伝わる
```

**試合が進むほどUIが情報を出し切った状態に収束する。** LTで最も見せたい20秒が、最も分かりやすい画面になる。

Inspector で 10 未満が設定されたら `Awake` で警告して 10 にクランプする。

## 5. 行入れ替えアニメーション

**決勝の20秒（100〜120秒）が最も激しく動く。演出はこの区間を基準に作る。**

| # | 規則 |
|---|---|
| A1 | 行の GameObject を**破棄・再生成しない**。`storeId` をキーにプールし、位置だけ動かす（99店ぶんの生成破棄はWebGLで詰まる） |
| A2 | 位置の移動は DOTween で `rowMoveDuration` 秒。**次の `Apply` が来たら現在位置から追従**（Tween を Kill してから張り直す） |
| A3 | リストに入ってきた行はフェードイン、出ていった行はフェードアウトしてプールへ戻す |
| A4 | **順位の数字はアニメーションさせない**。位置だけ動かし、数字は即時更新する（読めない時間を作らない） |
| A5 | `rowMoveDuration` より速く `Apply` が来る（1〜2Hz なので通常は来ない）場合も、Tween が積み重ならないこと |

## 6. 脱落済みの店の見せ方

| `Alive` | 見た目 |
|---|---|
| `true` | 通常 |
| `false` | 減光・打ち消し線など。**リストからは消さない**（順位は確定値として並び続ける） |

試合中の上位10件に脱落済みが入ることは通常ないが（脱落者の `Rank` は生存数より大きい）、`Ranking` の状態次第では起こり得るため、描き分けは持っておく。

## 7. 待機中・脱落後の扱い

| 状況 | 扱い |
|---|---|
| `GameBeforeView` 保持中 | **描かない**（既存 `holding` の規則に従う。[../hud/01](../hud/01-hud-composition.md) §4.2） |
| `Ranking.Rows` が空 | パネルごと非表示。空リストの枠だけ出さない |
| 自店が脱落（`Spectating`） | **描き続ける。** 観戦の主役になる。ただし全員表示は [03-spectator-ranking-view.md](./03-spectator-ranking-view.md) が担う |
| `Phase == Result` | リザルト画面へ移るため、このパネルは畳んでよい |

## 8. 依存関係

- 依存する：`pureC#` [match-state/02](../../../../pureC%23/docs/.sdd/match-state/02-ranking-store.md)、[../value-objects/08](../value-objects/08-ranking-row-view-state.md)、[../hud/01](../hud/01-hud-composition.md)（`Renderer` からの呼び出し）
- 依存される：[03-spectator-ranking-view.md](./03-spectator-ranking-view.md)（`RankingRowView` を再利用する）

## 9. テスト観点

| # | 観点 | 方法 |
|---|---|---|
| 1 | 上位10件が `Rank` 昇順で並ぶ | EditMode（`RankingRowViewState` の組み立てを純関数に切り出して検証） |
| 2 | 自分が50位のとき、10件＋自分の11行になる | 同上 |
| 3 | 自分が3位のとき、10行のまま（重複しない） | 同上 |
| 4 | 自分の行の順位が `state.Rank` で上書きされる（`Ranking` 側が古くても） | 同上 |
| 5 | `visibleCount = 5` を設定すると 10 にクランプされ警告が出る | 手動 |
| 6 | 99店を10秒間ランダムに入れ替えても行が生成破棄されない | `MainGameViewSampleDriver` にストレスケースを追加 |
| 7 | `Ranking.Rows` が空でパネルが非表示になる | 手動 |

## 10. 未確定事項

- ~~下位側（自分の周辺）も同時に見せるか~~ → **決定：見せる。** 下位30行の常設パネルを別に持つ（[05-bottom-ranking-panel.md](./05-bottom-ranking-panel.md)）
- 表示件数の最終値（10以上であること以外は自由）→ **決定：10。** スロットの要素数が正になる（[04-top-ranking-slots.md](./04-top-ranking-slots.md) §5.1）
- §5 の行入れ替えアニメーションは [06-rank-swap-animation.md](./06-rank-swap-animation.md) で具体化した（A1〜A5 は維持）
