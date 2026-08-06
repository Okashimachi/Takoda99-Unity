# 02-DisplayName

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`（`MatchmakingJoin.DisplayName` / `StoreSummary.DisplayName`）/ [Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) §2.1・§3.2・§3.10 / [用語集 2章](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

自店の表示名（画面の「たまちゃん屋」に相当）の**入力と送信**、および他店98店の表示名の**取得**を扱う。

## 1. 責務

- 自店の表示名をプレイヤーから受け取り、`MatchmakingJoin` に載せて送る
- 全店（自店＋他店98店）の表示名を `MatchStart.stores` / `StoreListUpdate.stores` から取得して保持する
- **しない**こと：表示名の検証・切り詰め・不適切語のフィルタを**クライアントの責任として実装しない**（§4。サーバーが正規化する）
- **しない**こと：他店の表示名をクライアントで生成・補完する（サーバーが必ず値を配る）

## 2. ⚠ 前提：C# ミラーに `displayName` が無い

**この仕様は、Proto の C# ミラーが修正されるまで実装に着手できない。**

| 言語 | v0.3.0 時点 | |
|---|---|---|
| **Go（正典）** | `DisplayName string \`json:"displayName,omitempty"\`` | ✅ **これが正しい** |
| C# | `public sealed class MatchmakingJoin { }` | ❌ フィールド無し |

Proto のコミット `d567a98`（2026-08-04）が Go のみを変更し、後続の v0.3.0 で C# に反映されなかった。

> TS ミラーにも同じ追従漏れがあるが、**このリポジトリは C# しか使わないため対象外**。要否の判断は Proto 側に委ねる。

**サーバーは `displayName` を読む実装になっている**ため、C# の型どおりに `{}` を送ると**全プレイヤーの表示名が空になり、フォールバック名が割り当たる**。

### 対応

1. **上流 Proto へ C# ミラーの追従を依頼する**（これが本筋。依頼文は [docs/server-sync/04-上流への依頼.md](../../../../docs/server-sync/04-上流への依頼.md#req-01)）
2. **このリポジトリで `pureC#/vendor/Takoda99.Proto/Messages.cs` を編集しない。** ミラーは正典の複製であり、こちら側で内容を変えない（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md) 絶対原則7）
3. 修正版の Proto が出たら `VERSION.md` の手順でミラーを差し替え、本仕様の実装に着手する

> 送信側の封筒を手組みして `displayName` を注入する回避策は**採らない。** 契約に無いフィールドをこのリポジトリの実装で足すことになり、絶対原則7に抵触する。ミラーが直るまで待つ。

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

> **現在の小画面（98店ミニ盤面）は他店の表示名を出していない**（[10-sub-store-board-view.md](../10-sub-store-board-view.md)）。値は届いているので、出す判断をした時点で追加できる。[pureC#/docs/.sdd/value-objects/02-store-state.md](../../../../pureC%23/docs/.sdd/value-objects/02-store-state.md) の `StoreSummaryState` は `DisplayName` を保持済み。

## 4. 表示名の制約（サーバー正規化）

| 項目 | 値 |
|---|---|
| 決定主体 | **プレイヤー入力** |
| 最大長 | **24文字** |
| 制御文字 | サーバーが**除去する** |
| 省略・空 | サーバーが**フォールバック名を割り当てる** |
| 送信タイミング | `MatchmakingJoin`（接続後3秒以内・[01](./01-matchmaking-flow.md) §4.1） |

**検証はサーバーが行う。クライアントは入力補助として同じ制限をUIに掛けてよいが、それを正としない。**

- 入力欄に24文字の上限を設けるのは**UXのため**であって、サーバーの制約を代替するものではない
- **サーバーが正規化した後の名前は `MatchStart.stores` の自店エントリで確認できる。** 送った文字列ではなく、**受信した `displayName` を画面に表示する**（切り詰めやフィルタの結果を正しく反映するため）
- 不適切語のフィルタはサーバー側の関心事。クライアントに辞書を持たない

## 5. Unity構成

- **入力UI**：マッチング画面（[01](./01-matchmaking-flow.md)）に表示名の入力欄を1つ置く。`TMP_InputField` を使い、`characterLimit = 24`
- **入力のタイミング**：`MatchmakingJoin` は接続後3秒以内に送る必要があるため、**接続してから名前を入力させる設計にしない。** 接続前の画面で名前を確定させ、接続確立と同時に送る
- **既定値**：前回入力した名前を `PlayerPrefs` に保持して初期値にしてよい（任意）

> ★**「接続 → 名前入力 → 送信」の順にすると、入力に3秒以上かかった時点で名前が失われる。** 必ず「名前入力 → 接続 → 即送信」の順にする。

## 6. 依存関係

- 依存する `pureC#` モジュール：`Contract`（`MatchmakingJoin` / `StoreSummary`）、`Store`（`StoreSummaryState.DisplayName`）
- 依存するUnity側モジュール：[01-matchmaking-flow.md](./01-matchmaking-flow.md)、`WebGLNetworkClient`
- 依存されるモジュール：小画面（他店名を出す判断をした場合）、主画面（自店名の表示）

## 7. テスト観点

- 名前入力 → 接続 → `MatchmakingJoin` 送信、の順に走ること（接続後に入力を待たないこと）
- 空欄のまま接続した場合でも `MatchmakingJoin` が送られること（空名でもサーバーがフォールバックするため、送らないより良い）
- 画面に出す自店名が、**送った文字列ではなく `MatchStart.stores` の受信値**であること
- `StoreListUpdate` で全店の `displayName` が上書きされ、脱落済みの店でも名前が保持されること
- 24文字を超える入力がUI上で止まること（サーバー正規化の代替ではなく、UXとして）

## 8. 未確定事項

- **上流 Proto の C# ミラー修正**（§2）。これが解決するまで実装しない
- 小画面に他店の表示名を出すか（現在の画面案では出していない。値は届いている）
- 前回名の `PlayerPrefs` 保持を行うか
- フォールバック名の書式（サーバーが何を割り当てるか未確認。表示上の桁数に影響し得る）
