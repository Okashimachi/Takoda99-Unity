using System;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.Lifecycle;

/// <summary>描画への離散イベント通知。実体は Unity 側（第3章 §3）。</summary>
public interface IRenderer
{
    void OnCustomerArrived(CustomerView customer);
    void OnCustomerLeft(string customerId, LeaveReason reason);
    void OnKeyFeedback(KeyResult result);                    // 正打/ミスの即時演出
    void OnOrderServed(string customerId);                   // 提供演出
    void OnPhaseChanged(Phase phase);
    void OnForcedEliminationWarning(int untilTick, double thresholdPct);
    void OnStoreEliminated(string storeId, EliminationReason reason, int finalRank);
    void OnMatchEnd(int finalRank, MatchStats stats);
    void OnLifecycleChanged(ClientPhase from, ClientPhase to);
    void OnConnectionTrouble(string kind);
}

/// <summary>入力の抽象。実体は Unity 側（Input System で文字キーのみへ正規化）。</summary>
public interface IInputSource
{
    event Action<char> OnCharKey;
}

/// <summary>接続先・バージョンゲートのブートストラップ設定（06-match-client-controller.md §3.5）。</summary>
public sealed class BootstrapConfig
{
    public string WebSocketUrl { get; init; } = "";  // コード直書き禁止。Unity 側の設定から渡す
    public string ProtoVersion { get; init; } = "";  // ビルド時定数
    public bool DevMode { get; init; }               // デバッグパネル・モック導線の有効化
}

public interface IMatchClientController
{
    ClientPhase Phase { get; }

    /// <summary>ブートストラップ設定を受けて開始する（Boot → Title）。</summary>
    void Start(BootstrapConfig config);

    /// <summary>プレイ開始操作（Title → Connecting）。</summary>
    void BeginPlay();

    /// <summary>キュー離脱操作（Matchmaking → Title）。</summary>
    void LeaveMatchmaking();

    /// <summary>「もう一度」操作（Result → Connecting・接続を張り直す）。</summary>
    void Rematch();

    /// <summary>タイトルへ戻る操作（Result → Title）。</summary>
    void BackToTitle();

    void Dispose();
}
