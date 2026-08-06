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
            string displayName = "たまちゃん屋",
            int? finalRank = null)
        {
            return new StoreSummary
            {
                StoreId = storeId,
                DisplayName = displayName,
                EvalNormalized = evalNormalized,
                Rank = rank,
                CreditLife = creditLife,
                Alive = alive,
                FinalRank = finalRank,
            };
        }

        public static MatchStart MatchStart(
            string selfStoreId = "store-01",
            Phase phase = Phase.Early,
            int maxStores = 99,
            int initialLife = 3,
            double stormThresholdPct = 0.1,
            int finalStageAliveThreshold = 10,
            int finalRushAliveThreshold = 3,
            List<StoreSummary>? stores = null)
        {
            return new MatchStart
            {
                MatchId = "match-001",
                SelfStoreId = selfStoreId,
                Phase = phase,
                Params = new GameParametersPublicSubset
                {
                    InitialLife = initialLife,
                    MaxStores = maxStores,
                    StormThresholdPct = stormThresholdPct,
                    FinalStageAliveThreshold = finalStageAliveThreshold,
                    FinalRushAliveThreshold = finalRushAliveThreshold,
                },
                Stores = stores ?? new List<StoreSummary> { Summary(selfStoreId) },
            };
        }

        public static CustomerView CustomerView(
            string customerId = "customer-01",
            int orderCount = 4,
            int patienceMaxMs = 20_000,
            long patienceStartedAtServerMs = 0,
            CustomerAttribute attribute = CustomerAttribute.Normal,
            List<string>? words = null)
        {
            return new CustomerView
            {
                CustomerId = customerId,
                Attribute = attribute,
                OrderCount = orderCount,
                PatienceMaxMs = patienceMaxMs,
                PatienceStartedAtServerMs = patienceStartedAtServerMs,
                Words = words ?? new List<string> { "たこ", "やき", "ソース", "あおのり" },
            };
        }
    }
}
