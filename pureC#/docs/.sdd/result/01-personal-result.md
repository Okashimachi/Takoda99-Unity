# 01-個人成績の保持と試合終了（`PersonalResult` / `MatchEnd`）

> 参照する上流：[Takoda99-Proto v0.8.0](https://github.com/Okashimachi/Takoda99-Proto)（`PersonalResult` / `MatchStats` / `MatchEnd`）／[12_差分_クライアント §6](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md)／[30_通信シーケンス §4.3・§5](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)。矛盾したら上流優先。

## 0. このファイルが解こうとしている問題

**予選で実際に起きたバグ**：

```
脱落モーダル →「次へ」→ 個人成績シーンへ遷移
                         ↓
             このシーンでサーバーからデータを受け取る設計だった
                         ↓
             しかしサーバーが個人成績を送るのは【全員の試合が終わった時】
                         ↓
             1位が決まる前に遷移すると、何も表示されない ★バグ
```

原因は **画面遷移のタイミングとデータ受信のタイミングが結びついていたこと**。プレイヤーがボタンを押す速さに、データの有無が依存していた。

**本選の解**：サーバーは**脱落した瞬間に** `PersonalResult` を送る。クライアントは受け取って保持し、個人成績画面は保持データを表示するだけ。**サーバーへ問い合わせない。**

## 1. 責務

**する**

- `PersonalResult` を受信して `ClientState` に保持する
- 次の試合が始まる前に、保持データを**確実に破棄する**
- `MatchEnd`（空ペイロード）を「試合が終わった」という合図としてのみ扱い、`ClientPhase.Result` へ移す

**しない**

- 個人成績をサーバーへ問い合わせない（**そういう C2S メッセージは存在しない**）
- `PersonalResult` の中身を再計算・補正しない（`Score` も `FinalRank` もサーバー権威）
- `MatchEnd` から順位や統計を読まない（**空クラスであり、フィールドが存在しない**）

## 2. 値オブジェクト

```csharp
namespace Takoda99.Client.State;

/// <summary>
/// 自店の脱落確定と同時に届く個人成績。**保持して、任意のタイミングで画面に出す。**
/// 予選の MatchResult を置き換える（MatchEnd が空になったため、成績の供給源はこれだけ）。
/// </summary>
public sealed class PersonalResultState
{
    /// <summary>確定した最終順位（1始まり）。リザルト演出の分岐はこの値だけで行う。</summary>
    public int FinalRank { get; init; }

    /// <summary>最終スコア。順位を決めた値そのもの。負値もあり得る。</summary>
    public int Score { get; init; }

    /// <summary>作ったたこ焼きの総数（＝累計 orderCount）。Stats.ServedCount とは別物。</summary>
    public int TakoyakiCount { get; init; }

    /// <summary>試合開始から脱落までの積算ミリ秒。</summary>
    public long SurvivedMs { get; init; }

    /// <summary>提供数・精度・属性別内訳などの統計（Proto DTO をそのまま保持する）。</summary>
    public MatchStats Stats { get; init; } = new();
}
```

> **`ServedCount` と `TakoyakiCount` を混同しない。** 前者は「提供した**客**の数」、後者は「作ったたこ焼きの数（客ごとの `orderCount` の合計）」。個人成績で「たこ焼き◯個」と出すのは後者。
>
> **総ミス数は `Stats.TotalMisses`。** `PersonalResult` に重複して持たれていない。

`ClientState` へ追加：

```csharp
/// <summary>受信して保持している個人成績。未受信なら null。</summary>
public PersonalResultState? PersonalResult { get; init; }

/// <summary>MatchEnd を受信済みか。試合全体が終わったことの唯一の合図。</summary>
public bool MatchEnded { get; init; }
```

既存の `MatchResult` クラスと `ClientState.Result` は**削除する**（`Reason` / `CreditLeft` / `EvalRaw` / `EvalNormalized` はすべて v0.8.0 で失われた値）。

## 3. Action と Reducer

### 3.1 `PersonalResultAction`

```csharp
public sealed class PersonalResultAction : IAction
{
    public int FinalRank { get; init; }
    public int Score { get; init; }
    public int TakoyakiCount { get; init; }
    public long SurvivedMs { get; init; }
    public MatchStats Stats { get; init; } = new();
}
```

Reducer：`state.With(personalResult: new PersonalResultState { … })`。

**`Phase` を変えない。** 受信時点ではまだ試合画面にいる（脱落モーダルの表示は `Phase == Spectating` への遷移が担う）。

**エッジケース**

| ケース | 扱い |
|---|---|
| `StoreEliminatedBatch` より先に届いた | そのまま保持する。順序に依存しない |
| 2回届いた | 後着で上書きする（冪等） |
| `Stats` が `null` | `Dispatcher` の Decode で `new MatchStats()` へ正規化する |
| 一度も届かないまま `MatchEnd` が来た | `PersonalResult == null` のままリザルトへ進む。**描画側は null を「成績なし」として扱い、画面を出さないのではなく空欄で出す**（試合が終わったのに画面から出られない状態を作らない） |

### 3.2 `MatchEndAction`

```csharp
/// <summary>ペイロードを持たない（Proto v0.8.0 の MatchEnd は空クラス）。</summary>
public sealed class MatchEndAction : IAction { }
```

Reducer：`state.With(matchEnded: true, phase: ClientPhase.Result)`。

| 旧（v0.5.0） | 新（v0.8.0） |
|---|---|
| `MatchEndAction { FinalRank, Stats, Reason, MatchElapsedMs, CreditLeft, EvalRaw, EvalNormalized }` | **フィールドなし** |
| `state.Result` に成績が入る | `state.PersonalResult` が既に持っている |
| 優勝者だけ `MatchEnd`、脱落者は `StoreEliminated` | **全員が `MatchEnd` を受け取る**（120秒に全店脱落） |

> **「優勝者には `StoreEliminated` が来ない」という予選の特殊ケースは消えた。** 決勝の10店も120秒に脱落し、`StoreEliminatedBatch` → `PersonalResult` → `MatchEnd` の同じ経路を通る（[30_通信シーケンス 5-B](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/30_通信シーケンス.md)）。**例外処理が要らない。**
>
> 既存 `Renderer.cs` にある「1位には `OnStoreEliminated` が来ないためここでしか順位一覧に載せられない」という分岐は、この変更によって**不要になる**（[../cleanup/01-removed-features.md](../cleanup/01-removed-features.md)）。

## 4. ★保持データの破棄（必須要件）

**満たすべき唯一の条件**：*次の試合の `MatchStart` を受け取る前に破棄されていること*。前の試合の成績が残っていると、次の試合で誤表示する。

### 決定：破棄は `MatchmakingJoin` を送る直前に、1箇所だけで行う

```csharp
/// <summary>試合に紐づくローカル保持値をすべて捨てる。再戦・タイトル復帰の入口で1回だけ呼ぶ。</summary>
public sealed class LocalMatchResetAction : IAction { }
```

Reducer のふるまい（`ClientState` を試合前の状態へ戻す）：

```
PersonalResult = null
MatchEnded     = false
Ranking        = new RankingTable()      // 空
DisplayNames   = 空の辞書
Cull           = null
Score = 0 / Rank = 0 / AliveCount = 0 / Alive = false
MatchId = "" / SelfStoreId = ""
Queue = 空 / CurrentOrder = null
Params         = new GameParametersPublicSubset()
```

`Connection` / `Phase` / `LastError` / `EventLog` は**触らない**（呼び出し側のライフサイクル管理に属する値のため）。

呼び出し箇所は `MatchClientController` の**2箇所だけ**：

| メソッド | タイミング |
|---|---|
| `BeginPlay(displayName)` | `Connecting` へ移る直前 |
| `Rematch()` | 切断して `Connecting` へ移る直前 |

**なぜこの方式か**

| 候補 | 評価 |
|---|---|
| リザルト画面を離れたとき | 画面側が破棄を担うことになり、**破棄の責務が複数の画面に散る**。予選のバグと同じ「画面の都合にデータの寿命が依存する」構造 |
| タイトルへ戻ったとき | リザルトから直接「再戦」した場合に破棄されない |
| **次の試合を始めるとき（採用）** | 破棄が**1箇所**に集まり、要件（次の `MatchStart` より前）を構造的に満たす。画面がいつ・何度データを読んでも安全 |

> **保険として `MatchStartAction` の Reducer でも `PersonalResult = null` / `MatchEnded = false` にする**（[../match-state/01 §3.1](../match-state/01-score-and-self-rank.md) 手順7）。二重の防御であり、こちらは「万一 `LocalMatchReset` を通らない経路が生まれても壊れない」ためのもの。**破棄の責務の所在は `LocalMatchReset` 側**。

## 5. 画面遷移との関係

```
自店の脱落確定
  ↓
StoreEliminatedBatch（自店を含む）→ Phase = Spectating   … match-state/03
PersonalResult                    → state.PersonalResult に保持  ← ★ここ
  ↓
脱落モーダルを表示（Unity 側）
  ↓
プレイヤーが「次へ」を押す ★いつ押してもよい
  ↓
個人成績シーンへ遷移 → state.PersonalResult を表示するだけ
  ↓
（試合は続いている）
  ↓
120秒：MatchEnd → Phase = Result → リザルトへ
```

| # | 満たすべきこと |
|---|---|
| 1 | 個人成績シーンは**サーバーへ何も送らない**（そういうメッセージが契約に存在しない） |
| 2 | 個人成績シーンへは**いつ遷移してもよい**。`state.PersonalResult` は脱落した瞬間から入っている |
| 3 | `Phase == Spectating` の間も `PersonalResult` は保持され続ける |
| 4 | `MatchEnd` で `Phase == Result` に変わっても `PersonalResult` は消えない |

## 6. `Dispatcher` の phase ゲート

| MessageType | 受け付ける `ClientPhase` |
|---|---|
| `PersonalResult` | `InMatch` / `Spectating` |
| `MatchEnd` | `InMatch` / `Spectating` |

`PersonalResult` を `Result` で受け付けないのは、`MatchEnd` より後に個人成績が来る経路がサーバー側に存在しないため（4-F / 5-B で必ず先行する）。

## 7. 依存関係

- 依存するモジュール：[contract/01](../contract/01-proto-v0.8.0-migration.md)、[../match-state/03-cull-warning.md](../match-state/03-cull-warning.md)
- 依存されるモジュール：[02-lifecycle-and-renderer.md](./02-lifecycle-and-renderer.md)、Unity `result-view/`

## 8. テスト観点

| # | 観点 |
|---|---|
| 1 | `PersonalResult` 受信 → `state.PersonalResult` に入り、**`Phase` が変わらない** |
| 2 | `PersonalResult` → `MatchEnd` の順で受信 → `Phase == Result` かつ `PersonalResult` が残っている |
| 3 | `MatchEnd` → `PersonalResult` の逆順でも、最終状態が2と同じ |
| 4 | `MatchEnd` を受けても `PersonalResult` が上書き・消去されない |
| 5 | `LocalMatchReset` 後、`PersonalResult == null` / `MatchEnded == false` / `Ranking.Rows` が空 |
| 6 | `LocalMatchReset` が `Connection` と `EventLog` を変えない |
| 7 | 1試合目の `PersonalResult` 保持 → `Rematch()` → 2試合目の `MatchStart` 時点で `PersonalResult == null` |
| 8 | `PersonalResult` 未受信のまま `MatchEnd` → 例外なく `Phase == Result` になる |
| 9 | `MatchEnd` のペイロードが `{}` でも `MatchEndAction` が生成される |

## 9. 未確定事項

- 個人成績画面に**どの項目を出すか**は企画・アートと合意する（[12_差分_クライアント §10](https://github.com/Okashimachi/Takoda99-Docs/blob/main/00_本選差分/12_差分_クライアント.md) 論点6）。ただし **`pureC#` は `PersonalResult` の全項目を保持する**ので、表示項目が後から増えても `pureC#` の変更は発生しない
