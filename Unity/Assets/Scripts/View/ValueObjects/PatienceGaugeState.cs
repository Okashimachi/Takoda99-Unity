// 仕様書: Unity/docs/.sdd/value-objects/07-patience-gauge-state.md
// 我慢ゲージの残量比と色段階（3段階固定）を導出する。色そのものは持たない（演出は PatienceGaugePalette の担当）。
// 我慢切れの確定はしない（CustomerLeft がサーバー権威）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>我慢ゲージの色段階。3段階で固定し、増減させない。</summary>
    public enum PatienceGaugeStage
    {
        /// <summary>余裕（残量比が Caution 閾値以上）。</summary>
        Safe = 0,

        /// <summary>注意（残量比が Danger 閾値以上、Caution 閾値未満）。</summary>
        Caution = 1,

        /// <summary>危険（残量比が Danger 閾値未満）。</summary>
        Danger = 2,
    }

    /// <summary>
    /// 我慢ゲージのバーを描くのに必要な量。残量比と、そこから決まる色段階・左端の位置だけを持つ。
    /// </summary>
    /// <remarks>
    /// 客の表情を決める <see cref="CustomerMoodState"/> とは別の表示区分で、統合しない
    /// （時刻の起点も閾値も異なる。仕様書 §1）。
    /// </remarks>
    public readonly struct PatienceGaugeState
    {
        /// <summary>残量比 0..1（クランプ済み）。</summary>
        public double RemainingRatio { get; }

        public PatienceGaugeStage Stage { get; }

        public PatienceGaugeState(double RemainingRatio, PatienceGaugeStage Stage)
        {
            this.RemainingRatio = RemainingRatio;
            this.Stage = Stage;
        }

        /// <summary>
        /// バー左端のアンカー位置（0..1）。**右端は固定**で、ここだけを右へ寄せて残量を表す。
        /// </summary>
        public double LeftEdgeAnchorX => 1d - RemainingRatio;

        /// <summary>残り時間と我慢時間の総量から状態を導出する。<paramref name="totalMs"/> が0以下でも0除算しない。</summary>
        public static PatienceGaugeState From(long remainingMs, long totalMs, PatienceGaugeThresholds thresholds)
        {
            if (totalMs <= 0)
            {
                return new PatienceGaugeState(0d, PatienceGaugeStage.Danger);
            }

            var clamped = remainingMs < 0 ? 0 : (remainingMs > totalMs ? totalMs : remainingMs);
            var ratio = (double)clamped / totalMs;

            return new PatienceGaugeState(ratio, StageOf(ratio, thresholds));
        }

        /// <summary>残量比を3段階に分類する。境界値ちょうどは上位側（余裕がある側）に入る。</summary>
        public static PatienceGaugeStage StageOf(double remainingRatio, PatienceGaugeThresholds thresholds)
        {
            if (remainingRatio >= thresholds.Caution)
            {
                return PatienceGaugeStage.Safe;
            }

            return remainingRatio >= thresholds.Danger ? PatienceGaugeStage.Caution : PatienceGaugeStage.Danger;
        }
    }

    /// <summary>段階の境界（残量比 0..1）。実値は未確定（仕様書 §7）のため <c>Default</c> は仮置き。</summary>
    public readonly struct PatienceGaugeThresholds
    {
        public double Caution { get; }

        public double Danger { get; }

        public PatienceGaugeThresholds(double Caution, double Danger)
        {
            this.Caution = Caution;
            this.Danger = Danger;
        }

        /// <summary>残 50% 以上で Safe、25% 以上で Caution、それ未満で Danger。</summary>
        public static PatienceGaugeThresholds Default =>
            new PatienceGaugeThresholds(Caution: 0.5d, Danger: 0.25d);
    }
}
