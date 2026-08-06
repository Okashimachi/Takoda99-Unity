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

    // 脱落済みの店のみ入る（生存店では省略）。小画面(98店)に脱落順位を出すため。
    // **欠落を 0 として扱わないこと**（順位0は存在しない）。だから int? ＋ 省略。
    [JsonPropertyName("finalRank")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FinalRank { get; set; }
}

// CustomerView は来店した客の情報。CustomerArrived のペイロードそのもの。
public sealed class CustomerView
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("attribute")] public CustomerAttribute Attribute { get; set; }
    [JsonPropertyName("orderCount")] public int OrderCount { get; set; } // = 打つ単語数
    [JsonPropertyName("words")] public List<string> Words { get; set; } = new(); // お題単語（サーバー発行）
    [JsonPropertyName("patienceMaxMs")] public int PatienceMaxMs { get; set; }

    // 我慢ゲージの起点（サーバー基準の単調時刻・ms）。
    // ゲージが「注文N個を打ち切るまでの制限時間」という主要UIに昇格したため、
    // クライアントの受信時刻起点だと受信遅延ぶんのズレがそのまま体験に出る。
    [JsonPropertyName("patienceStartedAtServerMs")] public long PatienceStartedAtServerMs { get; set; }
}

// MatchStats はリザルトの統計。
// AttributeTally は客属性ごとの捌き／取りこぼしの内訳（リザルト演出用）。
public sealed class AttributeTally
{
    [JsonPropertyName("served")] public int Served { get; set; }
    [JsonPropertyName("left")] public int Left { get; set; }
}

// MatchStats はリザルトの統計。**自店ぶんのみ**（他店の最終状態は StoreListUpdate の
// 最後のスナップショットに finalRank 込みで入っているので、そちらを保持して使う）。
//
// ⚠ 最大コンボは**サーバーからは返せない**。サーバーは打鍵列を受け取らず（OrderServed は
// 客1人ぶんの elapsedMs / missCount のみ）、連続無ミス数を知る手段が無い。加えて「コンボ」は
// 企画転換で概念ごと廃止されている。リザルトに出すならクライアント側で自前に数えること。
public sealed class MatchStats
{
    [JsonPropertyName("servedCount")] public int ServedCount { get; set; }
    [JsonPropertyName("avgAccuracy")] public double AvgAccuracy { get; set; } // 0..1
    [JsonPropertyName("avgElapsedMs")] public int AvgElapsedMs { get; set; }

    // 我慢切れで帰られた客の数（＝取りこぼし）。
    [JsonPropertyName("leftCount")] public int LeftCount { get; set; }

    // 打鍵の生の合計。AvgAccuracy は客ごとの精度の平均なので、
    // 「全体で何打鍵中いくつミスしたか」はこちらでないと出せない。
    [JsonPropertyName("totalKeystrokes")] public int TotalKeystrokes { get; set; }
    [JsonPropertyName("totalMisses")] public int TotalMisses { get; set; }

    // 1客を捌くのに要した最短・最長（ms）。提供0なら 0。
    [JsonPropertyName("fastestMs")] public int FastestMs { get; set; }
    [JsonPropertyName("slowestMs")] public int SlowestMs { get; set; }

    // 属性別の内訳。
    [JsonPropertyName("normal")] public AttributeTally Normal { get; set; } = new();
    [JsonPropertyName("bonus")] public AttributeTally Bonus { get; set; } = new();
    [JsonPropertyName("claimer")] public AttributeTally Claimer { get; set; } = new();
    [JsonPropertyName("buzz")] public AttributeTally Buzz { get; set; } = new();
}

// GameParameters の唯一の on-wire 契約（公開サブセット）。フルスキーマはサーバー内部（AGENTS §4）。
// v0.3.0 で matchTimeLimitMs を削除（破壊的変更）。試合の終了条件は「生存店=1」のみになり、
// 制限時間という概念自体が無くなったため（Takoda99-Docs 01_全体仕様 §8.3）。
public sealed class GameParametersPublicSubset
{
    [JsonPropertyName("initialLife")] public int InitialLife { get; set; }
    [JsonPropertyName("maxStores")] public int MaxStores { get; set; }

    // 順位バーに「淘汰圏」の帯を常時描くため。
    [JsonPropertyName("stormThresholdPct")] public double StormThresholdPct { get; set; }
    // 終盤演出へ切り替える生存店数。
    [JsonPropertyName("finalStageAliveThreshold")] public int FinalStageAliveThreshold { get; set; }
    // 最終盤演出へ切り替える生存店数。
    [JsonPropertyName("finalRushAliveThreshold")] public int FinalRushAliveThreshold { get; set; }

    // Late フェーズでの我慢ゲージの減り方の補正。サーバーは Late の間 dt / PatienceLateMul で
    // 我慢を減らす（既定 0.6 → 約1.67倍速）。PatienceMaxMs は書き換わらず**減る速度だけ**が変わり、
    // 行列内の来店済みの客にも即座に効く。この値が無いと Late 突入以降ゲージがズレ続ける。
    [JsonPropertyName("patienceLateMul")] public double PatienceLateMul { get; set; }
    // 「もうすぐ帰る」警告表示へ切り替える残り時間（ms）。サーバーは判定に使わない（表示専用）。
    [JsonPropertyName("patienceAlertMs")] public int PatienceAlertMs { get; set; }
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

// MatchmakingJoin はマッチングキュー参加操作時に送る。
// DisplayName は盤面表示名（任意）。空/未指定ならサーバーがフォールバック名を割り当てる。
//
// Go 正典の `omitempty` に厳密対応する指定は C# に無いが、サーバーは空文字とキー欠落を
// 同じ「名前なし」として扱うため実害は無い。同ファイル内の他の文字列フィールドに揃えて
// 無条件シリアライズにしている。
public sealed class MatchmakingJoin
{
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
}

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

    // 表示専用の星（0..5）。starRating = 5*(maxStores-rank)/(maxStores-1)。
    // 母集団は生存店ではなく99店全体（脱落店は下位に積む）。
    // Normalized とは別物で、分配重み・下位淘汰はサーバーが Normalized を使う。
    [JsonPropertyName("starRating")] public double StarRating { get; set; }
    // 前ティックからの増減（提供直後の「★+0.2」演出用）。
    [JsonPropertyName("starDelta")] public double StarDelta { get; set; }
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

    // 自店が淘汰の対象圏内か。対象者に画面全体アラートを出すため。
    // rank と thresholdPct の比較をクライアントにさせない。
    [JsonPropertyName("selfAtRisk")] public bool SelfAtRisk { get; set; }
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

    // 自店がどう終わったか。優勝（最後まで残った）なら空文字。
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";

    // 試合の総経過時間（試合開始からの積算 ms）。自店が途中で脱落していても
    // **試合が終わるまでの時間**が入る。
    [JsonPropertyName("matchElapsedMs")] public long MatchElapsedMs { get; set; }

    // 終了時点の残り信用。自滅なら 0。
    [JsonPropertyName("creditLeft")] public int CreditLeft { get; set; }

    // 最終評価。順位計算には使われない表示用の値。
    [JsonPropertyName("evalRaw")] public double EvalRaw { get; set; }
    [JsonPropertyName("evalNormalized")] public double EvalNormalized { get; set; }
}

// MatchmakingParticipant はマッチング待機中の参加者1人ぶん。
// 表示名は最大6文字に正規化済み。名前を送らなかった参加者にもサーバーがフォールバック名を
// 割り当てて配るので、クライアント側で生成・補完しないこと。
public sealed class MatchmakingParticipant
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
}

// MatchmakingStatus は待機者へ配信する待機状況。
//
// ⚠ **宛先ごとに内容が違う**（SelfStoreId が受信者自身を指すため）。
//
// Participants の並び順は試合開始後の MatchStart.stores[] の先頭部分と一致する
// （待機プールの順がそのまま席順になり、定員に足りない分の Bot はその後ろへ付く）。
// CountdownMs はカウントダウン中のみ（Waiting 中は省略）。
public sealed class MatchmakingStatus
{
    [JsonPropertyName("waitingCount")] public int WaitingCount { get; set; }
    [JsonPropertyName("minPlayers")] public int MinPlayers { get; set; }
    [JsonPropertyName("countdownMs")] public int? CountdownMs { get; set; }

    // 受信者自身の識別子。マッチング画面で自分を強調表示するために配る。
    // 試合開始後の MatchStart.selfStoreId と同じ値。
    [JsonPropertyName("selfStoreId")] public string SelfStoreId { get; set; } = "";

    // 待機中の参加者一覧。Bot は含まない（定員補完は試合開始時に行われるため）。
    [JsonPropertyName("participants")] public List<MatchmakingParticipant> Participants { get; set; } = new();
}
