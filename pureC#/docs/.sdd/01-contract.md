# 01-Contract（Proto参照と Envelope コーデック）

> 参照する上流：[Takoda99-Client-Docs 第3章 §1・§4](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)（`Contract` は本リポジトリで定義しない）／[第5章 §2](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/05_メッセージディスパッチ層.md)（封筒の形）／[Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `csharp/Takoda99.Proto/Messages.cs`。矛盾したら上流優先。

## 1. 責務

**する：**
- Takoda99-Proto の C# DTO（`Takoda99.Proto` 名前空間）を**参照して使う**ための土台を整える。
- `Envelope { type, payload }` と各メッセージ DTO の相互変換（シリアライズ／デシリアライズ）を1箇所に閉じ込める。
- ワイヤ上の JSON（camelCase）と C# 型の対応を、**シリアライザの実装を差し替えても壊れない形**で提供する。

**しない：**
- **DTO・メッセージ型・`GameParametersPublicSubset` をここで定義しない。** 正典は Takoda99-Proto（[docs/rules/01](../../../docs/rules/01-責務と絶対原則.md) 絶対原則7）。
- メッセージの意味の解釈・振り分け（→ [05-dispatcher](./05-dispatcher.md)）。
- 通信そのもの（→ Unity 側 `WebGLNetworkClient`）。

## 2. 公開インターフェース

```csharp
namespace Takoda99.Client.Contract;

/// <summary>
/// Envelope とメッセージ DTO の相互変換。シリアライザ実装（System.Text.Json /
/// Newtonsoft.Json）をこのインターフェースの裏に隠す（§6 未確定事項）。
/// </summary>
public interface IEnvelopeCodec
{
    /// <summary>受信した生 JSON テキストを Envelope に復元する。</summary>
    /// <returns>復元できなければ null（例外を投げない。第5章 §3「1メッセージの失敗で接続を切らない」）。</returns>
    Envelope? DecodeEnvelope(string json);

    /// <summary>Envelope.Payload を指定の DTO 型へ復元する。</summary>
    /// <returns>必須フィールド欠落・型不一致なら null（例外を投げない）。</returns>
    T? DecodePayload<T>(Envelope envelope) where T : class;

    /// <summary>送信メッセージを Envelope に包んで JSON テキストにする。</summary>
    /// <remarks>payload が空のメッセージも "payload": {} を必ず出力する（第5章 §2）。</remarks>
    string EncodeEnvelope(string type, object payload);
}
```

- `Envelope` 型そのものは `Takoda99.Proto.Envelope` を使う（再定義しない）。
- 戻り値を `null` にして例外を投げないのは、ディスパッチ層が「破棄＋ログで継続」できるようにするため。

## 3. ふるまいの詳細

### 3.1 デシリアライズ（受信）
| 入力 | 結果 |
|---|---|
| 正常な `{"type":"MatchStart","payload":{...}}` | `Envelope` を返す |
| JSON として壊れている | `null`（呼び出し側が破棄＋ログ） |
| `type` が空文字／欠落 | `null` |
| `payload` が欠落 | `Envelope` を返す（`Payload` は空扱い）。`DecodePayload<T>` 側で `null` になる |
| 既知 `type` だが payload の必須フィールドが欠落 | `DecodePayload<T>` が `null` |
| 未知の `type` | `DecodeEnvelope` は成功する（未知判定は [05-dispatcher](./05-dispatcher.md) の責務） |

### 3.2 シリアライズ（送信）
- `EncodeEnvelope("MatchmakingJoin", new MatchmakingJoin())` → `{"type":"MatchmakingJoin","payload":{}}`
- **フィールド名は camelCase 固定**。C# のプロパティ名（PascalCase）をそのまま出さない。Proto の `[JsonPropertyName]` に従う。
- `null` のオプショナルフィールド（`MatchmakingStatus.CountdownMs` 等）は**出力しない**（Proto 側の `JsonIgnoreCondition.WhenWritingNull` に従う）。

### 3.3 enum の扱い
- `CustomerAttribute` / `Phase` / `EliminationReason` / `LeaveReason` / `CreditReason` は**文字列**でやり取りする（Proto 側が `JsonStringEnumConverter` を指定済み）。数値化しない。
- **未知の enum 値**（サーバーが新しい値を追加した場合）でデシリアライズ全体を失敗させない。既定値へフォールバックし、呼び出し側がログできるようにする。

## 4. 依存関係

- 依存するモジュール：**Takoda99-Proto のみ**（`Takoda99.Proto` 名前空間）
- 依存されるモジュール：[05-dispatcher](./05-dispatcher.md) / [04-store-reducer](./04-store-reducer.md) / Unity 側 `WebGLNetworkClient`
- 依存方向：`Contract` は何にも依存しない（[第3章 §2 ルール1](https://github.com/Okashimachi/Takoda99-Client-Docs/blob/main/03_モジュール分割とレイヤー責務.md)）

## 5. テスト観点

| # | ケース | 期待 |
|---|---|---|
| 1 | 全 S2C メッセージのサンプル JSON をデコード | 各 DTO に正しく復元される |
| 2 | 全 C2S メッセージをエンコード | camelCase の JSON になる |
| 3 | 壊れた JSON | `null`（例外を投げない） |
| 4 | 必須フィールド欠落 | `DecodePayload` が `null`（例外を投げない） |
| 5 | 空 payload メッセージのエンコード | `"payload":{}` が出力される（キー省略でない） |
| 6 | `CountdownMs = null` のエンコード | `countdownMs` キーが出力されない |
| 7 | enum の文字列往復（`"Claimer"` ↔ `CustomerAttribute.Claimer`） | 一致する |
| 8 | 未知 enum 値（`"Unknown"`） | 全体が失敗せず既定値になる |
| 9 | **ラウンドトリップ**：エンコード → デコードで同値 | 全メッセージで一致 |

> テストで使うサンプル JSON は、可能なら [Takoda99-Server](https://github.com/Okashimachi/Takoda99-Server) の実配信と突き合わせる（`proto/wire_test.go` が Go 側の対）。

## 6. 未確定事項

- **JSON シリアライザの選定（要判断）。** Proto の `Messages.cs` は `System.Text.Json` 前提（`Envelope.Payload` が `JsonElement`）だが、Unity では `com.unity.nuget.newtonsoft-json` が事実上の標準で、WebGL/IL2CPP でのリフレクション制約もある。`IEnvelopeCodec` の裏に隠す設計にしてあるので後から差し替えられるが、**`Envelope.Payload` の型が `JsonElement` に固定されている点は Proto 側の変更が要る可能性がある**（変更する場合は Proto の人間承認フロー）。
- Proto の C# 配布方法（NuGet / GitHub Packages / ソース手ミラー）が未確定。決まり次第 [pureC#/README.md](../../README.md) §3 に追記する。
