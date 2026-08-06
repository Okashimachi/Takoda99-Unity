using System;
using Takoda99.Client.Net;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.Lifecycle;

/// <summary><see cref="IMatchClientController"/> の実装（06-match-client-controller.md）。最後に結線する統括層。</summary>
public sealed class MatchClientController : IMatchClientController
{
    private readonly INetworkClient _networkClient;
    private readonly IDispatcher _dispatcher;
    private readonly IStore _store;
    private readonly ITypingJudge _typingJudge;
    private readonly ISendQueue _sendQueue;
    private readonly IRenderer _renderer;
    private readonly IInputSource _inputSource;

    private readonly IDisposable _storeSubscription;
    private BootstrapConfig _config = new();
    private string _displayName = "";
    private string? _servingCustomerId;
    private ClientPhase _lastNotifiedPhase = ClientPhase.Boot;

    public MatchClientController(
        INetworkClient networkClient,
        IDispatcher dispatcher,
        IStore store,
        ITypingJudge typingJudge,
        ISendQueue sendQueue,
        IRenderer renderer,
        IInputSource inputSource)
    {
        _networkClient = networkClient;
        _dispatcher = dispatcher;
        _store = store;
        _typingJudge = typingJudge;
        _sendQueue = sendQueue;
        _renderer = renderer;
        _inputSource = inputSource;

        _networkClient.OnReceiveRaw += _dispatcher.HandleRaw;
        _networkClient.OnConnectionChanged += HandleConnectionChanged;
        _inputSource.OnCharKey += HandleCharKey;
        _dispatcher.OnActionApplied += HandleActionApplied;
        _storeSubscription = _store.Subscribe(HandleStateChanged);
    }

    public ClientPhase Phase => _store.State.Phase;

    public void Start(BootstrapConfig config)
    {
        _config = config;
        _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Title));
    }

    public void BeginPlay(string displayName = "")
    {
        _displayName = displayName ?? "";
        _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Connecting));
        _networkClient.Connect(_config.WebSocketUrl);
    }

    public void LeaveMatchmaking()
    {
        _sendQueue.Enqueue(MessageType.MatchmakingLeave, new MatchmakingLeave());
        _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Title));
    }

    public void Rematch()
    {
        // 再マッチは接続の張り直し（既存接続を再利用しない。§3.1）。
        _networkClient.Disconnect();
        _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Connecting));
        _networkClient.Connect(_config.WebSocketUrl);
    }

    public void BackToTitle()
    {
        _networkClient.Disconnect();
        _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Title));
    }

    public void Dispose()
    {
        _networkClient.OnReceiveRaw -= _dispatcher.HandleRaw;
        _networkClient.OnConnectionChanged -= HandleConnectionChanged;
        _inputSource.OnCharKey -= HandleCharKey;
        _dispatcher.OnActionApplied -= HandleActionApplied;
        _storeSubscription.Dispose();
    }

    private void HandleConnectionChanged(ConnectionState state, string? error)
    {
        _store.Apply(new LocalConnectionChangedAction(state, error));

        if (state == ConnectionState.Connected)
        {
            if (Phase == ClientPhase.Connecting)
            {
                _store.Apply(new LocalLifecycleChangedAction(ClientPhase.Matchmaking));
                _sendQueue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin { DisplayName = _displayName });
            }
            else if (Phase == ClientPhase.Matchmaking)
            {
                // 再接続成功：待機プールから外れているため MatchmakingJoin を再送する（05-dispatcher.md §3.4）。
                _sendQueue.Enqueue(MessageType.MatchmakingJoin, new MatchmakingJoin { DisplayName = _displayName });
            }

            _sendQueue.Flush();
            return;
        }

        if (state is ConnectionState.Reconnecting or ConnectionState.Failed)
        {
            _renderer.OnConnectionTrouble(state.ToString());
        }
    }

    private void HandleCharKey(char c)
    {
        if (Phase != ClientPhase.InMatch)
        {
            // Spectating / Result 等では入力を止める（§3.3）。
            return;
        }

        var result = _typingJudge.PressKey(c);

        switch (result)
        {
            case KeyResult.Correct:
            case KeyResult.Miss:
            case KeyResult.WordCleared:
                _store.Apply(new LocalKeyJudgedAction(result, _typingJudge.CurrentView));
                _renderer.OnKeyFeedback(result);
                break;

            case KeyResult.OrderCleared:
                HandleOrderCleared();
                break;

            case KeyResult.Ignored:
                break;
        }
    }

    private void HandleOrderCleared()
    {
        var report = _typingJudge.BuildReport();
        var customerId = _servingCustomerId ?? "";
        _servingCustomerId = null;

        // ② 先に CurrentOrder を null にしてから ③ 送信する（二重送信を作らない。§3.2）。
        _store.Apply(new LocalOrderClearedAction(customerId));

        if (report is { } r)
        {
            _sendQueue.Enqueue(MessageType.OrderServed, new OrderServed
            {
                CustomerId = r.CustomerId,
                ElapsedMs = r.ElapsedMs,
                MissCount = r.MissCount,
                ClientTimestamp = r.ClientTimestamp,
            });
        }

        _renderer.OnOrderServed(customerId);
        TryBeginNextOrder();
    }

    private void HandleActionApplied(IAction action)
    {
        switch (action)
        {
            case CustomerArrivedAction a:
                _renderer.OnCustomerArrived(a.Customer);
                TryBeginNextOrder();
                break;

            case CustomerLeftAction a:
                _renderer.OnCustomerLeft(a.CustomerId, a.Reason);
                if (_servingCustomerId == a.CustomerId)
                {
                    // 対応中の客が離脱：計測を破棄し OrderServed を送らない（§3.3）。
                    _typingJudge.AbortOrder();
                    _servingCustomerId = null;
                }

                TryBeginNextOrder();
                break;

            case ForcedEliminationWarningAction a:
                _renderer.OnForcedEliminationWarning(a.UntilTick, a.ThresholdPct);
                break;

            case StoreEliminatedAction a:
                _renderer.OnStoreEliminated(a.StoreId, a.Reason, a.FinalRank);
                if (a.StoreId == _store.State.SelfStoreId)
                {
                    _typingJudge.AbortOrder();
                    _servingCustomerId = null;
                }

                break;

            case MatchEndAction a:
                _renderer.OnMatchEnd(a.FinalRank, a.Stats);
                break;

            case PhaseChangeAction a:
                _renderer.OnPhaseChanged(a.Phase);
                break;
        }
    }

    private void HandleStateChanged(ClientState state)
    {
        if (state.Phase == _lastNotifiedPhase)
        {
            return;
        }

        var from = _lastNotifiedPhase;
        _lastNotifiedPhase = state.Phase;

        if (state.Phase == ClientPhase.Spectating)
        {
            // 自店の脱落：入力を止め TypingJudge を Idle に固定する（§3.3）。
            _typingJudge.AbortOrder();
            _servingCustomerId = null;
        }

        _renderer.OnLifecycleChanged(from, state.Phase);
    }

    private void TryBeginNextOrder()
    {
        if (Phase != ClientPhase.InMatch || !_typingJudge.IsIdle)
        {
            return;
        }

        if (_store.State.Queue.Count == 0)
        {
            return;
        }

        var front = _store.State.Queue[0];
        _servingCustomerId = front.View.CustomerId;
        _typingJudge.BeginOrder(front.View.CustomerId, front.View.Words);
        _store.Apply(new LocalOrderBeganAction(front.View.CustomerId, front.View.OrderCount));
    }
}
