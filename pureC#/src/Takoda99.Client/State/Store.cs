using System;
using System.Collections.Generic;

namespace Takoda99.Client.State;

/// <summary><see cref="IStore"/> の実装。<see cref="ClientState"/> の唯一の保持者（04-store-reducer.md）。</summary>
public sealed class Store : IStore
{
    private readonly List<Action<ClientState>> _listeners = new();
    private readonly Queue<IAction> _pendingActions = new();
    private bool _isNotifying;

    public Store(ClientState? initialState = null)
    {
        State = initialState ?? new ClientState();
    }

    public ClientState State { get; private set; }

    public void Apply(IAction action)
    {
        if (_isNotifying)
        {
            // 通知中の再入：無限再帰を防ぐため次のループで処理する（§3.5）。
            _pendingActions.Enqueue(action);
            return;
        }

        ApplyInternal(action);

        while (_pendingActions.Count > 0)
        {
            ApplyInternal(_pendingActions.Dequeue());
        }
    }

    private void ApplyInternal(IAction action)
    {
        var next = Reducer.Apply(State, action);
        if (ReferenceEquals(next, State))
        {
            return;
        }

        State = next;
        Notify();
    }

    private void Notify()
    {
        _isNotifying = true;
        try
        {
            foreach (var listener in _listeners.ToArray())
            {
                listener(State);
            }
        }
        finally
        {
            _isNotifying = false;
        }
    }

    public IDisposable Subscribe(Action<ClientState> listener)
    {
        _listeners.Add(listener);
        return new Subscription(this, listener);
    }

    private sealed class Subscription : IDisposable
    {
        private Store? _store;
        private readonly Action<ClientState> _listener;

        public Subscription(Store store, Action<ClientState> listener)
        {
            _store = store;
            _listener = listener;
        }

        public void Dispose()
        {
            _store?._listeners.Remove(_listener);
            _store = null;
        }
    }
}
