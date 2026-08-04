// 仕様書: Unity/docs/.sdd/value-objects/02-customer-mood-state.md
// 我慢ゲージ残量（クライアント推定）からムード4区分へ分類する。離脱の確定はしない（CustomerLeft がサーバー権威）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（StoreVisualState.cs 冒頭の注記を参照）。

namespace Takoda99.View.ValueObjects
{
    public enum CustomerMood { Calm, Irritated, Angry, TurnedAway }

    /// <summary>
    /// 客の我慢ゲージ残量を「普通・いらだち・怒り・退転」に分類した表示用状態。
    /// 我慢ゲージの減算そのものは <c>PatienceTimer</c> の責務で、ここは分類だけを行う。
    /// </summary>
    public readonly struct CustomerMoodState
    {
        public string CustomerId { get; }

        public CustomerMood Mood { get; }

        public CustomerMoodState(string CustomerId, CustomerMood Mood)
        {
            this.CustomerId = CustomerId;
            this.Mood = Mood;
        }

        /// <summary>
        /// 表示用の残量推定。
        /// <c>PatienceMaxMs - (MatchState.ElapsedMs - CustomerState.ArrivedAtElapsedMs)</c>。
        /// </summary>
        /// <remarks>
        /// <b>この値はサーバーから配信されない</b>（SV-03）。終盤短縮が適用されるとサーバー実態とズレるため、
        /// この値で離脱を確定させてはならない。離脱の確定は <c>CustomerLeft</c> の受信のみ。
        /// </remarks>
        public static long PatienceLeftMsDisplay(int patienceMaxMs, long arrivedAtElapsedMs, long matchElapsedMs)
        {
            return patienceMaxMs - (matchElapsedMs - arrivedAtElapsedMs);
        }

        /// <summary>
        /// <c>CustomerState</c> の値と現在の <c>MatchState.ElapsedMs</c> からムードを導出する。
        /// pureC# 側の型を Unity から参照する方法が未確定のため、入力は素の値で受ける。
        /// </summary>
        public static CustomerMoodState From(
            string customerId,
            int patienceMaxMs,
            long arrivedAtElapsedMs,
            long matchElapsedMs,
            CustomerMoodThresholds thresholds)
        {
            var leftMs = PatienceLeftMsDisplay(patienceMaxMs, arrivedAtElapsedMs, matchElapsedMs);
            if (leftMs <= 0 || patienceMaxMs <= 0)
            {
                // 表示上ゲージが尽きた瞬間から、CustomerLeft を受信して行列から実除去されるまでの演出状態。
                return new CustomerMoodState(customerId, CustomerMood.TurnedAway);
            }

            var ratio = (double)leftMs / patienceMaxMs;
            if (ratio >= thresholds.Irritated)
            {
                return new CustomerMoodState(customerId, CustomerMood.Calm);
            }

            return new CustomerMoodState(
                customerId,
                ratio >= thresholds.Angry ? CustomerMood.Irritated : CustomerMood.Angry);
        }
    }

    /// <summary>
    /// ムード分類の閾値（残量比 0..1）。実値は未確定（仕様書 §7）のため <c>Default</c> は仮置き。
    /// </summary>
    public readonly struct CustomerMoodThresholds
    {
        public double Irritated { get; }

        public double Angry { get; }

        public CustomerMoodThresholds(double Irritated, double Angry)
        {
            this.Irritated = Irritated;
            this.Angry = Angry;
        }

        /// <summary>仮置きの閾値（残 2/3 以上で普通、1/3 以上でいらだち、それ未満で怒り）。</summary>
        public static CustomerMoodThresholds Default =>
            new CustomerMoodThresholds(Irritated: 2d / 3d, Angry: 1d / 3d);
    }
}
