# 調査: 自店脱落後も客が流れ続ける

対象ログ: クライアント（Unity）デバッグパネル / 1 試合ぶん。

## 1. 整理したログ（クライアント）

時刻は再生開始からの経過秒。`NET` はサーバー由来（IRenderer 通知）。

| 時刻 | 出来事 | 備考 |
|---|---|---|
| 0.39s | Boot → Title | |
| 5.65s | Title → Connecting | |
| 6.63s | Connecting → Matchmaking | |
| 13.49s | Matchmaking → InMatch | 試合開始 |
| 18.65–18.68s | **客が3人来店**（c-97 / c-134 / c-150） | このとき既に `alive=False` |
| 19.65–19.73s | 3人とも `Timeout` で離脱 | 提供 0 件 |
| 19.77s | **自店 p-1191 が脱落**（SelfCollapse, rank=99/99） | 最下位＝一番最初に脱落 |
| 19.75s | InMatch → Spectating | 観戦へ |
| 20.35–23.44s | 他店 98 店が SelfCollapse で連鎖脱落（rank 98 → 2） | 約 3 秒で全滅 |
| 21.76s / 20.75s | PHASE Late / Mid | |
| 23.44s | Spectating → Result | |
| 23.47s | MatchEnd rank=99 | `matchElapsedMs=4600`, `servedCount=0`, `leftCount=3` |

サーバーの最終集計（MatchEnd payload）:

```
finalRank: 99, reason: SelfCollapse, matchElapsedMs: 4600, creditLeft: 0
served: 0 / left: 3（すべて normal 属性）
totalKeystrokes: 0, totalMisses: 0
```

## 2. 診断

### 所見A（本題）— 画面の客はサーバー由来ではない

ログ上、**サーバーからの客の通知は 18.65〜18.68s の3人だけ**で、
脱落（19.77s）以降 `ARRIVE` は 1 件も来ていない。
つまり「脱落後も客が流れる」のはサーバーの送信が止まらないからではない。

原因はクライアントのシーン構成にある。

- `Unity/Assets/Scenes/MainGame.unity` に `TestDriver`（`CustomerQueueTestDriver`）が
  **有効なまま置かれている**（`m_IsActive: 1`, `_autoArrive: 1`, `_arriveIntervalSeconds: 1.5`）。
  これは 1.5 秒ごとに客を自動生成し続けるだけのコンポーネントで、試合状態も脱落も見ていない。
- 一方 `Renderer.cs` は `CustomerQueueView` への参照を一切持たず、
  `CustomerQueueView.Apply(ClientState)` は**どこからも呼ばれていない**。

結果として、画面の行列は最初から最後までテストドライバの産物であり、
脱落しようが試合が終わろうが 1.5 秒間隔で客が湧き続ける。

同種の事故は `MainGameViewSampleDriver` で既に対処済みで、
そちらは `Start()` で `GameBootstrapper.Instance != null` なら自分を無効化している。
`CustomerQueueTestDriver` にはこのガードが無い。

→ **サーバー側の対応は不要。** クライアント側で
(a) TestDriver に同じ実試合ガードを入れる、(b) シーンから外す、
(c) `Renderer` → `CustomerQueueView.Apply(state)` を結線する、の対応となる。

### 所見B（サーバー側に確認したい）— 自店の `alive` が最初から false

客が来た 18.65s の時点で、クライアントの `ClientState.Alive` が既に `False`。
自店の `StoreEliminated` を受け取るのはその 1 秒後（19.77s）なので、
**脱落前・試合中（phase=InMatch）から自店が生存扱いになっていない**。

クライアントは `Alive` を評価表示などに使っているため、
サーバーの初期 `StoreListUpdate` / MatchStart で自店の `alive` が
正しく `true` で入っているかを確認したい。

### 所見C（サーバー側に確認したい）— 試合が 4.6 秒で全滅

`matchElapsedMs=4600` で 99 店すべてが `SelfCollapse`。
自店は客 3 人を Timeout させただけで信用度が 0 になり最下位。
その後 3 秒で残り 98 店も全滅している。

初期クレジット、または客の Timeout ペナルティが想定より重い可能性がある。
所見B と同じ「初期状態が入っていない」1 つの原因に帰着するかもしれないので、
併せて確認をお願いしたい。

## 3. まとめ

| 所見 | 担当 | 内容 |
|---|---|---|
| A | クライアント | 脱落後も客が流れるのは `CustomerQueueTestDriver` がシーンに残っているため。サーバー無関係 |
| B | サーバー | 自店 `alive` が試合中から false |
| C | サーバー | 4.6 秒で全店 SelfCollapse。初期クレジット/ペナルティ要確認 |
