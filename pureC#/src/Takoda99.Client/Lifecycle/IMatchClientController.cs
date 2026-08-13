using System;
using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;

namespace Takoda99.Client.Lifecycle;

/// <summary>描画への離散イベント通知。実体は Unity 側（第3章 §3）。</summary>
public interface IRenderer
{
    void OnCustomerArrived(CustomerView customer);
    void OnKeyFeedback(KeyResult result);                    // 正打/ミスの即時演出
    void OnOrderServed(string customerId);                   // 提供演出
    void OnPhaseChanged(Phase phase);

    /// <summary>足切りの予告。常時届く（1〜2Hz）。秒読みは CullWarning.RemainingMsAt で補間する。</summary>
    void OnCullWarning(CullWarning warning);

    /// <summary>
    /// 1ステージぶんの一斉脱落。**最大49件が1回で届く。**
    /// 1件ずつ演出せず、まとめて1つの演出に集約すること（音も1回）。
    /// </summary>
    /// <param name="includesSelf">自店が entries に含まれるか。描画側で判定しない。</param>
    void OnStoreEliminatedBatch(int stageIndex, IReadOnlyList<StoreEliminated> entries, bool includesSelf);

    /// <summary>個人成績を受信した。保持は Store が行うので、ここは演出の契機としてだけ使う。</summary>
    void OnPersonalResult(PersonalResultState result);

    /// <summary>試合全体の終了。**引数を持たない**（MatchEnd は空ペイロード）。
    /// 順位別の演出分岐は state.PersonalResult.FinalRank を読んで行う。</summary>
    void OnMatchEnd();

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

    /// <summary>
    /// プレイ開始操作（Title → Connecting）。<paramref name="displayName"/> は接続確立直後の
    /// MatchmakingJoin にそのまま乗せる（Proto v0.4.0 / REQ-01）。空/未指定ならサーバーが
    /// フォールバック名を割り当てる。
    /// </summary>
    void BeginPlay(string displayName = "");

    /// <summary>キュー離脱操作（Matchmaking → Title）。</summary>
    void LeaveMatchmaking();

    /// <summary>「もう一度」操作（Result → Connecting・接続を張り直す）。</summary>
    void Rematch();

    /// <summary>タイトルへ戻る操作（Result → Title）。</summary>
    void BackToTitle();

    void Dispose();
}
