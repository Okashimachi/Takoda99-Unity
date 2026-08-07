// テストモード用のリザルトサンプルデータ。Boot シーンやサーバー接続なしで
// リザルト画面の全表示要素（たこ焼き生成を含む）を確認するために使う。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;

namespace Takoda99.View
{
    /// <summary>リザルト画面のテストモードで注入するサンプルデータ一式。</summary>
    public static class ResultSampleData
    {
        public const string SelfStoreId = "store-007";

        /// <summary>サンプルの既定の提供数（＝生成されるたこ焼きの個数）。</summary>
        public const int DefaultServedCount = 43;

        /// <summary>
        /// MatchEnd 相当のサンプル。数値は「それらしい1試合ぶん」の値。
        /// <paramref name="servedCount"/> は提供数（＝生成されるたこ焼きの個数）。
        /// 属性別の内訳と打鍵数もこの値に比例させるので、成績表示とたこ焼きの数が食い違わない。
        /// </summary>
        public static MatchResult CreateResult(int servedCount = DefaultServedCount)
        {
            var served = servedCount < 0 ? 0 : servedCount;

            // 既定値（43人）のときの内訳を基準に、指定された提供数へ比例配分する。
            var normal = Scale(served, 28);
            var bonus = Scale(served, 8);
            var claimer = Scale(served, 5);
            // 端数はすべて JK に寄せて、4属性の合計が必ず提供数と一致するようにする。
            var buzz = served - normal - bonus - claimer;

            return new MatchResult
            {
                FinalRank = 7,
                Reason = "Cull",
                MatchElapsedMs = 8 * 60 * 1000 + 42 * 1000,
                CreditLeft = 2,
                EvalRaw = 1834.5,
                EvalNormalized = 0.732,
                Stats = new MatchStats
                {
                    ServedCount = served,
                    LeftCount = 6,
                    AvgAccuracy = 0.941,
                    AvgElapsedMs = 4820,
                    // 1客あたり約30打鍵・そのうち約6%がミス、という想定で提供数から作る。
                    TotalKeystrokes = served * 30,
                    TotalMisses = served * 30 * 6 / 100,
                    FastestMs = 1980,
                    SlowestMs = 11460,
                    Normal = new AttributeTally { Served = normal, Left = 3 },
                    Bonus = new AttributeTally { Served = bonus, Left = 1 },
                    Claimer = new AttributeTally { Served = claimer, Left = 2 },
                    Buzz = new AttributeTally { Served = buzz, Left = 0 },
                },
            };
        }

        /// <summary>既定値（43人）での内訳 <paramref name="baseValue"/> を、指定の提供数へ比例配分する。</summary>
        private static int Scale(int servedCount, int baseValue) =>
            servedCount * baseValue / DefaultServedCount;

        /// <summary>99店ぶんの最終スナップショットのサンプル（自店は 7 位で脱落済み）。</summary>
        public static IReadOnlyList<StoreSummary> CreateStores()
        {
            var stores = new List<StoreSummary>(99);
            for (var i = 0; i < 99; i++)
            {
                var rank = i + 1;
                var isSelf = rank == 7;
                stores.Add(new StoreSummary
                {
                    StoreId = isSelf ? SelfStoreId : $"store-{rank:000}",
                    DisplayName = isSelf ? "たこ屋" : $"店{rank:000}",
                    // 上位ほど評価が高くなるよう単調に散らす。
                    EvalNormalized = 1.0 - (i / 99.0),
                    Rank = rank,
                    CreditLife = rank == 1 ? 3 : 0,
                    Alive = rank == 1,
                    FinalRank = rank == 1 ? null : rank,
                });
            }

            return stores;
        }
    }
}
