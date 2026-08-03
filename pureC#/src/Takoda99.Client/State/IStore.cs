using System;

namespace Takoda99.Client.State;

public interface IStore
{
    ClientState State { get; }

    void Apply(IAction action);

    /// <summary>購読解除は戻り値の Dispose で行う（イベント解除漏れを防ぐ）。</summary>
    IDisposable Subscribe(Action<ClientState> listener);
}
