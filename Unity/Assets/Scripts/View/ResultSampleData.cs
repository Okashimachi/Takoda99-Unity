// テストモード用のリザルトサンプルデータ。Boot シーンやサーバー接続なしで
// リザルト画面の全表示要素（たこ焼き生成を含む）を確認するために使う。
//
// 本選（Proto v0.8.0）では MatchEnd が空ペイロードになったため、成績の供給源は PersonalResult だけ。
// Obsolete な値（reason / creditLeft / 評価まわり / leftCount）はサンプルにも入れない。

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
        /// PersonalResult 相当のサンプル。数値は「それらしい1試合ぶん」の値。
        /// <paramref name="servedCount"/> は提供した客の数（＝生成されるたこ焼きの個数）。
        /// 属性別の内訳と打鍵数もこの値に比例させるので、成績表示とたこ焼きの数が食い違わない。
        /// </summary>
        /// <param name="finalRank">リザルトの Tier 分岐を確認するために変えられるようにしている。</param>
        public static PersonalResultState CreateResult(int servedCount = DefaultServedCount, int finalRank = 7)
        {
            var served = servedCount < 0 ? 0 : servedCount;

            // 既定値（43人）のときの内訳を基準に、指定された提供数へ比例配分する。
            var normal = Scale(served, 28);
            var bonus = Scale(served, 8);
            var claimer = Scale(served, 5);
            // 端数はすべて JK に寄せて、4属性の合計が必ず提供数と一致するようにする。
            var buzz = served - normal - bonus - claimer;

            return new PersonalResultState
            {
                FinalRank = finalRank,
                // 順位を決めた累積値。W_TAKOYAKI×たこ焼き数 − W_MISS×ミス数 のオーダーに合わせる。
                Score = served * 100 - served * 30 * 6 / 100 * 20,
                // ★提供した「客」の数（ServedCount）ではなく、作ったたこ焼きの総数。
                TakoyakiCount = served * 4,
                SurvivedMs = 8 * 60 * 1000 + 42 * 1000,
                Stats = new MatchStats
                {
                    ServedCount = served,
                    AvgAccuracy = 0.941,
                    AvgElapsedMs = 4820,
                    // 1客あたり約30打鍵・そのうち約6%がミス、という想定で提供数から作る。
                    TotalKeystrokes = served * 30,
                    TotalMisses = served * 30 * 6 / 100,
                    FastestMs = 1980,
                    SlowestMs = 11460,
                    // Left（取りこぼし）は本選では常に 0（客が逃げない）。設定しない。
                    Normal = new AttributeTally { Served = normal },
                    Bonus = new AttributeTally { Served = bonus },
                    Claimer = new AttributeTally { Served = claimer },
                    Buzz = new AttributeTally { Served = buzz },
                },
            };
        }

        /// <summary>既定値（43人）での内訳 <paramref name="baseValue"/> を、指定の提供数へ比例配分する。</summary>
        private static int Scale(int servedCount, int baseValue) =>
            servedCount * baseValue / DefaultServedCount;

        /// <summary>
        /// 99店ぶんの最終ランキングのサンプル（自店は 7 位）。
        /// 120秒には全店が脱落するため、**全行が Alive == false**（優勝者も含む）。
        /// </summary>
        public static RankingTable CreateRanking()
        {
            var rows = new List<RankingRow>(99);
            for (var i = 0; i < 99; i++)
            {
                var rank = i + 1;
                var isSelf = rank == 7;
                rows.Add(new RankingRow
                {
                    StoreId = isSelf ? SelfStoreId : $"store-{rank:000}",
                    DisplayName = isSelf ? "たこ屋" : $"店{rank:000}",
                    Rank = rank,
                    // 上位ほどスコアが高くなるよう単調に散らす。
                    Score = (100 - rank) * 120,
                    Alive = false,
                });
            }

            return new RankingTable { Rows = rows };
        }
    }
}
