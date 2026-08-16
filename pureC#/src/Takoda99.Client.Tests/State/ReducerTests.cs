using System.Collections.Generic;
using System.Linq;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.State;

public class ReducerTests
{
    private static ClientState Initial() => new();

    private static MatchStartAction MatchStart(
        string selfStoreId = "s1",
        IReadOnlyList<StoreSummary>? stores = null,
        long startedAtLocalMs = 0)
    {
        return new MatchStartAction
        {
            MatchId = "m1",
            SelfStoreId = selfStoreId,
            Params = new GameParametersPublicSubset { MaxStores = 99 },
            MatchPhase = Phase.Early,
            Stores = stores ?? System.Array.Empty<StoreSummary>(),
            StartedAtLocalMs = startedAtLocalMs,
        };
    }

    private static List<StoreSummary> Stores99()
    {
        var stores = new List<StoreSummary>(99);
        for (var i = 1; i <= 99; i++)
        {
            stores.Add(new StoreSummary
            {
                StoreId = $"store-{i:00}",
                DisplayName = $"店{i:00}",
                Rank = i,
                Score = 0,
                Alive = true,
            });
        }

        return stores;
    }

    // ── MatchStart（match-state/01） ─────────────────────────

    [Fact]
    public void MatchStart_99店で表示名がキャッシュされ自店のRankが入る()
    {
        var next = Reducer.Apply(Initial(), MatchStart("store-07", Stores99()));

        Assert.Equal(99, next.DisplayNames.Count);
        Assert.Equal("店07", next.DisplayNames["store-07"]);
        Assert.Equal(7, next.Rank);
        Assert.Equal(0, next.Score);
        Assert.Equal(99, next.AliveCount);
        Assert.True(next.Alive);
        Assert.Equal(ClientPhase.InMatch, next.Phase);
        Assert.Empty(next.Queue);
    }

    [Fact]
    public void MatchStart_Rankingが99行でRank昇順になる()
    {
        var next = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        Assert.Equal(99, next.Ranking.Rows.Count);
        Assert.Equal(1, next.Ranking.Rows[0].Rank);
        Assert.Equal("store-01", next.Ranking.Rows[0].StoreId);
        Assert.Equal("店01", next.Ranking.Rows[0].DisplayName);
    }

    [Fact]
    public void MatchStart_StartedAtMsはローカル単調時計の値を使う()
    {
        var next = Reducer.Apply(Initial(), MatchStart(startedAtLocalMs: 12_345));

        Assert.Equal(12_345, next.StartedAtMs);
    }

    [Fact]
    public void MatchStart_Storesが空でも例外にならずInMatchへ進む()
    {
        var next = Reducer.Apply(Initial(), MatchStart(stores: System.Array.Empty<StoreSummary>()));

        Assert.Empty(next.DisplayNames);
        Assert.Empty(next.Ranking.Rows);
        Assert.Equal(0, next.AliveCount);
        Assert.Equal(ClientPhase.InMatch, next.Phase);
    }

    [Fact]
    public void MatchStart_自店がStoresに居なければRankは0のまま()
    {
        var next = Reducer.Apply(Initial(), MatchStart("not-in-list", Stores99()));

        Assert.Equal(0, next.Rank);
    }

    [Fact]
    public void MatchStart_storeIdが重複したら先勝ちになる()
    {
        var stores = new List<StoreSummary>
        {
            new() { StoreId = "s1", DisplayName = "さき", Rank = 1, Alive = true },
            new() { StoreId = "s1", DisplayName = "あと", Rank = 2, Alive = true },
        };

        var next = Reducer.Apply(Initial(), MatchStart("s1", stores));

        Assert.Equal("さき", next.DisplayNames["s1"]);
    }

    [Fact]
    public void MatchStart_前の試合のPersonalResultが保険で破棄される()
    {
        var state = Initial().With(
            personalResult: new PersonalResultState { FinalRank = 3 },
            matchEnded: true);

        var next = Reducer.Apply(state, MatchStart());

        Assert.Null(next.PersonalResult);
        Assert.False(next.MatchEnded);
    }

    // ── EvaluationUpdate（match-state/01） ───────────────────

    [Fact]
    public void EvaluationUpdate_負のScoreがクランプされずそのまま入る()
    {
        var next = Reducer.Apply(Initial(), new EvaluationUpdateAction { Score = -30, Rank = 88, AliveCount = 75 });

        Assert.Equal(-30, next.Score);
        Assert.Equal(88, next.Rank);
        Assert.Equal(75, next.AliveCount);
    }

    [Fact]
    public void EvaluationUpdate_連続受信でScoreが累積せず最後の値になる()
    {
        var state = Initial();
        for (var i = 1; i <= 10; i++)
        {
            state = Reducer.Apply(state, new EvaluationUpdateAction { Score = i * 100, Rank = i, AliveCount = 99 - i });
        }

        Assert.Equal(1_000, state.Score);
        Assert.Equal(10, state.Rank);
    }

    [Fact]
    public void EvaluationUpdate_脱落済みでもそのまま反映する()
    {
        var state = Initial().With(alive: false, phase: ClientPhase.Spectating);

        var next = Reducer.Apply(state, new EvaluationUpdateAction { Score = 50, Rank = 40, AliveCount = 20 });

        Assert.Equal(20, next.AliveCount);
        Assert.Equal(ClientPhase.Spectating, next.Phase);
    }

    // ── RankingSnapshot / RankingDelta（match-state/02） ─────

    [Fact]
    public void RankingSnapshot_全行が置き換わり前の表に居た店が消える()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        var next = Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = new List<RankingEntry>
            {
                new() { StoreId = "store-01", Rank = 1, Score = 500, Alive = true },
                new() { StoreId = "store-02", Rank = 2, Score = 400, Alive = true },
            },
        });

        Assert.Equal(2, next.Ranking.Rows.Count);
        Assert.Null(next.Ranking.Find("store-50"));
        Assert.Equal(500, next.Ranking.Find("store-01")!.Score);
        // DisplayName は MatchStart のキャッシュから解決される。
        Assert.Equal("店01", next.Ranking.Find("store-01")!.DisplayName);
    }

    [Fact]
    public void 空のRankingSnapshotでは表が消えない()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        var next = Reducer.Apply(state, new RankingSnapshotAction());

        Assert.Same(state, next);
        Assert.Equal(99, next.Ranking.Rows.Count);
    }

    [Fact]
    public void RankingSnapshot_未知storeIdも行として残りDisplayNameは空文字()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        var next = Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = new List<RankingEntry> { new() { StoreId = "ghost", Rank = 1, Score = 0, Alive = true } },
        });

        Assert.Equal("", next.Ranking.Find("ghost")!.DisplayName);
    }

    [Fact]
    public void RankingDelta_スコアを上げた店の順位が上がり他店が押し下がる()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));
        // まず全店に順位付きのスコアを配る（1位=99点 … 99位=1点）。
        state = Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = Stores99().Select((s, i) => new RankingEntry
            {
                StoreId = s.StoreId,
                Rank = i + 1,
                Score = 99 - i,
                Alive = true,
            }).ToList(),
        });

        // 最下位の店を一気に最高得点へ。
        var next = Reducer.Apply(state, new RankingDeltaAction
        {
            Entries = new List<RankingChange> { new() { StoreId = "store-99", Score = 1_000, Alive = true } },
        });

        Assert.Equal(1, next.Ranking.Find("store-99")!.Rank);
        Assert.Equal(2, next.Ranking.Find("store-01")!.Rank);
    }

    [Fact]
    public void RankingDelta_同点の並びがStoreId順で安定する()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        var order = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            // 全店 0 点のまま差分を流し続ける（同点集団が入れ替わらないことの確認）。
            state = Reducer.Apply(state, new RankingDeltaAction
            {
                Entries = new List<RankingChange>
                {
                    new() { StoreId = "store-05", Score = 0, Alive = true },
                    new() { StoreId = "store-03", Score = 0, Alive = true },
                },
            });

            var snapshot = string.Join(",", state.Ranking.Rows.Select(r => r.StoreId));
            order.Add(snapshot);
        }

        Assert.Single(order.Distinct());
        // 同点なので StoreId 昇順にそのまま 1..99 が振られる。
        Assert.Equal("store-01", state.Ranking.Rows[0].StoreId);
        Assert.Equal(1, state.Ranking.Rows[0].Rank);
    }

    [Fact]
    public void RankingDelta_脱落済みの行のRankは触らない()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));
        state = Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = new List<RankingEntry>
            {
                new() { StoreId = "store-01", Rank = 1, Score = 100, Alive = true },
                new() { StoreId = "store-40", Rank = 40, Score = 10, Alive = false },
            },
        });

        var next = Reducer.Apply(state, new RankingDeltaAction
        {
            Entries = new List<RankingChange> { new() { StoreId = "store-40", Score = 9_999, Alive = false } },
        });

        Assert.Equal(40, next.Ranking.Find("store-40")!.Rank);
        Assert.Equal(9_999, next.Ranking.Find("store-40")!.Score);
    }

    [Fact]
    public void RankingDelta_未知storeIdは行として追加されDisplayNameは空文字()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        var next = Reducer.Apply(state, new RankingDeltaAction
        {
            Entries = new List<RankingChange> { new() { StoreId = "ghost", Score = 5, Alive = true } },
        });

        Assert.Equal("", next.Ranking.Find("ghost")!.DisplayName);
        Assert.Equal(100, next.Ranking.Rows.Count);
    }

    [Fact]
    public void RankingDelta_辞書に載っているstoreIdはDisplayNameが解決される()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));
        // 全量で表を作り直しても、辞書は MatchStart のまま残っている。
        state = Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = new List<RankingEntry> { new() { StoreId = "store-01", Rank = 1, Score = 0, Alive = true } },
        });

        var next = Reducer.Apply(state, new RankingDeltaAction
        {
            Entries = new List<RankingChange> { new() { StoreId = "store-42", Score = 7, Alive = true } },
        });

        Assert.Equal("店42", next.Ranking.Find("store-42")!.DisplayName);
    }

    [Fact]
    public void 空のRankingDeltaはstateを変えない()
    {
        var state = Reducer.Apply(Initial(), MatchStart("store-01", Stores99()));

        Assert.Same(state, Reducer.Apply(state, new RankingDeltaAction()));
    }

    // ── ForcedEliminationWarning（match-state/03） ───────────

    [Fact]
    public void CullWarning_残りミリ秒が経過ぶん減る()
    {
        var next = Reducer.Apply(Initial(), new ForcedEliminationWarningAction
        {
            UntilMs = 20_000,
            ReceivedAtLocalMs = 1_000,
            StageIndex = 1,
            StageTotal = 6,
            CutLineRank = 51,
        });

        Assert.Equal(15_000, next.Cull!.RemainingMsAt(6_000));
    }

    [Fact]
    public void CullWarning_経過超過でも0未満にならない()
    {
        var next = Reducer.Apply(Initial(), new ForcedEliminationWarningAction { UntilMs = 1_000, ReceivedAtLocalMs = 0 });

        Assert.Equal(0, next.Cull!.RemainingMsAt(999_999));
    }

    [Fact]
    public void CullWarning_UntilMsが負でも0を返す()
    {
        var next = Reducer.Apply(Initial(), new ForcedEliminationWarningAction { UntilMs = -500, ReceivedAtLocalMs = 0 });

        Assert.Equal(0, next.Cull!.RemainingMsAt(0));
    }

    [Fact]
    public void CullWarning_新しい予告でまるごと差し替わる()
    {
        var state = Reducer.Apply(Initial(), new ForcedEliminationWarningAction
        {
            StageIndex = 1,
            CutStoreIds = new List<string> { "store-90", "store-91" },
            SelfAtRisk = true,
        });

        var next = Reducer.Apply(state, new ForcedEliminationWarningAction { StageIndex = 2 });

        Assert.Equal(2, next.Cull!.StageIndex);
        Assert.Empty(next.Cull.CutStoreIds);
        Assert.False(next.Cull.SelfAtRisk);
    }

    // ── StoreEliminatedBatch（match-state/03） ───────────────

    private static ClientState WithRankedTable(string selfStoreId = "store-01")
    {
        var state = Reducer.Apply(Initial(), MatchStart(selfStoreId, Stores99()));
        return Reducer.Apply(state, new RankingSnapshotAction
        {
            Entries = Stores99().Select((s, i) => new RankingEntry
            {
                StoreId = s.StoreId,
                Rank = i + 1,
                Score = 99 - i,
                Alive = true,
            }).ToList(),
        });
    }

    private static StoreEliminatedBatchAction Batch(int stageIndex, params (string StoreId, int FinalRank)[] entries)
    {
        return new StoreEliminatedBatchAction
        {
            StageIndex = stageIndex,
            Entries = entries.Select(e => new StoreEliminated
            {
                StoreId = e.StoreId,
                Reason = EliminationReason.Cull,
                FinalRank = e.FinalRank,
            }).ToList(),
        };
    }

    [Fact]
    public void Batch_対象店がAliveFalseとFinalRankになり生存店のRankは変わらない()
    {
        var state = WithRankedTable();
        var entries = Enumerable.Range(76, 24).Select(i => ($"store-{i:00}", i)).ToArray();

        var next = Reducer.Apply(state, Batch(1, entries));

        foreach (var (storeId, finalRank) in entries)
        {
            var row = next.Ranking.Find(storeId)!;
            Assert.False(row.Alive);
            Assert.Equal(finalRank, row.Rank);
        }

        // 生存店は一切触らない（直後の EvaluationUpdate / RankingSnapshot が正しい値を運ぶ）。
        Assert.Equal(1, next.Ranking.Find("store-01")!.Rank);
        Assert.Equal(75, next.Ranking.Find("store-75")!.Rank);
    }

    [Fact]
    public void Batch_自店を含むならSpectatingへ移り行列が空になる()
    {
        var state = WithRankedTable("store-90").With(
            queue: new List<CustomerEntry> { new() { View = new CustomerView { CustomerId = "c1" } } },
            currentOrder: new CurrentOrder { CustomerId = "c1" });

        var next = Reducer.Apply(state, Batch(3, ("store-90", 90), ("store-91", 91)));

        Assert.Equal(ClientPhase.Spectating, next.Phase);
        Assert.Null(next.CurrentOrder);
        Assert.Empty(next.Queue);
        Assert.False(next.Alive);
    }

    [Fact]
    public void Batch_自店を含まなければPhaseはInMatchのまま()
    {
        var state = WithRankedTable("store-01");

        var next = Reducer.Apply(state, Batch(1, ("store-90", 90)));

        Assert.Equal(ClientPhase.InMatch, next.Phase);
        Assert.True(next.Alive);
    }

    [Fact]
    public void Batch_2回適用しても最終状態が同じ_冪等()
    {
        var state = WithRankedTable("store-90");
        var batch = Batch(3, ("store-90", 90), ("store-91", 91));

        var once = Reducer.Apply(state, batch);
        var twice = Reducer.Apply(once, batch);

        Assert.Equal(ClientPhase.Spectating, twice.Phase);
        Assert.Equal(
            once.Ranking.Rows.Select(r => (r.StoreId, r.Rank, r.Alive)),
            twice.Ranking.Rows.Select(r => (r.StoreId, r.Rank, r.Alive)));
    }

    [Fact]
    public void Batch_FinalRank1を含む最終バッチでも自店が居ればSpectatingへ()
    {
        var state = WithRankedTable("store-01");

        var next = Reducer.Apply(state, Batch(6, ("store-01", 1), ("store-02", 2)));

        // 優勝者も脱落する（企画書 3.7）。特別扱いをしない。
        Assert.Equal(ClientPhase.Spectating, next.Phase);
        Assert.Equal(1, next.Ranking.Find("store-01")!.Rank);
        Assert.False(next.Ranking.Find("store-01")!.Alive);
    }

    [Fact]
    public void Batch_空ならstateを変えない()
    {
        var state = WithRankedTable();

        Assert.Same(state, Reducer.Apply(state, new StoreEliminatedBatchAction { StageIndex = 1 }));
    }

    [Fact]
    public void Batch_49件を1回のApplyで処理し通知は1回だけ()
    {
        var store = new Store(WithRankedTable("store-01"));
        var callCount = 0;
        using var _ = store.Subscribe(_ => callCount++);

        var entries = Enumerable.Range(51, 49).Select(i => ($"store-{i:00}", i)).ToArray();
        store.Apply(Batch(1, entries));

        Assert.Equal(1, callCount);
        Assert.Equal(49, store.State.Ranking.Rows.Count(r => !r.Alive));
    }

    // ── PersonalResult / MatchEnd（result/01） ───────────────

    [Fact]
    public void PersonalResult_保持されPhaseは変わらない()
    {
        var state = Initial().With(phase: ClientPhase.Spectating);

        var next = Reducer.Apply(state, new PersonalResultAction
        {
            FinalRank = 42,
            Score = 1_234,
            TakoyakiCount = 56,
            SurvivedMs = 78_000,
            Stats = new MatchStats { ServedCount = 12, TotalMisses = 7 },
        });

        Assert.Equal(ClientPhase.Spectating, next.Phase);
        Assert.Equal(42, next.PersonalResult!.FinalRank);
        Assert.Equal(1_234, next.PersonalResult.Score);
        Assert.Equal(56, next.PersonalResult.TakoyakiCount);
        Assert.Equal(78_000, next.PersonalResult.SurvivedMs);
        Assert.Equal(7, next.PersonalResult.Stats.TotalMisses);
    }

    [Fact]
    public void PersonalResultのあとMatchEndでもPersonalResultが残る()
    {
        var state = Reducer.Apply(Initial(), new PersonalResultAction { FinalRank = 42, Score = 100 });

        var next = Reducer.Apply(state, new MatchEndAction());

        Assert.Equal(ClientPhase.Result, next.Phase);
        Assert.True(next.MatchEnded);
        Assert.Equal(42, next.PersonalResult!.FinalRank);
    }

    [Fact]
    public void MatchEndのあとPersonalResultでも最終状態は同じ_順序非依存()
    {
        var forward = Reducer.Apply(
            Reducer.Apply(Initial(), new PersonalResultAction { FinalRank = 42, Score = 100 }),
            new MatchEndAction());

        var reverse = Reducer.Apply(
            Reducer.Apply(Initial(), new MatchEndAction()),
            new PersonalResultAction { FinalRank = 42, Score = 100 });

        Assert.Equal(forward.Phase, reverse.Phase);
        Assert.Equal(forward.MatchEnded, reverse.MatchEnded);
        Assert.Equal(forward.PersonalResult!.FinalRank, reverse.PersonalResult!.FinalRank);
    }

    [Fact]
    public void PersonalResult未受信でもMatchEndでResultへ進む()
    {
        var next = Reducer.Apply(Initial(), new MatchEndAction());

        Assert.Equal(ClientPhase.Result, next.Phase);
        Assert.Null(next.PersonalResult);
    }

    [Fact]
    public void PersonalResultは後着で上書きされる()
    {
        var state = Reducer.Apply(Initial(), new PersonalResultAction { FinalRank = 42 });

        var next = Reducer.Apply(state, new PersonalResultAction { FinalRank = 7 });

        Assert.Equal(7, next.PersonalResult!.FinalRank);
    }

    // ── LocalMatchReset（result/01 §4） ──────────────────────

    [Fact]
    public void LocalMatchResetで試合に紐づく値がすべて捨てられる()
    {
        var state = Reducer.Apply(WithRankedTable("store-01"), new PersonalResultAction { FinalRank = 42 });
        state = Reducer.Apply(state, new MatchEndAction());
        state = Reducer.Apply(state, new ForcedEliminationWarningAction { UntilMs = 1_000 });

        var next = Reducer.Apply(state, new LocalMatchResetAction());

        Assert.Null(next.PersonalResult);
        Assert.False(next.MatchEnded);
        Assert.Empty(next.Ranking.Rows);
        Assert.Empty(next.DisplayNames);
        Assert.Null(next.Cull);
        Assert.Equal(0, next.Score);
        Assert.Equal(0, next.Rank);
        Assert.Equal(0, next.AliveCount);
        Assert.False(next.Alive);
        Assert.Equal("", next.MatchId);
        Assert.Equal("", next.SelfStoreId);
        Assert.Empty(next.Queue);
        Assert.Null(next.CurrentOrder);
    }

    [Fact]
    public void LocalMatchResetはConnectionとEventLogを変えない()
    {
        var log = new List<LogEntry> { new() { Message = "hello", AtMs = 1 } };
        var state = Initial().With(
            connection: ConnectionState.Connected,
            phase: ClientPhase.Result,
            lastError: "boom",
            eventLog: log);

        var next = Reducer.Apply(state, new LocalMatchResetAction());

        Assert.Equal(ConnectionState.Connected, next.Connection);
        Assert.Equal(ClientPhase.Result, next.Phase);
        Assert.Equal("boom", next.LastError);
        Assert.Same(log, next.EventLog);
    }

    // ── 既存のふるまい（変更なし） ─────────────────────────

    [Fact]
    public void Reducerは純粋_入力stateを変更しない()
    {
        var state = Initial();

        var next = Reducer.Apply(state, new EvaluationUpdateAction { Score = 2 });

        Assert.Equal(0, state.Score);
        Assert.Equal(2, next.Score);
    }

    [Fact]
    public void 重複CustomerArrivedは後着が無視される()
    {
        var state = Initial();
        var arrived = new CustomerArrivedAction { Customer = new CustomerView { CustomerId = "c1" }, ArrivedAtLocalMs = 0 };
        var afterFirst = Reducer.Apply(state, arrived);

        var duplicate = new CustomerArrivedAction { Customer = new CustomerView { CustomerId = "c1" }, ArrivedAtLocalMs = 100 };
        var afterSecond = Reducer.Apply(afterFirst, duplicate);

        Assert.Same(afterFirst, afterSecond);
        Assert.Single(afterSecond.Queue);
    }

    [Fact]
    public void LocalOrderCleared_行列先頭が除去される()
    {
        var state = Initial().With(
            queue: new List<CustomerEntry>
            {
                new() { View = new CustomerView { CustomerId = "c1" } },
                new() { View = new CustomerView { CustomerId = "c2" } },
            },
            currentOrder: new CurrentOrder { CustomerId = "c1" });

        var next = Reducer.Apply(state, new LocalOrderClearedAction("c1"));

        Assert.Single(next.Queue);
        Assert.Equal("c2", next.Queue[0].View.CustomerId);
        Assert.Null(next.CurrentOrder);
    }

    [Fact]
    public void 購読は変化時のみ発火しDispose後は発火しない()
    {
        var store = new Store();
        var callCount = 0;
        var subscription = store.Subscribe(_ => callCount++);

        // 空の差分は state を変えないので通知も出ない。
        store.Apply(new RankingDeltaAction());
        Assert.Equal(0, callCount);

        store.Apply(new EvaluationUpdateAction { Score = 5 });
        Assert.Equal(1, callCount);

        subscription.Dispose();
        store.Apply(new EvaluationUpdateAction { Score = 1 });
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void LocalKeyJudgedはCurrentOrderの進捗を更新する()
    {
        var state = Initial().With(currentOrder: new CurrentOrder { CustomerId = "c1", OrderCount = 2 });
        var view = new TypingView("たこ", 1, "k", 0, 2, 1);

        var next = Reducer.Apply(state, new LocalKeyJudgedAction(KeyResult.Correct, view));

        Assert.Equal(1, next.CurrentOrder!.TypedLength);
        Assert.Equal(1, next.CurrentOrder.MissCount);
    }

    // ── RankingTable のヘルパー（match-state/02 §2） ─────────

    [Fact]
    public void RankingTable_Topは不足分を詰めて返しnが0以下なら空()
    {
        var table = Reducer.Apply(Initial(), MatchStart("store-01", Stores99())).Ranking;

        Assert.Equal(10, table.Top(10).Count);
        Assert.Equal(99, table.Top(500).Count);
        Assert.Empty(table.Top(0));
        Assert.Empty(table.Top(-1));
    }

    [Fact]
    public void RankingTable_Findは無ければnullを返す()
    {
        var table = Reducer.Apply(Initial(), MatchStart("store-01", Stores99())).Ranking;

        Assert.NotNull(table.Find("store-42"));
        Assert.Null(table.Find("store-999"));
    }
}
