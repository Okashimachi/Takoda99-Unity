using System.Collections.Generic;

namespace Takoda99.Client.Net;

public enum EnvelopeLogDirection { Incoming, Outgoing }

public sealed class EnvelopeLogEntry
{
    public EnvelopeLogEntry(string json, EnvelopeLogDirection direction)
    {
        Json = json;
        Direction = direction;
    }

    public string Json { get; }
    public EnvelopeLogDirection Direction { get; }
}

/// <summary>送受信の生 JSON を時系列1本で保持する（デバッグパネル用）。</summary>
public interface IEnvelopeLog
{
    void RecordIncoming(string json);

    void RecordOutgoing(string json);

    IReadOnlyList<EnvelopeLogEntry> Entries { get; }  // 新しい順
}
