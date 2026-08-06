// 仕様書: pureC#/docs/.sdd/value-objects/01-match-state.md §6 テスト観点

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.State
{
    public class MatchStateTests
    {
        [Fact]
        public void MatchStart受信時_AliveCountはalive件数であってmaxStoresではない()
        {
            var stores = new List<StoreSummary>
            {
                TestMessages.Summary("store-01"),
                TestMessages.Summary("store-02"),
                TestMessages.Summary("store-03", alive: false),
            };
            var state = MatchState.FromMatchStart(TestMessages.MatchStart(maxStores: 99, stores: stores), 1_000);

            Assert.Equal(2, state.AliveCount);
            Assert.Equal(99, state.MaxStores);
        }

        [Fact]
        public void MatchStartのphaseがEarly以外でもそのまま反映される()
        {
            var state = MatchState.FromMatchStart(TestMessages.MatchStart(phase: Phase.Late), 0);

            Assert.Equal(Phase.Late, state.Phase);
        }

        [Fact]
        public void DifficultyUpdate未受信の間はHeatLevelが0のまま()
        {
            var state = MatchState.FromMatchStart(TestMessages.MatchStart(), 0);
            Assert.Equal(0, state.HeatLevel);

            state = state.Apply(new PhaseChange { Phase = Phase.Mid });
            Assert.Equal(0, state.HeatLevel);

            state = state.Apply(new DifficultyUpdate { HeatLevel = 3 });
            Assert.Equal(3, state.HeatLevel);
        }

        [Fact]
        public void AliveCountはEvaluationUpdateとStoreListUpdateの双方から更新され後着が勝つ()
        {
            var state = MatchState.FromMatchStart(TestMessages.MatchStart(), 0);

            state = state.Apply(new EvaluationUpdate { AliveCount = 42 });
            Assert.Equal(42, state.AliveCount);

            state = state.Apply(new StoreListUpdate { AliveCount = 40 });
            Assert.Equal(40, state.AliveCount);

            state = state.Apply(new EvaluationUpdate { AliveCount = 39 });
            Assert.Equal(39, state.AliveCount);
        }

        [Fact]
        public void ElapsedMsはMatchStart受信時刻を起点としたローカル計測値()
        {
            var state = MatchState.FromMatchStart(TestMessages.MatchStart(), 10_000);
            Assert.Equal(0, state.ElapsedMs);

            state = state.Tick(25_000);

            Assert.Equal(15_000, state.ElapsedMs);
        }

        [Fact]
        public void 表示用閾値はMatchStartのparamsからそのまま保持される()
        {
            var state = MatchState.FromMatchStart(
                TestMessages.MatchStart(
                    stormThresholdPct: 0.2,
                    finalStageAliveThreshold: 12,
                    finalRushAliveThreshold: 4),
                0);

            Assert.Equal(0.2, state.StormThresholdPct);
            Assert.Equal(12, state.FinalStageAliveThreshold);
            Assert.Equal(4, state.FinalRushAliveThreshold);
        }
    }
}
