// 仕様書: Unity/docs/.sdd/value-objects/09-cull-countdown-state.md §6 テスト観点

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests
{
    public class CullCountdownStateTests
    {
        private const long ReceivedAt = 1_000;

        private static CullWarning Warning(
            int untilMs = 20_000,
            int stageIndex = 3,
            int stageTotal = 6,
            int cutLineRank = 12,
            bool selfAtRisk = false,
            IReadOnlyList<string> cutStoreIds = null)
        {
            return new CullWarning
            {
                UntilMs = untilMs,
                ReceivedAtLocalMs = ReceivedAt,
                StageIndex = stageIndex,
                StageTotal = stageTotal,
                CutLineRank = cutLineRank,
                SelfAtRisk = selfAtRisk,
                CutStoreIds = cutStoreIds ?? new string[0],
            };
        }

        [Fact]
        public void 未受信ならHasWarningがfalseになる()
        {
            var state = CullCountdownState.From(null, 0);

            Assert.False(state.HasWarning);
            Assert.Equal(0, state.RemainingSeconds);
            Assert.Equal(string.Empty, state.StageText);
            Assert.Equal(0f, state.AlertIntensity);
        }

        [Fact]
        public void 受信の5秒後に残り15秒になる()
        {
            var state = CullCountdownState.From(Warning(untilMs: 20_000), ReceivedAt + 5_000);

            Assert.True(state.HasWarning);
            Assert.Equal(15, state.RemainingSeconds);
            Assert.Equal("15", state.RemainingText);
        }

        [Fact]
        public void 経過が超過しても0で止まり負にならない()
        {
            var state = CullCountdownState.From(Warning(untilMs: 20_000), ReceivedAt + 25_000);

            Assert.Equal(0, state.RemainingSeconds);
            Assert.Equal("0", state.RemainingText);
        }

        /// <summary>切り上げの境界。残り 1ms なら「1」、0ms なら「0」。</summary>
        [Theory]
        [InlineData(1, 1)]
        [InlineData(0, 0)]
        [InlineData(999, 1)]
        [InlineData(1_000, 1)]
        [InlineData(1_001, 2)]
        public void 表示秒は切り上げになる(int remainingMs, int expectedSeconds)
        {
            var state = CullCountdownState.From(Warning(untilMs: remainingMs), ReceivedAt);

            Assert.Equal(expectedSeconds, state.RemainingSeconds);
        }

        [Fact]
        public void StageTextが段階と総数の形になる()
        {
            var state = CullCountdownState.From(Warning(stageIndex: 3, stageTotal: 6), ReceivedAt);

            Assert.Equal("3 / 6", state.StageText);
        }

        /// <summary>異常が見えるほうがよいのでクランプしない。</summary>
        [Fact]
        public void StageIndexがStageTotalを超えてもクランプしない()
        {
            var state = CullCountdownState.From(Warning(stageIndex: 9, stageTotal: 6), ReceivedAt);

            Assert.Equal("9 / 6", state.StageText);
        }

        [Fact]
        public void CutLineRankが0以下ならCutLineTextは空文字()
        {
            Assert.Equal(string.Empty, CullCountdownState.From(Warning(cutLineRank: 0), ReceivedAt).CutLineText);
            Assert.Equal(string.Empty, CullCountdownState.From(Warning(cutLineRank: -1), ReceivedAt).CutLineText);
        }

        /// <summary>
        /// 最終ステージでは CutLineRank == 2 が届く（処理上は1位も脱落するが、表示は
        /// 「1位以外が脱落対象」とするのが企画意図）。ここでは特別扱いをしない。
        /// </summary>
        [Fact]
        public void 最終ステージのCutLineRank2もそのまま描く()
        {
            var state = CullCountdownState.From(Warning(cutLineRank: 2), ReceivedAt);

            Assert.Equal("2位以下が脱落", state.CutLineText);
        }

        [Fact]
        public void SelfAtRiskでなければAlertIntensityは0()
        {
            var state = CullCountdownState.From(Warning(untilMs: 0, selfAtRisk: false), ReceivedAt);

            Assert.Equal(0f, state.AlertIntensity);
        }

        [Fact]
        public void SelfAtRiskかつ残り0msでAlertIntensityが1になる()
        {
            var state = CullCountdownState.From(Warning(untilMs: 0, selfAtRisk: true), ReceivedAt);

            Assert.Equal(1f, state.AlertIntensity);
        }

        [Fact]
        public void SelfAtRiskで残りがアラート窓の外ならAlertIntensityは0()
        {
            var state = CullCountdownState.From(Warning(untilMs: 20_000, selfAtRisk: true), ReceivedAt);

            Assert.Equal(0f, state.AlertIntensity);
        }

        /// <summary>
        /// パネルは Update() で毎フレーム From を呼ぶ。同一秒内なら等しいと判定して
        /// TMP.text への代入ごと省く（AlertIntensity は毎フレーム変わるので比較に含めない）。
        /// </summary>
        [Fact]
        public void 同一秒内の2つの結果はAlertIntensityが違っても等しい()
        {
            var warning = Warning(untilMs: 20_000, selfAtRisk: true);

            var a = CullCountdownState.From(warning, ReceivedAt + 15_100);
            var b = CullCountdownState.From(warning, ReceivedAt + 15_900);

            Assert.Equal(a.RemainingSeconds, b.RemainingSeconds);
            Assert.NotEqual(a.AlertIntensity, b.AlertIntensity);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void 秒が変わったら等しくない()
        {
            var warning = Warning(untilMs: 20_000);

            var a = CullCountdownState.From(warning, ReceivedAt + 5_000);
            var b = CullCountdownState.From(warning, ReceivedAt + 6_000);

            Assert.False(a.Equals(b));
        }
    }
}
