# 02-DisplayName

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`（`MatchmakingJoin.DisplayName` / `StoreSummary.DisplayName`）/ [Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) §2.1・§3.2・§3.10 / [用語集 2章](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

自店の表示名（画面の「たまちゃん屋」に相当）の**入力と送信**、および他店98店の表示名の**取得**を扱う。

## 1. 責務

- 自店の表示名をプレイヤーから受け取り、`MatchmakingJoin` に載せて送る
- 全店（自店＋他店98店）の表示名を `MatchStart.stores` / `StoreListUpdate.stores` から取得して保持する
- **しない**こと：表示名の検証・切り詰め・不適切語のフィルタを**クライアントの責任として実装しない**（§4。サーバーが正規化する）
- **しない**こと：他店の表示名をクライアントで生成・補完する（サーバーが必ず値を配る）

## 2. ⚠ 実装の前提：上流の変更待ち（合意済み・作業中）

**送信部分（§5・§6）は、Proto の C# ミラーに `MatchmakingJoin.displayName` が入るまで実装できない。** 受信部分（§3 の他店名取得）は現契約のままで実装できる。

| 言語 | v0.3.0 時点 | |
|---|---|---|
| **Go（正典）** | `DisplayName string \`json:"displayName,omitempty"\`` | ✅ **これが正しい** |
| C# | `public sealed class MatchmakingJoin { }` | ❌ フィールド無し |

Proto のコミット `d567a98`（2026-08-04）が Go のみを変更し、後続の v0.3.0 で C# に反映されなかった。**サーバーは `displayName` を読む実装になっている**ため、C# の型どおりに `{}` を送ると**全プレイヤーの表示名が空になり、フォールバック名が割り当たる**。

### 依頼済みの内容（2026-08-06 合意）

サーバーマネージャと合意し、**上流での実装内容を確定させた**。指示書は [05-表示名の実装指示.md](../../../../docs/server-sync/05-表示名の実装指示.md)。

| # | 宛先 | 内容 |
|---|---|---|
| A | Proto | C# ミラーに `displayName` を追加 |
| B | サーバー | 表示名の最大長を 24 → **6文字** へ |
| C | サーバー | フォールバック名・Bot名も6文字以内にし、書式を共有 |
| D | Docs | 正典の「24文字」の記述を更新 |

### このリポジトリで守ること

1. **`pureC#/vendor/Takoda99.Proto/Messages.cs` を編集しない。** ミラーは正典の複製であり、こちら側で内容を変えない（[01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md) 絶対原則7）
2. **送信側の封筒を手組みして `displayName` を注入する回避策も採らない。** 契約に無いフィールドをこのリポジトリの実装で足すことになり、同じ原則に抵触する
3. 上流のタグが切られたら [05-表示名の実装指示.md](../../../../docs/server-sync/05-表示名の実装指示.md#こちら側の追従手順上流タグ確定後) の手順で追従する

### 先に実装してよいもの

上流待ちなのは**「`MatchmakingJoin` に名前を載せて送る」ところだけ**。以下は今のうちに作ってよい。

- 入力UI（`WriteNameModal` / `NameInputField` / `Decide`）と、`characterLimit = 6`
- 「名前確定 → 接続」の**順序**（[01-matchmaking-flow.md](./01-matchmaking-flow.md) §8.5）。名前を保持しておき、送信だけ後から繋ぐ
- `BeginPlay(displayName)` の配線（[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) §3.6）。**`MatchmakingJoin` に代入する1行だけがミラー待ち**
- 受信した名前の表示（§3・§4）

## 3. 他店の表示名の取得

**専用のメッセージは無い。全店ぶんがまとめて2経路で届く。**

| 経路 | タイミング | 内容 |
|---|---|---|
| `MatchStart.stores` | 試合開始時に1回 | 全店（`maxStores` 件）の `StoreSummary` |
| `StoreListUpdate.stores` | **250ms ごと**に全員へ | 同上（フルスナップ） |

`StoreSummary.displayName` は**生存・脱落を問わず常に入っている**。したがって他店名は `StoreListUpdate` を保持していれば常に参照できる。

- **`MatchStart` の時点で全店名が揃う。** 他店名の表示開始を `StoreListUpdate` の初回まで待つ必要はない
- 表示名は**試合中に変わらない**前提でよい（サーバー側に改名の経路が無い）。ただし `StoreListUpdate` は毎回フルスナップなので、**受信値で素直に上書きしてよい**（差分検知して据え置く最適化は不要）
- 定員に満たないぶんは **Bot が補完**される。Bot の表示名もサーバーが配るため、クライアント側で「Bot だから名前が無い」ケースを考えなくてよい

> **現在の小画面（98店ミニ盤面）は他店の表示名を出していない**（[04-sub-store-board-view.md](../match-view/04-sub-store-board-view.md)）。値は届いているので、出す判断をした時点で追加できる。[02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md) の `StoreSummaryState` は `DisplayName` を保持済み。

## 4. 表示名の制約（サーバー正規化）

| 項目 | 値 |
|---|---|
| 決定主体 | **プレイヤー入力** |
| 最大長（**サーバー**） | **6文字**（依頼済み・上流の実装待ち。現行は24文字） |
| 最大長（**入力欄**） | **6文字**（UIの制約。下記） |
| 制御文字 | サーバーが**除去する** |
| 省略・空 | サーバーが**フォールバック名を割り当てる** |
| 送信タイミング | `MatchmakingJoin`（接続後3秒以内・[01](./01-matchmaking-flow.md) §4.1） |

**検証はサーバーが行う。クライアントは入力補助として同じ制限をUIに掛けてよいが、それを正としない。**

- 入力欄の上限は **6文字**（`NameInputField.characterLimit = 6`）。マッチング画面の参加者一覧が縦9×横11の小さなタイルで、長い名前を収めきれないための**表示上の都合**
- **サーバー側の上限も6文字へ揃えることで合意済み**（2026-08-06。[05-表示名の実装指示.md](../../../../docs/server-sync/05-表示名の実装指示.md) の B）。**上流に入るまでは、入力欄の6文字制限は自分の名前にしか効かない。** 他プレイヤーやBot、フォールバック名は**最大24文字で届き得る**
- **クライアントの入力制限をサーバーの制約の代替にしない。** 上限が6に揃ったあとも、受信した名前を切り詰めずに描ける幅を確保するか、TMPの省略表示（`overflow`）で受けること。**受信値を6文字で切って表示する処理をクライアントに書かない**（サーバーが正規化した結果を勝手に加工することになる）
- 入力欄に上限を設けるのは**UXのため**であって、サーバーの制約を代替するものではない
- **サーバーが正規化した後の名前は `MatchStart.stores` の自店エントリで確認できる。** 送った文字列ではなく、**受信した `displayName` を画面に表示する**（切り詰めやフィルタの結果を正しく反映するため）
- 不適切語のフィルタはサーバー側の関心事。クライアントに辞書を持たない

## 5. Unity構成

- **入力UI**：`MatchiMaking` シーンの `MatchMakingCanvas/WriteNameModal/NameInput` に `NameInputField`（`TMP_InputField`・`characterLimit = 6`）と `Decide`（Button）を置く（[01](./01-matchmaking-flow.md) §7.1）
- **入力のタイミング**：`MatchmakingJoin` は接続後3秒以内に送る必要があるため、**接続してから名前を入力させる設計にしない。** 接続前の画面で名前を確定させ、接続確立と同時に送る。`Decide` 押下が接続の起点になる（[01](./01-matchmaking-flow.md) §8.5）
- **既定値**：前回入力した名前を `PlayerPrefs` に保持して初期値にしてよい（任意）

> ★**「接続 → 名前入力 → 送信」の順にすると、入力に3秒以上かかった時点で名前が失われる。** 必ず「名前入力 → 接続 → 即送信」の順にする。

## 6. 依存関係

- 依存する `pureC#` モジュール：`Contract`（`MatchmakingJoin` / `StoreSummary`）、`Store`（`StoreSummaryState.DisplayName`）
- 依存するUnity側モジュール：[01-matchmaking-flow.md](./01-matchmaking-flow.md)、`WebGLNetworkClient`
- 依存されるモジュール：小画面（他店名を出す判断をした場合）、主画面（自店名の表示）

## 7. テスト観点

- 名前入力 → 接続 → `MatchmakingJoin` 送信、の順に走ること（接続後に入力を待たないこと）
- 空欄のまま接続した場合でも `MatchmakingJoin` が送られること（空名でもサーバーがフォールバックするため、送らないより良い）
- **再接続時に再送される `MatchmakingJoin` にも同じ名前が載ること**（[06-match-client-controller.md](../../../../pureC%23/docs/.sdd/06-match-client-controller.md) §3.6。送信箇所が2つあり、片方だけ直すと再接続時のみ名前が消える）
- 画面に出す自店名が、**送った文字列ではなく `MatchStart.stores` の受信値**であること
- `StoreListUpdate` で全店の `displayName` が上書きされ、脱落済みの店でも名前が保持されること
- 6文字を超える入力がUI上で止まること（サーバー正規化の代替ではなく、UXとして）
- **7文字以上をサーバーへ直接送った場合に、受信値が6文字へ切り詰められていること**（サーバー側の正規化の確認。上流の B が入った後）

## 8. 未確定事項

- **上流の実装待ち**（§2）。[05-表示名の実装指示.md](../../../../docs/server-sync/05-表示名の実装指示.md) の A〜D
- **フォールバック名・Bot名の書式**（上流 C で共有依頼中）。表示幅の設計に影響する
- **サーバー側の文字数の数え方**（コードポイント / バイト / rune）。`TMP_InputField.characterLimit` は UTF-16 コードユニット数で数えるため厳密には一致しない。**サーバーの数え方が正**
- 小画面に他店の表示名を出すか（現在の画面案では出していない。値は届いている）
- 前回名の `PlayerPrefs` 保持を行うか
