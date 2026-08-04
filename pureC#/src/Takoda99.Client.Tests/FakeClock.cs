using Takoda99.Client;

namespace Takoda99.Client.Tests;

/// <summary>テスト用の手動進行クロック。実時間に依存するテストを避ける（03-typing-judge.md §5）。</summary>
public sealed class FakeClock : IClock
{
    public long MonotonicMs { get; set; }

    public long WallClockUnixMs { get; set; }

    public void Advance(long ms) => MonotonicMs += ms;
}
