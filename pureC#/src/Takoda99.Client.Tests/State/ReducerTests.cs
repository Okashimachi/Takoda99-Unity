using System.Collections.Generic;
using System.Linq;
using Takoda99.Client.State;
using Takoda99.Client.Typing;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.State;

public class ReducerTests
{
    private static ClientState Initial() => new();

    [Fact]
    public void MatchStart_CreditLifeがInitialLifeになる()
    {
        var state = Initial();
        var action = new MatchStartAction
        {
            MatchId = "m1",
            SelfStoreId = "s1",
            Params = new GameParametersPublicSubset { InitialLife = 3, MaxStores = 99 },
            MatchPhase = Phase.Early,
            Stores = new List<StoreSummary>(),
        };

        var next = Reducer.Apply(state, action);

        Assert.Equal(3, next.CreditLife);
        Assert.Equal(ClientPhase.InMatch, next.Phase);
        Assert.Equal("m1", next.MatchId);
        Assert.Empty(next.Queue);
    }

    [Fact]
    public void Reducerは純粋_入力stateを変更しない()
    {
        var state = Initial();
        var action = new CreditUpdateAction { Life = 2, Reason = CreditReason.CustomerLeft };

        var next = Reducer.Apply(state, action);

        Assert.Equal(0, state.CreditLife);
        Assert.Equal(2, next.CreditLife);
    }

    [Fact]
    public void CreditUpdateはdeltaを使わずLifeの絶対値のみ採用する()
    {
        var state = Initial().With(creditLife: 5);
        var next = Reducer.Apply(state, new CreditUpdateAction { Life = 1, Reason = CreditReason.CustomerLeft });

        Assert.Equal(1, next.CreditLife);
    }

    [Fact]
    public void StoreEliminated_自店ならSpectatingへ()
    {
        var state = Initial().With(
            selfStoreId: "s1",
            phase: ClientPhase.InMatch,
            queue: new List<CustomerEntry> { new() { View = new CustomerView { CustomerId = "c1" } } },
            currentOrder: new CurrentOrder { CustomerId = "c1" },
            stores: new List<StoreSummary> { new() { StoreId = "s1", Alive = true } });

        var next = Reducer.Apply(state, new StoreEliminatedAction { StoreId = "s1", Reason = EliminationReason.SelfCollapse, FinalRank = 5 });

        Assert.Equal(ClientPhase.Spectating, next.Phase);
        Assert.Empty(next.Queue);
        Assert.Null(next.CurrentOrder);
        Assert.False(next.Alive);
    }

    [Fact]
    public void StoreEliminated_他店なら自店フィールドは変わらない()
    {
        var state = Initial().With(
            selfStoreId: "s1",
            phase: ClientPhase.InMatch,
            creditLife: 3,
            stores: new List<StoreSummary> { new() { StoreId = "s1", Alive = true }, new() { StoreId = "s2", Alive = true } });

        var next = Reducer.Apply(state, new StoreEliminatedAction { StoreId = "s2", Reason = EliminationReason.SelfCollapse, FinalRank = 10 });

        Assert.Equal(ClientPhase.InMatch, next.Phase);
        Assert.Equal(3, next.CreditLife);
        Assert.True(next.Stores.Single(s => s.StoreId == "s1").Alive);
        Assert.False(next.Stores.Single(s => s.StoreId == "s2").Alive);
        Assert.Equal(10, next.Stores.Single(s => s.StoreId == "s2").FinalRank);
    }

    [Fact]
    public void MatchEndはPhaseがResultになりResultが入る()
    {
        var state = Initial();
        var next = Reducer.Apply(state, new MatchEndAction { FinalRank = 1, Stats = new MatchStats { ServedCount = 10 } });

        Assert.Equal(ClientPhase.Result, next.Phase);
        Assert.NotNull(next.Result);
        Assert.Equal(1, next.Result!.FinalRank);
    }

    [Fact]
    public void 未知customerIdのCustomerLeftは例外にならずstate不変()
    {
        var state = Initial().With(queue: new List<CustomerEntry> { new() { View = new CustomerView { CustomerId = "c1" } } });

        var next = Reducer.Apply(state, new CustomerLeftAction { CustomerId = "does-not-exist", Reason = LeaveReason.Timeout });

        Assert.Same(state, next);
    }

    [Fact]
    public void 重複CustomerArrivedは後着が無視される()
    {
        var state = Initial();
        var arrived = new CustomerArrivedAction { Customer = new CustomerView { CustomerId = "c1" }, ArrivedAtLocalMs = 0 };
        var afterFirst = Reducer.Apply(state, arrived);

        var duplicate = new CustomerArrivedAction { Customer = new CustomerView { CustomerId = "c1" }, ArrivedAtLocalMs = 100 };
        var afterSecond = Reducer.Apply(afterFirst, duplicate);

        Assert.Same(afterFirst, afterSecond);
        Assert.Single(afterSecond.Queue);
    }

    [Fact]
    public void LocalOrderCleared_行列先頭が除去される()
    {
        var state = Initial().With(
            queue: new List<CustomerEntry>
            {
                new() { View = new CustomerView { CustomerId = "c1" } },
                new() { View = new CustomerView { CustomerId = "c2" } },
            },
            currentOrder: new CurrentOrder { CustomerId = "c1" });

        var next = Reducer.Apply(state, new LocalOrderClearedAction("c1"));

        Assert.Single(next.Queue);
        Assert.Equal("c2", next.Queue[0].View.CustomerId);
        Assert.Null(next.CurrentOrder);
    }

    [Fact]
    public void StoreListUpdateに自店が無ければ自店フィールドは変わらない()
    {
        var state = Initial().With(selfStoreId: "s1", creditLife: 3, phase: ClientPhase.InMatch);

        var next = Reducer.Apply(state, new StoreListUpdateAction
        {
            Stores = new List<StoreSummary> { new() { StoreId = "s2" } },
            AliveCount = 5,
        });

        Assert.Equal(3, next.CreditLife);
        Assert.Equal(ClientPhase.InMatch, next.Phase);
        Assert.Single(next.Stores);
    }

    [Fact]
    public void 購読は変化時のみ発火しDispose後は発火しない()
    {
        var store = new Store();
        var callCount = 0;
        var subscription = store.Subscribe(_ => callCount++);

        store.Apply(new CustomerLeftAction { CustomerId = "no-such-customer", Reason = LeaveReason.Timeout });
        Assert.Equal(0, callCount);

        store.Apply(new CreditUpdateAction { Life = 5, Reason = CreditReason.CustomerLeft });
        Assert.Equal(1, callCount);

        subscription.Dispose();
        store.Apply(new CreditUpdateAction { Life = 1, Reason = CreditReason.CustomerLeft });
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void LocalKeyJudgedはCurrentOrderの進捗を更新する()
    {
        var state = Initial().With(currentOrder: new CurrentOrder { CustomerId = "c1", OrderCount = 2 });
        var view = new TypingView("たこ", 1, "k", 0, 2, 1);

        var next = Reducer.Apply(state, new LocalKeyJudgedAction(KeyResult.Correct, view));

        Assert.Equal(1, next.CurrentOrder!.TypedLength);
        Assert.Equal(1, next.CurrentOrder.MissCount);
    }
}
