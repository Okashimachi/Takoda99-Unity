// 仕様書: pureC#/docs/.sdd/value-objects/03-customer-state.md §8 テスト観点

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.State
{
    public class CustomerStateTests
    {
        [Fact]
        public void CustomerArrivedで行列末尾に追加されArrivedAtElapsedMsが記録される()
        {
            var store = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1");
            var match = MatchState.FromMatchStart(TestMessages.MatchStart(), 0).Tick(12_345);

            var customer = CustomerState.FromCustomerView(TestMessages.CustomerView("c2"), match.ElapsedMs);
            store = store.WithCustomerEnqueued(customer.CustomerId);

            Assert.Equal(12_345, customer.ArrivedAtElapsedMs);
            Assert.Equal(new[] { "c1", "c2" }, store.StoreQueue);
        }

        [Fact]
        public void 残量推定が0になってもCustomerLeftを受信するまで行列に残り続ける()
        {
            var store = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1");
            var customer = CustomerState.FromCustomerView(
                TestMessages.CustomerView("c1", patienceMaxMs: 5_000), arrivedAtElapsedMs: 0);

            // 我慢ゲージの推定残量が尽きた時刻でも、離脱はサーバー権威（CustomerLeft）でしか確定しない
            var estimatedLeftMs = customer.PatienceMaxMs - (10_000 - customer.ArrivedAtElapsedMs);
            Assert.True(estimatedLeftMs <= 0);
            Assert.Equal(new[] { "c1" }, store.StoreQueue);

            store = store.WithCustomerDequeued(new CustomerLeft { CustomerId = "c1", Reason = LeaveReason.Timeout }
                .CustomerId);

            Assert.Empty(store.StoreQueue);
        }

        [Fact]
        public void 提供完了で先頭客が除去され次客が繰り上がる()
        {
            var store = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1")
                .WithCustomerEnqueued("c2");

            store = store.WithCustomerDequeued("c1");

            Assert.Equal("c2", store.CurrentCustomerId);
            Assert.DoesNotContain("c1", store.StoreQueue);
        }

        [Fact]
        public void Words件数とOrderCountの不一致は補正せず検出だけする()
        {
            var consistent = CustomerState.FromCustomerView(
                TestMessages.CustomerView(orderCount: 4, words: new List<string> { "a", "b", "c", "d" }), 0);
            var inconsistent = CustomerState.FromCustomerView(
                TestMessages.CustomerView(orderCount: 4, words: new List<string> { "a", "b" }), 0);

            Assert.True(consistent.HasConsistentWordCount);
            Assert.False(inconsistent.HasConsistentWordCount);
            // 補正しない：受信値がそのまま残る
            Assert.Equal(4, inconsistent.OrderCount);
            Assert.Equal(2, inconsistent.Words.Count);
        }

        [Fact]
        public void 受信値がそのまま保持される()
        {
            var view = TestMessages.CustomerView("c9", orderCount: 6, patienceMaxMs: 18_000,
                attribute: CustomerAttribute.Buzz);

            var customer = CustomerState.FromCustomerView(view, 500);

            Assert.Equal("c9", customer.CustomerId);
            Assert.Equal(CustomerAttribute.Buzz, customer.Attribute);
            Assert.Equal(6, customer.OrderCount);
            Assert.Equal(18_000, customer.PatienceMaxMs);
        }

        [Fact]
        public void PatienceStartedAtServerMsは受信値のまま我慢ゲージの起点として保持される()
        {
            var view = TestMessages.CustomerView("c9", patienceStartedAtServerMs: 4_200);

            var customer = CustomerState.FromCustomerView(view, arrivedAtElapsedMs: 5_000);

            Assert.Equal(4_200, customer.PatienceStartedAtServerMs);
            // ArrivedAtElapsedMs はサーバー時刻とのドリフト検知用で、起点には使わない
            Assert.Equal(5_000, customer.ArrivedAtElapsedMs);
        }
    }
}
