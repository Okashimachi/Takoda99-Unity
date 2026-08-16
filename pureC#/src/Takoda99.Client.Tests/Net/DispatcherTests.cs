using Takoda99.Client.Contract;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using Xunit;

namespace Takoda99.Client.Tests.Net;

public class DispatcherTests
{
    private readonly EnvelopeCodec _codec = new();
    private readonly Store _store;
    private readonly EnvelopeLog _log = new();
    private readonly FakeClock _clock = new();
    private readonly Dispatcher _dispatcher;

    public DispatcherTests()
    {
        _store = new Store(new ClientState().With(phase: ClientPhase.InMatch));
        _dispatcher = new Dispatcher(_codec, _store, _log, _clock);
    }

    private Dispatcher DispatcherAt(ClientPhase phase, out Store store)
    {
        store = new Store(new ClientState().With(phase: phase));
        return new Dispatcher(_codec, store, _log, _clock);
    }

    [Fact]
    public void 既知typeはActionとしてStoreに反映される()
    {
        _dispatcher.HandleRaw("""{"type":"EvaluationUpdate","payload":{"score":420,"rank":2,"aliveCount":10}}""");

        Assert.Equal(420, _store.State.Score);
        Assert.Equal(2, _store.State.Rank);
    }

    /// <summary>Obsolete フィールドが 0 で届いても、state に取り込まれる経路が無いこと。</summary>
    [Fact]
    public void EvaluationUpdateのObsoleteフィールドは読まれない()
    {
        _dispatcher.HandleRaw(
            """{"type":"EvaluationUpdate","payload":{"score":420,"rank":2,"aliveCount":10,"evalRaw":0,"normalized":0,"starRating":0,"starDelta":0}}""");

        Assert.Equal(420, _store.State.Score);
    }

    [Fact]
    public void MatchEndはペイロードが空でも成功しResultへ進む()
    {
        string? droppedReason = null;
        _dispatcher.OnMessageDropped += (_, reason) => droppedReason = reason;

        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{}}""");

        Assert.Null(droppedReason);
        Assert.Equal(ClientPhase.Result, _store.State.Phase);
        Assert.True(_store.State.MatchEnded);
    }

    [Fact]
    public void PersonalResultが保持されPhaseは変わらない()
    {
        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":42,"score":1234,"takoyakiCount":56,"survivedMs":78000,"stats":{"servedCount":12,"totalMisses":7,"normal":{"served":12,"left":0},"bonus":{"served":0,"left":0},"claimer":{"served":0,"left":0},"buzz":{"served":0,"left":0}}}}""");

        Assert.Equal(ClientPhase.InMatch, _store.State.Phase);
        Assert.Equal(42, _store.State.PersonalResult!.FinalRank);
        Assert.Equal(56, _store.State.PersonalResult.TakoyakiCount);
        Assert.Equal(7, _store.State.PersonalResult.Stats.TotalMisses);
    }

    [Fact]
    public void PersonalResultのstatsがnullでも空のMatchStatsへ正規化される()
    {
        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":42,"score":1234,"takoyakiCount":56,"survivedMs":78000,"stats":null}}""");

        Assert.NotNull(_store.State.PersonalResult!.Stats);
        Assert.Equal(0, _store.State.PersonalResult.Stats.ServedCount);
    }

    /// <summary>Proto が「null で届き得る」と明記しているコレクションを落とさないこと。</summary>
    [Fact]
    public void entriesが欠落したRankingSnapshotは空リスト扱いで捨てられない()
    {
        string? droppedReason = null;
        _dispatcher.OnMessageDropped += (_, reason) => droppedReason = reason;

        _dispatcher.HandleRaw("""{"type":"RankingSnapshot","payload":{}}""");

        Assert.Null(droppedReason);
        // 空の全量は「情報なし」なので表を消さない。
        Assert.Empty(_store.State.Ranking.Rows);
    }

    [Fact]
    public void entriesがnullのRankingDeltaも捨てられない()
    {
        string? droppedReason = null;
        _dispatcher.OnMessageDropped += (_, reason) => droppedReason = reason;

        _dispatcher.HandleRaw("""{"type":"RankingDelta","payload":{"entries":null}}""");

        Assert.Null(droppedReason);
    }

    [Fact]
    public void cutStoreIdsがnullのForcedEliminationWarningも捨てられない()
    {
        _dispatcher.HandleRaw(
            """{"type":"ForcedEliminationWarning","payload":{"untilMs":5000,"stageIndex":1,"stageTotal":6,"cutLineRank":51,"cutStoreIds":null,"selfAtRisk":false}}""");

        Assert.NotNull(_store.State.Cull);
        Assert.Empty(_store.State.Cull!.CutStoreIds);
    }

    /// <summary>補間の起点は「予告を受け取った瞬間」でなければならない（result/02 §2.2）。</summary>
    [Fact]
    public void ForcedEliminationWarningのReceivedAtLocalMsにClockの値が入る()
    {
        _clock.MonotonicMs = 4_242;

        _dispatcher.HandleRaw(
            """{"type":"ForcedEliminationWarning","payload":{"untilMs":5000,"stageIndex":1,"stageTotal":6,"cutLineRank":51,"cutStoreIds":[],"selfAtRisk":true}}""");

        Assert.Equal(4_242, _store.State.Cull!.ReceivedAtLocalMs);
        Assert.True(_store.State.Cull.SelfAtRisk);
        Assert.Equal(5_000, _store.State.Cull.RemainingMsAt(4_242));
    }

    [Fact]
    public void 壊れたJSONは例外にならず後続メッセージが処理される()
    {
        _dispatcher.HandleRaw("not json at all");
        _dispatcher.HandleRaw("""{"type":"EvaluationUpdate","payload":{"score":9,"rank":1,"aliveCount":2}}""");

        Assert.Equal(9, _store.State.Score);
    }

    [Fact]
    public void 未知typeはOnUnknownMessageが発火しstateは不変()
    {
        string? capturedType = null;
        _dispatcher.OnUnknownMessage += (type, _) => capturedType = type;

        var before = _store.State;
        _dispatcher.HandleRaw("""{"type":"SomeFutureMessage","payload":{}}""");

        Assert.Equal("SomeFutureMessage", capturedType);
        Assert.Same(before, _store.State);
    }

    /// <summary>
    /// 本選で送られなくなったメッセージ。型は Proto に残っているが受理表から消えているので、
    /// 既存の未知メッセージ経路で黙って捨てられる（contract/01 §6）。落ちないことが要件。
    /// </summary>
    [Theory]
    [InlineData("CustomerLeft", """{"customerId":"c1","reason":"Timeout"}""")]
    [InlineData("CreditUpdate", """{"life":2,"delta":-1,"reason":"CustomerLeft"}""")]
    [InlineData("StoreListUpdate", """{"stores":[],"aliveCount":5}""")]
    [InlineData("StoreEliminated", """{"storeId":"s2","reason":"Cull","finalRank":40}""")]
    public void 廃止済みメッセージはOnUnknownMessageが1回発火しstateが変わらない(string type, string payload)
    {
        var unknownCount = 0;
        string? capturedReason = null;
        _dispatcher.OnUnknownMessage += (_, reason) =>
        {
            unknownCount++;
            capturedReason = reason;
        };

        var before = _store.State;
        _dispatcher.HandleRaw($$"""{"type":"{{type}}","payload":{{payload}}}""");

        Assert.Equal(1, unknownCount);
        Assert.Equal("unknown-type", capturedReason);
        Assert.Same(before, _store.State);
    }

    [Fact]
    public void 必須フィールド欠落は破棄されOnMessageDroppedが発火する()
    {
        string? droppedType = null;
        _dispatcher.OnMessageDropped += (type, _) => droppedType = type;

        var before = _store.State;
        // customerId は識別子なので欠落を許さない。
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"attribute":"Normal","orderCount":1,"words":["たこ"]}}""");

        Assert.Equal("CustomerArrived", droppedType);
        Assert.Same(before, _store.State);
    }

    [Fact]
    public void phase外メッセージは破棄されstateは不変()
    {
        var dispatcher = DispatcherAt(ClientPhase.Title, out var store);
        string? droppedType = null;
        dispatcher.OnMessageDropped += (type, _) => droppedType = type;

        var before = store.State;
        dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"]}}""");

        Assert.Equal("CustomerArrived", droppedType);
        Assert.Same(before, store.State);
    }

    // ── 受理表（result/02 §2.1） ─────────────────────────────

    [Fact]
    public void SpectatingではRankingDeltaが受理される()
    {
        var dispatcher = DispatcherAt(ClientPhase.Spectating, out var store);

        dispatcher.HandleRaw(
            """{"type":"RankingDelta","payload":{"entries":[{"storeId":"s1","score":10,"alive":true}]}}""");

        Assert.Single(store.State.Ranking.Rows);
    }

    [Fact]
    public void ResultではRankingDeltaが落ちる()
    {
        var dispatcher = DispatcherAt(ClientPhase.Result, out var store);
        string? droppedType = null;
        string? droppedReason = null;
        dispatcher.OnMessageDropped += (type, reason) =>
        {
            droppedType = type;
            droppedReason = reason;
        };

        dispatcher.HandleRaw(
            """{"type":"RankingDelta","payload":{"entries":[{"storeId":"s1","score":10,"alive":true}]}}""");

        Assert.Equal("RankingDelta", droppedType);
        Assert.Equal("phase-not-allowed", droppedReason);
    }

    /// <summary>最後の全量（＝全店の最終順位）をリザルト画面が使うため。</summary>
    [Fact]
    public void ResultでもRankingSnapshotは受理される()
    {
        var dispatcher = DispatcherAt(ClientPhase.Result, out var store);

        dispatcher.HandleRaw(
            """{"type":"RankingSnapshot","payload":{"entries":[{"storeId":"s1","rank":1,"score":10,"alive":false}]}}""");

        Assert.Single(store.State.Ranking.Rows);
    }

    [Fact]
    public void MatchmakingではPersonalResultが落ちる()
    {
        var dispatcher = DispatcherAt(ClientPhase.Matchmaking, out var store);
        string? droppedReason = null;
        dispatcher.OnMessageDropped += (_, reason) => droppedReason = reason;

        dispatcher.HandleRaw("""{"type":"PersonalResult","payload":{"finalRank":1,"score":0,"takoyakiCount":0,"survivedMs":0}}""");

        Assert.Equal("phase-not-allowed", droppedReason);
        Assert.Null(store.State.PersonalResult);
    }

    [Fact]
    public void ResultでもStoreEliminatedBatchは受理される()
    {
        var dispatcher = DispatcherAt(ClientPhase.Result, out var store);

        dispatcher.HandleRaw(
            """{"type":"StoreEliminatedBatch","payload":{"stageIndex":6,"entries":[{"storeId":"s1","reason":"Cull","finalRank":1}]}}""");

        Assert.Single(store.State.Ranking.Rows);
        Assert.False(store.State.Ranking.Rows[0].Alive);
    }

    [Fact]
    public void Spectatingでのevaluationupdateは受理される()
    {
        var dispatcher = DispatcherAt(ClientPhase.Spectating, out var store);

        dispatcher.HandleRaw("""{"type":"EvaluationUpdate","payload":{"score":33,"rank":2,"aliveCount":10}}""");

        Assert.Equal(2, store.State.Rank);
        Assert.Equal(33, store.State.Score);
    }

    [Fact]
    public void Spectatingでのcustomerarrivedは無視される()
    {
        var dispatcher = DispatcherAt(ClientPhase.Spectating, out var store);
        var before = store.State;

        dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"]}}""");

        Assert.Same(before, store.State);
    }

    [Fact]
    public void MatchStartのStartedAtLocalMsにClockの値が入る()
    {
        var dispatcher = DispatcherAt(ClientPhase.Matchmaking, out var store);
        _clock.MonotonicMs = 9_876;

        dispatcher.HandleRaw(
            """{"type":"MatchStart","payload":{"matchId":"m1","selfStoreId":"s1","phase":"Early","startsAtServerMs":1700000000000,"params":{"maxStores":99},"stores":[{"storeId":"s1","displayName":"たこ屋","rank":1,"alive":true,"score":0}]}}""");

        Assert.Equal(9_876, store.State.StartedAtMs);
        Assert.Equal("たこ屋", store.State.DisplayNames["s1"]);
    }

    [Fact]
    public void ログリングバッファは送受信が時系列で並ぶ()
    {
        _dispatcher.HandleRaw("""{"type":"EvaluationUpdate","payload":{"score":1,"rank":1,"aliveCount":1}}""");
        _log.RecordOutgoing("""{"type":"OrderServed","payload":{}}""");

        Assert.Equal(EnvelopeLogDirection.Outgoing, _log.Entries[0].Direction);
        Assert.Equal(EnvelopeLogDirection.Incoming, _log.Entries[1].Direction);
    }
}
