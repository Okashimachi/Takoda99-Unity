# 07-ScenarioPlayer

> 参照する上流：[Takoda99-Client-Docs 第3章](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`INetworkClient` / `IInputSource` の抽象）/ [Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`（S2Cメッセージの形）。矛盾したら上流優先。

サーバーへ接続せず、**サンプルデータ（シナリオ）を流し込んでクライアントの状態遷移と表示分岐を検証する**ための再生モジュール。

## 1. 責務

**する**こと：

- シナリオファイル（S2Cメッセージ列＋発生時刻＋入力キー）を読み込む
- `INetworkClient` の実装として振る舞い、シナリオの `receive` ステップを `OnReceiveRaw` へ生JSONとして流す
- `IClock` の実装として時刻を供給し、シナリオの時刻に従って**決定論的に**進める（実時間を待たない）
- `IInputSource` の実装として、シナリオの `input` ステップで文字キーを発火する
- 接続状態の変化（`OnConnectionChanged`）を再現する

**しない**こと：

- **サーバーのロジックを一切再現しない。** 客分配・評価計算・信用増減・フェーズ判定・火力更新・下位淘汰判定・お題単語生成のいずれも実装しない（[docs/rules/01-責務と絶対原則.md](../../../docs/rules/01-責務と絶対原則.md)）
- シナリオの内容に応じて値を計算・補間しない。**書かれている値をそのまま流すだけ**
- 検証（アサーション）自体は持たない。アサーションはテストコード側に書く

## 2. サーバー権威の値の扱い（この仕様書の要）

**シナリオに書く値は「サーバーが決めた結果」であり、クライアントはそれを再現も検証もしない。**

たとえば「クレーマー客に時間をかけたら評価が下がる」という挙動をテストしたい場合、`ScenarioPlayer` が客の属性から評価を計算するのではなく、**シナリオに次のように結果だけを書く**。

```jsonc
// 1) クレーマーが来店（属性はサーバーが決めた事実）
{ "atMs": 1000, "kind": "receive", "type": "CustomerArrived",
  "payload": { "customerId": "c-01", "attribute": "Claimer", "orderCount": 6, "words": ["たこ","やき","ふね","かぜ","みせ","あめ"], "patienceMaxMs": 12000 } }

// 2) 時間が経ち、サーバーが評価を下げた結果が届く（下げ幅の算出はサーバーの責務）
{ "atMs": 9000, "kind": "receive", "type": "EvaluationUpdate",
  "payload": { "evalRaw": 2.8, "normalized": 0.21, "rank": 63, "aliveCount": 71 } }
```

クライアント側のテストで確認するのは「**`normalized: 0.21` を受け取ったとき、順位バーの位置とミニ盤面の色が正しくなるか**」であって、0.21 という値の正しさではない。値の正しさはサーバーの責務。

この分離により、サーバー未完成でもクライアントの分岐を網羅的にテストできる。

## 3. シナリオのデータ形式

JSON。`pureC#/testdata/scenarios/*.json` に置く。

```jsonc
{
  "name": "claimer-drops-evaluation",
  "description": "クレーマー客の対応が遅れ、評価が下がって赤帯に落ちる",
  "steps": [
    { "atMs": 0,    "kind": "connection", "state": "Connected" },
    { "atMs": 0,    "kind": "receive", "type": "MatchStart", "payload": { } },
    { "atMs": 1000, "kind": "receive", "type": "CustomerArrived", "payload": { } },
    { "atMs": 1500, "kind": "input",   "keys": "takoyaki" },
    { "atMs": 9000, "kind": "receive", "type": "EvaluationUpdate", "payload": { } },
    { "atMs": 9500, "kind": "wait" }
  ]
}
```

| `kind` | 意味 | フィールド |
|---|---|---|
| `receive` | S2Cメッセージの受信を再現 | `type`（`MessageType` の値）、`payload`（Protoの該当DTOの形） |
| `input` | 文字キー入力を再現 | `keys`（1文字ずつ順に `OnCharKey` を発火） |
| `connection` | 接続状態の変化 | `state`（`ConnectionState`）、`error`（任意） |
| `wait` | 時刻を進めるだけ | なし |

- `atMs` は**シナリオ開始からの相対ミリ秒**。昇順に並べる。同一 `atMs` は記述順に実行する
- `payload` は `Envelope` に包まれて `{"type": ..., "payload": ...}` の生JSONとして `OnReceiveRaw` へ渡る。**`ScenarioPlayer` は payload の中身を検証しない**（不正な payload を流して耐性を試せるようにするため）

## 4. 公開インターフェース

```csharp
namespace Takoda99.Client.Testing;

public sealed class Scenario
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<ScenarioStep> Steps { get; init; } = Array.Empty<ScenarioStep>();

    /// <summary>JSON文字列から読み込む。形式不正なら ScenarioFormatException。</summary>
    public static Scenario Parse(string json);

    /// <summary>testdata/scenarios/{name}.json を読み込む。</summary>
    public static Scenario Load(string name);
}

public sealed class ScenarioStep
{
    public long AtMs { get; init; }
    public string Kind { get; init; } = "";      // receive / input / connection / wait
    public string? Type { get; init; }           // kind == receive
    public JsonElement? Payload { get; init; }   // kind == receive
    public string? Keys { get; init; }           // kind == input
    public ConnectionState? State { get; init; } // kind == connection
    public string? Error { get; init; }          // kind == connection
}

/// <summary>シナリオを INetworkClient / IInputSource / IClock として供給する再生機。</summary>
public sealed class ScenarioPlayer : INetworkClient, IInputSource, IClock, IDisposable
{
    public ScenarioPlayer(Scenario scenario);

    /// <summary>現在のシナリオ内時刻（ms）。</summary>
    public long CurrentMs { get; }

    /// <summary>まだ実行していないステップが残っているか。</summary>
    public bool HasPendingSteps { get; }

    /// <summary>指定時刻まで進め、その時刻以下の未実行ステップをすべて実行する。</summary>
    public void AdvanceTo(long atMs);

    /// <summary>相対時間ぶん進める。</summary>
    public void Advance(long deltaMs);

    /// <summary>最後のステップまで一気に実行する。</summary>
    public void RunToEnd();

    /// <summary>実行済みステップの記録（テストの事後検証・デバッグ用）。</summary>
    public IReadOnlyList<ScenarioStep> Executed { get; }

    // INetworkClient
    public ConnectionState State { get; }
    public void Connect(string url);
    public void Disconnect();
    public void Send(string type, object payload);
    public event Action<string>? OnReceiveRaw;
    public event Action<ConnectionState, string?>? OnConnectionChanged;

    /// <summary>クライアントが送信した C2S メッセージ（OrderServed 等）の記録。</summary>
    public IReadOnlyList<(string Type, object Payload)> Sent { get; }

    // IInputSource
    public event Action<char>? OnCharKey;

    // IClock
    public long MonotonicMs { get; }
    public long WallClockUnixMs { get; }
}
```

## 5. ふるまいの詳細

### 決定論

- **実時間を一切待たない。** `MonotonicMs` は `CurrentMs` をそのまま返し、`AdvanceTo` でのみ進む
- `WallClockUnixMs` は固定の基準時刻＋`CurrentMs`。`clientTimestamp` が毎回変わってテストが不安定になるのを防ぐ
- 同じシナリオを2回流したら、`Sent` の内容まで完全に一致すること

### ステップ実行

- `AdvanceTo(t)` は「`AtMs <= t` の未実行ステップを昇順に実行してから、`CurrentMs = t` にする」
- 各ステップ実行の瞬間、`CurrentMs` はそのステップの `AtMs` に一致していること（イベントハンドラ内で時刻を読んでも矛盾しないため）
- `Connect()` / `Disconnect()` の呼び出しは記録するだけで、接続状態はシナリオの `connection` ステップのみが変える

### エッジケース

| ケース | 挙動 |
|---|---|
| `atMs` が昇順でない | `Scenario.Parse` で `ScenarioFormatException` |
| 未知の `kind` | `ScenarioFormatException` |
| 未知の `type`（`MessageType` に無い） | **そのまま流す。** `Dispatcher` の前方互換（未知typeを無視）を試すために必要 |
| `payload` が壊れている | そのまま流す。デコード側の耐性を試すために必要 |
| `AdvanceTo` に過去の時刻 | `ArgumentOutOfRangeException`（巻き戻しはできない） |
| 空の `steps` | 正常。`RunToEnd()` は何もしない |

## 6. サンプルデータの置き場所とビルド設定

```
pureC#/
  testdata/
    scenarios/
      minimal-match.json
      claimer-drops-evaluation.json
      ...
```

テストプロジェクトから読めるよう、`Takoda99.Client.Tests.csproj` に出力コピーを追加する。

```xml
<ItemGroup>
  <None Include="..\..\testdata\scenarios\*.json"
        Link="testdata\scenarios\%(Filename)%(Extension)"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## 7. 用意するシナリオ（最低限）

各シナリオは**1つの分岐を確認する**粒度にする。網羅は組み合わせではなく本数で稼ぐ。

| シナリオ | 確認する分岐 |
|---|---|
| `minimal-match` | `MatchStart` → 客1人来店 → 全単語打鍵 → `OrderServed` 送信。最短の正常系 |
| `order-progress-variants` | `orderCount` が 4/6/8/12 のとき、たこ焼き台の `Batter`/`Cooked`/`Empty` の数が正しいか |
| `customer-leaves-while-typing` | **打鍵の途中で `CustomerLeft` が届く。** 進行中の注文が破棄され、次客へ繰り上がるか（幽霊注文が残らないか） |
| `patience-expires-but-no-leave` | 我慢ゲージの推定値が0になっても `CustomerLeft` が来ない。**客が行列に残り続けるか**（サーバー権威の確認） |
| `queue-accumulates` | 複数 `CustomerArrived` → 行列の並び順と先頭の繰り上がり |
| `evaluation-bands` | `normalized` を 0.9 / 0.5 / 0.1 と流し、評価3段階（緑/黄/赤）が切り替わるか |
| `credit-decreases` | `CreditUpdate` で `life` が 3→2→1→0。提灯の点灯数が追従し、**`delta` の加算で値を作っていないか** |
| `store-list-snapshot` | `StoreListUpdate` のフルスナップで全店が置換され、**自店の `StoreState` が巻き戻らないか** |
| `self-eliminated` | 自店の `StoreEliminated` → リザルト遷移、以後の入力が無効になるか |
| `other-store-eliminated` | 他店の `StoreEliminated` → ミニ盤面のみ更新、自店は無影響 |
| `phase-and-heat` | `PhaseChange` / `DifficultyUpdate`。**`DifficultyUpdate` 未受信の間 `heatLevel` が 0 のままか** |
| `unknown-message-type` | 未知typeを流しても落ちず、後続のメッセージが正常に処理されるか（前方互換） |
| `few-players-match` | `maxStores` に満たない人数（例20店）で開始。ミニ盤面が破綻しないか |

## 8. 依存関係

- 依存するモジュール：`01-contract`（`Envelope` の形）、Proto の DTO
- 依存されるモジュール：テストプロジェクトのみ。**製品コード（`Takoda99.Client` の本体機能）からは依存しない**

### Unity側 View 用派生状態との通しテスト

`Unity/tests/Takoda99.View.Tests` は Unity を起動しない通常の .NET テストプロジェクトであり、View用派生状態も `UnityEngine` に依存しない純粋なC#である。したがって、**同プロジェクトに `Takoda99.Client` への `ProjectReference` を足せば、`Store` → 派生状態 → アサーション の通しテストを書ける**（Unity側の参照方法とは独立に成立する）。

シナリオ再生から表示分岐までを一続きで検証したい場合は、この形を使う。
- `Takoda99.Client.Testing` 名前空間に置き、`Takoda99.Client` 本体アセンブリに含めるか別アセンブリにするかは §10 参照

## 9. テスト観点

- 同一シナリオを2回実行して `Sent` / `Executed` が完全一致すること（決定論）
- `AdvanceTo` の境界（`AtMs` ちょうどのステップが実行されること）
- ステップ実行時点の `CurrentMs` が `AtMs` と一致すること
- 未知type・壊れた payload がそのまま流れること（`ScenarioPlayer` が握りつぶさないこと）
- `atMs` 非昇順・未知 `kind` で `ScenarioFormatException` が出ること
- §7 の各シナリオが、対応する分岐を実際に踏むこと

## 10. 未確定事項

- `ScenarioPlayer` を `Takoda99.Client` 本体に含める（`Testing` 名前空間）か、`Takoda99.Client.Testing` として別プロジェクトに分けるか。**Unity側からも同じ再生機を使いたい**場合は別プロジェクトが有利
- シナリオJSONのスキーマ検証を実行時に行うか、テスト時のみとするか
