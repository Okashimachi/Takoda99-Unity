# 05-Dispatcher（メッセージディスパッチ層）

> 参照する上流：[Takoda99-Client-Docs 第5章 全体](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)（受信フロー・送信キュー・接続断・バージョン不一致）／[第7章 §3・§4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/07_ライフサイクルと画面遷移.md)（phase 別の受理表）。矛盾したら上流優先。

## 1. 責務

**する：**
- 受信 `Envelope` を `type` で振り分け、payload をデコードして **Action 化し `Store` へ渡す**。
- 現在の `phase` で受理しないメッセージを**破棄＋ログ**する。
- 未知 `type`・デコード失敗を**破棄＋ログで継続**させる（前方互換）。
- 送信の**単一 FIFO キュー**（順序保証・再送方針・上限）。
- 送受信した全 `Envelope` の生 JSON を**リングバッファに保持**する。

**しない：**
- **ゲーム計算**（Action へ変換して渡すだけ。[第5章 §3 約束3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）。
- WebSocket の接続そのもの（`INetworkClient` の実体は Unity 側）。
- phase の**遷移判断**（遷移は [06-match-client-controller](./06-match-client-controller.md)。ここは「現在 phase を見て受理可否を決める」だけ）。

## 2. 公開インターフェース

```csharp
namespace Takoda99.Client.Net;

/// <summary>通信の抽象。実体は Unity 側（WebGLNetworkClient）。</summary>
/// <remarks>
/// WebGL は Thread / 一部 Task に制約があるため、async/await を契約に持ち込まず
/// コールバック／イベントで表現する（第3章 §6-5）。
/// </remarks>
public interface INetworkClient
{
    ConnectionState State { get; }
    void Connect(string url);
    void Disconnect();
    /// <summary>Envelope に包んで送る。実際の送信順序は ISendQueue が保証する。</summary>
    void Send(string type, object payload);

    event Action<string> OnReceiveRaw;                        // 生 JSON テキスト
    event Action<ConnectionState, string?> OnConnectionChanged;
}

public interface IDispatcher
{
    /// <summary>受信した生 JSON を処理する（デコード → 受理判定 → Action 化 → Store.Apply）。</summary>
    void HandleRaw(string json);

    /// <summary>未知 type を検出したときの通知（開発ビルドでのみ画面表示する）。</summary>
    event Action<string, string> OnUnknownMessage;  // (type, reason)

    /// <summary>受理されず破棄されたときの通知（phase 外・デコード失敗）。</summary>
    event Action<string, string> OnMessageDropped;  // (type, reason)
}

/// <summary>送信キュー。順序保証と再送方針を1箇所に閉じる。</summary>
public interface ISendQueue
{
    void Enqueue(string type, object payload);
    /// <summary>接続確立時に呼ぶ。キュー順で flush する。</summary>
    void Flush();
    /// <summary>切断時に呼ぶ。OrderServed を破棄する（§3.3）。</summary>
    void OnDisconnected();
}

/// <summary>送受信の生 JSON を時系列1本で保持する（デバッグパネル用）。</summary>
public interface IEnvelopeLog
{
    void RecordIncoming(string json);
    void RecordOutgoing(string json);
    IReadOnlyList<EnvelopeLogEntry> Entries { get; }  // 新しい順
}
```

## 3. ふるまいの詳細

### 3.1 受信フロー（[第5章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）

```
HandleRaw(json)
  → IEnvelopeLog に記録
  → IEnvelopeCodec.DecodeEnvelope が null → 破棄 + ログ（接続は切らない）
  → Envelope.type で分岐
      ├ 既知 type → 現在 phase の受理表を確認
      │    ├ 受理しない phase → 破棄 + OnMessageDropped
      │    └ 受理する → payload をデコード
      │         ├ 成功 → Action 化 → Store.Apply →（必要なら Renderer へイベント通知）
      │         └ 失敗 → 破棄 + OnMessageDropped
      └ 未知 type → 破棄 + OnUnknownMessage（前方互換：新メッセージ追加で落ちない）
```

**設計上の約束**
1. **1メッセージの失敗で接続を切らない。** 破棄してログに残し、次のメッセージを処理する。
2. **`switch`（type → ハンドラ）を1箇所に集約する。** 追加時の変更点が1点で済む形を保つ。
3. **ハンドラ内でゲーム計算をしない。**
4. **未知 type は無視が正**（クラッシュ・警告ダイアログにしない）。開発時のみ画面に出す。

### 3.2 phase 別の受理表（[第7章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/07_ライフサイクルと画面遷移.md) が正典）

| S2C メッセージ | Boot | Title | Connecting | Matchmaking | InMatch | Spectating | Result |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| `MatchmakingStatus` | – | – | ○ | ✅ | – | – | – |
| `MatchStart` | – | – | – | ✅ | – | – | – |
| `CustomerArrived` / `CustomerLeft` | – | – | – | – | ✅ | – | – |
| `CreditUpdate` | – | – | – | – | ✅ | – | – |
| `EvaluationUpdate` | – | – | – | – | ✅ | ○ | – |
| `DifficultyUpdate` | – | – | – | – | ✅ | ○ | – |
| `PhaseChange` | – | – | – | – | ✅ | ○ | – |
| `StoreListUpdate` | – | – | – | ○ | ✅ | ✅ | ○ |
| `ForcedEliminationWarning` | – | – | – | – | ✅ | ○ | – |
| `StoreEliminated` | – | – | – | – | ✅ | ✅ | ○ |
| `MatchEnd` | – | – | – | – | ✅ | ✅ | – |

凡例：✅=主要受理、○=受理可（表示更新のみ）、–=無視（ログ）。

### 3.3 送信フロー（[第5章 §4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）

**送るのはこの3種のみ。** 契約外の `type` を生やさない。

| メッセージ | 送信契機 | 許可 phase |
|---|---|---|
| `OrderServed` | 注文 N 単語を打ち切った瞬間 | **InMatch のみ** |
| `MatchmakingJoin` | 接続確立直後／キュー参加操作 | Connecting 直後・Matchmaking |
| `MatchmakingLeave` | キュー離脱操作 | Matchmaking |

- **単一 FIFO キュー**で順序を保証する（`MatchmakingLeave` → `MatchmakingJoin` の意思の順序が入れ替わらないこと）。
- 未接続・再接続中の送信は**キューに積み**、接続確立後に順に flush する。
- **`OrderServed` は再送しない。** 切断中に発生した `OrderServed` は**破棄**しログに残す（時限性が高く、遅延到達は不整合の元）。
- **`MatchmakingJoin` / `MatchmakingLeave` は最新の意思のみ保持**（キュー内に同種があれば置換）。
- キュー上限（提案：16件）を超えたら**古いものから捨てる**。
- **冪等性**：`OrderServed` は同一 `customerId` に対し1試合中1回だけ。`CurrentOrder` を `null` にしてから送信する順序を守る。

### 3.4 接続断・再接続（[第5章 §5](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）

| 状況 | 挙動 |
|---|---|
| 接続確立前に失敗 | `Failed` + `LastError`。**指数バックオフで最大3回**（1s/2s/4s）自動リトライ。以降は手動リトライ導線 |
| マッチング待機中に切断 | `Reconnecting` へ。再接続成功で **`MatchmakingJoin` を再送**（待機プールから外れているため） |
| 試合中に切断 | `Reconnecting` へ。入力停止・切断表示。再接続を試行 |
| 試合中に再接続成功 | **同一試合への復帰はサポート対象外（現時点）。** `MatchEnd` を待たずリザルト（切断終了）へ |
| 再接続が最大回数失敗 | `Failed`。タイトルへ戻る導線 |

- **再接続中に受信 state を捨てない**（最後の state を保持し「切断中」オーバーレイを重ねる）。
- クライアント側で勝手に復帰プロトコルを設計しない（契約変更は Proto の承認フロー）。

### 3.5 proto バージョン不一致（[第5章 §6](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）

| 症状 | 解釈 | 挙動 |
|---|---|---|
| 未知 `type` が届く | サーバーが新しい | 無視して継続。開発時は警告表示 |
| 既知 `type` の必須フィールド欠落 | 破壊的変更 | 破棄＋ログ。連続したら「バージョン不一致の可能性」を開発用表示に出す |
| `MatchStart` のデコードに失敗 | 致命的 | 試合を開始せず `Failed`。ユーザーには再読み込みを促す |

### 3.6 ロギング
- 受信した全 `Envelope` を生 JSON のまま**リングバッファに保持**（提案：直近 200 件）。**送信も含めて時系列1本**に並べる。
- 開発ビルドで画面表示できるようにする（Unity 側デバッグパネル）。「サーバーのバグか表示のバグか」を即断できる状態を維持する。

## 4. 依存関係

- 依存するモジュール：[01-contract](./01-contract.md)（`IEnvelopeCodec`）、[04-store-reducer](./04-store-reducer.md)（`IStore`）
- 依存されるモジュール：[06-match-client-controller](./06-match-client-controller.md)
- `INetworkClient` は**インターフェースのみ pureC# に置き、実体は Unity 側**

## 5. テスト観点

| # | ケース | 期待 |
|---|---|---|
| 1 | 全 S2C の振り分け | 対応する Action が `Store.Apply` に渡る |
| 2 | 壊れた JSON | 例外にならず破棄・後続メッセージは処理される |
| 3 | 未知 `type` | `OnUnknownMessage` 発火・state 不変・例外なし |
| 4 | 必須フィールド欠落 | 破棄＋`OnMessageDropped`・state 不変 |
| 5 | phase 外メッセージ（例：`Title` で `CustomerArrived`） | 破棄＋ログ・state 不変 |
| 6 | `Spectating` での `EvaluationUpdate` | 受理される（○） |
| 7 | `Spectating` での `CustomerArrived` | 無視される（–） |
| 8 | 送信順序 | Enqueue 順に flush される |
| 9 | 切断中の `OrderServed` | **破棄される**（再送されない） |
| 10 | 切断中の `Join`→`Leave`→`Join` | 最新の `Join` のみ残る |
| 11 | キュー上限超過 | 古いものから捨てられる |
| 12 | 再接続成功（待機中） | `MatchmakingJoin` が再送される |
| 13 | ログリングバッファ | 上限超過で古いものから消える・送受信が時系列で並ぶ |

> `INetworkClient` / `IStore` / `IEnvelopeCodec` はフェイクを用意し、実通信なしでテストする。

## 6. 未確定事項

- ハートビート／keep-alive を入れるか（プロキシのアイドルタイムアウト対策。サーバー仕様と揃える。[第5章 §9](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)）。
- 再接続時の同一試合復帰の可否（サーバー仕様待ち）。入ったら §3.4 と第7章 §5 を同時に更新する。
- 再接続リトライ回数・バックオフの確定値（提案：3回 / 1s・2s・4s）。
- 送信キュー上限の確定値（提案：16件）。
