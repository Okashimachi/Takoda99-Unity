using System;
using System.Collections.Generic;
using System.Linq;
using Takoda99.Proto;

namespace Takoda99.Client.State;

/// <summary>純粋関数。同じ (state, action) からは常に同じ結果を返す（04-store-reducer.md）。</summary>
public static class Reducer
{
    public static ClientState Apply(ClientState state, IAction action)
    {
        return action switch
        {
            MatchStartAction a => ApplyMatchStart(state, a),
            CustomerArrivedAction a => ApplyCustomerArrived(state, a),
            EvaluationUpdateAction a => state.With(score: a.Score, rank: a.Rank, aliveCount: a.AliveCount),
            DifficultyUpdateAction a => state.With(heatLevel: a.HeatLevel),
            PhaseChangeAction a => state.With(matchPhase: a.Phase),
            RankingSnapshotAction a => ApplyRankingSnapshot(state, a),
            RankingDeltaAction a => ApplyRankingDelta(state, a),
            ForcedEliminationWarningAction a => state.With(cull: new CullWarning
            {
                UntilMs = a.UntilMs,
                ReceivedAtLocalMs = a.ReceivedAtLocalMs,
                StageIndex = a.StageIndex,
                StageTotal = a.StageTotal,
                CutLineRank = a.CutLineRank,
                CutStoreIds = a.CutStoreIds,
                SelfAtRisk = a.SelfAtRisk,
            }),
            StoreEliminatedBatchAction a => ApplyStoreEliminatedBatch(state, a),
            PersonalResultAction a => state.With(personalResult: new PersonalResultState
            {
                FinalRank = a.FinalRank,
                Score = a.Score,
                TakoyakiCount = a.TakoyakiCount,
                SurvivedMs = a.SurvivedMs,
                Stats = a.Stats,
            }),
            MatchEndAction => state.With(matchEnded: true, phase: ClientPhase.Result),
            MatchmakingStatusAction a => state.With(waitingCount: a.WaitingCount, minPlayers: a.MinPlayers, countdownMs: a.CountdownMs, clearCountdownMs: a.CountdownMs is null, selfStoreId: a.SelfStoreId, matchmakingParticipants: a.Participants),

            LocalMatchResetAction => ApplyLocalMatchReset(state),
            LocalOrderBeganAction a => state.With(currentOrder: new CurrentOrder { CustomerId = a.CustomerId, OrderCount = a.OrderCount }),
            LocalKeyJudgedAction a => ApplyLocalKeyJudged(state, a),
            LocalOrderClearedAction => ApplyLocalOrderCleared(state),
            LocalConnectionChangedAction a => state.With(connection: a.State, lastError: a.Error, clearLastError: a.Error is null),
            LocalLifecycleChangedAction a => state.With(phase: a.Phase),

            _ => state,
        };
    }

    private static ClientState ApplyMatchStart(ClientState state, MatchStartAction a)
    {
        // storeId → 表示名。表示名が配られるのは MatchStart だけなのでここでキャッシュする。
        // 重複した storeId は先勝ち（match-state/01 §3.1 手順3）。
        var displayNames = new Dictionary<string, string>(a.Stores.Count, StringComparer.Ordinal);
        foreach (var s in a.Stores)
        {
            if (!displayNames.ContainsKey(s.StoreId))
            {
                displayNames[s.StoreId] = s.DisplayName;
            }
        }

        var rows = a.Stores
            .Select(s => new RankingRow
            {
                StoreId = s.StoreId,
                DisplayName = s.DisplayName,
                Rank = s.Rank,
                Score = s.Score,
                Alive = s.Alive,
            });

        var self = a.Stores.FirstOrDefault(s => string.Equals(s.StoreId, a.SelfStoreId, StringComparison.Ordinal));

        return state.With(
            matchId: a.MatchId,
            selfStoreId: a.SelfStoreId,
            gameParams: a.Params,
            matchPhase: a.MatchPhase,
            startedAtMs: a.StartedAtLocalMs,
            displayNames: displayNames,
            ranking: new RankingTable { Rows = SortRows(rows) },
            score: 0,
            // 自店が Stores に居なければ 0（順位未確定）。例外を投げない。
            rank: self?.Rank ?? 0,
            aliveCount: a.Stores.Count,
            alive: true,
            phase: ClientPhase.InMatch,
            queue: Array.Empty<CustomerEntry>(),
            // 前の試合の成績が残らないための保険（破棄の責務は LocalMatchReset 側。result/01 §4）。
            clearPersonalResult: true,
            matchEnded: false);
    }

    private static ClientState ApplyCustomerArrived(ClientState state, CustomerArrivedAction a)
    {
        // 対応中／待機中を問わず、同一 customerId が既に行列にいれば後着を無視する（§3.4）。
        if (state.Queue.Any(e => e.View.CustomerId == a.Customer.CustomerId))
        {
            return state;
        }

        var queue = state.Queue
            .Append(new CustomerEntry { View = a.Customer, ArrivedAtLocalMs = a.ArrivedAtLocalMs })
            .ToList();

        return state.With(queue: queue);
    }

    private static ClientState ApplyRankingSnapshot(ClientState state, RankingSnapshotAction a)
    {
        // 空の全量は「情報なし」と解釈し、表を消さない（99店ぶんが必ず来る契約であり、空はサーバー不整合）。
        if (a.Entries.Count == 0)
        {
            return state;
        }

        // 全量はサーバーが正しい順位を付けているので、Rank をローカル再計算しない。
        var rows = a.Entries.Select(e => new RankingRow
        {
            StoreId = e.StoreId,
            DisplayName = ResolveDisplayName(state, e.StoreId),
            Rank = e.Rank,
            Score = e.Score,
            Alive = e.Alive,
        });

        // 既存の Rows は破棄する（マージしない。全量の役割は整合性の回復）。
        return state.With(ranking: new RankingTable { Rows = SortRows(rows) });
    }

    private static ClientState ApplyRankingDelta(ClientState state, RankingDeltaAction a)
    {
        if (a.Entries.Count == 0)
        {
            // 無駄な再ソートをしない。
            return state;
        }

        var byStoreId = new Dictionary<string, RankingRow>(StringComparer.Ordinal);
        var order = new List<string>(state.Ranking.Rows.Count + a.Entries.Count);
        foreach (var row in state.Ranking.Rows)
        {
            if (byStoreId.TryAdd(row.StoreId, row))
            {
                order.Add(row.StoreId);
            }
        }

        foreach (var change in a.Entries)
        {
            if (byStoreId.TryGetValue(change.StoreId, out var existing))
            {
                byStoreId[change.StoreId] = new RankingRow
                {
                    StoreId = existing.StoreId,
                    DisplayName = existing.DisplayName,
                    Rank = existing.Rank,
                    Score = change.Score,
                    Alive = change.Alive,
                };
            }
            else
            {
                // 表に無い storeId は新しい行として追加する（Rank = 0）。
                byStoreId[change.StoreId] = new RankingRow
                {
                    StoreId = change.StoreId,
                    DisplayName = ResolveDisplayName(state, change.StoreId),
                    Rank = 0,
                    Score = change.Score,
                    Alive = change.Alive,
                };
                order.Add(change.StoreId);
            }
        }

        return state.With(ranking: new RankingTable { Rows = Rerank(order.Select(id => byStoreId[id])) });
    }

    /// <summary>
    /// 差分は rank を運ばないため、表示用の順位をクライアントが決める（match-state/02 §3.4）。
    /// 同じ入力から常に同じ並びになること（行入れ替えアニメーションがちらつくため）。
    /// </summary>
    private static IReadOnlyList<RankingRow> Rerank(IEnumerable<RankingRow> rows)
    {
        var all = rows.ToList();

        // 生存店を Score 降順 → StoreId 序数昇順で並べ、先頭から 1,2,3,… を振る。
        // Score は整数で同点が頻出するため、安定したタイブレーク基準を持たないと UI が意味なく踊る。
        var alive = all
            .Where(r => r.Alive)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.StoreId, StringComparer.Ordinal)
            .ToList();

        var assigned = new Dictionary<string, int>(alive.Count, StringComparer.Ordinal);
        for (var i = 0; i < alive.Count; i++)
        {
            assigned[alive[i].StoreId] = i + 1;
        }

        var provisionalDeadRank = alive.Count + 1;

        var result = all.Select(r =>
        {
            if (r.Alive)
            {
                return WithRank(r, assigned[r.StoreId]);
            }

            // 脱落済みの確定順位は以後不変。Rank == 0 のまま脱落している行（差分だけで脱落を知った行）
            // だけは暫定値を入れる。次の RankingSnapshot で正しい値に直る。
            return r.Rank > 0 ? r : WithRank(r, provisionalDeadRank);
        });

        return SortRows(result);
    }

    private static ClientState ApplyStoreEliminatedBatch(ClientState state, StoreEliminatedBatchAction a)
    {
        if (a.Entries.Count == 0)
        {
            return state;
        }

        var finalRanks = new Dictionary<string, int>(a.Entries.Count, StringComparer.Ordinal);
        foreach (var e in a.Entries)
        {
            // 同じ storeId が2度含まれても最後の値で上書きする（冪等）。
            finalRanks[e.StoreId] = e.FinalRank;
        }

        var rows = state.Ranking.Rows
            .Select(r => finalRanks.TryGetValue(r.StoreId, out var finalRank)
                ? new RankingRow
                {
                    StoreId = r.StoreId,
                    DisplayName = r.DisplayName,
                    Rank = finalRank,
                    Score = r.Score,
                    Alive = false,
                }
                : r)
            .ToList();

        // 表に無い storeId は行を追加する。
        var known = new HashSet<string>(rows.Select(r => r.StoreId), StringComparer.Ordinal);
        foreach (var e in a.Entries)
        {
            if (known.Add(e.StoreId))
            {
                rows.Add(new RankingRow
                {
                    StoreId = e.StoreId,
                    DisplayName = ResolveDisplayName(state, e.StoreId),
                    Rank = e.FinalRank,
                    Score = 0,
                    Alive = false,
                });
            }
        }

        // **生存店の再ランクは行わない。** 直後に届く EvaluationUpdate と RankingSnapshot が
        // 正しい値を運ぶ。ここで独自計算すると一瞬だけ嘘の順位が出る（match-state/03 §4.1 手順4）。
        var ranking = new RankingTable { Rows = SortRows(rows) };

        if (!finalRanks.ContainsKey(state.SelfStoreId))
        {
            return state.With(ranking: ranking);
        }

        // 自店が含まれる場合。Phase は Result にしない（試合は続き、観戦しながら MatchEnd を待つ）。
        return state.With(
            ranking: ranking,
            alive: false,
            phase: ClientPhase.Spectating,
            clearCurrentOrder: true,
            queue: Array.Empty<CustomerEntry>());
    }

    private static ClientState ApplyLocalMatchReset(ClientState state)
    {
        return state.With(
            clearPersonalResult: true,
            matchEnded: false,
            ranking: new RankingTable(),
            displayNames: new Dictionary<string, string>(),
            clearCull: true,
            score: 0,
            rank: 0,
            aliveCount: 0,
            alive: false,
            matchId: "",
            selfStoreId: "",
            queue: Array.Empty<CustomerEntry>(),
            clearCurrentOrder: true,
            gameParams: new GameParametersPublicSubset());
    }

    private static ClientState ApplyLocalKeyJudged(ClientState state, LocalKeyJudgedAction a)
    {
        if (state.CurrentOrder is null)
        {
            return state;
        }

        var updated = new CurrentOrder
        {
            CustomerId = state.CurrentOrder.CustomerId,
            OrderCount = state.CurrentOrder.OrderCount,
            StartedAtMs = state.CurrentOrder.StartedAtMs,
            WordIndex = a.View.WordIndex,
            TypedLength = a.View.TypedKanaLength,
            MissCount = a.View.MissCount,
        };

        return state.With(currentOrder: updated);
    }

    private static ClientState ApplyLocalOrderCleared(ClientState state)
    {
        if (state.Queue.Count == 0)
        {
            return state.With(clearCurrentOrder: true);
        }

        return state.With(queue: state.Queue.Skip(1).ToList(), clearCurrentOrder: true);
    }

    /// <summary>Rank 昇順 → StoreId 序数昇順。表示順を決定的にするための唯一の並べ方。</summary>
    private static IReadOnlyList<RankingRow> SortRows(IEnumerable<RankingRow> rows)
        => rows.OrderBy(r => r.Rank).ThenBy(r => r.StoreId, StringComparer.Ordinal).ToList();

    private static RankingRow WithRank(RankingRow source, int rank)
        => new()
        {
            StoreId = source.StoreId,
            DisplayName = source.DisplayName,
            Rank = rank,
            Score = source.Score,
            Alive = source.Alive,
        };

    /// <summary>未知の storeId は捨てず、DisplayName を空文字にする（描画側でフォールバックできる形）。</summary>
    private static string ResolveDisplayName(ClientState state, string storeId)
        => state.DisplayNames.TryGetValue(storeId, out var name) ? name : "";
}
