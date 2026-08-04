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

    [Fact]
    public void 既知typeはActionとしてStoreに反映される()
    {
        _dispatcher.HandleRaw("""{"type":"CreditUpdate","payload":{"life":2,"reason":"CustomerLeft"}}""");

        Assert.Equal(2, _store.State.CreditLife);
    }

    [Fact]
    public void 壊れたJSONは例外にならず後続メッセージが処理される()
    {
        _dispatcher.HandleRaw("not json at all");
        _dispatcher.HandleRaw("""{"type":"CreditUpdate","payload":{"life":9,"reason":"CustomerLeft"}}""");

        Assert.Equal(9, _store.State.CreditLife);
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

    [Fact]
    public void 必須フィールド欠落は破棄されOnMessageDroppedが発火する()
    {
        string? droppedType = null;
        _dispatcher.OnMessageDropped += (type, _) => droppedType = type;

        var before = _store.State;
        _dispatcher.HandleRaw("""{"type":"CustomerLeft","payload":{"reason":"Timeout"}}""");

        Assert.Equal("CustomerLeft", droppedType);
        Assert.Same(before, _store.State);
    }

    [Fact]
    public void phase外メッセージは破棄されstateは不変()
    {
        var store = new Store(new ClientState().With(phase: ClientPhase.Title));
        var dispatcher = new Dispatcher(_codec, store, _log, _clock);
        string? droppedType = null;
        dispatcher.OnMessageDropped += (type, _) => droppedType = type;

        var before = store.State;
        dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":1000}}""");

        Assert.Equal("CustomerArrived", droppedType);
        Assert.Same(before, store.State);
    }

    [Fact]
    public void Spectatingでのevaluationupdateは受理される()
    {
        var store = new Store(new ClientState().With(phase: ClientPhase.Spectating));
        var dispatcher = new Dispatcher(_codec, store, _log, _clock);

        dispatcher.HandleRaw("""{"type":"EvaluationUpdate","payload":{"evalRaw":1.5,"normalized":0.5,"rank":2,"aliveCount":10}}""");

        Assert.Equal(2, store.State.Rank);
    }

    [Fact]
    public void Spectatingでのcustomerarrivedは無視される()
    {
        var store = new Store(new ClientState().With(phase: ClientPhase.Spectating));
        var dispatcher = new Dispatcher(_codec, store, _log, _clock);
        var before = store.State;

        dispatcher.HandleRaw("""{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Normal","orderCount":1,"words":["たこ"],"patienceMaxMs":1000}}""");

        Assert.Same(before, store.State);
    }

    [Fact]
    public void ログリングバッファは送受信が時系列で並ぶ()
    {
        _dispatcher.HandleRaw("""{"type":"CreditUpdate","payload":{"life":1,"reason":"CustomerLeft"}}""");
        _log.RecordOutgoing("""{"type":"OrderServed","payload":{}}""");

        Assert.Equal(EnvelopeLogDirection.Outgoing, _log.Entries[0].Direction);
        Assert.Equal(EnvelopeLogDirection.Incoming, _log.Entries[1].Direction);
    }
}
