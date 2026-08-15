// 仕様書: Unity/docs/.sdd/value-objects/08-ranking-row-view-state.md §6 テスト観点

using System.Collections.Generic;
using System.Linq;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class RankingRowViewStateTests
    {
        private static RankingRow Row(string storeId, int rank, int score = 0, string displayName = "店", bool alive = true)
            => new RankingRow { StoreId = storeId, Rank = rank, Score = score, DisplayName = displayName, Alive = alive };

        /// <summary>store-01 が1位 … store-99 が99位。スコアは 99 点差で降順。</summary>
        private static RankingTable Table99()
        {
            var rows = new List<RankingRow>(99);
            for (var i = 1; i <= 99; i++)
            {
                rows.Add(Row($"store-{i:00}", i, score: 100 - i, displayName: $"店{i:00}"));
            }

            return new RankingTable { Rows = rows };
        }

        // ── RankingRowViewState ─────────────────────────────

        [Fact]
        public void Rankが0なら順位未確定の表記になる()
        {
            var state = RankingRowViewState.From(Row("s1", 0), false);

            Assert.Equal("--", state.RankText);
        }

        [Fact]
        public void DisplayNameが空ならStoreIdを出す()
        {
            var state = RankingRowViewState.From(Row("s1", 1, displayName: ""), false);

            Assert.Equal("s1", state.NameText);
        }

        /// <summary>スコアは累積の絶対値で負値もあり得る。0でクランプしない。</summary>
        [Fact]
        public void 負のスコアがそのまま文字列になる()
        {
            var state = RankingRowViewState.From(Row("s1", 90, score: -30), false);

            Assert.Equal("-30", state.ScoreText);
        }

        [Fact]
        public void 同じ値から作った2つの状態は等しい()
        {
            var row = Row("s1", 5, score: 120, displayName: "たこ屋");

            var a = RankingRowViewState.From(row, false);
            var b = RankingRowViewState.From(Row("s1", 5, score: 120, displayName: "たこ屋"), false);

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void スコアが変われば等しくない()
        {
            var a = RankingRowViewState.From(Row("s1", 5, score: 120), false);
            var b = RankingRowViewState.From(Row("s1", 5, score: 121), false);

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void FromSelfは順位とスコアを権威値で上書きする()
        {
            // Ranking 側は古い値（差分の取りこぼしでズレ得る）。
            var state = RankingRowViewState.FromSelf(Row("s1", 40, score: 100, displayName: "自店"), 3, 900);

            Assert.Equal("3", state.RankText);
            Assert.Equal("900", state.ScoreText);
            Assert.Equal("自店", state.NameText);
            Assert.True(state.IsSelf);
        }

        // ── SelfRankViewState ───────────────────────────────

        [Fact]
        public void 自店HUDはRankが0以下なら順位未確定の表記になる()
        {
            Assert.Equal("--", SelfRankViewState.From(0, 0, 99).RankText);
            Assert.Equal("--", SelfRankViewState.From(-1, 0, 99).RankText);
        }

        [Fact]
        public void 自店HUDの負のスコアがそのまま文字列になる()
        {
            Assert.Equal("-30", SelfRankViewState.From(88, -30, 75).ScoreText);
        }

        [Fact]
        public void 自店HUDの生存数は0以下なら表示しない()
        {
            Assert.Equal("残り 55 店", SelfRankViewState.From(12, 1200, 55).AliveCountText);
            Assert.Equal(string.Empty, SelfRankViewState.From(12, 1200, 0).AliveCountText);
        }

        [Fact]
        public void 自店HUDの同じ値から作った2つの状態は等しい()
        {
            Assert.True(SelfRankViewState.From(12, 1200, 55).Equals(SelfRankViewState.From(12, 1200, 55)));
            Assert.False(SelfRankViewState.From(12, 1200, 55).Equals(SelfRankViewState.From(11, 1200, 55)));
        }

        // ── RankingRowsBuilder.Build ────────────────────────

        [Fact]
        public void Build_自分が50位なら上位10件と自分の11行になる()
        {
            var rows = RankingRowsBuilder.Build(Table99(), "store-50", 50, 50, 10);

            Assert.Equal(11, rows.Count);
            Assert.Equal("store-50", rows[10].StoreId);
            Assert.True(rows[10].IsSelf);
            Assert.Single(rows.Where(r => r.IsSelf));
        }

        [Fact]
        public void Build_自分が3位なら10行のまま重複しない()
        {
            var rows = RankingRowsBuilder.Build(Table99(), "store-03", 3, 97, 10);

            Assert.Equal(10, rows.Count);
            Assert.Single(rows.Where(r => r.IsSelf));
            Assert.Equal("store-03", rows.Single(r => r.IsSelf).StoreId);
        }

        /// <summary>100秒時点の生存数が10人＝上位10名リストが生存者全員になるため。</summary>
        [Fact]
        public void Build_visibleCountが10未満なら10にクランプされる()
        {
            var rows = RankingRowsBuilder.Build(Table99(), "store-03", 3, 97, 5);

            Assert.Equal(10, rows.Count);
        }

        [Fact]
        public void Build_自分の行は権威値で上書きされRankingが古くても正しい()
        {
            // Ranking 上は 50 位だが、EvaluationUpdate では 7 位。
            var rows = RankingRowsBuilder.Build(Table99(), "store-50", 7, 888, 10);

            var self = rows.Single(r => r.IsSelf);
            Assert.Equal("7", self.RankText);
            Assert.Equal("888", self.ScoreText);
        }

        [Fact]
        public void Build_空のRankingTableなら空リストになる()
        {
            Assert.Empty(RankingRowsBuilder.Build(new RankingTable(), "store-01", 1, 0, 10));
            Assert.Empty(RankingRowsBuilder.Build(null, "store-01", 1, 0, 10));
        }

        [Fact]
        public void Build_自分がRankingに居なければ権威値だけの行が末尾に足される()
        {
            var rows = RankingRowsBuilder.Build(Table99(), "ghost", 42, 300, 10);

            Assert.Equal(11, rows.Count);
            var self = rows[10];
            Assert.Equal("ghost", self.StoreId);
            Assert.Equal("42", self.RankText);
            Assert.Equal("300", self.ScoreText);
            // 表示名が解決できないので storeId をそのまま出す（空欄にしない）。
            Assert.Equal("ghost", self.NameText);
        }

        [Fact]
        public void Build_並び順はRankingの順を保つ()
        {
            var rows = RankingRowsBuilder.Build(Table99(), "store-50", 50, 50, 10);

            Assert.Equal(
                new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" },
                rows.Take(10).Select(r => r.RankText));
        }

        // ── RankingRowsBuilder.BuildAll ─────────────────────

        [Fact]
        public void BuildAll_99行がRankingの順のまま返る()
        {
            var rows = RankingRowsBuilder.BuildAll(Table99(), "store-50", 50, 50);

            Assert.Equal(99, rows.Count);
            Assert.Equal("store-01", rows[0].StoreId);
            Assert.Equal("store-99", rows[98].StoreId);
            Assert.Single(rows.Where(r => r.IsSelf));
        }

        [Fact]
        public void BuildAll_自分の行が権威値で上書きされる()
        {
            var rows = RankingRowsBuilder.BuildAll(Table99(), "store-50", 7, 888);

            var self = rows.Single(r => r.IsSelf);
            Assert.Equal("7", self.RankText);
            Assert.Equal("888", self.ScoreText);
        }

        /// <summary>脱落済みの行はリストから消さない（確定順位として並び続ける）。</summary>
        [Fact]
        public void BuildAll_脱落済みの行も残りIsAliveがfalseになる()
        {
            var table = new RankingTable
            {
                Rows = new List<RankingRow>
                {
                    Row("s1", 1, score: 900),
                    Row("s2", 40, score: 10, alive: false),
                },
            };

            var rows = RankingRowsBuilder.BuildAll(table, "s1", 1, 900);

            Assert.Equal(2, rows.Count);
            Assert.False(rows[1].IsAlive);
            Assert.Equal("40", rows[1].RankText);
        }

        [Fact]
        public void BuildAll_空のRankingTableなら空リストになる()
        {
            Assert.Empty(RankingRowsBuilder.BuildAll(new RankingTable(), "s1", 1, 0));
            Assert.Empty(RankingRowsBuilder.BuildAll(null, "s1", 1, 0));
        }
    }
}
