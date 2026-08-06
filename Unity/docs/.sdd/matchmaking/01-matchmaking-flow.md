# 01-MatchmakingFlow

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`（`MatchmakingJoin` / `MatchmakingLeave` / `MatchmakingStatus` / `MatchStart`）/ [Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) §1・§2.1・§2.2・§3.1・§3.2。矛盾したら上流優先。

## 1. 責務

- 接続確立から `MatchStart` 受信までの**画面と通信の進行**を持つ
- 待機人数・カウントダウンの表示用状態を提供する
- **しない**こと：マッチングの成立判定（サーバー権威。`MatchStart` が来たことが唯一の真実）
- **しない**こと：試合中の描画（[08-main-store-view.md](../08-main-store-view.md) 以降の責務）。`MatchStart` を受け取ったら状態を引き渡して退場する
- **しない**こと：`WebSocket` の接続処理そのもの（`WebGLNetworkClient` / [01-network-client.md](../01-network-client.md) の責務）

## 2. 画面の状態

```csharp
public enum MatchmakingScreenState
{
    Connecting,   // WebSocket 接続中。まだ何も送っていない
    Joining,      // MatchmakingJoin 送信済み。MatchmakingStatus をまだ受けていない
    Waiting,      // 待機中。waitingCount / minPlayers を表示
    CountingDown, // カウントダウン中。countdownMs を表示
    Starting,     // MatchStart 受信。試合画面へ遷移中
    Rejected,     // 接続を拒否された（同時接続上限など）
}
```

## 3. 公開インターフェース

```csharp
public readonly record struct MatchmakingViewState(
    MatchmakingScreenState State,
    int WaitingCount,
    int MinPlayers,
    int? CountdownMs   // カウントダウン中のみ。null は「カウントダウンしていない」
);
```

> **`CountdownMs` は `int?`。** `MatchmakingStatus.countdownMs` は待機中は**キーごと存在しない**（[client-integration §3.1](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md)）。**欠落を 0 として扱うと「あと0秒」と表示され、いつまでも始まらない画面になる。**

## 4. 送信（C2S）

### 4.1 `MatchmakingJoin` — ★接続したら最初に、すぐ送る

```json
{ "type": "MatchmakingJoin", "payload": { "displayName": "たこ焼き太郎" } }
```

**サーバーは接続を受けると最初の1メッセージを最大3秒待つ**（サーバー実装 `cmd/server/main.go` の `awaitJoinName`）。この待ち受けには以下の失敗様態がある。

| 状況 | 結果 |
|---|---|
| 3秒以内に `MatchmakingJoin` を送る | 表示名が確定する（正常） |
| 何も送らない | **3秒待たされたうえで表示名が空**になり、フォールバック名が割り当たる |
| **別種のメッセージを先に送る** | 即座に空名で続行される（`env.Type != MatchmakingJoin` で打ち切り） |
| JSON のパースに失敗 | 空名 |

> **接続確立後、他のどのメッセージよりも先に `MatchmakingJoin` を送ること。** デバッグ用の疎通確認メッセージ等を先に挟むと、それだけで表示名が失われる。

`displayName` の制約と入力UIは [02-display-name.md](./02-display-name.md)。

### 4.2 `MatchmakingLeave`

```json
{ "type": "MatchmakingLeave", "payload": {} }
```

待機列から抜ける。**切断でも自動的に外れる**ため、明示送信は必須ではない（幽霊待機者にはならない）。画面に「やめる」導線を置く場合のみ送る。

## 5. 受信（S2C）— `MatchmakingStatus`

```json
{ "waitingCount": 12, "minPlayers": 20, "countdownMs": 15000 }
```

配信されるのは次の3つの場合のみ（サーバー実装 `internal/matchmaking/matchmaking.go` の `Run`）。

1. **1秒ごとのティッカー**（待機者が1人以上いるとき）
2. カウントダウンの**開始時**
3. カウントダウンの**中断時**（`minPlayers` を割り込んだとき。`countdownMs` が消える）

### 5.1 ★ `MatchmakingJoin` を送っても、すぐには返らない

**最大1秒、`MatchmakingStatus` が来ない。** join 時の強制配信は行われていない。

> 会場で99人が一斉参加したとき「1 join ごとに全員へ配信」だと O(N²)＝約4,900通のバーストになり、送信キューが溢れて待機者が切断される。それを避けるための設計。

**したがって `Joining` は「接続はできたが人数が不明」な状態として、それ専用の見た目を持つ。** `WaitingCount = 0` を表示すると「誰もいない」と誤読される。「接続しました。参加者を確認しています…」等、**数値を出さない**表示にする。

### 5.2 カウントダウンの中断

`countdownMs` が入っていた状態から**キーごと消えた**メッセージが届く。`CountingDown` → `Waiting` へ戻す。**戻り得ることを前提に画面を組む**（カウントダウン開始後の一方通行にしない）。

## 6. 受信（S2C）— `MatchStart`

自分宛てに1通。受信したら `Starting` へ遷移し、試合画面へ引き渡す。

- `selfStoreId` で `stores[]` の中の自店を特定する
- **`params` の値を表示に使う**（[pureC#/docs/.sdd/value-objects/01-match-state.md](../../../../pureC%23/docs/.sdd/value-objects/01-match-state.md)）
- `stores[]` には**全店の `displayName` が入っている**。他店名の取得経路は [02-display-name.md](./02-display-name.md) §3
- 定員に満たないぶんは **Bot が補完**される。`stores[]` の件数は常に `maxStores`

## 7. Unity構成

- **Inspector 公開値**：接続先URL。**本番URLをコードや仕様書に直書きしない**（[docs/rules/03-Git運用.md](../../../../docs/rules/03-Git運用.md)「秘密情報（本番URL・トークン）をコミットしない」）。Inspector か外部設定から与える
- **シーン**：試合シーンとは別シーン、または同一シーン内の別 Canvas。`MatchStart` 受信時に切り替える
- 接続の実体は `WebGLNetworkClient` に委譲し、このモジュールは**送受信するメッセージの意味だけ**を持つ

## 8. ふるまいの詳細

### 8.1 接続直後の1秒間

**状態が空**として画面を組む（§5.1）。`WaitingCount` / `MinPlayers` は「未取得」を表現できる形にする。

### 8.2 同時接続上限

サーバーの同時接続上限は **200**（99人＋再接続・観戦の余裕）。超過すると **503** が返る。`Rejected` へ遷移し、再試行の導線を出す。

### 8.3 脱落しても接続を切らない

これは試合中の話だが、接続のライフサイクルとして関係するため記す。**自店が脱落してもサーバーは接続を保持し、`StoreListUpdate` / `StoreEliminated` / `MatchEnd` を送り続ける**（観戦とリザルトのため）。マッチング画面側で `StoreEliminated` を根拠に切断処理を書かないこと。

## 9. 依存関係

- 依存する `pureC#` モジュール：`Contract`（Proto の DTO 型）、`Dispatcher`
- 依存するUnity側モジュール：`WebGLNetworkClient`（[01-network-client.md](../01-network-client.md)・未作成）
- 依存されるモジュール：試合画面（`MatchStart` を受け取る側）

## 10. テスト観点

- `MatchmakingJoin` が、接続確立後に送られる**最初の**メッセージであること
- `countdownMs` が欠落したメッセージで `CountdownMs` が `null` になり、**0 にならない**こと
- カウントダウン中に `countdownMs` の無いメッセージが来たら `Waiting` へ戻ること
- 接続直後〜最初の `MatchmakingStatus` 受信までの間、待機人数の数値が画面に出ないこと
- `MatchStart` 受信で `Starting` へ遷移し、以降 `MatchmakingStatus` を受けても状態が巻き戻らないこと

## 11. 未確定事項

- `Rejected`（503）時の再接続ポリシー（自動リトライの有無・間隔）
- 試合中に接続が切れた場合の扱い（[SV-08](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-08)）。**サーバー側の再同期手段が無いため未解決**
- 待機画面の見た目（人数の出し方・カウントダウンの演出）
