using System;

namespace Takoda99.Client.Testing;

/// <summary>シナリオJSONの形式が不正なときにスローされる（07-scenario-player.md §5）。</summary>
public sealed class ScenarioFormatException : Exception
{
    public ScenarioFormatException(string message) : base(message)
    {
    }
}
