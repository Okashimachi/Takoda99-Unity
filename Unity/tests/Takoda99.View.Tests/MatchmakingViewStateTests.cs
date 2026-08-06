// 仕様書: Unity/docs/.sdd/matchmaking/01-matchmaking-flow.md §10 テスト観点

using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class MatchmakingViewStateTests
    {
        /// <summary>既定は「名前確定済み・接続済み・未開始」。各テストで必要な条件だけを上書きする。</summary>
        private static MatchmakingViewState Make(
            bool connectionFailed = false,
            bool nameDecided = true,
            bool connected = true,
            bool matchStarted = false,
            bool hasReceivedStatus = true,
            int waitingCount = 0,
            int minPlayers = 0,
            int? countdownMs = null)
        {
            return MatchmakingViewState.From(
                connectionFailed, nameDecided, connected, matchStarted,
                hasReceivedStatus, waitingCount, minPlayers, countdownMs);
        }

        [Fact]
        public void 名前未確定の間はNameEntryでWriteNameModalを出す()
        {
            var state = Make(nameDecided: false, connected: false, hasReceivedStatus: false);

            Assert.Equal(MatchmakingScreenState.NameEntry, state.State);
            Assert.Equal(MatchmakingPanel.WriteNameModal, state.Panel);
        }

        [Fact]
        public void 名前確定後接続前はConnectingでWaitingPanelを出す()
        {
            var state = Make(connected: false, hasReceivedStatus: false);

            Assert.Equal(MatchmakingScreenState.Connecting, state.State);
            Assert.Equal(MatchmakingPanel.WaitingPanel, state.Panel);
        }

        [Fact]
        public void 接続直後MatchmakingStatus受信前はJoiningで数値を出さない()
        {
            var state = Make(hasReceivedStatus: false);

            Assert.Equal(MatchmakingScreenState.Joining, state.State);
            Assert.Equal(MatchmakingPanel.WaitingPanel, state.Panel);
            Assert.Equal(0, state.WaitingCount);
            Assert.Equal(0, state.MinPlayers);
        }

        [Fact]
        public void countdownMsが無いStatus受信でWaitingになりCountdownMsはnull()
        {
            var state = Make(waitingCount: 12, minPlayers: 20);

            Assert.Equal(MatchmakingScreenState.Waiting, state.State);
            Assert.Equal(MatchmakingPanel.MatchingPanel, state.Panel);
            Assert.Null(state.CountdownMs);
            Assert.Equal(12, state.WaitingCount);
            Assert.Equal(20, state.MinPlayers);
        }

        [Fact]
        public void countdownMsがあるStatus受信でCountingDownになる()
        {
            var state = Make(waitingCount: 20, minPlayers: 20, countdownMs: 15000);

            Assert.Equal(MatchmakingScreenState.CountingDown, state.State);
            Assert.Equal(MatchmakingPanel.MatchingPanel, state.Panel);
            Assert.Equal(15000, state.CountdownMs);
        }

        [Fact]
        public void カウントダウン中断でCountingDownからWaitingへ戻ってもパネルは変わらない()
        {
            var counting = Make(waitingCount: 20, minPlayers: 20, countdownMs: 15000);
            Assert.Equal(MatchmakingScreenState.CountingDown, counting.State);

            var backToWaiting = Make(waitingCount: 18, minPlayers: 20, countdownMs: null);

            Assert.Equal(MatchmakingScreenState.Waiting, backToWaiting.State);
            Assert.Null(backToWaiting.CountdownMs);

            // 3パネルの遷移は不可逆（§8.4）。カウントダウンが中断しても MatchingPanel のまま。
            Assert.Equal(MatchmakingPanel.MatchingPanel, counting.Panel);
            Assert.Equal(MatchmakingPanel.MatchingPanel, backToWaiting.Panel);
        }

        [Fact]
        public void MatchStart受信でStartingになりどのパネルも出さない()
        {
            var state = Make(matchStarted: true, waitingCount: 20, minPlayers: 20);

            Assert.Equal(MatchmakingScreenState.Starting, state.State);
            Assert.Equal(MatchmakingPanel.None, state.Panel);
        }

        [Fact]
        public void Starting以降はMatchmakingStatusを受けても巻き戻らない()
        {
            var state = Make(matchStarted: true, waitingCount: 20, minPlayers: 20, countdownMs: 3000);

            Assert.Equal(MatchmakingScreenState.Starting, state.State);
        }

        [Fact]
        public void 接続失敗はRejectedになりWaitingPanelに文言を出す()
        {
            var state = Make(connectionFailed: true, connected: false, hasReceivedStatus: false);

            Assert.Equal(MatchmakingScreenState.Rejected, state.State);

            // 空画面にしない。「Decide を押したのに無反応」に見えるのを避ける（§8.2）。
            Assert.Equal(MatchmakingPanel.WaitingPanel, state.Panel);
        }

        [Fact]
        public void 名前未確定でも接続拒否はRejectedが優先される()
        {
            var state = Make(connectionFailed: true, nameDecided: false, connected: false, hasReceivedStatus: false);

            Assert.Equal(MatchmakingScreenState.Rejected, state.State);
        }

        [Fact]
        public void パネルの進行は名前確定と状態受信が戻らない限り単調である()
        {
            // NameEntry → WaitingPanel → MatchingPanel の順に進み、逆行しないことを通しで確認する。
            var nameEntry = Make(nameDecided: false, connected: false, hasReceivedStatus: false);
            var connecting = Make(connected: false, hasReceivedStatus: false);
            var joining = Make(hasReceivedStatus: false);
            var waiting = Make(waitingCount: 5, minPlayers: 20);

            Assert.Equal(MatchmakingPanel.WriteNameModal, nameEntry.Panel);
            Assert.Equal(MatchmakingPanel.WaitingPanel, connecting.Panel);
            Assert.Equal(MatchmakingPanel.WaitingPanel, joining.Panel);
            Assert.Equal(MatchmakingPanel.MatchingPanel, waiting.Panel);
        }
    }
}
