using System;
using System.Collections.Generic;
using Takoda99.Client.Net;
using Takoda99.Client.State;

namespace Takoda99.Client.Tests.Net;

public sealed class FakeNetworkClient : INetworkClient
{
    public List<(string Type, object Payload)> Sent { get; } = new();

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public void Connect(string url)
    {
    }

    public void Disconnect()
    {
    }

    public void Send(string type, object payload) => Sent.Add((type, payload));

    public event Action<string>? OnReceiveRaw;

    public event Action<ConnectionState, string?>? OnConnectionChanged;

    public void RaiseReceive(string json) => OnReceiveRaw?.Invoke(json);

    public void SetState(ConnectionState state, string? error = null)
    {
        State = state;
        OnConnectionChanged?.Invoke(state, error);
    }
}
