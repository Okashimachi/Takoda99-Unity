# 01-MatchmakingFlow

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`（`MatchmakingJoin` / `MatchmakingLeave` / `MatchmakingStatus` / `MatchStart`）/ [Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) §1・§2.1・§2.2・§3.1・§3.2。矛盾したら上流優先。

## 1. 責務

- 接続確立から `MatchStart` 受信までの**画面と通信の進行**を持つ
- 待機人数・カウントダウンの表示用状態を提供する
- **しない**こと：マッチングの成立判定（サーバー権威。`MatchStart` が来たことが唯一の真実）
- **しない**こと：試合中の描画（[02-main-store-view.md](../match-view/02-main-store-view.md) 以降の責務）。`MatchStart` を受け取ったら状態を引き渡して退場する
- **しない**こと：`WebSocket` の接続処理そのもの（`WebGLNetworkClient` / [01-network-client.md](../platform/01-network-client.md) の責務）

## 2. 画面の状態

```csharp
public enum MatchmakingScreenState
{
    NameEntry,    // 表示名の入力中。★まだ接続していない（02-display-name.md §5）
    Connecting,   // 名前確定後。WebSocket 接続中
    Joining,      // MatchmakingJoin 送信済み。MatchmakingStatus をまだ受けていない
    Waiting,      // 待機中。waitingCount / minPlayers を表示
    CountingDown, // カウントダウン中。countdownMs を表示
    Starting,     // MatchStart 受信。試合シーンへ遷移中
    Rejected,     // 接続を拒否された（同時接続上限など）
}
```

> **`NameEntry` は「接続していない」ことを表すために要る。** 接続してから名前を入力させると、サーバーの3秒の待ち受け（§4.1）を超えて表示名が失われる。この状態を持たずに「接続中」から始めると、その順序違反をコンパイル時にも実行時にも検知できない。

## 3. 公開インターフェース

```csharp
public readonly struct MatchmakingViewState  // Unity は C# 9 までのため record struct は使えない
{
    MatchmakingScreenState State { get; }
    int WaitingCount { get; }
    int MinPlayers { get; }
    int? CountdownMs { get; }   // カウントダウン中のみ。null は「カウントダウンしていない」
    MatchmakingPanel Panel { get; }  // 表示すべきパネル（§8.4）
}
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
- **`params` の値を表示に使う**（[01-match-state.md](../../../../pureC%23/docs/.sdd/value-objects/01-match-state.md)）
- `stores[]` には**全店の `displayName` が入っている**。他店名の取得経路は [02-display-name.md](./02-display-name.md) §3
- 定員に満たないぶんは **Bot が補完**される。`stores[]` の件数は常に `maxStores`

## 7. Unity構成

- **シーン**：`MatchiMaking` シーン（試合シーン `MainGame` とは**別シーン**。[02-scene-composition.md](../foundation/02-scene-composition.md) §2 で確定）。`MatchStart` 受信＝`ClientPhase.InMatch` 到達時に `GameBootstrapper` がシーンごと切り替える
- **接続先URL**は `GameBootstrapper`（`Boot` シーン）が持つ。**本番URLをコードや仕様書に直書きしない**（[03-Git運用.md](../../../../docs/rules/03-Git運用.md)「秘密情報（本番URL・トークン）をコミットしない」）
- 接続の実体は `WebGLNetworkClient` に委譲し、このモジュールは**送受信するメッセージの意味だけ**を持つ

### 7.1 シーン階層

```
MatchMakingCanvas               ← MatchmakingScreenView
├── BG
├── WriteNameModal              ← NameEntry
│   └── NameInput
│       ├── NameInputField      （TMP_InputField・characterLimit = 6）
│       └── Decide              （Button → GameBootstrapper.DecideDisplayName）
├── WaitingPanel                ← Connecting / Joining
└── MatchingPanel               ← Waiting / CountingDown
    ├── PaticipantsNumPanel     （待機人数）
    ├── PaticipantsList         （§9.1・Proto v0.5.0で実装可能）
    │   └── Paticipants         （Paticipant Prefab の親。参加人数ぶん生成し自店を強調表示）
    ├── Timer                   （countdownMs。「のこりOO秒」・§8.5）
    │   └── Text (TMP)
    └── MatchingComplete        （既定で非表示。カウントダウン後〜MatchStart まで・§8.5）
```

## 8. ふるまいの詳細

### 8.1 接続直後の1秒間

**状態が空**として画面を組む（§5.1）。`WaitingCount` / `MinPlayers` は「未取得」を表現できる形にする。

### 8.2 同時接続上限（今回は想定しない）

サーバーの同時接続上限は **200**（99人＋再接続・観戦の余裕）。超過すると **503** が返る。

**ハッカソン用途では上限に達しないため、503 専用のUI・再試行導線は作らない**（2026-08-06 決定）。

ただし **`Rejected` 状態そのものは残す。** 503 以外の接続失敗（サーバー未起動・URL誤り・ネットワーク断）は開発中に頻繁に起きるうえ、これを表示しないと**「Decide を押したのに何も起こらない」画面**になって原因が分からなくなる。`WaitingPanel` に接続失敗の文言を出すところまでを実装し、自動リトライは持たない。

### 8.3 脱落しても接続を切らない

これは試合中の話だが、接続のライフサイクルとして関係するため記す。**自店が脱落してもサーバーは接続を保持し、`StoreListUpdate` / `StoreEliminated` / `MatchEnd` を送り続ける**（観戦とリザルトのため）。マッチング画面側で `StoreEliminated` を根拠に切断処理を書かないこと。

### 8.4 3パネルの切り替え

`MatchMakingCanvas` 直下の3パネルは、`MatchmakingViewState.Panel` に従って**表示/非表示だけ**で切り替える（シーンを分けない）。

| `MatchmakingScreenState` | パネル |
|---|---|
| `NameEntry` | `WriteNameModal` |
| `Connecting` / `Joining` | `WaitingPanel` |
| `Waiting` / `CountingDown` | `MatchingPanel` |
| `Starting` | なし（シーン遷移中） |
| `Rejected` | `WaitingPanel`（接続失敗の文言を出す。§8.2） |

**この遷移は不可逆だが、そのためのラッチを別途持たない。** 「名前が確定した」「最初の `MatchmakingStatus` を受けた」はどちらも一度成立したら戻らないため、上の対応表をそのまま適用するだけで単調に進む。

> **`CountingDown` → `Waiting` の巻き戻り（§5.2）はパネルの巻き戻りではない。** どちらも `MatchingPanel` に対応するため、カウントダウンが中断しても画面は切り替わらず、`Timer` の表示だけが消える。**「不可逆」と §5.2 は矛盾しない。**

### 8.5 `Timer` の表示と `MatchingComplete`

`Timer/Text (TMP)` は「のこり**OO**秒」の形で出す（文面は `MatchmakingScreenView.timerFormat`）。秒数は `countdownMs` を**切り上げ**た値で、`countdownMs` が欠落している間は**空文字**にする（§3 のとおり 0 と出さない）。

**カウントダウンが尽きてから `MatchStart` が届くまで数秒の空白がある。** この間 `MatchingPanel/MatchingComplete` を出す。

問題は「尽きた」をどう知るかで、**サーバーは尽きた瞬間に `countdownMs: 0` を送り直さない**（配信されるのは待機ティッカー・開始時・中断時のみ。§5）。最後に届いた値のまま止まるので、サーバー値だけを見ていると「のこり1秒」で固まる。

そこで `countdownMs` を受け取った時刻からローカルの締切（`受信時刻 + countdownMs`）を引き、締切を過ぎたら完了として扱う。区分の判定は [`MatchmakingCountdownState`](../value-objects/README.md) の純粋関数が持つ。

| `countdownMs` | ローカル締切までの残り | 区分 |
|---|---|---|
| あり | > 0 | カウントダウン中（秒数を表示） |
| あり | ≦ 0 | **完了**（`MatchingComplete` を出す） |
| なし | > 0 | **中断**（§5.2。何も出さない） |
| なし | ≦ 0 | 完了 |
| なし | 締切なし | 待機中（何も出さない） |

- 締切はサーバー値が**変わったときだけ**引き直す。毎フレーム引き直すと締切が前へ逃げ続けて永久に尽きない
- **「尽きた」はマッチング成立の根拠ではない。** 成立は `MatchStart` 受信のみ（§1）。`MatchingComplete` は待ち時間の見た目にすぎず、これを根拠に画面遷移や送信を行わない
- 中断（§5.2）と完了を取り違えないこと。`minPlayers` を割り込んで `countdownMs` が消えた場合はまだ時間が残っているため、上表のとおり中断側に落ちる

### 8.6 名前確定と接続の順序

**`WriteNameModal` の Decide を押して初めて接続する。** `Boot` シーンでも `Title` シーンでも接続しない。

```
Boot（生成のみ・接続しない）
  → Title（Start ボタン）
  → MatchiMaking シーンをロード ＝ WriteNameModal
  → Decide 押下 ＝ 表示名確定 → ここで初めて Connect
  → 接続確立と同時に MatchmakingJoin 送信（WaitingPanel）
```

先に接続してしまうと、名前の入力に3秒以上かかった時点で表示名が失われる（§4.1・[02-display-name.md](./02-display-name.md) §5 ★）。**`Boot` シーンで「通信の確認」として実接続を行ってはいけない。**

## 9. 依存関係

- 依存する `pureC#` モジュール：`Contract`（Proto の DTO 型）、`Dispatcher`、`Store`
- 依存するUnity側モジュール：`WebGLNetworkClient`（[01-network-client.md](../platform/01-network-client.md)）、`GameBootstrapper`（[02-scene-composition.md](../foundation/02-scene-composition.md)）
- 依存されるモジュール：試合画面（`MatchStart` を受け取る側）

### 9.1 `PaticipantsList`（Proto v0.5.0 / REQ-03 対応済み）

**マッチング中に参加者の一覧・表示名を配る契約が Proto v0.5.0 で追加された**（[REQ-03](../../../../docs/server-sync/04-上流への依頼.md#req-03)。`MatchmakingStatus.participants` / `selfStoreId`）。

| 画面要素 | 実装状況 |
|---|---|
| `PaticipantsNumPanel`（待機人数） | ✅ `MatchmakingStatus.waitingCount` |
| `Timer`（締切） | ✅ `MatchmakingStatus.countdownMs` |
| `PaticipantsList`（待機中の参加者名一覧） | ✅ `MatchmakingStatus.participants`。Bot は含まない（定員補完は `MatchStart` 時） |
| 自分だけ赤で強調 | ✅ `MatchmakingStatus.selfStoreId` と `participants[].storeId` を突き合わせて判定 |

**参加者一覧は受信ぶんだけ `Paticipants` の下に `Paticipant` プレハブを生成する。** 空欄の枠を先に描かない（実データと乖離した見た目を作らないため。「他店の表示名をクライアントで生成・補完しない」[02-display-name.md](./02-display-name.md) §1 に整合）。`MatchmakingScreenView.ApplyParticipants` が人数の増減に合わせてプレハブを足し引きする。

## 10. テスト観点

- `MatchmakingJoin` が、接続確立後に送られる**最初の**メッセージであること
- `countdownMs` が欠落したメッセージで `CountdownMs` が `null` になり、**0 にならない**こと
- カウントダウン中に `countdownMs` の無いメッセージが来たら `Waiting` へ戻ること
- 接続直後〜最初の `MatchmakingStatus` 受信までの間、待機人数の数値が画面に出ないこと
- `MatchStart` 受信で `Starting` へ遷移し、以降 `MatchmakingStatus` を受けても状態が巻き戻らないこと
- カウントダウンの最後の値（例 1000ms）が届いたまま更新が止まっても、**「のこり1秒」で固まらず**に `MatchingComplete` へ移ること（§8.5）
- `minPlayers` 割れで `countdownMs` が消えたとき、`MatchingComplete` が**出ない**こと（中断と完了の取り違え）

## 11. 未確定事項

- ~~`Rejected` のときにどのパネルを出すか~~ → **決定（2026-08-06）**：`WaitingPanel` に接続失敗の文言。503 専用UIは作らない（§8.2）
- ~~`Rejected`（503）時の再接続ポリシー~~ → **決定（2026-08-06）**：自動リトライを持たない（§8.2）
- 試合中に接続が切れた場合の扱い（[SV-08](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-08)）。**サーバー側の再同期手段が無いため未解決**
- 待機画面の見た目（人数の出し方・カウントダウンの演出）
