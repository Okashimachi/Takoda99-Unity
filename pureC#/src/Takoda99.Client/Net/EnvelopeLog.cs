using System.Collections.Generic;
using System.Linq;

namespace Takoda99.Client.Net;

/// <summary><see cref="IEnvelopeLog"/> の実装。直近 N 件のリングバッファ（提案：200件。05-dispatcher.md §3.6）。</summary>
public sealed class EnvelopeLog : IEnvelopeLog
{
    private readonly int _capacity;
    private readonly LinkedList<EnvelopeLogEntry> _entries = new();

    public EnvelopeLog(int capacity = 200)
    {
        _capacity = capacity;
    }

    public IReadOnlyList<EnvelopeLogEntry> Entries => _entries.ToList();

    public void RecordIncoming(string json) => Record(json, EnvelopeLogDirection.Incoming);

    public void RecordOutgoing(string json) => Record(json, EnvelopeLogDirection.Outgoing);

    private void Record(string json, EnvelopeLogDirection direction)
    {
        _entries.AddFirst(new EnvelopeLogEntry(json, direction));
        if (_entries.Count > _capacity)
        {
            _entries.RemoveLast();
        }
    }
}
