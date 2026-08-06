// 仕様書: pureC#/docs/.sdd/value-objects/01-match-state.md
// 試合全体の進行状況を保持する値オブジェクト。フェーズ移行・火力の判定は一切持たない（サーバー権威）。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>
    /// 試合全体の進行状況（フェーズ・生存数・火力・表示用閾値）。
    /// 配信された結果を保持するだけで、フェーズ移行や火力上昇の判定は行わない。
    /// </summary>
    /// <remarks>
    /// <see cref="Phase"/> は仕様書の定義（Proto の <see cref="Takoda99.Proto.Phase"/> と同一）に従い、
    /// 契約の二重定義を避けるため Proto の列挙をそのまま使う。
    /// </remarks>
    public readonly record struct MatchState(
        string MatchId,
        Phase Phase,
        int AliveCount,
        int MaxStores,
        double StormThresholdPct,
        int FinalStageAliveThreshold,
        int FinalRushAliveThreshold,
        int HeatLevel,
        long StartedAtLocalMs, // MatchStart を受信したクライアントローカル時刻
        long ElapsedMs         // クライアントのローカル計測値。サーバー由来ではない
    )
    {
        /// <summary>
        /// <c>MatchStart</c> から初期状態を生成する。
        /// <paramref name="receivedAtLocalMs"/> は受信時点のクライアントローカル時刻。
        /// </summary>
        /// <remarks>
        /// <c>HeatLevel</c> は契約上 <c>MatchStart</c> にも <c>GameParametersPublicSubset</c> にも含まれないため、
        /// 最初の <c>DifficultyUpdate</c> を受信するまで 0（＝不明）とする。
        /// </remarks>
        public static MatchState FromMatchStart(MatchStart message, long receivedAtLocalMs)
        {
            return new MatchState(
                MatchId: message.MatchId,
                Phase: message.Phase,
                AliveCount: CountAlive(message.Stores),
                MaxStores: message.Params.MaxStores,
                StormThresholdPct: message.Params.StormThresholdPct,
                FinalStageAliveThreshold: message.Params.FinalStageAliveThreshold,
                FinalRushAliveThreshold: message.Params.FinalRushAliveThreshold,
                HeatLevel: 0,
                StartedAtLocalMs: receivedAtLocalMs,
                ElapsedMs: 0);
        }

        public MatchState Apply(PhaseChange message) => this with { Phase = message.Phase };

        public MatchState Apply(DifficultyUpdate message) => this with { HeatLevel = message.HeatLevel };

        /// <summary>
        /// 自店専用メッセージだが <c>aliveCount</c> は試合全体の値のため、生存数のみ反映する。
        /// </summary>
        public MatchState Apply(EvaluationUpdate message) => this with { AliveCount = message.AliveCount };

        public MatchState Apply(StoreListUpdate message) => this with { AliveCount = message.AliveCount };

        /// <summary>
        /// ローカル tick。<c>ElapsedMs</c> は <c>MatchStart</c> 受信時刻を起点としたクライアントの推定値であり、
        /// サーバー確定値で補正する経路は契約に存在しない（SV-07）。
        /// Proto v0.3.0 で制限時間が廃止されたため、残り時間ではなく経過時間としてのみ使う。
        /// </summary>
        public MatchState Tick(long nowLocalMs) => this with { ElapsedMs = nowLocalMs - StartedAtLocalMs };

        private static int CountAlive(List<StoreSummary> stores)
        {
            if (stores == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var store in stores)
            {
                if (store.Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
