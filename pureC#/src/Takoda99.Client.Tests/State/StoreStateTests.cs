// 仕様書: pureC#/docs/.sdd/value-objects/02-store-state.md §8 テスト観点

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
            TestMessages.Summary("store-01", evalNormalized: 0.5, rank: 50),
            TestMessages.Summary("store-02", evalNormalized: 0.8, rank: 20),
            TestMessages.Summary("store-03", evalNormalized: 0.1, rank: 90),
        };

        [Fact]
        public void MatchStartで自店のStoreStateがinitialLifeと自店サマリーから初期化される()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-02", initialLife: 3, stores: ThreeStores());

            var self = StoreState.FromMatchStart(message);

            Assert.Equal("store-02", self.StoreId);
            Assert.Equal(3, self.CreditLife);
            Assert.Equal(0.8, self.EvalNormalized);
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

            self = self.Apply(new EvaluationUpdate { EvalRaw = 12.5, Normalized = 0.9, Rank = 7, AliveCount = 80 });

            Assert.Equal(0.9, self.EvalNormalized);
            Assert.Equal(12.5, self.EvalRaw);
            Assert.Equal(7, self.Rank);
            // 全店サマリーは StoreListUpdate 以外では変化しない
            Assert.Equal(0.5, summaries[0].EvalNormalized);
            Assert.Equal(50, summaries[0].Rank);
        }

        [Fact]
        public void StoreListUpdateは全店サマリーを置換するが自店のStoreStateを巻き戻さない()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-01", stores: ThreeStores());
            var self = StoreState.FromMatchStart(message)
                .Apply(new EvaluationUpdate { EvalRaw = 20, Normalized = 0.95, Rank = 3, AliveCount = 70 });

            var update = new StoreListUpdate
            {
                AliveCount = 70,
                Stores = new List<StoreSummary>
                {
                    TestMessages.Summary("store-01", evalNormalized: 0.4, rank: 60),
                    TestMessages.Summary("store-02", evalNormalized: 0.7, rank: 30),
                },
            };
            var summaries = StoreSummaryState.FromAll(update.Stores);

            Assert.Equal(2, summaries.Count);
            Assert.Equal(0.4, summaries[0].EvalNormalized);
            // 自店の StoreState はより新しい EvaluationUpdate の値を保つ
            Assert.Equal(0.95, self.EvalNormalized);
            Assert.Equal(3, self.Rank);
        }

        [Fact]
        public void CreditUpdateはlifeを確定値として使いdeltaで加減算しない()
        {
            var self = StoreState.FromMatchStart(TestMessages.MatchStart(initialLife: 3));

            self = self.Apply(new CreditUpdate { Life = 1, Delta = -1, Reason = CreditReason.CustomerLeft });

            Assert.Equal(1, self.CreditLife);
        }

        [Fact]
        public void StoreEliminatedは自店と他店の正しい対象に適用される()
        {
            var message = TestMessages.MatchStart(selfStoreId: "store-01", stores: ThreeStores());
            var self = StoreState.FromMatchStart(message);
            var summaries = StoreSummaryState.FromAll(message.Stores);

            var otherEliminated = new StoreEliminated
            {
                StoreId = "store-03",
                Reason = EliminationReason.Cull,
                FinalRank = 90,
            };
            summaries = StoreSummaryState.ApplyEliminated(summaries, otherEliminated);
            self = self.Apply(otherEliminated);

            Assert.False(summaries[2].Alive);
            Assert.True(summaries[0].Alive);
            Assert.True(self.Alive);

            var selfEliminated = new StoreEliminated
            {
                StoreId = "store-01",
                Reason = EliminationReason.SelfCollapse,
                FinalRank = 55,
            };
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
            Assert.True(self.Alive);
        }

        [Fact]
        public void 行列は到着順に積まれ離脱と提供完了で取り除かれる()
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
                .Apply(new StoreEliminated { StoreId = "store-01", Reason = EliminationReason.SelfCollapse });

            Assert.False(self.Alive);
            Assert.Equal(new[] { "c1" }, self.StoreQueue);
        }
    }
}
