// 仕様書: Unity/docs/.sdd/value-objects/14-cull-final-countdown-state.md
// 淘汰直前（残り5秒）に画面中央へ出す大きな数字の表示状態。受信値＋ローカル経過から作る純関数。
//
// 「誰に出すか」はここで決めない。CullAlertState が出した段階（Tier）をそのまま受け取る
// （淘汰圏内＝SelfAtRisk はサーバー権威、ぎりぎり圏外＝下位パネルの表示範囲。docs/rules/01）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない。

using System;

namespace Takoda99.View.ValueObjects
{
    /// <summary>
    /// 中央カウントダウンが描く値。時刻を自分で取得せず残り時間を引数で受ける（テスト可能にするため）。
    /// </summary>
    public readonly struct CullFinalCountdownState : IEquatable<CullFinalCountdownState>
    {
        /// <summary>中央カウントダウンを出し始める残り時間（ms）。これを切ってから出す。</summary>
        public const int DefaultWindowMs = 5_000;

        /// <summary>出すか。段階が None・窓の外・0秒以下ならすべて false。</summary>
        public bool Visible { get; }

        /// <summary>表示する秒（切り上げ）。<see cref="Visible"/> が false なら 0。</summary>
        public int Seconds { get; }

        /// <summary>"5" 等。構築時に1回だけ ToString する（毎フレーム作り直さない）。</summary>
        public string Text { get; }

        /// <summary>
        /// いま表示している数字が出てからの進み具合 0..1（1で次の数字へ変わる）。
        /// 出現アニメーション（拡大・フェード）はこの値だけから引く。
        /// </summary>
        public float SecondProgress { get; }

        private CullFinalCountdownState(bool visible, int seconds, string text, float secondProgress)
        {
            Visible = visible;
            Seconds = seconds;
            Text = text;
            SecondProgress = secondProgress;
        }

        public static CullFinalCountdownState Hidden
            => new CullFinalCountdownState(false, 0, string.Empty, 0f);

        public static CullFinalCountdownState From(CullAlertTier tier, long remainingMs)
            => From(tier, remainingMs, DefaultWindowMs);

        /// <param name="tier">
        /// <see cref="CullAlertState"/> が出した段階。None なら出さない。
        /// **これを順位と CutLineRank の比較で作り直さないこと**（docs/rules/01）。
        /// </param>
        /// <param name="remainingMs">次の足切りまでの残り（ms）。負は 0 として扱う。</param>
        /// <param name="windowMs">出し始める残り時間（ms）。0 以下なら出さない。</param>
        public static CullFinalCountdownState From(CullAlertTier tier, long remainingMs, int windowMs)
        {
            // 安全圏・脱落後・未受信は Tier が None で届く。窓の中でも出さない。
            if (tier == CullAlertTier.None || windowMs <= 0)
            {
                return Hidden;
            }

            if (remainingMs <= 0 || remainingMs > windowMs)
            {
                // 0秒は「もう淘汰の瞬間」。数字を 0 で残さず消す（結果の演出へ譲る）。
                return Hidden;
            }

            // 切り上げ。残り 4001ms → 5、残り 4000ms → 4（CullCountdownState と同じ規則）。
            var seconds = (int)((remainingMs + 999) / 1000);

            // いま出している数字が出てからの経過（ms）。4800ms 残り → 5 が出て 200ms 経過。
            var elapsedInSecond = (1000 - remainingMs % 1000) % 1000;

            return new CullFinalCountdownState(
                true,
                seconds,
                seconds.ToString(),
                elapsedInSecond / 1000f);
        }

        /// <summary>
        /// 文字列の更新可否だけを判定する。**<see cref="SecondProgress"/> は比較に含めない**
        /// （毎フレーム変わるため。アニメーションは別途毎フレーム適用してよい）。
        /// </summary>
        public bool Equals(CullFinalCountdownState other)
        {
            return Visible == other.Visible
                && Seconds == other.Seconds
                && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is CullFinalCountdownState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Visible ? 1 : 0;
                hash = (hash * 397) ^ Seconds;
                return hash;
            }
        }
    }
}
