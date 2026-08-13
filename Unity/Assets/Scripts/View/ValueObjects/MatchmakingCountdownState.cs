// 仕様書: Unity/docs/.sdd/matchmaking/01-matchmaking-flow.md §5.2, §8.6
// マッチングのカウントダウンを「残り秒数の表示」と「尽きた（＝MatchStart 待ち）」に分類する。
// マッチングの成立判定はしない（サーバー権威。MatchStart が来たことが唯一の真実）。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（Unity のスクリプティングランタイム制約）。

namespace Takoda99.View.ValueObjects
{
    /// <summary>マッチング画面の Timer / MatchingComplete の表示区分。</summary>
    public readonly struct MatchmakingCountdownState
    {
        /// <summary>残り秒数を表示すべきか。</summary>
        public bool IsCountingDown { get; }

        /// <summary>
        /// カウントダウンが尽きたか。<c>MatchStart</c> が届くまでの待ち時間にあたる。
        /// <b>これは「マッチングが成立した」ことの根拠にはならない</b>（表示上の目安にすぎない）。
        /// </summary>
        public bool IsComplete { get; }

        /// <summary>表示する残り秒数（0以上）。<see cref="IsCountingDown"/> が false のときは 0。</summary>
        public int RemainingSeconds { get; }

        public MatchmakingCountdownState(bool IsCountingDown, bool IsComplete, int RemainingSeconds)
        {
            this.IsCountingDown = IsCountingDown;
            this.IsComplete = IsComplete;
            this.RemainingSeconds = RemainingSeconds;
        }

        /// <summary>
        /// サーバーの残り時間と、そこから引いたローカル締切までの残りから区分を決める。
        /// </summary>
        /// <param name="countdownMs">
        /// 直近の <c>MatchmakingStatus.countdownMs</c>。キーごと欠落している間は null（§3・§5.2）。
        /// </param>
        /// <param name="remainingToDeadlineMs">
        /// 直近の <paramref name="countdownMs"/> を受け取った時刻から引いたローカル締切までの残り(ms)。
        /// 締切が無ければ null。<b>カウントダウンが尽きてもサーバーは最後の値を送り直さない</b>ため、
        /// 「尽きた」の判定はサーバー値ではなくこちらで行う。
        /// </param>
        public static MatchmakingCountdownState From(int? countdownMs, long? remainingToDeadlineMs)
        {
            // 尽きたかを先に見る。countdownMs には最後に届いた値（1000 等）が残ったままなので、
            // そちらを先に見ると「のこり1秒」で止まった画面になる。
            if (remainingToDeadlineMs.HasValue && remainingToDeadlineMs.Value <= 0L)
            {
                return new MatchmakingCountdownState(IsCountingDown: false, IsComplete: true, RemainingSeconds: 0);
            }

            // 欠落は「カウントダウンしていない」。締切が残っていても、まだ時間があるうちに
            // 消えたのなら中断（minPlayers 割れ・§5.2）であって完了ではない。
            if (!countdownMs.HasValue)
            {
                return new MatchmakingCountdownState(IsCountingDown: false, IsComplete: false, RemainingSeconds: 0);
            }

            var seconds = CeilToSeconds(countdownMs.Value);
            return new MatchmakingCountdownState(IsCountingDown: true, IsComplete: false, RemainingSeconds: seconds);
        }

        /// <summary>ms を秒へ切り上げる。負値は 0 にする。</summary>
        private static int CeilToSeconds(int milliseconds)
        {
            if (milliseconds <= 0)
            {
                return 0;
            }

            return (milliseconds + 999) / 1000;
        }
    }
}
