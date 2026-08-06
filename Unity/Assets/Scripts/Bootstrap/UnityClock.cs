// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §4
// IClock の Unity 実体。Time.realtimeSinceStartupAsDouble（単調）/ DateTimeOffset（壁時計）で実装する
// （pureC#/src/Takoda99.Client/IClock.cs のコメント通り）。

using System;
using Takoda99.Client;
using UnityEngine;

namespace Takoda99.Bootstrap
{
    /// <summary><see cref="IClock"/> の Unity 実体。</summary>
    public sealed class UnityClock : IClock
    {
        public long MonotonicMs => (long)(Time.realtimeSinceStartupAsDouble * 1000d);

        public long WallClockUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
