using System;
using Takoda99.Client.Lifecycle;

namespace Takoda99.Client.Tests.Lifecycle;

public sealed class FakeInputSource : IInputSource
{
    public event Action<char>? OnCharKey;

    public void Press(char c) => OnCharKey?.Invoke(c);

    public void Type(string s)
    {
        foreach (var c in s)
        {
            Press(c);
        }
    }
}
