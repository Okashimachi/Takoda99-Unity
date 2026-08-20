// 仕様書: Unity/docs/.sdd/ranking-view/02-cull-countdown-panel.md §5

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class CullAlertStateTests
    {
        private const long ReceivedAt = 1_000;

        private static CullWarning Warning(int untilMs = 20_000, bool selfAtRisk = false)
        {
            return new CullWarning
            {
                UntilMs = untilMs,
                ReceivedAtLocalMs = ReceivedAt,
                StageIndex = 3,
                StageTotal = 6,
                CutLineRank = 12,
                SelfAtRisk = selfAtRisk,
                CutStoreIds = new string[0],
            };
        }

        [Fact]
        public void 未受信ならアラートを出さない()
        {
            var state = CullAlertState.From(null, ReceivedAt, selfAlive: true, selfInBottomRange: true);

            Assert.Equal(CullAlertTier.None, state.Tier);
            Assert.Equal(0f, state.Progress);
        }

        [Fact]
        public void 残りが窓より多い間は出さない()
        {
            // 残り 20 秒。窓は 10 秒なのでまだ出さない。
            var state = CullAlertState.From(
                Warning(untilMs: 20_000, selfAtRisk: true), ReceivedAt, true, true);

            Assert.Equal(CullAlertTier.None, state.Tier);
        }

        [Fact]
        public void 残り10秒ちょうどから出はじめる()
        {
            var state = CullAlertState.From(
                Warning(untilMs: 10_000, selfAtRisk: true), ReceivedAt, true, true);

            Assert.Equal(CullAlertTier.Danger, state.Tier);
            Assert.Equal(0f, state.Progress);
        }

        [Fact]
        public void SelfAtRiskならDangerになる()
        {
            var state = CullAlertState.From(
                Warning(untilMs: 5_000, selfAtRisk: true), ReceivedAt, true, selfInBottomRange: false);

            Assert.Equal(CullAlertTier.Danger, state.Tier);
        }

        [Fact]
        public void SelfAtRiskでなくても下位範囲にいればCautionになる()
        {
            var state = CullAlertState.From(
                Warning(untilMs: 5_000, selfAtRisk: false), ReceivedAt, true, selfInBottomRange: true);

            Assert.Equal(CullAlertTier.Caution, state.Tier);
        }

        [Fact]
        public void 範囲から外れたら完全に消える()
        {
            var state = CullAlertState.From(
                Warning(untilMs: 1_000, selfAtRisk: false), ReceivedAt, true, selfInBottomRange: false);

            Assert.Equal(CullAlertTier.None, state.Tier);
            Assert.Equal(0f, state.Progress);
        }

        [Fact]
        public void 脱落したらすべての演出を止める()
        {
            var state = CullAlertState.From(
                Warning(untilMs: 1_000, selfAtRisk: true), ReceivedAt, selfAlive: false, selfInBottomRange: true);

            Assert.Equal(CullAlertTier.None, state.Tier);
            Assert.Equal(0f, state.Progress);
        }

        [Fact]
        public void 残りが少ないほどProgressが1に近づく()
        {
            var half = CullAlertState.From(
                Warning(untilMs: 5_000, selfAtRisk: true), ReceivedAt, true, true);
            var end = CullAlertState.From(
                Warning(untilMs: 0, selfAtRisk: true), ReceivedAt, true, true);

            Assert.Equal(0.5f, half.Progress, 3);
            Assert.Equal(1f, end.Progress, 3);
        }

        [Fact]
        public void Progressは0から1に収まる()
        {
            // 受信から窓を大きく超えて経過しても、RemainingMsAt が 0 で止まるので 1 を超えない。
            var state = CullAlertState.From(
                Warning(untilMs: 1_000, selfAtRisk: true), ReceivedAt + 60_000, true, true);

            Assert.Equal(CullAlertTier.Danger, state.Tier);
            Assert.Equal(1f, state.Progress, 3);
        }

        // ── 「ぎりぎり圏外」の判定根拠（RankingRowsBuilder.IsInBottomRange）─────────

        private static RankingTable Table99()
        {
            var rows = new List<RankingRow>(99);
            for (var i = 1; i <= 99; i++)
            {
                rows.Add(new RankingRow
                {
                    StoreId = $"store-{i:00}",
                    Rank = i,
                    Score = 100 - i,
                    DisplayName = $"店{i:00}",
                    Alive = true,
                });
            }

            return new RankingTable { Rows = rows };
        }

        [Fact]
        public void 生存99なら70位以降が下位範囲に入る()
        {
            var table = Table99();

            Assert.False(RankingRowsBuilder.IsInBottomRange(table, "store-69", aliveCount: 99, count: 30));
            Assert.True(RankingRowsBuilder.IsInBottomRange(table, "store-70", aliveCount: 99, count: 30));
            Assert.True(RankingRowsBuilder.IsInBottomRange(table, "store-99", aliveCount: 99, count: 30));
        }

        [Fact]
        public void 生存20でも上位5位はアラート対象から除外される()
        {
            // 淘汰終盤で生存者が減っても、1位を含む上位5位に「ぎりぎり圏外」警告を出さない
            // （最終ステージ直前の20→10人のような淘汰で優勝候補まで警告が出るのはおかしいため）。
            var table = Table99();

            Assert.False(RankingRowsBuilder.IsInBottomRange(table, "store-01", aliveCount: 20, count: 30));
            Assert.False(RankingRowsBuilder.IsInBottomRange(table, "store-05", aliveCount: 20, count: 30));
            Assert.True(RankingRowsBuilder.IsInBottomRange(table, "store-06", aliveCount: 20, count: 30));
            Assert.True(RankingRowsBuilder.IsInBottomRange(table, "store-35", aliveCount: 20, count: 30));
            Assert.False(RankingRowsBuilder.IsInBottomRange(table, "store-36", aliveCount: 20, count: 30));
        }

        [Fact]
        public void 空のランキングや空IDでも例外にならない()
        {
            Assert.False(RankingRowsBuilder.IsInBottomRange(new RankingTable(), "store-01", 99, 30));
            Assert.False(RankingRowsBuilder.IsInBottomRange(Table99(), "", 99, 30));
            Assert.False(RankingRowsBuilder.IsInBottomRange(null, "store-01", 99, 30));
            Assert.False(RankingRowsBuilder.IsInBottomRange(Table99(), "store-01", 99, 0));
        }

        [Fact]
        public void IsInBottomRangeはBuildBottomと同じ範囲を返す()
        {
            var table = Table99();
            const int aliveCount = 55;
            const int count = 30;

            var rows = RankingRowsBuilder.BuildBottom(table, "", 0, 0, aliveCount, count);
            var expected = new HashSet<string>();
            foreach (var row in rows)
            {
                expected.Add(row.StoreId);
            }

            for (var i = 1; i <= 99; i++)
            {
                var id = $"store-{i:00}";
                Assert.Equal(
                    expected.Contains(id),
                    RankingRowsBuilder.IsInBottomRange(table, id, aliveCount, count));
            }
        }
    }
}
