using System;
using System.Linq;
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
        // 前の試合の保持値を捨てる。破棄はここと Rematch の2箇所だけ（result/01 §4）。
        _store.Apply(new LocalMatchResetAction());
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
        _store.Apply(new LocalMatchResetAction());
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

            // 接続確立時にしか Flush が呼ばれないため、ここで明示的に flush しないと
            // OrderServed がキューに溜まったまま送信されない（05-dispatcher.md §3.3）。
            // Flush() は呼ぶと _connected を true にしてしまうため、実際に接続中でない
            // （Reconnecting 中に打ち切った等）場合まで誤って「接続済み」扱いにしないよう、
            // 現在の接続状態を確認してから呼ぶ。
            if (_store.State.Connection == ConnectionState.Connected)
            {
                _sendQueue.Flush();
            }
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

            // 本選では客が逃げないため CustomerLeft の割り込みが無い。
            // 一度出たお題は必ず打ち切られる（result/02 §5.1）。

            case ForcedEliminationWarningAction:
                // OnActionApplied は Apply の後に発火するため state は更新済み。
                _renderer.OnCullWarning(_store.State.Cull!);
                break;

            case StoreEliminatedBatchAction a:
            {
                // includesSelf の判定はここで1回だけ行い、描画側へ渡す。
                var includesSelf = a.Entries.Any(e => e.StoreId == _store.State.SelfStoreId);
                _renderer.OnStoreEliminatedBatch(a.StageIndex, a.Entries, includesSelf);
                if (includesSelf)
                {
                    // 本選に残る唯一の中断経路（「客が消える」のではなく「試合から出る」）。
                    _typingJudge.AbortOrder();
                    _servingCustomerId = null;
                }

                break;
            }

            case PersonalResultAction:
                _renderer.OnPersonalResult(_store.State.PersonalResult!);
                break;

            case MatchEndAction:
                _renderer.OnMatchEnd();
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
