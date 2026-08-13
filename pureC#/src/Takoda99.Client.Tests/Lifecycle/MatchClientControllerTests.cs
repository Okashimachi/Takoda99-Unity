using System.Linq;
using Takoda99.Client.Contract;
using Takoda99.Client.Lifecycle;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using Takoda99.Client.Tests.Net;
using Takoda99.Client.Typing;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.Lifecycle;

public class MatchClientControllerTests
{
    private readonly FakeNetworkClient _networkClient = new();
    private readonly EnvelopeCodec _codec = new();
    private readonly Store _store = new();
    private readonly EnvelopeLog _log = new();
    private readonly FakeClock _clock = new();
    private readonly Dispatcher _dispatcher;
    private readonly TypingJudge _typingJudge;
    private readonly SendQueue _sendQueue;
    private readonly FakeRenderer _renderer = new();
    private readonly FakeInputSource _inputSource = new();
    private readonly MatchClientController _controller;

    public MatchClientControllerTests()
    {
        _dispatcher = new Dispatcher(_codec, _store, _log, _clock);
        _typingJudge = new TypingJudge(new DefaultRomajiTable(), _clock);
        _sendQueue = new SendQueue(_networkClient, _codec, _log);
        _controller = new MatchClientController(_networkClient, _dispatcher, _store, _typingJudge, _sendQueue, _renderer, _inputSource);
    }

    // JSON は ' で書いて " へ置換する（生文字列＋補間だと波括弧のエスケープで読みづらくなるため）。
    private static string Json(string singleQuoted) => singleQuoted.Replace('\'', '"');

    private const string SelfStore = "{'storeId':'s1','displayName':'たこ屋','rank':1,'alive':true,'score':0}";

    private void GoToInMatch()
    {
        _controller.Start(new BootstrapConfig { WebSocketUrl = "wss://example" });
        _controller.BeginPlay();
        _networkClient.SetState(ConnectionState.Connected);
        _dispatcher.HandleRaw(Json(
            "{'type':'MatchStart','payload':{'matchId':'m1','selfStoreId':'s1','params':{'maxStores':99},"
            + "'phase':'Early','startsAtServerMs':0,'stores':[" + SelfStore + "]}}"));
    }

    private void Arrive(string customerId, string word)
        => _dispatcher.HandleRaw(Json(
            "{'type':'CustomerArrived','payload':{'customerId':'" + customerId
            + "','attribute':'Normal','orderCount':1,'words':['" + word + "']}}"));

    /// <summary>指定件数の脱落バッチを流す。includeSelf を true にすると自店 s1 を含める。</summary>
    private void EliminateBatch(int stageIndex, int count, bool includeSelf)
    {
        var entries = Enumerable.Range(1, count)
            .Select(i => "{'storeId':'other-" + i + "','reason':'Cull','finalRank':" + (50 + i) + "}")
            .ToList();

        if (includeSelf)
        {
            entries.Insert(0, "{'storeId':'s1','reason':'Cull','finalRank':50}");
        }

        _dispatcher.HandleRaw(Json(
            "{'type':'StoreEliminatedBatch','payload':{'stageIndex':" + stageIndex
            + ",'entries':[" + string.Join(",", entries) + "]}}"));
    }

    [Fact]
    public void phase遷移_BootからConnectingまで()
    {
        Assert.Equal(ClientPhase.Boot, _controller.Phase);
        _controller.Start(new BootstrapConfig());
        Assert.Equal(ClientPhase.Title, _controller.Phase);
        _controller.BeginPlay();
        Assert.Equal(ClientPhase.Connecting, _controller.Phase);
    }

    [Fact]
    public void 接続確立でMatchmakingへ遷移しJoinを送信する()
    {
        _controller.Start(new BootstrapConfig { WebSocketUrl = "wss://example" });
        _controller.BeginPlay();
        _networkClient.SetState(ConnectionState.Connected);

        Assert.Equal(ClientPhase.Matchmaking, _controller.Phase);
        Assert.Contains(_networkClient.Sent, s => s.Type == MessageType.MatchmakingJoin);
    }

    [Fact]
    public void BeginPlayに渡した表示名がMatchmakingJoinに乗る()
    {
        _controller.Start(new BootstrapConfig { WebSocketUrl = "wss://example" });
        _controller.BeginPlay("たこ焼き");
        _networkClient.SetState(ConnectionState.Connected);

        var sent = Assert.Single(_networkClient.Sent, s => s.Type == MessageType.MatchmakingJoin);
        var payload = Assert.IsType<MatchmakingJoin>(sent.Payload);
        Assert.Equal("たこ焼き", payload.DisplayName);
    }

    [Fact]
    public void MatchStart受信でInMatchへ()
    {
        GoToInMatch();
        Assert.Equal(ClientPhase.InMatch, _controller.Phase);
    }

    // ── 一斉脱落（match-state/03・result/02 §5.2） ───────────

    [Fact]
    public void 自店を含むバッチでSpectatingへ移りAbortOrderされる()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        _inputSource.Type("ta");

        EliminateBatch(3, count: 2, includeSelf: true);

        Assert.Equal(ClientPhase.Spectating, _controller.Phase);
        Assert.True(_typingJudge.IsIdle);
        Assert.Empty(_renderer.OrderServed);

        var batch = Assert.Single(_renderer.EliminatedBatches);
        Assert.True(batch.IncludesSelf);
        Assert.Equal(3, batch.StageIndex);
    }

    [Fact]
    public void 自店を含まないバッチではAbortOrderされずInMatchのまま()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        _inputSource.Type("ta");

        EliminateBatch(1, count: 5, includeSelf: false);

        Assert.Equal(ClientPhase.InMatch, _controller.Phase);
        Assert.False(_typingJudge.IsIdle);

        var batch = Assert.Single(_renderer.EliminatedBatches);
        Assert.False(batch.IncludesSelf);
    }

    /// <summary>1ステージで最大49店が同時に脱落する。演出は1回に集約すること。</summary>
    [Fact]
    public void 一斉脱落49件でもOnStoreEliminatedBatchは1回だけ呼ばれる()
    {
        GoToInMatch();

        EliminateBatch(2, count: 49, includeSelf: false);

        var batch = Assert.Single(_renderer.EliminatedBatches);
        Assert.Equal(49, batch.Entries.Count);
    }

    // ── 足切り予告・個人成績・試合終了 ───────────────────────

    [Fact]
    public void ForcedEliminationWarningでOnCullWarningが呼ばれる()
    {
        GoToInMatch();
        _clock.MonotonicMs = 500;

        _dispatcher.HandleRaw(
            """{"type":"ForcedEliminationWarning","payload":{"untilMs":8000,"stageIndex":2,"stageTotal":6,"cutLineRank":31,"cutStoreIds":["other-1"],"selfAtRisk":true}}""");

        var warning = Assert.Single(_renderer.CullWarnings);
        Assert.Equal(2, warning.StageIndex);
        Assert.True(warning.SelfAtRisk);
        Assert.Equal(500, warning.ReceivedAtLocalMs);
    }

    [Fact]
    public void PersonalResultでOnPersonalResultが呼ばれstateからも読める()
    {
        GoToInMatch();

        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":42,"score":1234,"takoyakiCount":56,"survivedMs":78000,"stats":{"servedCount":12,"totalMisses":7,"normal":{},"bonus":{},"claimer":{},"buzz":{}}}}""");

        var result = Assert.Single(_renderer.PersonalResults);
        Assert.Equal(42, result.FinalRank);
        Assert.Equal(42, _store.State.PersonalResult!.FinalRank);
        // 受信時点ではまだ試合画面にいる。
        Assert.Equal(ClientPhase.InMatch, _controller.Phase);
    }

    [Fact]
    public void MatchEndでOnMatchEndが引数なしで呼ばれResultへ進む()
    {
        GoToInMatch();

        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{}}""");

        Assert.Equal(1, _renderer.MatchEndCount);
        Assert.Equal(ClientPhase.Result, _controller.Phase);
    }

    [Fact]
    public void MatchEndでResultへ_Spectatingから()
    {
        GoToInMatch();
        EliminateBatch(3, count: 1, includeSelf: true);

        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{}}""");

        Assert.Equal(ClientPhase.Result, _controller.Phase);
    }

    /// <summary>脱落 → 個人成績 → 終了 の全経路を通っても成績が残っていること。</summary>
    [Fact]
    public void 脱落から試合終了までPersonalResultが保持され続ける()
    {
        GoToInMatch();

        EliminateBatch(3, count: 1, includeSelf: true);
        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":50,"score":900,"takoyakiCount":30,"survivedMs":60000,"stats":{"servedCount":8,"normal":{},"bonus":{},"claimer":{},"buzz":{}}}}""");

        Assert.Equal(ClientPhase.Spectating, _controller.Phase);
        Assert.Equal(50, _store.State.PersonalResult!.FinalRank);

        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{}}""");

        Assert.Equal(ClientPhase.Result, _controller.Phase);
        Assert.Equal(50, _store.State.PersonalResult!.FinalRank);
    }

    // ── 打鍵まわり ───────────────────────────────────────────

    [Fact]
    public void OrderClearedの順序_LocalOrderClearedしてからOrderServedをEnqueueする()
    {
        GoToInMatch();
        Arrive("c1", "たこ");

        _inputSource.Type("tako");

        Assert.Null(_store.State.CurrentOrder);
        Assert.Contains(_renderer.OrderServed, id => id == "c1");
    }

    [Fact]
    public void 同一客への二重OrderServedは送られない()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        _inputSource.Type("tako");
        _sendQueue.Flush();

        _networkClient.Sent.Clear();
        _sendQueue.Flush();

        Assert.DoesNotContain(_networkClient.Sent, s => s.Type == MessageType.OrderServed);
    }

    [Fact]
    public void Spectating中の打鍵はTypingJudgeに届かずOrderServedが送られない()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        EliminateBatch(3, count: 1, includeSelf: true);

        _inputSource.Type("tako");

        Assert.Empty(_renderer.OrderServed);
        Assert.True(_typingJudge.IsIdle);
    }

    /// <summary>
    /// 本選では客が逃げないため、打鍵中に対象が消える割り込みが存在しない。
    /// 次の客が来ても、対応中の注文は中断されない（離脱経路を削除したことの裏取り）。
    /// </summary>
    [Fact]
    public void 打鍵中にCustomerArrivedが来ても対応中の注文が中断されない()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        _inputSource.Type("ta");

        Arrive("c2", "いか");

        Assert.False(_typingJudge.IsIdle);
        Assert.Equal("たこ", _typingJudge.CurrentView.CurrentWord);

        // そのまま打ち切れる。
        _inputSource.Type("ko");
        Assert.Contains(_renderer.OrderServed, id => id == "c1");
    }

    [Fact]
    public void 提供後の先頭入れ替わりで次の客にBeginOrderされる()
    {
        GoToInMatch();
        Arrive("c1", "たこ");
        Arrive("c2", "いか");

        _inputSource.Type("tako");

        Assert.False(_typingJudge.IsIdle);
        Assert.Equal("いか", _typingJudge.CurrentView.CurrentWord);
    }

    // ── ライフサイクル（result/01 §4） ───────────────────────

    [Fact]
    public void Rematchで接続が張り直されPersonalResultが破棄される()
    {
        GoToInMatch();
        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":1,"score":9999,"takoyakiCount":100,"survivedMs":120000,"stats":{"servedCount":40,"normal":{},"bonus":{},"claimer":{},"buzz":{}}}}""");
        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{}}""");
        Assert.NotNull(_store.State.PersonalResult);

        _networkClient.Sent.Clear();
        _controller.Rematch();

        Assert.Equal(ClientPhase.Connecting, _controller.Phase);
        Assert.Null(_store.State.PersonalResult);
        Assert.False(_store.State.MatchEnded);
        Assert.Empty(_store.State.Ranking.Rows);

        _networkClient.SetState(ConnectionState.Connected);
        Assert.Contains(_networkClient.Sent, s => s.Type == MessageType.MatchmakingJoin);
    }

    [Fact]
    public void BeginPlayでも前の試合の保持値が破棄される()
    {
        GoToInMatch();
        _dispatcher.HandleRaw(
            """{"type":"PersonalResult","payload":{"finalRank":3,"score":10,"takoyakiCount":1,"survivedMs":10,"stats":{"servedCount":1,"normal":{},"bonus":{},"claimer":{},"buzz":{}}}}""");

        _controller.BackToTitle();
        _controller.BeginPlay();

        Assert.Null(_store.State.PersonalResult);
        Assert.Empty(_store.State.DisplayNames);
    }

    [Fact]
    public void 接続断で入力が止まりOnConnectionTroubleが発火する()
    {
        GoToInMatch();
        _networkClient.SetState(ConnectionState.Reconnecting);

        Assert.Contains("Reconnecting", _renderer.ConnectionTroubles);

        _inputSource.Type("t");
        Assert.True(_typingJudge.IsIdle || _renderer.KeyFeedback.Count == 0);
    }
}
