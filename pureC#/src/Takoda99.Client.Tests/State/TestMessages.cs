// テスト用の Proto メッセージ組み立てヘルパー。契約そのものは Proto のミラーを使う。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.Tests.State
{
    internal static class TestMessages
    {
        public static StoreSummary Summary(
            string storeId,
            bool alive = true,
            double evalNormalized = 0.5,
            int rank = 1,
            int creditLife = 3,
            string displayName = "たまちゃん屋")
        {
            return new StoreSummary
            {
                StoreId = storeId,
                DisplayName = displayName,
                EvalNormalized = evalNormalized,
                Rank = rank,
                CreditLife = creditLife,
                Alive = alive,
            };
        }

        public static MatchStart MatchStart(
            string selfStoreId = "store-01",
            Phase phase = Phase.Early,
            int maxStores = 99,
            int matchTimeLimitMs = 300_000,
            int initialLife = 3,
            List<StoreSummary>? stores = null)
        {
            return new MatchStart
            {
                MatchId = "match-001",
                SelfStoreId = selfStoreId,
                Phase = phase,
                Params = new GameParametersPublicSubset
                {
                    MatchTimeLimitMs = matchTimeLimitMs,
                    InitialLife = initialLife,
                    MaxStores = maxStores,
                },
                Stores = stores ?? new List<StoreSummary> { Summary(selfStoreId) },
            };
        }

        public static CustomerView CustomerView(
            string customerId = "customer-01",
            int orderCount = 4,
            int patienceMaxMs = 20_000,
            CustomerAttribute attribute = CustomerAttribute.Normal,
            List<string>? words = null)
        {
            return new CustomerView
            {
                CustomerId = customerId,
                Attribute = attribute,
                OrderCount = orderCount,
                PatienceMaxMs = patienceMaxMs,
                Words = words ?? new List<string> { "たこ", "やき", "ソース", "あおのり" },
            };
        }
    }
}
