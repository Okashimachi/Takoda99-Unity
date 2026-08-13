// 仕様書: Unity/docs/.sdd/value-objects/09-cull-countdown-state.md
// 足切り秒読みの表示用状態。受信値＋ローカル経過から表示値を作る純関数。
// 予選の PatienceGaugeState と同じ立ち位置（我慢ゲージが消え、秒読みがこの役割を担う）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（Unity のスクリプティングランタイム制約）。

using System;
using Takoda99.Client.State;

namespace Takoda99.View.ValueObjects
{
    /// <summary>
    /// 次の足切りまでの秒読みパネルが描く値。時刻を自分で取得せず <c>nowLocalMs</c> を引数で受ける
    /// （テスト可能にするため）。
    /// </summary>
    public readonly struct CullCountdownState : IEquatable<CullCountdownState>
    {
        /// <summary>SelfAtRisk の警告が最大になるまでの窓（ms）。</summary>
        public const int DefaultAlertWindowMs = 10_000;

        /// <summary>表示する残り秒（切り上げ）。0 以上。</summary>
        public int RemainingSeconds { get; }

        /// <summary>"15" 等。構築時に1回だけ ToString する（毎フレーム作り直さない）。</summary>
        public string RemainingText { get; }

        /// <summary>"3 / 6"。</summary>
        public string StageText { get; }

        /// <summary>"12位以下が脱落"。CutLineRank が 0 以下なら空文字。</summary>
        public string CutLineText { get; }

        /// <summary>自店が対象圏内か（サーバー値そのまま。クライアントで rank と比較しない）。</summary>
        public bool SelfAtRisk { get; }

        /// <summary>警告の強さ 0..1。SelfAtRisk かつ残りが少ないほど 1 に近づく。</summary>
        public float AlertIntensity { get; }

        /// <summary>warning が null なら false。パネルの表示可否（0秒と区別する）。</summary>
        public bool HasWarning { get; }

        private CullCountdownState(
            int remainingSeconds,
            string remainingText,
            string stageText,
            string cutLineText,
            bool selfAtRisk,
            float alertIntensity,
            bool hasWarning)
        {
            RemainingSeconds = remainingSeconds;
            RemainingText = remainingText;
            StageText = stageText;
            CutLineText = cutLineText;
            SelfAtRisk = selfAtRisk;
            AlertIntensity = alertIntensity;
            HasWarning = hasWarning;
        }

        public static CullCountdownState From(CullWarning warning, long nowLocalMs)
            => From(warning, nowLocalMs, DefaultAlertWindowMs);

        public static CullCountdownState From(CullWarning warning, long nowLocalMs, int alertWindowMs)
        {
            // C1: 未受信はパネル非表示の合図。0秒と区別する。
            if (warning == null)
            {
                return new CullCountdownState(0, string.Empty, string.Empty, string.Empty, false, 0f, false);
            }

            // C3: RemainingMsAt が Math.Max(0, …) で吸収済みなので負にならない。
            var remainingMs = warning.RemainingMsAt(nowLocalMs);

            // C2 / C4: 切り上げ。残り 1ms → 1、残り 0ms → 0。
            var remainingSeconds = (remainingMs + 999) / 1000;

            // C5: StageIndex > StageTotal でもクランプしない（異常が見えるほうがよい）。
            var stageText = warning.StageIndex + " / " + warning.StageTotal;

            // C6 / §3.1: 最終ステージの CutLineRank == 2 も特別扱いせず「2位以下が脱落」と出す。
            var cutLineText = warning.CutLineRank > 0 ? warning.CutLineRank + "位以下が脱落" : string.Empty;

            var alertIntensity = 0f;
            if (warning.SelfAtRisk)
            {
                // C7: 残りが少ないほど 1 に近づく。
                alertIntensity = alertWindowMs <= 0 ? 1f : 1f - Clamp01((float)remainingMs / alertWindowMs);
            }

            return new CullCountdownState(
                remainingSeconds,
                remainingSeconds.ToString(),
                stageText,
                cutLineText,
                warning.SelfAtRisk,
                alertIntensity,
                true);
        }

        /// <summary>
        /// 文字列の更新可否だけを判定する。**<see cref="AlertIntensity"/> は比較に含めない**
        /// （毎フレーム変わり得るため。強度は別途毎フレーム適用してよい）。
        /// </summary>
        public bool Equals(CullCountdownState other)
        {
            return HasWarning == other.HasWarning
                && RemainingSeconds == other.RemainingSeconds
                && SelfAtRisk == other.SelfAtRisk
                && string.Equals(RemainingText, other.RemainingText, StringComparison.Ordinal)
                && string.Equals(StageText, other.StageText, StringComparison.Ordinal)
                && string.Equals(CutLineText, other.CutLineText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is CullCountdownState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = HasWarning ? 1 : 0;
                hash = (hash * 397) ^ RemainingSeconds;
                hash = (hash * 397) ^ (SelfAtRisk ? 1 : 0);
                hash = (hash * 397) ^ (StageText != null ? StageText.GetHashCode() : 0);
                hash = (hash * 397) ^ (CutLineText != null ? CutLineText.GetHashCode() : 0);
                return hash;
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
