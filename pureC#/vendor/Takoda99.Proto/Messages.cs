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

// 客の種類。試合中不変。
//
// ⚠ **v0.8.0（本選）ではゲームに一切影響しない。見た目の出し分け専用。**
// 予選では属性ごとに評価が増減したが、「同じように打ったのに評価が違う」という
// 運の要素になっていたため廃止。キャラ・アイコン・行列の賑わいは画面に残るので、
// 引き続きこの値で絵を出し分ける。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomerAttribute
{
    Normal,  // 標準の客
    Bonus,   // ヒョウ柄おばちゃん等
    Claimer, // クレーマー（序盤は来店しない）
    Buzz,    // JK（注文個数が多め）
}

// 試合の局面。生存数と経過時間の両軸、どちらか先に達した方で移行（サーバー権威）。
// v0.8.0 では**演出の切り替えとお題難度の目安**としてのみ意味を持つ
// （脱落は Phase ではなく CullSchedule の時刻で起きる）。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Phase
{
    Early, // 序盤（開店ブースト・クレーマー来店なし）
    Mid,   // 中盤（通常営業・クレーマー解禁）
    Late,  // 終盤（早期決戦・火力急上昇）
}

// 脱落理由。
// v0.8.0（本選）では**脱落経路が段階的足切りの1本のみ**になったため、常に Cull が届く。
// 型は後方互換のために残している。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EliminationReason
{
    SelfCollapse, // 自滅（信用0）。Obsolete: v0.8.0 では発生しない
    Cull,         // 足切りによる脱落。v0.8.0 はこれだけ
}

// 客が離脱した理由。
// Obsolete: v0.8.0（本選）では客が逃げないため使われない。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LeaveReason
{
    Timeout, // 我慢ゲージ0で離脱
}

// 信用（ライフ）が変化した理由。
// Obsolete: v0.8.0（本選）では信用制そのものが廃止されたため使われない。
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CreditReason
{
    CustomerLeft, // 客の離脱による減少
}

// ── 共通DTO ────────────────────────────────────────────────

// StoreSummary は99店概況の最小サブセット。
//
// v0.8.0（本選）以降、試合中の定期配信は RankingSnapshot / RankingDelta が担う。
// StoreSummary は **MatchStart.Stores で初期状態を配る唯一の場**として使う
// （表示名はここでしか配られない。以降は storeId 参照）。
public sealed class StoreSummary
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("alive")] public bool Alive { get; set; }

    // 順位を決める累積値（v0.8.0・本選）。
    // W_TAKOYAKI×たこ焼き数 − W_MISS×ミス数 の累計。上限はなく、**負値もあり得る**。
    [JsonPropertyName("score")] public int Score { get; set; }

    // Obsolete: v0.8.0 以降サーバーは値を入れない（相対評価の廃止）。読まないこと。
    [JsonPropertyName("evalNormalized")] public double EvalNormalized { get; set; }
    // Obsolete: v0.8.0 以降サーバーは値を入れない（信用制の廃止）。読まないこと。
    [JsonPropertyName("creditLife")] public int CreditLife { get; set; }

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
    // v0.8.0: 見た目の出し分けのみ。スコアには影響しない。
    [JsonPropertyName("attribute")] public CustomerAttribute Attribute { get; set; }
    [JsonPropertyName("orderCount")] public int OrderCount { get; set; } // = 打つ単語数 = たこ焼きの個数
    [JsonPropertyName("words")] public List<string> Words { get; set; } = new(); // お題単語（サーバー発行）

    // Obsolete: v0.8.0 以降サーバーは値を入れない（我慢ゲージ・離脱の廃止）。
    // 客は逃げないため、**一度出たお題は必ず打ち切られる**。
    [JsonPropertyName("patienceMaxMs")] public int PatienceMaxMs { get; set; }
    // Obsolete: 同上（我慢ゲージの廃止）。
    [JsonPropertyName("patienceStartedAtServerMs")] public long PatienceStartedAtServerMs { get; set; }
}

// MatchStats はリザルトの統計。
// AttributeTally は客属性ごとの捌き／取りこぼしの内訳（リザルト演出用）。
//
// 「ヒョウ柄おばちゃんを12人さばいた」のような**成績の彩り**に使う。
// v0.8.0 では属性がスコアに影響しないので、純粋に見せ物の数字。
public sealed class AttributeTally
{
    [JsonPropertyName("served")] public int Served { get; set; }
    // Obsolete: v0.8.0 では常に 0（客は逃げない）。
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

// CullStageView は段階的足切りの1ステージ（v0.8.0・本選）。
//
// AtMs に到達した時点で、生存数が TargetAliveCount になるまでスコア下位から脱落させる。
// 最終ステージは TargetAliveCount=0（＝全店脱落＝試合終了）。
public sealed class CullStageView
{
    [JsonPropertyName("atMs")] public int AtMs { get; set; }
    [JsonPropertyName("targetAliveCount")] public int TargetAliveCount { get; set; }
}

// GameParameters の唯一の on-wire 契約（公開サブセット）。フルスキーマはサーバー内部（AGENTS §4）。
// v0.3.0 で matchTimeLimitMs を削除（破壊的変更）。
// v0.8.0（本選）で CullSchedule を追加。試合は最終ステージ（120秒）で全店が脱落して終わる。
public sealed class GameParametersPublicSubset
{
    [JsonPropertyName("maxStores")] public int MaxStores { get; set; }

    // 段階的足切りのスケジュール（v0.8.0・本選）。20秒等間隔×6段階。
    // 試合全体のタイムラインUIを描くため。秒読みは ForcedEliminationWarning.UntilMs を使う。
    // ⚠ **null で届き得る**（空リストとして扱うこと）。
    [JsonPropertyName("cullSchedule")] public List<CullStageView> CullSchedule { get; set; } = new();

    // スコアの重み（v0.8.0・本選）。score = ScoreWeightTakoyaki×たこ焼き数 − ScoreWeightMiss×ミス数。
    // **算出はサーバー権威**。配るのは「+100」等の加点演出のためだけ。
    [JsonPropertyName("scoreWeightTakoyaki")] public int ScoreWeightTakoyaki { get; set; }
    [JsonPropertyName("scoreWeightMiss")] public int ScoreWeightMiss { get; set; }

    // 終盤演出へ切り替える生存店数。
    [JsonPropertyName("finalStageAliveThreshold")] public int FinalStageAliveThreshold { get; set; }
    // 最終盤演出へ切り替える生存店数。
    [JsonPropertyName("finalRushAliveThreshold")] public int FinalRushAliveThreshold { get; set; }

    // Obsolete: v0.8.0 以降サーバーは値を入れない（信用制の廃止）。
    // ⚠ **0 が届く**ので、ライフゲージの最大値として使わないこと。
    [JsonPropertyName("initialLife")] public int InitialLife { get; set; }
    // Obsolete: 足切りが時刻スケジュール化されたため（CullSchedule を使う）。
    [JsonPropertyName("stormThresholdPct")] public double StormThresholdPct { get; set; }
    // Obsolete: 我慢ゲージの廃止。
    [JsonPropertyName("patienceLateMul")] public double PatienceLateMul { get; set; }
    // Obsolete: 我慢ゲージの廃止。
    [JsonPropertyName("patienceAlertMs")] public int PatienceAlertMs { get; set; }
}

// ── メッセージ封筒 ────────────────────────────────────────

// Envelope は全メッセージを包む共通の外側。
//
// WS 上は必ず { "type": "<MessageName>", "payload": {...} }（text frame）で流れる。
// 受信側は Type で分岐し、Payload を対応するクラスへデシリアライズする。
//
//   var env = JsonSerializer.Deserialize<Envelope>(raw);   // 1. 封筒を開ける
//   switch (env.Type)                                      // 2. 種別で分岐
//   {
//       case MessageType.CustomerArrived:
//           var m = env.Payload.Deserialize<CustomerView>(); // 3. 中身を型へ
//           break;
//   }
//
// Type に入る値は MessageType 定数を使う（文字列直書きをしない。タイポが実行時まで露見しない）。
public sealed class Envelope
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("payload")] public System.Text.Json.JsonElement Payload { get; set; }
}

// ── メッセージの3分類（実装前に必ず読む） ──────────────────
//
// 「このメッセージは取りこぼしてよいのか」で実装が変わる。各クラスのコメントに分類を書いてある。
//
//   全量       … 受け取った内容で状態を丸ごと置き換える
//                （MatchStart.Stores / RankingSnapshot）
//   定期更新   … 一定周期で届く。**取りこぼしてよい**（次で追いつく）。
//                差分を累積せず最新値で上書きする
//                （EvaluationUpdate / ForcedEliminationWarning /
//                  RankingDelta / RankingSnapshot / DifficultyUpdate）
//   イベント   … 発生時に1度だけ。**取りこぼすと状態がずれる**
//                （CustomerArrived / StoreEliminatedBatch /
//                  PersonalResult / PhaseChange / MatchEnd）
//
// 特に CustomerArrived を落とすとお題が出ずゲームが止まる。
// 詳細な順序と周期: Takoda99-Docs/00_本選差分/30_通信シーケンス.md

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
    public const string StoreEliminatedBatch = "StoreEliminatedBatch";
    public const string RankingSnapshot = "RankingSnapshot";
    public const string RankingDelta = "RankingDelta";
    public const string PersonalResult = "PersonalResult";
    public const string MatchEnd = "MatchEnd";
    public const string MatchmakingStatus = "MatchmakingStatus";
}

// ── C2S メッセージ ───────────────────────────────────────
// 送らないもの：完成報告・焦げ報告・離脱報告・脱落報告（サーバー自律確定・0章）。

// OrderServed は「客1人ぶんの注文（単語N個）を打ち切った」報告。
//
//   用途 : スコア加算のトリガ。**試合中クライアントが送る唯一のメッセージ**
//   いつ : 注文を打ち切った瞬間に1回（平均して数秒に1回）
//   分類 : イベント
//
// サーバーはサニティ検証を通してから
// deltaScore = W_TAKOYAKI×orderCount − W_MISS×missCount を加算する。
// **打鍵中は1文字ごとの送信をしない。**
//
// v0.8.0 では ElapsedMs はスコア計算に使われない（速さは「時間内に何個作れたか」に
// 自然に表れる）。ただし報告値の妥当性チェックに使うので送ること。
public sealed class OrderServed
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("elapsedMs")] public int ElapsedMs { get; set; }
    [JsonPropertyName("missCount")] public int MissCount { get; set; }
    [JsonPropertyName("clientTimestamp")] public long ClientTimestamp { get; set; }
}

// MatchmakingJoin はマッチングキューへの参加表明。
//
//   用途 : 「対戦相手を探す」を押した時。接続直後にこれを送って初めて待機列に入る
//   いつ : WebSocket 接続後に1回
//   分類 : イベント
//
// DisplayName は盤面表示名（任意）。**最大6文字にサーバーが正規化する**。
// 空/未指定ならサーバーがフォールバック名（「ゲスト12」等）を割り当てるので、
// クライアント側で採番・補完しないこと。
//
// Go 正典の `omitempty` に厳密対応する指定は C# に無いが、サーバーは空文字とキー欠落を
// 同じ「名前なし」として扱うため実害は無い。同ファイル内の他の文字列フィールドに揃えて
// 無条件シリアライズにしている。
public sealed class MatchmakingJoin
{
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
}

// マッチングキューからの離脱表明（待機画面で「やめる」）。分類: イベント。
public sealed class MatchmakingLeave { }

// ── S2C メッセージ ───────────────────────────────────────

// MatchStart は試合開始時の初期状態一式。
//
//   用途 : 試合画面を構築するための土台。**初期状態の唯一の供給源**
//   いつ : マッチング成立時に1回
//   分類 : 全量
//
// ⚠ **これを受け取るまで試合画面を構築しないこと。**
//
// ⚠ **DisplayName を配るのはここだけ。** 以降のメッセージ（RankingSnapshot / RankingDelta 等）は
// 帯域削減のため storeId しか送らない。Stores を Dictionary としてキャッシュし、
// storeId → 表示名 を自前で引くこと。**再送は期待しない。**
//
// CustomerArrived は必ずこの後に届く（先に客が来ることはない）。
public sealed class MatchStart
{
    [JsonPropertyName("matchId")] public string MatchId { get; set; } = "";
    [JsonPropertyName("selfStoreId")] public string SelfStoreId { get; set; } = "";
    [JsonPropertyName("params")] public GameParametersPublicSubset Params { get; set; } = new();
    [JsonPropertyName("phase")] public Phase Phase { get; set; }
    [JsonPropertyName("stores")] public List<StoreSummary> Stores { get; set; } = new();
    [JsonPropertyName("startsAtServerMs")] public long StartsAtServerMs { get; set; }
}

// CustomerArrived は自店への客の来店＝**次のお題の配布**。
// ペイロードは CustomerView（上記）を使う。専用クラスは設けない。
//
//   用途 : お題単語をクライアントへ渡す。行列に客を1人追加する（演出）
//   いつ : 前の客の提供完了後、間を置かず
//   分類 : イベント
//
// 🔴 **これを取りこぼすとお題が出ず、そのプレイヤーのゲームが止まる。**
// 全メッセージ中もっとも落としてはいけない。お題単語はサーバーが発行する。

// CustomerLeft は我慢ゲージ切れによる客の離脱通知。
//
// Obsolete: v0.8.0（本選）以降サーバーは送信しない。**客は逃げなくなった**。
// これが消えたことで状態遷移が単純になる：予選は「打っている最中に客が消える」割り込みが
// あり入力中断処理が必要だったが、本選では **一度出たお題は必ず打ち切られる**。
// （ただし足切りで自店が脱落した場合はシーン遷移として打鍵が中断される。それは別の話。）
public sealed class CustomerLeft
{
    [JsonPropertyName("customerId")] public string CustomerId { get; set; } = "";
    [JsonPropertyName("reason")] public LeaveReason Reason { get; set; }
}

// 信用（ライフ）は離脱でのみ減少・回復なし。
// Obsolete: v0.8.0（本選）以降サーバーは送信しない（信用制の廃止）。
public sealed class CreditUpdate
{
    [JsonPropertyName("life")] public int Life { get; set; }
    [JsonPropertyName("delta")] public int Delta { get; set; }
    [JsonPropertyName("reason")] public CreditReason Reason { get; set; }
}

// 自店のスコア・順位の定期配信（v0.8.0 では 2〜4Hz 程度）。
//
// **自店の順位はこれが権威。** RankingSnapshot / RankingDelta は他店を含む表示用で
// 差分の取りこぼしがあり得るため、自分の順位は必ずこちらを使う。
public sealed class EvaluationUpdate
{
    // 順位を決める累積値（v0.8.0・本選）。負値もあり得る。
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("aliveCount")] public int AliveCount { get; set; }

    // Obsolete: v0.8.0 以降サーバーは値を入れない（Score を使う）。
    [JsonPropertyName("evalRaw")] public double EvalRaw { get; set; }
    // Obsolete: 同上。
    [JsonPropertyName("normalized")] public double Normalized { get; set; }
    // Obsolete: 同上（星は相対評価前提の表示だった）。
    [JsonPropertyName("starRating")] public double StarRating { get; set; }
    // Obsolete: 同上。
    [JsonPropertyName("starDelta")] public double StarDelta { get; set; }
}

// DifficultyUpdate は火力（お題の難度レベル）の更新。全店共通の値。
//
//   用途 : 「難しくなってきた」ことを見せる演出。お題の見た目や BGM の切り替え
//   いつ : 低頻度でよい（表示優先度は低い）
//   分類 : 定期更新（取りこぼし可）
//
// HeatLevel は**お題辞書のレベル**に対応する。上がるほど長く難しい単語が出る。
// 実際にどの単語が出るかは CustomerView.Words で届くので、
// この値を難度計算に使う必要はない（表示のためだけ）。
public sealed class DifficultyUpdate
{
    [JsonPropertyName("heatLevel")] public int HeatLevel { get; set; }
}

// PhaseChange は試合の局面（Early / Mid / Late）の移行通知。
//
//   用途 : 背景・BGM・演出の切り替え
//   いつ : 移行した瞬間に1回（1試合で2回）
//   分類 : イベント
public sealed class PhaseChange
{
    [JsonPropertyName("phase")] public Phase Phase { get; set; }
}

// 99店概況の低頻度フルスナップ。
// Obsolete: v0.8.0 以降サーバーは定期配信しない。
// ランキングは RankingSnapshot（全量）／RankingDelta（差分）が担う。
public sealed class StoreListUpdate
{
    [JsonPropertyName("stores")] public List<StoreSummary> Stores { get; set; } = new();
    [JsonPropertyName("aliveCount")] public int AliveCount { get; set; }
}

// 全店ランキングの1行（v0.8.0・本選）。
//
// Rank の意味：**生存店は現在順位、脱落店は確定順位（以後不変）**。
// これにより観戦中も99店を1本の Rank で並べられる。
public sealed class RankingEntry
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("alive")] public bool Alive { get; set; }
}

// 全店の順位の全量配信（v0.8.0・本選）。低頻度。
//
// 役割は**整合性の回復**。差分の取りこぼしで累積したズレをここでリセットする。
// 足切り直後と試合終了直前には必ず流れる。DisplayName は含まない（MatchStart で配布済み）。
// ⚠ Entries は **null で届き得る**（空リストとして扱うこと）。
public sealed class RankingSnapshot
{
    [JsonPropertyName("entries")] public List<RankingEntry> Entries { get; set; } = new();
}

// RankingDelta の1行（v0.8.0・本選）。
//
// **Rank を持たない。** Rank は相対値なので1店の変動で他店も動き、差分の利点が消えるため。
// 表示順は Score でソートして復元する。**自店の権威 Rank は EvaluationUpdate から取ること。**
public sealed class RankingChange
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("alive")] public bool Alive { get; set; }
}

// 前回配信から変化した店のみの差分配信（v0.8.0・本選）。高頻度。
//
// **取りこぼしてよい**（次の RankingSnapshot で直る）。差分を累積せず、受け取った値で置き換える。
// ⚠ Entries は **null で届き得る**（空リストとして扱うこと）。
public sealed class RankingDelta
{
    [JsonPropertyName("entries")] public List<RankingChange> Entries { get; set; } = new();
}

// 次の足切りの予告（v0.8.0 以降は**常時**配信）。
// 右パネルが常設UIのため、常に「あと何秒」「誰が切られるか」が届いている必要がある。
public sealed class ForcedEliminationWarning
{
    // 次の足切りまでの残りミリ秒。
    // **クライアントは受信時刻起点でローカル補間する**（1秒ごとの正確な配信は保証されない）。
    [JsonPropertyName("untilMs")] public int UntilMs { get; set; }
    // 「第何段階 / 全何段階」（1始まり）。
    [JsonPropertyName("stageIndex")] public int StageIndex { get; set; }
    [JsonPropertyName("stageTotal")] public int StageTotal { get; set; }
    // この順位より下が切られる境界（= 目標生存数 + 1）。
    // 最終ステージ（全店脱落）だけは例外で **2 が届く**。処理上は1位も脱落するが、
    // 表示は「1位以外が脱落対象」とするのが企画意図（決勝の緊張を最大化する）。
    [JsonPropertyName("cutLineRank")] public int CutLineRank { get; set; }
    // 現時点で切られる予定の店。表示件数ぶんに上限あり。⚠ **null で届き得る**。
    [JsonPropertyName("cutStoreIds")] public List<string> CutStoreIds { get; set; } = new();

    // 自店が淘汰の対象圏内か。対象者に画面全体アラートを出すため。
    // rank と cutLineRank の比較をクライアントにさせない。
    [JsonPropertyName("selfAtRisk")] public bool SelfAtRisk { get; set; }

    // Obsolete: v0.8.0 以降サーバーは値を入れない（時刻スケジュール化）。
    [JsonPropertyName("untilTick")] public int UntilTick { get; set; }
    // Obsolete: 同上（CutLineRank を使う）。
    [JsonPropertyName("thresholdPct")] public double ThresholdPct { get; set; }
}

// 自店なら → リザルト遷移、他店なら → 盤面更新。
public sealed class StoreEliminated
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    // v0.8.0 では常に Cull（脱落経路が足切りの1本のみになったため）。
    [JsonPropertyName("reason")] public EliminationReason Reason { get; set; }
    [JsonPropertyName("finalRank")] public int FinalRank { get; set; }
}

// 1回の足切りで脱落した店をまとめて全員へ配信する（v0.8.0・本選）。
//
// **1件ずつは届かない。** 演出は1つに集約して再生すること（音も1回）。
// 120秒の最終バッチには FinalRank=1（優勝者）が含まれる。
// ⚠ Entries は **null で届き得る**（空リストとして扱うこと）。
public sealed class StoreEliminatedBatch
{
    // 第何段階の足切りか（1始まり）。
    [JsonPropertyName("stageIndex")] public int StageIndex { get; set; }
    [JsonPropertyName("entries")] public List<StoreEliminated> Entries { get; set; } = new();
}

// 自店の脱落が確定した瞬間に、そのプレイヤー宛に送信される個人成績。
//
// **全員の試合終了を待たずに届く。** 受け取ったら保持しておき、任意のタイミングで
// 個人成績画面に表示する（サーバーへ問い合わせない）。画面遷移とデータ受信を切り離す設計。
// v0.8.0 では120秒に全店が脱落するため、**優勝者を含む全員がこの経路を通る**。
public sealed class PersonalResult
{
    [JsonPropertyName("finalRank")] public int FinalRank { get; set; }
    [JsonPropertyName("stats")] public MatchStats Stats { get; set; } = new();

    // プレイヤーの生存時間（試合開始から脱落までの積算 ms）。
    [JsonPropertyName("survivedMs")] public long SurvivedMs { get; set; }

    // 最終スコア（v0.8.0・本選）。順位を決めた値そのもの。
    [JsonPropertyName("score")] public int Score { get; set; }
    // 作ったたこ焼きの総数（= 累計 orderCount）。
    // Stats.ServedCount は「提供した**客**の数」であってたこ焼きの数ではない。
    // **総ミス数は Stats.TotalMisses**（ここには重複して持たない）。
    [JsonPropertyName("takoyakiCount")] public int TakoyakiCount { get; set; }

    // Obsolete: v0.8.0 以降サーバーは値を入れない（脱落経路が1本になった）。
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EliminationReason? Reason { get; set; }

    // Obsolete: 信用制の廃止。
    [JsonPropertyName("creditLeft")] public int CreditLeft { get; set; }

    // Obsolete: 相対評価の廃止（Score を使う）。
    [JsonPropertyName("evalRaw")] public double EvalRaw { get; set; }
    // Obsolete: 同上。
    [JsonPropertyName("evalNormalized")] public double EvalNormalized { get; set; }
}

// 試合全体の終了を全員へ知らせる締めの合図。ペイロードは持たない。
//
// **勝者の特別扱いはサーバーが持たない。** 優勝者の識別子は StoreEliminatedBatch
// （FinalRank=1）で、最終スコアは直前の RankingSnapshot で既に届いている。
// リザルト演出は PersonalResult.FinalRank に応じて分岐する。
//
// 配信順序（120秒）: StoreEliminatedBatch → PersonalResult → RankingSnapshot → MatchEnd
public sealed class MatchEnd { }

// MatchmakingParticipant はマッチング待機中の参加者1人ぶん。
// 表示名は最大6文字に正規化済み。名前を送らなかった参加者にもサーバーがフォールバック名を
// 割り当てて配るので、クライアント側で生成・補完しないこと。
public sealed class MatchmakingParticipant
{
    [JsonPropertyName("storeId")] public string StoreId { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("isBot")] public bool IsBot { get; set; }
}

// MatchmakingStatus は待機画面に出す「あと何人か・誰がいるか」。
//
//   用途 : マッチング待機画面。人数・参加者一覧・開始カウントダウン
//   いつ : 待機人数が変わるたび／カウントダウン中
//   分類 : 定期更新（取りこぼし可・最新値で上書き）
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
