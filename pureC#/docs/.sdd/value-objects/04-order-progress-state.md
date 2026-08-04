# 04-OrderProgressState

> 参照する上流：[用語集 4章「注文・お題・提供(タイピング)」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md)。矛盾したら上流優先。**この値オブジェクトはサーバーへ送らないクライアントローカル状態**（用語集4章「注文進捗」の定義通り）。

## 1. 責務

- 現在対応中（行列先頭）の客に対する注文の**進捗**（何単語タイプ済みか・ミス数・経過時間）を保持する
- `TypingJudge`（[04-typing-judge.md](../04-typing-judge.md)）の判定結果を蓄積する入れ物であり、判定ロジック自体は持たない
- **しない**こと：たこ焼き台の「なにもない/生地/焼けた」という**見た目状態**は持たない（Unity側 `value-objects/03-takoyaki-stand-state.md` が `OrderProgressState` を入力に導出する）

## 2. データ定義

```csharp
public readonly record struct OrderProgressState(
    string StoreId,
    string CustomerId,      // 対応中の客
    int OrderCount,         // CustomerState.OrderCount のコピー（注文確定時点でスナップショット）
    int TypedWordCount,     // 何単語ぶんタイプし終えたか（0..OrderCount）
    int MissCount,          // このOrderにおける累計ミスタイプ数
    long StartedAtMs,       // 対応開始時刻（elapsedMs基準）
    long ElapsedMs          // 現在までの所要時間
);
```

## 3. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| 行列先頭の客が確定（新規到着 or 前客提供完了で繰り上がり） | 新しい `OrderProgressState` を生成。`OrderCount = CustomerState.OrderCount`、`TypedWordCount = 0`、`MissCount = 0`、`StartedAtMs = MatchState.ElapsedMs` |
| `TypingJudge` が1単語ぶんの正誤判定を確定（1単語タイプし終えた瞬間） | `TypedWordCount += 1`。誤入力があった場合は都度 `MissCount += 1`（1文字ミスごとに加算。用語集の `missCount` 定義に合わせる） |
| 毎フレーム（表示用） | `ElapsedMs = MatchState.ElapsedMs - StartedAtMs` |
| `TypedWordCount == OrderCount` に到達 | `Serve`/`OrderServed` をトリガーし、この `OrderProgressState` を破棄（サーバーへ `elapsedMs` / `missCount` を含む `OrderServed` を送信するのは `MatchClientController` の責務） |

## 4. 不変条件

- `0 <= TypedWordCount <= OrderCount`
- `MissCount >= 0`
- `ElapsedMs >= 0`

## 5. 依存関係

- 依存するモジュール：`04-typing-judge.md`（判定結果の入力元）、`03-customer-state.md`（`Words`/`OrderCount` の参照元）、`01-match-state.md`（`ElapsedMs` 基準時刻）
- 依存されるモジュール：`06-match-client-controller.md`（`OrderServed` 送信時のペイロード生成）、Unity側 `value-objects/03-takoyaki-stand-state.md`

## 6. テスト観点

- 客の繰り上がり時に前の `OrderProgressState` が正しく破棄され、新規生成されるか
- `MissCount` が1文字ミスごとに正しく加算されるか（重複加算・取りこぼしがないか）
- `TypedWordCount == OrderCount` 到達時に確実に `Serve` がトリガーされるか

## 7. 未確定事項

- ミス数の集計粒度（1文字ミスごとか、1単語あたり最大1回のみカウントか）はサーバー側のサニティ検証仕様（`SanityCheck`）と整合させる必要があり、`Takoda99-Docs` 側の確定を待つ
