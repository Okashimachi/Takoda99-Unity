# 04-未解決の storeId を検知する（表示名キャッシュのカナリア）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`MatchStart` / `RankingSnapshot` / `RankingDelta` / `StoreEliminatedBatch`）。矛盾したら上流優先。既存 [01-score-and-self-rank.md](./01-score-and-self-rank.md)（`DisplayNames` の生成）／[02-ranking-store.md](./02-ranking-store.md)（`DisplayName` の解決）の**補足**であり、両者の設計は変えない。

## 0. このファイルが解こうとしている問題

**サーバー担当からの指摘（2026-08-14）**：

> 表示名は MatchStart でキャッシュしてください。以降のランキングは storeId しか送らんけど大丈夫？

この方式は既に実装済み（`ClientState.DisplayNames`）。**問題はキャッシュが外れたときに何も起きないこと。**

現状 [Reducer.cs:352](../../../src/Takoda99.Client/State/Reducer.cs:352) は、キャッシュに無い `storeId` に対して**空文字を返して黙って続行する**：

```csharp
=> state.DisplayNames.TryGetValue(storeId, out var name) ? name : "";
```

落ちないのは正しい（[02-ranking-store.md](./02-ranking-store.md)：未知の storeId は「捨てない」）。しかし**気付けない**。ランキングの1行だけ店名が空欄になるが、本番の99行の中では誰も異常だと思わない。原因（`MatchStart` に載っていなかった／`LocalMatchReset` が走った後だった／サーバーが新しい storeId を送り始めた）は、後から追えない。

**サーバー担当に確認済みの事実（2026-08-15 回答）**：

> `MatchStart.stores[]` には **Bot を含む全店が入っている**

**つまり未解決の storeId は、正常系では1件も出ないはず。** 出たらサーバーかクライアントのどちらかが壊れている。**発生件数がゼロであることを前提にできるので、1件でも出たらエラーとして扱ってよい**（ノイズにならない）。

## 1. 責務

**する**

- `DisplayNames` に無い `storeId` を検知し、**イベントとして外へ通知する**
- 同じ `storeId` について**1試合に1回だけ**通知する（`RankingDelta` は高頻度で届くため、抑止しないとログが溢れる）
- 検知しても**状態は変えない**。従来どおり空文字で行を作り、描画は続行する

**しない**

- 例外を投げない・行を捨てない（**表示が壊れるより、名前が空のほうがまし**）
- `Reducer` にログ出力や副作用を持ち込まない（`Reducer` は純関数。[04-store-reducer.md](../04-store-reducer.md)）
- `pureC#` から `UnityEngine.Debug` を呼ばない（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md)）
- 表示名をサーバーへ問い合わせない（**そういう C2S メッセージは存在しない**）

## 2. 設計：どこで検知するか

`Reducer` が純関数である以上、通知は `Reducer` の外に置く。既に**同じ性質の経路が `Dispatcher` にある**（未知メッセージの `OnUnknownMessage`）ので、それに揃える。

```csharp
// IDispatcher へ追加
/// <summary>
/// MatchStart の表示名キャッシュで解決できない storeId を受信した。引数は storeId。
/// 正常系では発火しない（MatchStart には Bot を含む全店が載る契約）。発火＝サーバーかクライアントの異常。
/// 同じ storeId では1試合に1回しか発火しない。
/// </summary>
event Action<string> OnUnresolvedStoreId;
```

| 選択肢 | 採否 | 理由 |
|---|---|---|
| `Reducer` の中でログを吐く | ❌ | 純関数でなくなる。テストが副作用を持つ |
| `ClientState` に「未解決リスト」を持たせる | ❌ | 状態が増える。描画のたびに `state` が変わる契機を作らない（[03-cull-warning.md](./03-cull-warning.md) と同じ判断） |
| **`Dispatcher` の Decode でイベント発火** | ✅ | 既存の `OnUnknownMessage` / `OnMessageDropped` と同じ形。Unity 側の購読口も既にある |

## 3. ふるまいの詳細

### 3.1 検知の対象

`storeId` を運ぶ受信のうち、**`MatchStart` より後に届くもの**すべて。

| メッセージ | 見るフィールド |
|---|---|
| `RankingSnapshot` | `entries[].storeId` |
| `RankingDelta` | `entries[].storeId` |
| `StoreEliminatedBatch` | `entries[].storeId` |
| `ForcedEliminationWarning` | `cutStoreIds[]` |

`MatchStart` 自身は対象外（キャッシュを作る側）。

### 3.2 手順

`Dispatcher` の Decode で、Action を組み立てて `_store.Apply(...)` する**直前**に行う。

| # | 処理 |
|---|---|
| 1 | 対象メッセージから `storeId` を集める |
| 2 | `_store.State.DisplayNames` に含まれないものを抜き出す |
| 3 | そのうち**まだ通知していないもの**だけ `OnUnresolvedStoreId` を発火する |
| 4 | 通知済み集合へ加える |
| 5 | Action の適用は**通常どおり続行する**（ここで return しない） |

通知済み集合は `Dispatcher` のプライベートフィールド（`HashSet<string>`、`StringComparer.Ordinal`）。**`MatchStart` を処理したときに空へ戻す**（試合をまたいで抑止が効き続けると、次の試合の異常を見逃す）。

### 3.3 エッジケース

| ケース | 扱い |
|---|---|
| `MatchStart` 前に届いた | `Dispatcher` の phase ゲートで既に落ちている。ここまで来ない |
| `DisplayNames` が空（`MatchStart` の `stores[]` が空だった） | **全 storeId が未解決になる。** それでも1件ずつ通知する（抑止集合があるので溢れない）。この状況自体が重大な異常であり、握り潰さない |
| 同じ `storeId` が1メッセージ内に2度 | 1回だけ通知する |
| 表示名が空文字で届いていた（キーはあるが値が空） | **未解決として扱わない。** サーバーが空の表示名を配ったのは契約上あり得る話で、キャッシュの欠落とは別の問題 |

### 3.4 Unity 側の受け口

[GameBootstrapper.cs:105](../../../../Unity/Assets/Scripts/Bootstrap/GameBootstrapper.cs:105) の既存の購読と並べて書く。**`LogWarning` ではなく `LogError`**（正常系ではゼロ件のため、警告に埋もれさせない）。

```csharp
dispatcher.OnUnresolvedStoreId += storeId =>
    Debug.LogError(
        $"{nameof(Dispatcher)}: MatchStart に無い storeId を受信 storeId=\"{storeId}\"。" +
        "表示名が空欄になります（MatchStart.stores[] の欠落を疑うこと）。", this);
```

あわせて `ClientEventLog` にも1行残し、デバッグパネル（[platform/03-debug-panel.md](../../../../Unity/docs/.sdd/platform/03-debug-panel.md)）から追えるようにする。

## 4. 依存関係

- 依存するモジュール：`ClientState.DisplayNames`（[01-score-and-self-rank.md](./01-score-and-self-rank.md)）、`Dispatcher`（[05-dispatcher.md](../05-dispatcher.md)）
- 依存されるモジュール：なし（検知だけで、誰の挙動も変えない）
- **変更するファイル**：`IDispatcher.cs` / `Dispatcher.cs` / `GameBootstrapper.cs`。**`Reducer.cs` は変更しない**

## 5. テスト観点

| # | 観点 | 期待 |
|---|---|---|
| 1 | `MatchStart` に載っていない `storeId` を含む `RankingDelta` を流す | `OnUnresolvedStoreId` が1回発火し、その行は `DisplayName = ""` で表に入る |
| 2 | 同じ `storeId` の `RankingDelta` を100回流す | 発火は1回だけ |
| 3 | `MatchStart` に載っている `storeId` だけを流す | 一度も発火しない |
| 4 | 発火後も `state` が正しく更新される | 検知が Action の適用を止めていない |
| 5 | 新しい `MatchStart` を流した後、同じ未知 `storeId` を流す | 再び発火する（抑止集合がリセットされている） |
| 6 | 表示名が空文字で `MatchStart` に載っている `storeId` | 発火しない |

## 6. 未確定事項

- なし
