# 03-CustomerState

> 参照する上流：[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`（`CustomerView` = `CustomerArrived` のペイロード / `CustomerLeft`）/ [用語集 3章「客・客プール」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 7章「我慢ゲージ・離脱」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。

## 1. 責務

- 自店の行列に存在する客1体ぶんの、サーバーから受信した事実を保持する
- **しない**こと：我慢ゲージの残量を保持しない（§4参照。残量はサーバーから配信されず、Unity側 `PatienceTimer` が起点 `PatienceStartedAtServerMs` から表示専用に算出する）
- **しない**こと：いらだち3段階＋退転の**ムード表示状態**を持たない（Unity側 `value-objects/02-customer-mood-state.md` の責務）

## 2. 前提：客のメッセージは自店専用

`CustomerArrived` のペイロードである `CustomerView` も、`CustomerLeft` も **`storeId` を持たない**。したがってどちらも自店に対する通知であり、**他店の客・行列の情報はクライアントに一切届かない**（[SV-10](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-10)）。

## 3. データ定義

```csharp
public enum CustomerAttribute { Normal, Bonus, Claimer, Buzz } // Proto と同一

public readonly record struct CustomerState(
    string CustomerId,
    CustomerAttribute Attribute,
    int PatienceMaxMs,          // CustomerView.patienceMaxMs
    int OrderCount,             // = 打つ単語数
    IReadOnlyList<string> Words, // お題単語。サーバー発行
    long PatienceStartedAtServerMs, // CustomerView.patienceStartedAtServerMs（Proto v0.3.0）。我慢ゲージ表示の起点
    long ArrivedAtElapsedMs     // 受信時点の MatchState.ElapsedMs。サーバー時刻の対応づけに使う
);
```

`PatienceStartedAtServerMs` は **Proto v0.3.0 で追加された、行列に入った時点の「試合開始からの経過ms」**（サーバー実装 `internal/game/session.go` の `c.patienceStartedAtMs = s.elapsedMs`）。我慢ゲージがこれを起点に描かれることで、**受信遅延ぶんのズレが表示に出なくなった**。

> **時刻同期は不要。** これは絶対時刻ではなく `MatchState.ElapsedMs` と同じ土俵の値（試合開始 = 0）であり、`MatchStart` 受信を 0 として自前で進めた時計とそのまま突き合わせられる（[client-integration §3.3](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md)）。

`ArrivedAtElapsedMs` は Proto に無いクライアント側の追加フィールドで、受信時点のローカル経過時間。**サーバーの経過時刻とローカル時計のドリフト検知**にのみ使う（表示の起点には使わない）。

## 4. 我慢ゲージ残量を保持しない理由

**現契約に我慢ゲージの残量（`patienceLeftMs`）を運ぶメッセージは存在しない。** `CustomerView`（v0.3.0）が持つのは `patienceMaxMs` と `patienceStartedAtServerMs` で、残量の周期的な同期メッセージは無い。クライアントが知り得るのは以下だけ。

- 来店時の最大値 `patienceMaxMs`
- カウントダウンの起点 `patienceStartedAtServerMs`（**v0.3.0 で追加**）
- 離脱した事実（`CustomerLeft`。`reason` は `LeaveReason.Timeout`）

したがって残量は「`PatienceMaxMs - (現在のサーバー推定時刻 - PatienceStartedAtServerMs)`」という**クライアントの推定値**にしかならず、サーバー権威の値として `CustomerState` に持たせるのは誤り。表示用の推定値は Unity側 `PatienceTimer` が算出する。

**既知の乖離リスク**（[SV-03](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-03) として調整中）:

- **★`Late` フェーズでゲージの減る速度が変わる。** サーバーは `stepPatience` で `effectiveDt = dtMs / Patience.LateMul` として**行列内の全客**の残量を削る。**`patienceMaxMs` は書き換わらず、減る速度だけが変わる**ため、線形にカウントダウンするクライアントは Late 突入以降ズレ続ける。しかも**来店済みの客にも即座に適用される**（Mid に来た客が待っている最中に Late へ移行したら、その瞬間から加速する）
- **`LateMul` は `GameParametersPublicSubset` に含まれない。** クライアントは倍率を知る手段が無く、**現契約では Late 以降の我慢ゲージを正しく描けない**
- WebGLでのタブ非アクティブによりローカル計測が遅れる。ただし起点が試合経過時刻のため、**復帰時に正しい残量へ復元できる**（受信時刻起点だった頃より影響は小さい）
- ~~ネットワーク遅延ぶんの初期ズレ~~ → **v0.3.0 で解消**（起点がサーバー基準になったため）

## 5. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `CustomerArrived`（ペイロード = `CustomerView`） | 新規 `CustomerState` を生成し、自店の `StoreQueue` 末尾へ追加。`ArrivedAtElapsedMs` に受信時点の `MatchState.ElapsedMs` を記録 |
| `CustomerLeft` | 対象 `CustomerId` を `StoreQueue` から除去し、`CustomerState` を破棄する。**離脱の確定は常にこのメッセージ**であり、クライアントの残量推定が0になっただけでは除去しない |
| 提供完了（`OrderProgressState` が `OrderCount` に到達し `OrderServed` を送信） | 対象客を `StoreQueue` から除去し、次の客が先頭へ繰り上がる |

## 6. 不変条件

- `Words.Count == OrderCount`（用語集4章「注文個数はそのままタイプする単語数になる」、Proto の `CustomerView` コメントも同旨）。一致しない場合はサーバー側の不整合として扱い、クライアントで補正しない
- `PatienceMaxMs > 0`

## 7. 依存関係

- 依存するモジュール：`Contract`（Proto の DTO 型）、`01-match-state.md`（`ElapsedMs` 基準時刻）
- 依存されるモジュール：`02-store-state.md`（`StoreQueue`）、`04-order-progress-state.md`、Unity側 `value-objects/02-customer-mood-state.md`、`Unity/docs/.sdd/match-view/05-patience-timer.md`

## 8. テスト観点

- `CustomerArrived` で行列末尾に追加され、`ArrivedAtElapsedMs` が記録されるか
- **残量推定が0になっても、`CustomerLeft` を受信するまで客が行列に残り続けるか**（サーバー権威の確認）
- `CustomerLeft` / 提供完了で対象客が確実に除去され、行列に幽霊が残らないか
- 先頭客の入れ替わり時、次客の `CustomerState` が正しく繰り上がって参照できるか

## 9. 未確定事項

- 終盤の我慢ゲージ短縮をクライアントへ伝える手段（[SV-03](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-03)）
- `orderCount` の取り得る最大値（[SV-14](../../../../docs/server-sync/01-プロトコル契約の差分.md#sv-14)）
