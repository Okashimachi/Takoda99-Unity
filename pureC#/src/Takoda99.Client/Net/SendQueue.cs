using System.Collections.Generic;
using System.Linq;
using Takoda99.Client.Contract;
using Takoda99.Proto;

namespace Takoda99.Client.Net;

/// <summary><see cref="ISendQueue"/> の実装（05-dispatcher.md §3.3）。</summary>
public sealed class SendQueue : ISendQueue
{
    private sealed class Item
    {
        public Item(string type, object payload)
        {
            Type = type;
            Payload = payload;
        }

        public string Type { get; }
        public object Payload { get; }
    }

    private readonly INetworkClient _networkClient;
    private readonly IEnvelopeCodec _codec;
    private readonly IEnvelopeLog _log;
    private readonly int _capacity;
    private readonly List<Item> _queue = new();
    private bool _connected;

    public SendQueue(INetworkClient networkClient, IEnvelopeCodec codec, IEnvelopeLog log, int capacity = 16)
    {
        _networkClient = networkClient;
        _codec = codec;
        _log = log;
        _capacity = capacity;
    }

    public void Enqueue(string type, object payload)
    {
        if (type == MessageType.OrderServed && !_connected)
        {
            // 切断中に発生した OrderServed は破棄する（再送しない。§3.3）。
            return;
        }

        if (IsMatchmakingIntent(type))
        {
            // MatchmakingJoin / MatchmakingLeave は最新の意思のみ保持する。
            _queue.RemoveAll(i => IsMatchmakingIntent(i.Type));
        }

        _queue.Add(new Item(type, payload));

        while (_queue.Count > _capacity)
        {
            _queue.RemoveAt(0);
        }
    }

    public void Flush()
    {
        _connected = true;
        var toSend = _queue.ToList();
        _queue.Clear();

        foreach (var item in toSend)
        {
            var json = _codec.EncodeEnvelope(item.Type, item.Payload);
            _log.RecordOutgoing(json);
            _networkClient.Send(item.Type, item.Payload);
        }
    }

    public void OnDisconnected()
    {
        _connected = false;
        _queue.RemoveAll(i => i.Type == MessageType.OrderServed);
    }

    private static bool IsMatchmakingIntent(string type) =>
        type == MessageType.MatchmakingJoin || type == MessageType.MatchmakingLeave;
}
