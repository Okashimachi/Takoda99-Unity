using System.Collections.Generic;
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

    private void GoToInMatch()
    {
        _controller.Start(new BootstrapConfig { WebSocketUrl = "wss://example" });
        _controller.BeginPlay();
        _networkClient.SetState(ConnectionState.Connected);
        _dispatcher.HandleRaw("""{"type":"MatchStart","payload":{"matchId":"m1","selfStoreId":"s1","params":{"initialLife":3,"maxStores":99},"phase":"Early","stores":[]}}""");
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
    public void MatchStart受信でInMatchへ()
    {
        GoToInMatch();
        Assert.Equal(ClientPhase.InMatch, _controller.Phase);
    }

    [Fact]
    public void 自店StoreEliminatedでSpectatingへ()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"StoreEliminated","payload":{"storeId":"s1","reason":"SelfCollapse","finalRank":50}}""");

        Assert.Equal(ClientPhase.Spectating, _controller.Phase);
    }

    [Fact]
    public void 他店StoreEliminatedはphaseが変わらない()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"StoreEliminated","payload":{"storeId":"other","reason":"SelfCollapse","finalRank":50}}""");

        Assert.Equal(ClientPhase.InMatch, _controller.Phase);
    }

    [Fact]
    public void MatchEndでResultへ_InMatchから()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{"finalRank":1,"stats":{"servedCount":10,"avgAccuracy":0.9,"avgElapsedMs":1000}}}""");

        Assert.Equal(ClientPhase.Result, _controller.Phase);
    }

    [Fact]
    public void MatchEndでResultへ_Spectatingから()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"StoreEliminated","payload":{"storeId":"s1","reason":"SelfCollapse","finalRank":50}}""");
        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{"finalRank":50,"stats":{"servedCount":1,"avgAccuracy":0.5,"avgElapsedMs":1000}}}""");

        Assert.Equal(ClientPhase.Result, _controller.Phase);
    }

    [Fact]
    public void OrderClearedの順序_LocalOrderClearedしてからOrderServedをEnqueueする()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":10000}}""");

        _inputSource.Type("tako");

        Assert.Null(_store.State.CurrentOrder);
        Assert.Contains(_renderer.OrderServed, id => id == "c1");
    }

    [Fact]
    public void 同一客への二重OrderServedは送られない()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":10000}}""");
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
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":10000}}""");
        _dispatcher.HandleRaw("""{"type":"StoreEliminated","payload":{"storeId":"s1","reason":"SelfCollapse","finalRank":50}}""");

        _inputSource.Type("tako");

        Assert.Empty(_renderer.OrderServed);
        Assert.True(_typingJudge.IsIdle);
    }

    [Fact]
    public void 対応中の客のCustomerLeftでAbortOrderされOrderServedが送られない()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":10000}}""");
        _inputSource.Type("ta");

        _dispatcher.HandleRaw("""{"type":"CustomerLeft","payload":{"customerId":"c1","reason":"Timeout"}}""");

        Assert.True(_typingJudge.IsIdle);
        Assert.Empty(_renderer.OrderServed);
    }

    [Fact]
    public void 提供後の先頭入れ替わりで次の客にBeginOrderされる()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":10000}}""");
        _dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c2","attribute":"Normal","orderCount":1,"words":["いか"],"patienceMaxMs":10000}}""");

        _inputSource.Type("tako");

        Assert.False(_typingJudge.IsIdle);
        Assert.Equal("いか", _typingJudge.CurrentView.CurrentWord);
    }

    [Fact]
    public void Rematchで接続が張り直される()
    {
        GoToInMatch();
        _dispatcher.HandleRaw("""{"type":"MatchEnd","payload":{"finalRank":1,"stats":{"servedCount":1,"avgAccuracy":1,"avgElapsedMs":100}}}""");

        _controller.Rematch();

        Assert.Equal(ClientPhase.Connecting, _controller.Phase);
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
