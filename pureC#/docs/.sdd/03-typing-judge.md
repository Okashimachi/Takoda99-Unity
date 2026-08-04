# 03-TypingJudge（打鍵判定）

> 参照する上流：[Takoda99-Client-Docs 第6章 全体](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/06_打鍵判定共通仕様.md)（判定・`missCount`・`elapsedMs` の正典）／[第3章 §3](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`ITypingJudge` のIF）。矛盾したら上流優先。

> **このモジュールはクライアント唯一のローカルドメイン。** ここ以外にドメインロジックを作らない（[docs/rules/01](../../../docs/rules/01-責務と絶対原則.md) 絶対原則1・2）。

## 1. 責務

**する：**
- お題単語列に対する**1打鍵ごとの正誤判定**（複数表記を受理する前方一致オートマトン）。
- **1注文を通じた `missCount` の集計**と、`elapsedMs` / `clientTimestamp` の計測。
- 注文完了（`OrderCleared`）の検出と、`OrderServed` 送信用レポートの生成。
- 表示用の現在状態（現在単語・打鍵済み位置・`x/N`）の提供。

**しない：**
- お題単語の生成・並べ替え・スキップ（**サーバー発行**）。
- 評価・信用・順位など経営数値の算出（サーバー権威）。
- `OrderServed` の**送信**（送るのは [06-match-client-controller](./06-match-client-controller.md)）。
- `ClientState` の書き換え（`Store` を知らない。[第3章 §2 ルール4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）。

## 2. 公開インターフェース

```csharp
namespace Takoda99.Client;

/// <summary>
/// 時刻の供給源。pureC# は UnityEngine.Time を参照できないため抽象化する。
/// Unity 側は Time.realtimeSinceStartupAsDouble / DateTimeOffset で実装する。
/// </summary>
public interface IClock
{
    /// <summary>単調増加ミリ秒。elapsedMs の計測に使う（壁時計補正・ポーズを混ぜない）。</summary>
    long MonotonicMs { get; }

    /// <summary>壁時計の Unix epoch ミリ秒。clientTimestamp にのみ使う。</summary>
    long WallClockUnixMs { get; }
}
```

```csharp
namespace Takoda99.Client.Typing;

public enum KeyResult
{
    Ignored,      // Idle 中・対象外キー（missCount を増やさない）
    Correct,      // 受理（単語継続中）
    Miss,         // 不一致（missCount++・バッファは巻き戻さない）
    WordCleared,  // 現在単語を打ち切った（wordIndex++）
    OrderCleared, // 最終単語を打ち切った（注文完了）
}

/// <summary>OrderServed の材料。BuildReport() が返す。</summary>
public readonly struct OrderReport
{
    public string CustomerId { get; }
    public int ElapsedMs { get; }        // 対応開始 → OrderCleared
    public int MissCount { get; }        // 1注文通算
    public long ClientTimestamp { get; } // OrderCleared 時点の壁時計
}

/// <summary>表示用スナップショット（Renderer がハイライトに使う）。</summary>
public readonly struct TypingView
{
    public string CurrentWord { get; }   // 現在のお題単語（かな原文）
    public int TypedKanaLength { get; }  // 確定済みかなの文字数（ハイライト幅）
    public string PendingInput { get; }  // 現在かなの未確定入力バッファ
    public int WordIndex { get; }        // x（0 起点）
    public int OrderCount { get; }       // N
    public int MissCount { get; }
}

public interface ITypingJudge
{
    /// <summary>客が行列先頭になり対応を開始する。ここが elapsedMs の起点（最初の打鍵ではない）。</summary>
    void BeginOrder(string customerId, IReadOnlyList<string> words);

    /// <summary>文字キー1つを与えて判定を進める。</summary>
    KeyResult PressKey(char c);

    /// <summary>CustomerLeft 受信時の中断。計測値は破棄し、OrderServed を送らない。</summary>
    void AbortOrder();

    /// <summary>OrderCleared 直後に呼ぶ。Idle 中・未完了時は null。</summary>
    OrderReport? BuildReport();

    /// <summary>現在の表示用状態。Idle 中は既定値。</summary>
    TypingView CurrentView { get; }

    /// <summary>Idle かどうか（Spectating 中は Idle に固定される）。</summary>
    bool IsIdle { get; }
}
```

## 3. ふるまいの詳細

### 3.1 状態遷移（第6章 §5）
```
Idle
 └ BeginOrder(customerId, words[])
    → Typing(wordIndex=0, missCount=0, startedAt=Clock.MonotonicMs)
         ├ PressKey → Correct / Miss（Typing 継続）
         ├ 単語完了 → WordCleared → wordIndex++（wordIndex < N なら継続）
         ├ 最終単語完了 → OrderCleared → BuildReport() 可能 → 呼び出し側が送信 → Idle
         └ AbortOrder() → 計測破棄 → Idle
```

### 3.2 判定アルゴリズム（第6章 §3）
1. [02-romaji-table](./02-romaji-table.md) の `Segment(word)` で現在単語を打鍵単位に分割しておく。
2. 入力文字を**小文字化**し、現在単位の未確定バッファ `b` に足した `b'` を作る。
3. `b'` が現在単位のいずれかのパターンの **prefix** なら `Correct`。
   - さらに `b'` がいずれかのパターンと**完全一致**し、かつ**より長い候補が残っていない**なら、その単位を確定して次へ進む。
   - より長い候補が残る場合（`n` に対する `nn` 等）は確定せず継続する。
4. `b'` がどの prefix にもならない場合、**バッファが既に候補と完全一致していれば単位を確定し、その文字を次の単位で再処理する**（`ん` の `n`/`nn` の prefix 競合をこれで解消する）。
5. それでも成立しなければ `Miss`（`missCount++`、**バッファ `b` は変更しない＝打ち直せる**）。
6. 最終単位を確定 → `WordCleared`。`wordIndex + 1 == OrderCount` なら `OrderCleared`。

### 3.3 共通ルール（第6章 §3）
- **ミスで巻き戻さない／単語をリセットしない。** ミスは無制限（ミス数で失敗にはならない。失敗経路は客の離脱＝サーバー権威のみ）。
- **大文字小文字を区別しない。**
- **文字キー以外は `missCount` の対象外**（Shift・矢印・Enter 等）。`PressKey` に渡す前に呼び出し側（`InputSource`）が文字キーのみへ正規化するが、判定側でも制御文字は `Ignored` にする。
- **`Idle` 中の打鍵は `Ignored`**（`missCount` を増やさない）。
- **先読み入力を捨てない**：`PressKey` は1呼び出し1文字の純粋な逐次処理とし、1フレームに複数キーが来ても呼び出し側が順に流せば取りこぼさない。

### 3.4 計測（第6章 §4・これが正典）
| 項目 | 定義 |
|---|---|
| `MissCount` | **1注文（N単語）通算**の `Miss` 判定数。単語ごとにリセットしない |
| `ElapsedMs` | `BeginOrder` から `OrderCleared` までの `IClock.MonotonicMs` 差分 |
| `ClientTimestamp` | `OrderCleared` 時点の `IClock.WallClockUnixMs` |

- **フレーム落ち・非アクティブ時間も差し引かない**（サーバーのサニティ検証は下限のみを見る）。
- **精度の分母（総打鍵数）はクライアントから送らない**（現行 `OrderServed` に無い。サーバーが正準打鍵数で計算する）。

### 3.5 エッジケース
| 状況 | 挙動 |
|---|---|
| `BeginOrder` の `words` が空 | `Idle` のまま（`OrderCleared` を即発火させない）。ログ対象 |
| `Typing` 中に再度 `BeginOrder` | 前の注文を破棄して新しい注文を開始（ログ対象。本来 Reducer 側で起きない） |
| `OrderCleared` 後に `PressKey` | `Ignored` |
| `Idle` で `BuildReport()` | `null` |
| `AbortOrder()` を `Idle` で呼ぶ | 何もしない（例外を投げない） |

## 4. 依存関係

- 依存するモジュール：[02-romaji-table](./02-romaji-table.md)（`IRomajiTable`）、`IClock`
- 依存されるモジュール：[06-match-client-controller](./06-match-client-controller.md)
- **`Store` を知らない**（判定結果は戻り値で返すだけ。state 更新は Controller が Action 経由で行う）

## 5. テスト観点

第6章 §6 のテストケース表は**本仕様の必須実装**。テーブル差し替え後も全ケースが通ること。

| # | ケース | 入力 | 期待 |
|---|---|---|---|
| 1 | 単純 | `たこ` → `tako` | Correct×3, WordCleared |
| 2 | 複数表記 | `し` → `shi` / `si` | どちらも WordCleared |
| 3 | 促音 | `たっこ` → `takko` / `taxtuko` | どちらも WordCleared |
| 4 | 撥音（母音前） | `かんい` → `kanni` | WordCleared（`kani` は不可） |
| 5 | 撥音（子音前） | `かんじ` → `kanzi` | WordCleared |
| 6 | ミス後の復帰 | `たこ` → `tapko` | Miss×1 の後 WordCleared、`missCount == 1` |
| 7 | 大文字 | `たこ` → `TAKO` | WordCleared |
| 8 | 対象外キー | `たこ` → `ta` + `\n` + `ko` | `missCount == 0` |
| 9 | 注文横断 | 2単語で各1ミス | `missCount == 2`（リセットされない） |
| 10 | 拗音の分割入力 | `きゃ` → `kya` / `kilya` | どちらも WordCleared |
| 11 | `elapsedMs` の起点 | `BeginOrder` 後に一定時間待って打鍵 | 待ち時間が `ElapsedMs` に含まれる |
| 12 | 中断 | `BeginOrder` → `AbortOrder` → `BuildReport` | `null` |
| 13 | Idle 打鍵 | `BeginOrder` 前に `PressKey` | `Ignored`・`missCount` 据え置き |

> `IClock` はテスト用のフェイク（手動で時刻を進められる実装）を用意する。実時間に依存するテストを書かない。

## 6. 未確定事項

- 「打鍵数の分母」をクライアントからも送るべきか（送るなら `OrderServed` にフィールド追加＝Proto の人間承認フロー。第6章 §9）。
- `TypingView` の粒度が Renderer の要求（1文字ごとの色分け等）を満たすか。Unity 側の描画実装が始まってから見直す。
