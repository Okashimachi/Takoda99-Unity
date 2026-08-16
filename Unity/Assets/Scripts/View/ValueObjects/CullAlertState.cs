// 仕様書: Unity/docs/.sdd/ranking-view/02-cull-countdown-panel.md §5
// 淘汰アラートの段階と強さ。受信値＋ローカル経過から表示値を作る純関数。
//
// 順位と CutLineRank を比較しない（docs/rules/01・02 §1）。
// 「淘汰圏内か」は SelfAtRisk（サーバー権威）、「ぎりぎり圏外か」は下位パネルの表示範囲に
// 自店が入っているか（value-objects/12 §4.2 の AtRisk と同じ根拠）で決める。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない。

using System;
using Takoda99.Client.State;

namespace Takoda99.View.ValueObjects
{
    /// <summary>淘汰アラートの段階。色と強さはこの段階から View が引く。</summary>
    public enum CullAlertTier
    {
        /// <summary>出さない（安全圏・未受信・脱落後・秒読みが窓の外）。</summary>
        None,

        /// <summary>ぎりぎり圏外。淡い暖色で軽く出す。</summary>
        Caution,

        /// <summary>淘汰圏内（SelfAtRisk）。赤で強めに出す。</summary>
        Danger,
    }

    /// <summary>
    /// 画面端アラートが描く値。時刻を自分で取得せず <c>nowLocalMs</c> を引数で受ける（テスト可能にするため）。
    /// </summary>
    public readonly struct CullAlertState : IEquatable<CullAlertState>
    {
        /// <summary>アラートを出し始める残り時間（ms）。これを切ってから出す。</summary>
        public const int DefaultAlertWindowMs = 10_000;

        public CullAlertTier Tier { get; }

        /// <summary>窓の中での進み具合 0..1。残りが少ないほど 1 に近づく。Tier が None なら 0。</summary>
        public float Progress { get; }

        private CullAlertState(CullAlertTier tier, float progress)
        {
            Tier = tier;
            Progress = progress;
        }

        public static CullAlertState None => new CullAlertState(CullAlertTier.None, 0f);

        public static CullAlertState From(
            CullWarning warning,
            long nowLocalMs,
            bool selfAlive,
            bool selfInBottomRange)
            => From(warning, nowLocalMs, selfAlive, selfInBottomRange, DefaultAlertWindowMs);

        /// <param name="selfAlive">自店が生存中か。脱落したら演出を完全に止めるため false を渡す。</param>
        /// <param name="selfInBottomRange">自店が下位パネルの表示範囲に入っているか（ぎりぎり圏外の根拠）。</param>
        public static CullAlertState From(
            CullWarning warning,
            long nowLocalMs,
            bool selfAlive,
            bool selfInBottomRange,
            int alertWindowMs)
        {
            // 脱落後はすべての演出を止める。観戦中に足切りが進んでも自分には関係がない。
            if (warning == null || !selfAlive)
            {
                return None;
            }

            var remainingMs = warning.RemainingMsAt(nowLocalMs);

            // 窓の外（まだ余裕がある）では出さない。
            if (alertWindowMs <= 0 || remainingMs > alertWindowMs)
            {
                return None;
            }

            // SelfAtRisk はサーバー権威。これが立っていなければ、下位パネルに映っている間だけ軽く警告する。
            // どちらでもなければ「範囲から外れた」ので完全に消す。
            var tier = warning.SelfAtRisk
                ? CullAlertTier.Danger
                : (selfInBottomRange ? CullAlertTier.Caution : CullAlertTier.None);

            if (tier == CullAlertTier.None)
            {
                return None;
            }

            var progress = 1f - Clamp01((float)remainingMs / alertWindowMs);
            return new CullAlertState(tier, progress);
        }

        public bool Equals(CullAlertState other)
            => Tier == other.Tier && Progress.Equals(other.Progress);

        public override bool Equals(object obj) => obj is CullAlertState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Tier * 397) ^ Progress.GetHashCode();
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
