// takoda99（たこ焼き経営バトロワ型タイピングゲーム）WebSocket 契約（メッセージDTO）の C# ミラー。
//
// これは proto/messages.go（Go 正典）の手ミラー。3言語のどれか1つを変えたら
// 3言語すべてを同じ変更で揃える（AGENTS §2）。json は camelCase（JsonPropertyName で明示）。
// 判定の権威は常にサーバー側。クライアントは打鍵の正誤判定＋その集計と表示のみ（プロトコル仕様 0章）。
//
// シリアライザは System.Text.Json を前提とする。Unity で Newtonsoft.Json を使う場合は
// 消費側でマッピングを合わせること（属性名は camelCase なので命名ポリシー差し替えでも整合する）。
//
// 正典ドキュメント: Takoda99-Docs/02_共通仕様/02_プロトコル仕様.md

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Takoda99.Proto;

// ── 共通ID ────────────────────────────────────────────────
// StoreId / CustomerId / MatchId は string のエイリアス相当（C# では素の string を使う）。

// ── 列挙 ──────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerAttribute
{
    Normal,  // 標準
    Bonus,   // プラス系（満足時に軽い評価加点）
    Claimer, // クレーマー（評価にマイナス寄与・中盤解禁・非対称）
    Buzz,    // JK（成功失敗とも評価に大きく影響・高難度）
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Phase
{
    Early, // 序盤（開店ブースト）
    Mid,   // 中盤（通常営業・クレーマー解禁）
    Late,  // 終盤（早期決戦・我慢短縮・火力急上昇）
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EliminationReason
{
    SelfCollapse, // 自滅（信用0）
    Cull,         // 強制（下位淘汰 storm）
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeaveReason
{
    Timeout, // 我慢ゲージ0で離脱
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CreditReason
{
    CustomerLeft, // 客の離脱による減少（信用は離脱でのみ減少・回復なし）
}

// ── 共通DTO ────────────────────────────────────────────────

// StoreSummary は99店概況（戦況ミニ盤面）の最小サブセット。
public sealed class StoreSummary
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("evalNormalized")] public double EvalNormalized { get; set; } // 0..1
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("creditLife")] public int CreditLife { get; set; }
    [JsonPropertyName("alive")] public bool Alive { get; set; }
}

// CustomerView は来店した客の情報。CustomerArrived のペイロードそのもの。
public sealed class CustomerView
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("attribute")] public CustomerAttribute Attribute { get; set; }
    [JsonPropertyName("orderCount")] public int OrderCount { get; set; } // = 打つ単語数
    [JsonPropertyName("words")] public List<string> Words { get; set; } = new(); // お題単語（サーバー発行）
    [JsonPropertyName("patienceMaxMs")] public int PatienceMaxMs { get; set; }
}

// MatchStats はリザルトの統計。
public sealed class MatchStats
{
    [JsonPropertyName("servedCount")] public int ServedCount { get; set; }
    [JsonPropertyName("avgAccuracy")] public double AvgAccuracy { get; set; } // 0..1
    [JsonPropertyName("avgElapsedMs")] public int AvgElapsedMs { get; set; }
}

// GameParameters の唯一の on-wire 契約（公開サブセット）。フルスキーマはサーバー内部（AGENTS §4）。
public sealed class GameParametersPublicSubset
{
    [JsonPropertyName("matchTimeLimitMs")] public int MatchTimeLimitMs { get; set; }
    [JsonPropertyName("initialLife")] public int InitialLife { get; set; }
    [JsonPropertyName("maxStores")] public int MaxStores { get; set; }
}

// ── メッセージ封筒 ────────────────────────────────────────
// WS 上は { "type": "<MessageName>", "payload": {...} }（text frame）で送受信する。

public sealed class Envelope
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("payload")] public System.Text.Json.JsonElement Payload { get; set; }
}

// メッセージ種別タグ（Envelope.Type に入る値）。
public static class MessageType
{
    // C2S（試合中は実質 OrderServed のみ）
    public const string OrderServed = "OrderServed";
    public const string MatchmakingJoin = "MatchmakingJoin";
    public const string MatchmakingLeave = "MatchmakingLeave";

    // S2C
    public const string MatchStart = "MatchStart";
    public const string CustomerArrived = "CustomerArrived";
    public const string CustomerLeft = "CustomerLeft";
    public const string CreditUpdate = "CreditUpdate";
    public const string EvaluationUpdate = "EvaluationUpdate";
    public const string DifficultyUpdate = "DifficultyUpdate";
    public const string PhaseChange = "PhaseChange";
    public const string StoreListUpdate = "StoreListUpdate";
    public const string ForcedEliminationWarning = "ForcedEliminationWarning";
    public const string StoreEliminated = "StoreEliminated";
    public const string MatchEnd = "MatchEnd";
    public const string MatchmakingStatus = "MatchmakingStatus";
}

// ── C2S メッセージ ───────────────────────────────────────
// 送らないもの：完成報告・焦げ報告・離脱報告・脱落報告（サーバー自律確定・0章）。

// OrderServed は注文N個の単語を打ち切った瞬間に送る。
public sealed class OrderServed
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("elapsedMs")] public int ElapsedMs { get; set; }
    [JsonPropertyName("missCount")] public int MissCount { get; set; }
    [JsonPropertyName("clientTimestamp")] public long ClientTimestamp { get; set; }
}

public sealed class MatchmakingJoin { }

public sealed class MatchmakingLeave { }

// ── S2C メッセージ ───────────────────────────────────────

public sealed class MatchStart
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("selfStoreId")] public string SelfStoreId { get; set; } = "";
    [JsonPropertyName("params")] public GameParametersPublicSubset Params { get; set; } = new();
    [JsonPropertyName("phase")] public Phase Phase { get; set; }
    [JsonPropertyName("stores")] public List<StoreSummary> Stores { get; set; } = new();
}

// CustomerArrived のペイロードは CustomerView（上記）を使う。専用クラスは設けない。

public sealed class CustomerLeft
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("reason")] public LeaveReason Reason { get; set; }
}

// 信用（ライフ）は離脱でのみ減少・回復なし。
public sealed class CreditUpdate
{
    [JsonPropertyName("life")] public int Life { get; set; }
    [JsonPropertyName("delta")] public int Delta { get; set; }
    [JsonPropertyName("reason")] public CreditReason Reason { get; set; }
}

public sealed class EvaluationUpdate
{
    [JsonPropertyName("evalRaw")] public double EvalRaw { get; set; }
    [JsonPropertyName("normalized")] public double Normalized { get; set; } // 0..1
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("aliveCount")] public int AliveCount { get; set; }
}

public sealed class DifficultyUpdate
{
    [JsonPropertyName("heatLevel")] public int HeatLevel { get; set; }
}

public sealed class PhaseChange
{
    [JsonPropertyName("phase")] public Phase Phase { get; set; }
}

// 99店概況の低頻度フルスナップ（差分版は将来の帯域対策・proto仕様 3.1）。
public sealed class StoreListUpdate
{
    [JsonPropertyName("stores")] public List<StoreSummary> Stores { get; set; } = new();
    [JsonPropertyName("aliveCount")] public int AliveCount { get; set; }
}

public sealed class ForcedEliminationWarning
{
    [JsonPropertyName("untilTick")] public int UntilTick { get; set; }
    [JsonPropertyName("thresholdPct")] public double ThresholdPct { get; set; }
}

// 自店なら → リザルト遷移、他店なら → 盤面更新。
public sealed class StoreEliminated
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("reason")] public EliminationReason Reason { get; set; }
    [JsonPropertyName("finalRank")] public int FinalRank { get; set; }
}

// 最終順位は脱落順のみ（評価は使わない）。
public sealed class MatchEnd
{
    [JsonPropertyName("finalRank")] public int FinalRank { get; set; }
    [JsonPropertyName("stats")] public MatchStats Stats { get; set; } = new();
}

// CountdownMs はカウントダウン中のみ（Waiting 中は省略）。
public sealed class MatchmakingStatus
{
    [JsonPropertyName("waitingCount")] public int WaitingCount { get; set; }
    [JsonPropertyName("minPlayers")] public int MinPlayers { get; set; }

    [JsonPropertyName("countdownMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CountdownMs { get; set; }
}
