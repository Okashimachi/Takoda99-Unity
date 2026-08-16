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

        /// <summary>
        /// 本選（v0.8.0）では客が逃げない。行列から客が減る契機は**提供完了だけ**であり、
        /// どれだけ時間が経っても勝手に消えない（一度出たお題は必ず打ち切られる）。
        /// </summary>
        [Fact]
        public void 時間が経っても客は行列から消えず提供完了でのみ取り除かれる()
        {
            var store = StoreState.FromMatchStart(TestMessages.MatchStart(selfStoreId: "store-01"))
                .WithCustomerEnqueued("c1");
            var customer = CustomerState.FromCustomerView(TestMessages.CustomerView("c1"), arrivedAtElapsedMs: 0);
            var match = MatchState.FromMatchStart(TestMessages.MatchStart(), 0).Tick(600_000);

            Assert.True(match.ElapsedMs - customer.ArrivedAtElapsedMs > 0);
            Assert.Equal(new[] { "c1" }, store.StoreQueue);

            store = store.WithCustomerDequeued("c1");

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
            var view = TestMessages.CustomerView("c9", orderCount: 6, attribute: CustomerAttribute.Buzz);

            var customer = CustomerState.FromCustomerView(view, 500);

            Assert.Equal("c9", customer.CustomerId);
            // v0.8.0 では属性は見た目の出し分け専用（スコアに影響しない）が、値は保持する。
            Assert.Equal(CustomerAttribute.Buzz, customer.Attribute);
            Assert.Equal(6, customer.OrderCount);
            Assert.Equal(500, customer.ArrivedAtElapsedMs);
        }
    }
}
