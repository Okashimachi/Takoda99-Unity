namespace Takoda99.Client;

/// <summary>
/// 時刻の供給源。pureC# は UnityEngine.Time を参照できないため抽象化する。
/// Unity 側は Time.realtimeSinceStartupAsDouble / DateTimeOffset で実装する。
/// </summary>
public interface IClock
{
    /// <summary>単調増加ミリ秒。elapsedMs の計測に使う（壁時計補正・ポーズを混ぜない）。</summary>
    long MonotonicMs { get; }

    /// <summary>壁時計の Unix epoch ミリ秒。clientTimestamp にのみ使う。</summary>
    long WallClockUnixMs { get; }
}
