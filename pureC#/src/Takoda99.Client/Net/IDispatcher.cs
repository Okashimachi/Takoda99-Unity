using System;
using Takoda99.Client.State;

namespace Takoda99.Client.Net;

public interface IDispatcher
{
    /// <summary>受信した生 JSON を処理する（デコード → 受理判定 → Action 化 → Store.Apply）。</summary>
    void HandleRaw(string json);

    /// <summary>未知 type を検出したときの通知（開発ビルドでのみ画面表示する）。</summary>
    event Action<string, string> OnUnknownMessage;  // (type, reason)

    /// <summary>受理されず破棄されたときの通知（phase 外・デコード失敗）。</summary>
    event Action<string, string> OnMessageDropped;  // (type, reason)

    /// <summary>
    /// Store.Apply が成功した直後に、適用した Action をそのまま通知する
    /// （05-dispatcher.md 追記分。06-match-client-controller が唯一の購読者）。
    /// </summary>
    event Action<IAction> OnActionApplied;
}
