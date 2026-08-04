// 仕様書: pureC#/docs/.sdd/value-objects/04-order-progress-state.md §6 テスト観点

using Takoda99.Client.State;
using Xunit;

namespace Takoda99.Client.Tests.State
{
    public class OrderProgressStateTests
    {
        private static CustomerState Customer(string customerId = "c1", int orderCount = 4) =>
            CustomerState.FromCustomerView(TestMessages.CustomerView(customerId, orderCount: orderCount), 0);

        [Fact]
        public void 繰り上がり時に新しいOrderProgressStateが初期値で生成される()
        {
            var first = OrderProgressState.Start("store-01", Customer("c1"), 1_000)
                .WithWordTyped()
                .WithMiss();

            var next = OrderProgressState.Start("store-01", Customer("c2", orderCount: 6), 8_000);

            Assert.Equal("c2", next.CustomerId);
            Assert.Equal(6, next.OrderCount);
            Assert.Equal(0, next.TypedWordCount);
            Assert.Equal(0, next.MissCount);
            Assert.Equal(8_000, next.StartedAtMs);
            // 前の進捗は引き継がれない
            Assert.Equal(1, first.TypedWordCount);
        }

        [Fact]
        public void MissCountは1文字ミスごとに加算される()
        {
            var progress = OrderProgressState.Start("store-01", Customer(), 0)
                .WithMiss()
                .WithMiss()
                .WithMiss();

            Assert.Equal(3, progress.MissCount);
        }

        [Fact]
        public void OrderCount到達でIsCompleteになる()
        {
            var progress = OrderProgressState.Start("store-01", Customer(orderCount: 3), 0);

            Assert.False(progress.IsComplete);
            progress = progress.WithWordTyped().WithWordTyped();
            Assert.False(progress.IsComplete);
            progress = progress.WithWordTyped();
            Assert.True(progress.IsComplete);
        }

        [Fact]
        public void TypedWordCountはOrderCountを超えない()
        {
            var progress = OrderProgressState.Start("store-01", Customer(orderCount: 2), 0)
                .WithWordTyped()
                .WithWordTyped()
                .WithWordTyped();

            Assert.Equal(2, progress.TypedWordCount);
        }

        [Fact]
        public void ElapsedMsは対応開始からの差分で0未満にならない()
        {
            var progress = OrderProgressState.Start("store-01", Customer(), 5_000);

            Assert.Equal(3_000, progress.WithElapsed(8_000).ElapsedMs);
            Assert.Equal(0, progress.WithElapsed(4_000).ElapsedMs);
        }

        [Fact]
        public void OrderServedのペイロードが進捗から生成される()
        {
            var progress = OrderProgressState.Start("store-01", Customer("c7", orderCount: 2), 1_000)
                .WithMiss()
                .WithWordTyped()
                .WithWordTyped()
                .WithElapsed(6_000);

            var served = progress.ToOrderServed(clientTimestamp: 1_770_000_000_000);

            Assert.Equal("c7", served.CustomerId);
            Assert.Equal(5_000, served.ElapsedMs);
            Assert.Equal(1, served.MissCount);
            Assert.Equal(1_770_000_000_000, served.ClientTimestamp);
        }
    }
}
