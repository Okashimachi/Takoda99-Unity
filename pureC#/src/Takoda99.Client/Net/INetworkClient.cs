using System;
using Takoda99.Client.State;

namespace Takoda99.Client.Net;

/// <summary>通信の抽象。実体は Unity 側（WebGLNetworkClient）。</summary>
/// <remarks>
/// WebGL は Thread / 一部 Task に制約があるため、async/await を契約に持ち込まず
/// コールバック／イベントで表現する（第3章 §6-5）。
/// </remarks>
public interface INetworkClient
{
    ConnectionState State { get; }

    void Connect(string url);

    void Disconnect();

    /// <summary>Envelope に包んで送る。実際の送信順序は ISendQueue が保証する。</summary>
    void Send(string type, object payload);

    event Action<string> OnReceiveRaw;                        // 生 JSON テキスト
    event Action<ConnectionState, string?> OnConnectionChanged;
}
