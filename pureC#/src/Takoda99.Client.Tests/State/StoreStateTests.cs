// 仕様書: pureC#/docs/.sdd/value-objects/02-store-state.md §8 テスト観点
// 本選（Proto v0.8.0）で信用・相対評価・星が廃止されたため、期待値を Score ベースへ書き換えている。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.State
{
    public class StoreStateTests
    {
        private static List<StoreSummary> ThreeStores() => new List<StoreSummary>
        {
            TestMessages.Summary("store-01", rank: 50, score: 300),
            TestMessages.Summary("store-02", rank: 20, score: 800),
            TestMessages.Summary("store-03", rank: 90, score: 100),
        };

        [Fact]
        public void MatchStartで自店のStoreStateが自店サマリーから初期化される()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-02", stores: ThreeStores());

            var self = StoreState.FromMatchStart(message);

            Assert.Equal("store-02", self.StoreId);
            Assert.Equal(800, self.Score);
            Assert.Equal(20, self.Rank);
            Assert.True(self.Alive);
            Assert.Empty(self.StoreQueue);
        }

        [Fact]
        public void EvaluationUpdateは自店のStoreStateのみを更新し全店サマリーを書き換えない()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-01", stores: ThreeStores());
            var self = StoreState.FromMatchStart(message);
            var summaries = StoreSummaryState.FromAll(message.Stores);

            self = self.Apply(new EvaluationUpdate { Score = 1_250, Rank = 7, AliveCount = 80 });

            Assert.Equal(1_250, self.Score);
            Assert.Equal(7, self.Rank);
            // 全店サマリーは自店専用メッセージでは変化しない。
            Assert.Equal(300, summaries[0].Score);
            Assert.Equal(50, summaries[0].Rank);
        }

        /// <summary>スコアは累積の絶対値。序盤はミスが先行して負値になり得るので、クランプしない。</summary>
        [Fact]
        public void EvaluationUpdateの負のScoreはそのまま保持される()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"));

            self = self.Apply(new EvaluationUpdate { Score = -40, Rank = 95, AliveCount = 99 });

            Assert.Equal(-40, self.Score);
        }

        [Fact]
        public void EvaluationUpdateは差分を累積せず最後の値で置換する()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"));

            self = self.Apply(new EvaluationUpdate { Score = 100, Rank = 10, AliveCount = 99 });
            self = self.Apply(new EvaluationUpdate { Score = 250, Rank = 6, AliveCount = 98 });

            Assert.Equal(250, self.Score);
            Assert.Equal(6, self.Rank);
        }

        [Fact]
        public void StoreEliminatedは自店と他店の正しい対象に適用される()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-01", stores: ThreeStores());
            var self = StoreState.FromMatchStart(message);
            var summaries = StoreSummaryState.FromAll(message.Stores);

            var otherEliminated = TestMessages.Eliminated("store-03", finalRank: 90);
            summaries = StoreSummaryState.ApplyEliminated(summaries, otherEliminated);
            self = self.Apply(otherEliminated);

            Assert.False(summaries[2].Alive);
            Assert.Equal(90, summaries[2].FinalRank);
            Assert.True(summaries[0].Alive);
            Assert.Null(summaries[0].FinalRank);
            Assert.True(self.Alive);

            var selfEliminated = TestMessages.Eliminated("store-01", finalRank: 55);
            summaries = StoreSummaryState.ApplyEliminated(summaries, selfEliminated);
            self = self.Apply(selfEliminated);

            Assert.False(summaries[0].Alive);
            Assert.False(self.Alive);
        }

        [Fact]
        public void 店舗数がmaxStoresと一致しなくても破綻しない()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-01", maxStores: 99, stores: ThreeStores());

            var summaries = StoreSummaryState.FromAll(message.Stores);

            Assert.Equal(3, summaries.Count);
        }

        [Fact]
        public void 自店がstoresに含まれない場合でも初期化できる()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-99", stores: ThreeStores());

            var self = StoreState.FromMatchStart(message);

            Assert.Equal("store-99", self.StoreId);
            Assert.Equal(0, self.Rank);
            Assert.True(self.Alive);
        }

        [Fact]
        public void 行列は到着順に積まれ提供完了で取り除かれる()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1")
                .WithCustomerEnqueued("c2")
                .WithCustomerEnqueued("c3");

            Assert.Equal(new[] { "c1", "c2", "c3" }, self.StoreQueue);
            Assert.Equal("c1", self.CurrentCustomerId);

            self = self.WithCustomerDequeued("c1");

            Assert.Equal(new[] { "c2", "c3" }, self.StoreQueue);
            Assert.Equal("c2", self.CurrentCustomerId);
        }

        [Fact]
        public void 行列に存在しない客の除去は無視される()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart())
                .WithCustomerEnqueued("c1");

            self = self.WithCustomerDequeued("unknown");

            Assert.Equal(new[] { "c1" }, self.StoreQueue);
        }

        [Fact]
        public void 脱落してもクライアント側で行列を強制クリアしない()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1")
                .Apply(TestMessages.Eliminated("store-01", finalRank: 55));

            Assert.False(self.Alive);
            Assert.Equal(new[] { "c1" }, self.StoreQueue);
        }
    }
}
