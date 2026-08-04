using Takoda99.Client.Contract;
using Takoda99.Client.Net;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.Net;

public class SendQueueTests
{
    private readonly FakeNetworkClient _networkClient = new();
    private readonly EnvelopeCodec _codec = new();
    private readonly EnvelopeLog _log = new();
    private readonly SendQueue _queue;

    public SendQueueTests()
    {
        _queue = new SendQueue(_networkClient, _codec, _log, capacity: 16);
    }

    [Fact]
    public void Enqueue順にflushされる()
    {
        _queue.Flush(); // 接続確立扱いにする
        _queue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin());
        _queue.Enqueue(MessageType.OrderServed, new OrderServed { CustomerId = "c1" });
        _queue.Flush();

        Assert.Equal(MessageType.MatchmakingJoin, _networkClient.Sent[0].Type);
        Assert.Equal(MessageType.OrderServed, _networkClient.Sent[1].Type);
    }

    [Fact]
    public void 切断中のOrderServedは破棄される()
    {
        _queue.Enqueue(MessageType.OrderServed, new OrderServed { CustomerId = "c1" });
        _queue.Flush();

        Assert.Empty(_networkClient.Sent);
    }

    [Fact]
    public void 接続中の切断でOrderServedが破棄される()
    {
        _queue.Flush(); // 接続扱い
        _queue.Enqueue(MessageType.OrderServed, new OrderServed { CustomerId = "c1" });
        _queue.OnDisconnected();
        _queue.Flush();

        Assert.Empty(_networkClient.Sent);
    }

    [Fact]
    public void 切断中のJoinLeaveJoinは最新のJoinのみ残る()
    {
        _queue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin());
        _queue.Enqueue(MessageType.MatchmakingLeave, new MatchmakingLeave());
        _queue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin());
        _queue.Flush();

        Assert.Single(_networkClient.Sent);
        Assert.Equal(MessageType.MatchmakingJoin, _networkClient.Sent[0].Type);
    }

    [Fact]
    public void キュー上限超過で古いものから捨てられる()
    {
        _queue.Flush();
        for (var i = 0; i < 20; i++)
        {
            _queue.Enqueue(MessageType.OrderServed, new OrderServed { CustomerId = $"c{i}" });
        }

        _queue.Flush();

        Assert.Equal(16, _networkClient.Sent.Count);
        Assert.Equal("c4", ((OrderServed)_networkClient.Sent[0].Payload).CustomerId);
    }

    [Fact]
    public void 再接続成功でJoinが再送される()
    {
        _queue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin());
        _queue.OnDisconnected();
        _queue.Flush();

        Assert.Single(_networkClient.Sent);
        Assert.Equal(MessageType.MatchmakingJoin, _networkClient.Sent[0].Type);
    }
}
