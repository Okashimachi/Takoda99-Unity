// テスト用の Proto メッセージ組み立てヘルパー。契約そのものは Proto のミラー（v0.8.0）を使う。
// Obsolete フィールド（evalNormalized / creditLife / starRating / patienceMaxMs 等）は
// 本選では 0 が届くため、ヘルパーからも一切設定しない。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.Tests.State
{
    internal static class TestMessages
    {
        public static StoreSummary Summary(
            string storeId,
            bool alive = true,
            int rank = 1,
            int score = 0,
            string displayName = "たまちゃん屋",
            int? finalRank = null)
        {
            return new StoreSummary
            {
                StoreId = storeId,
                DisplayName = displayName,
                Rank = rank,
                Score = score,
                Alive = alive,
                FinalRank = finalRank,
            };
        }

        public static MatchStart MatchStart(
            string selfStoreId = "store-01",
            Phase phase = Phase.Early,
            int maxStores = 99,
            int scoreWeightTakoyaki = 100,
            int scoreWeightMiss = 20,
            int finalStageAliveThreshold = 10,
            int finalRushAliveThreshold = 3,
            List<CullStageView>? cullSchedule = null,
            List<StoreSummary>? stores = null)
        {
            return new MatchStart
            {
                MatchId = "match-001",
                SelfStoreId = selfStoreId,
                Phase = phase,
                Params = new GameParametersPublicSubset
                {
                    MaxStores = maxStores,
                    CullSchedule = cullSchedule ?? new List<CullStageView>
                    {
                        new() { AtMs = 20_000, TargetAliveCount = 50 },
                        new() { AtMs = 40_000, TargetAliveCount = 30 },
                    },
                    ScoreWeightTakoyaki = scoreWeightTakoyaki,
                    ScoreWeightMiss = scoreWeightMiss,
                    FinalStageAliveThreshold = finalStageAliveThreshold,
                    FinalRushAliveThreshold = finalRushAliveThreshold,
                },
                Stores = stores ?? new List<StoreSummary> { Summary(selfStoreId) },
            };
        }

        /// <summary>99店ぶんの MatchStart。storeId は store-01 … store-99、Rank は 1..99。</summary>
        public static MatchStart MatchStart99(string selfStoreId = "store-01")
        {
            var stores = new List<StoreSummary>(99);
            for (var i = 1; i <= 99; i++)
            {
                var id = $"store-{i:00}";
                stores.Add(Summary(id, rank: i, displayName: $"店{i:00}"));
            }

            return MatchStart(selfStoreId: selfStoreId, stores: stores);
        }

        public static CustomerView CustomerView(
            string customerId = "customer-01",
            int orderCount = 4,
            CustomerAttribute attribute = CustomerAttribute.Normal,
            List<string>? words = null)
        {
            return new CustomerView
            {
                CustomerId = customerId,
                Attribute = attribute,
                OrderCount = orderCount,
                Words = words ?? new List<string> { "たこ", "やき", "ソース", "あおのり" },
            };
        }

        public static RankingEntry RankingEntry(string storeId, int rank, int score, bool alive = true)
            => new() { StoreId = storeId, Rank = rank, Score = score, Alive = alive };

        public static RankingSnapshot RankingSnapshot(params RankingEntry[] entries)
            => new() { Entries = new List<RankingEntry>(entries) };

        public static RankingChange RankingChange(string storeId, int score, bool alive = true)
            => new() { StoreId = storeId, Score = score, Alive = alive };

        public static RankingDelta RankingDelta(params RankingChange[] entries)
            => new() { Entries = new List<RankingChange>(entries) };

        public static StoreEliminated Eliminated(string storeId, int finalRank)
            => new() { StoreId = storeId, Reason = EliminationReason.Cull, FinalRank = finalRank };

        public static StoreEliminatedBatch EliminatedBatch(int stageIndex, params StoreEliminated[] entries)
            => new() { StageIndex = stageIndex, Entries = new List<StoreEliminated>(entries) };

        public static ForcedEliminationWarning CullWarning(
            int untilMs = 5_000,
            int stageIndex = 1,
            int stageTotal = 6,
            int cutLineRank = 51,
            bool selfAtRisk = false,
            List<string>? cutStoreIds = null)
        {
            return new ForcedEliminationWarning
            {
                UntilMs = untilMs,
                StageIndex = stageIndex,
                StageTotal = stageTotal,
                CutLineRank = cutLineRank,
                SelfAtRisk = selfAtRisk,
                CutStoreIds = cutStoreIds ?? new List<string>(),
            };
        }

        public static PersonalResult PersonalResult(
            int finalRank = 42,
            int score = 1_234,
            int takoyakiCount = 56,
            long survivedMs = 78_000,
            MatchStats? stats = null)
        {
            return new PersonalResult
            {
                FinalRank = finalRank,
                Score = score,
                TakoyakiCount = takoyakiCount,
                SurvivedMs = survivedMs,
                Stats = stats ?? new MatchStats { ServedCount = 12, TotalMisses = 7, AvgAccuracy = 0.94 },
            };
        }
    }
}
