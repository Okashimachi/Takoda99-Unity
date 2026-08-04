# 03-CustomerState

> 参照する上流：[用語集 3章「客・客プール」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / [用語集 7章「我慢ゲージ・離脱」](https://github.com/Okashimachi/Takoda99-Docs/blob/main/01_企画/00_用語集.md) / `CustomerArrived` / `CustomerLeft`（S2C）。矛盾したら上流優先。

## 1. 責務

- 自店舗の行列に存在する客（`StoreState.StoreQueue` が参照する客）1体ぶんの、サーバー権威データを保持する
- **しない**こと：いらだち3段階＋退転の**ムード表示状態**を持たない（Unity側 `value-objects/02-customer-mood-state.md` の責務）。`patienceLeftMs` のカウントダウン自体（表示用の減算処理）も持たない（Unity側 `PatienceTimer` の責務。[Unity/docs/.sdd/03-patience-timer.md](../../../../Unity/docs/.sdd/03-patience-timer.md) 参照）

## 2. データ定義

```csharp
public enum CustomerAttribute { Normal, Bonus, Claimer, Buzz }

public readonly record struct CustomerState(
    string CustomerId,
    CustomerAttribute Attribute,
    long PatienceMaxMs,
    long PatienceLeftMs, // サーバー確定値。表示用カウントダウンの基準
    int OrderCount,
    IReadOnlyList<string> Words // お題単語。サーバー発行
);
```

## 3. 加工プロセス

| 入力イベント | 更新内容 |
|---|---|
| `CustomerArrived` | 新規 `CustomerState` を生成し、対象店舗の `StoreQueue` 末尾（または割当位置）に追加。`PatienceLeftMs = PatienceMaxMs` で初期化 |
| サーバーからの我慢ゲージ同期（周期的に届く場合） | `PatienceLeftMs` をサーバー確定値で置換（クライアント側でのローカル減算は表示専用で権威にしない。[Unity/docs/.sdd/03-patience-timer.md](../../../../Unity/docs/.sdd/03-patience-timer.md) が担当） |
| `CustomerLeft` | 対象 `CustomerId` を `StoreQueue` から除去し、`CustomerState` を破棄する（`Store` は保持し続けない。用語集にある「たべたべエリアへ戻る」処理はサーバー側の内部処理でクライアントは関知しない） |
| `OrderServed`（提供完了、自店発行のC2Sに対する結果） | 対象客の `OrderCount` 分をタイプし終えたら、その客を `StoreQueue` から除去（次の客が先頭に繰り上がる） |

## 4. 不変条件

- `0 <= PatienceLeftMs <= PatienceMaxMs`
- `Words.Count == OrderCount`（お題単語数と注文個数は一致する前提。ズレを検知したら未確定事項へ）

## 5. 依存関係

- 依存するモジュール：`Contract`（DTO型）
- 依存されるモジュール：`Store`/`Reducer`、`04-order-progress-state.md`（先頭客の `CustomerState.Words` が `OrderProgressState` の入力になる）、Unity側 `value-objects/02-customer-mood-state.md`

## 6. テスト観点

- `CustomerArrived` で行列末尾に正しく追加されるか
- `CustomerLeft` / `OrderServed` 完了時に対象客が確実に除去されるか（除去漏れで行列に幽霊が残らないか）
- 先頭客の入れ替わり時、次客の `CustomerState` が正しく繰り上がって参照できるか

## 7. 未確定事項

- 我慢ゲージのサーバー同期頻度（毎tick送るのか、変化時のみか）によって、クライアント側カウントダウン表示とのズレ補正方法が変わる。`Unity/docs/.sdd/03-patience-timer.md` 側と合わせて確定する
