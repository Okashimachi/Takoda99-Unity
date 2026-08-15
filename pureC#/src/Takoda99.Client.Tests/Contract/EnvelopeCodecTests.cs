using System.Collections.Generic;
using Takoda99.Client.Contract;
using Takoda99.Proto;
using Xunit;

namespace Takoda99.Client.Tests.Contract;

public class EnvelopeCodecTests
{
    private readonly EnvelopeCodec _codec = new();

    [Fact]
    public void DecodeEnvelope_正常なJSON_Envelopeを返す()
    {
        var envelope = _codec.DecodeEnvelope("""{"type":"MatchStart","payload":{}}""");

        Assert.NotNull(envelope);
        Assert.Equal("MatchStart", envelope!.Type);
    }

    [Fact]
    public void DecodeEnvelope_壊れたJSON_nullを返す()
    {
        Assert.Null(_codec.DecodeEnvelope("{not json"));
    }

    [Fact]
    public void DecodeEnvelope_type欠落_nullを返す()
    {
        Assert.Null(_codec.DecodeEnvelope("""{"payload":{}}"""));
    }

    [Fact]
    public void DecodeEnvelope_payload欠落_Envelopeを返しPayloadは空扱い()
    {
        var envelope = _codec.DecodeEnvelope("""{"type":"MatchStart"}""");

        Assert.NotNull(envelope);
        Assert.Null(_codec.DecodePayload<MatchStart>(envelope!));
    }

    [Fact]
    public void DecodePayload_必須フィールド欠落_nullを返す()
    {
        // customerId は識別子なので、空・欠落で届いた時点で不正。
        var envelope = _codec.DecodeEnvelope(
            """{"type":"CustomerArrived","payload":{"attribute":"Normal","orderCount":1,"words":["たこ"]}}""");

        Assert.Null(_codec.DecodePayload<CustomerView>(envelope!));
    }

    [Fact]
    public void EncodeEnvelope_空payloadでもpayloadキーを出力する()
    {
        var json = _codec.EncodeEnvelope(MessageType.MatchmakingLeave, new MatchmakingLeave());

        Assert.Equal("""{"type":"MatchmakingLeave","payload":{}}""", json);
    }

    [Fact]
    public void EncodeEnvelope_countdownMsはnullでも無条件シリアライズされる()
    {
        // v0.5.0 で countdownMs から JsonIgnore(WhenWritingNull) が外れた（VERSION.md 参照）。
        var json = _codec.EncodeEnvelope(MessageType.MatchmakingStatus, new MatchmakingStatus
        {
            WaitingCount = 3,
            MinPlayers = 10,
            CountdownMs = null,
        });

        Assert.Contains("\"countdownMs\":null", json);
    }

    [Fact]
    public void EncodeEnvelope_nullのオプショナルフィールドは出力しない()
    {
        var json = _codec.EncodeEnvelope("StoreSummary", new StoreSummary
        {
            StoreId = "s1",
            DisplayName = "たこ焼き",
            FinalRank = null,
        });

        Assert.DoesNotContain("finalRank", json);
    }

    [Fact]
    public void Enum_文字列往復が一致する()
    {
        var json = _codec.EncodeEnvelope("CustomerArrived", new CustomerView
        {
            CustomerId = "c1",
            Attribute = CustomerAttribute.Claimer,
            OrderCount = 1,
            Words = new List<string> { "たこ" },
        });

        var envelope = _codec.DecodeEnvelope(json)!;
        var view = _codec.DecodePayload<CustomerView>(envelope);

        Assert.Equal(CustomerAttribute.Claimer, view!.Attribute);
    }

    [Fact]
    public void 未知enum値_全体が失敗せず既定値になる()
    {
        var envelope = _codec.DecodeEnvelope(
            """{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Unknown","orderCount":1,"words":["たこ"]}}""");

        var view = _codec.DecodePayload<CustomerView>(envelope!);

        Assert.NotNull(view);
        Assert.Equal(CustomerAttribute.Normal, view!.Attribute);
    }

    [Fact]
    public void ラウンドトリップ_MatchStart()
    {
        var original = new MatchStart
        {
            MatchId = "m1",
            SelfStoreId = "s1",
            Params = new GameParametersPublicSubset
            {
                MaxStores = 99,
                ScoreWeightTakoyaki = 100,
                ScoreWeightMiss = 20,
                CullSchedule = new List<CullStageView> { new() { AtMs = 20_000, TargetAliveCount = 50 } },
            },
            Phase = Phase.Mid,
            Stores = new List<StoreSummary>
            {
                new() { StoreId = "s1", DisplayName = "店1", Rank = 1, Score = 300, Alive = true },
            },
        };

        var json = _codec.EncodeEnvelope(MessageType.MatchStart, original);
        var decoded = _codec.DecodePayload<MatchStart>(_codec.DecodeEnvelope(json)!);

        Assert.NotNull(decoded);
        Assert.Equal(original.MatchId, decoded!.MatchId);
        Assert.Equal(original.SelfStoreId, decoded.SelfStoreId);
        Assert.Equal(original.Phase, decoded.Phase);
        Assert.Equal(original.Stores[0].StoreId, decoded.Stores[0].StoreId);
        Assert.Equal(300, decoded.Stores[0].Score);
        Assert.Equal(20_000, decoded.Params.CullSchedule[0].AtMs);
        Assert.Equal(50, decoded.Params.CullSchedule[0].TargetAliveCount);
    }

    // ── v0.8.0 で増えたメッセージの往復（contract/01 §8 観点2） ──

    [Fact]
    public void ラウンドトリップ_RankingSnapshot()
    {
        var original = new RankingSnapshot
        {
            Entries = new List<RankingEntry>
            {
                new() { StoreId = "s1", Rank = 1, Score = 900, Alive = true },
                new() { StoreId = "s2", Rank = 2, Score = -30, Alive = false },
            },
        };

        var json = _codec.EncodeEnvelope(MessageType.RankingSnapshot, original);
        var decoded = _codec.DecodePayload<RankingSnapshot>(_codec.DecodeEnvelope(json)!);

        Assert.NotNull(decoded);
        Assert.Equal(2, decoded!.Entries.Count);
        Assert.Equal("s1", decoded.Entries[0].StoreId);
        Assert.Equal(900, decoded.Entries[0].Score);
        Assert.Equal(-30, decoded.Entries[1].Score);
        Assert.False(decoded.Entries[1].Alive);
    }

    [Fact]
    public void ラウンドトリップ_RankingDelta()
    {
        var original = new RankingDelta
        {
            Entries = new List<RankingChange> { new() { StoreId = "s1", Score = 120, Alive = true } },
        };

        var json = _codec.EncodeEnvelope(MessageType.RankingDelta, original);
        // RankingChange は rank を持たない（差分の利点が消えるため）。
        Assert.DoesNotContain("\"rank\"", json);

        var decoded = _codec.DecodePayload<RankingDelta>(_codec.DecodeEnvelope(json)!);

        Assert.NotNull(decoded);
        Assert.Equal("s1", decoded!.Entries[0].StoreId);
        Assert.Equal(120, decoded.Entries[0].Score);
    }

    [Fact]
    public void ラウンドトリップ_StoreEliminatedBatch()
    {
        var original = new StoreEliminatedBatch
        {
            StageIndex = 6,
            Entries = new List<StoreEliminated>
            {
                new() { StoreId = "s1", Reason = EliminationReason.Cull, FinalRank = 1 },
                new() { StoreId = "s2", Reason = EliminationReason.Cull, FinalRank = 2 },
            },
        };

        var json = _codec.EncodeEnvelope(MessageType.StoreEliminatedBatch, original);
        var decoded = _codec.DecodePayload<StoreEliminatedBatch>(_codec.DecodeEnvelope(json)!);

        Assert.NotNull(decoded);
        Assert.Equal(6, decoded!.StageIndex);
        Assert.Equal(2, decoded.Entries.Count);
        Assert.Equal(EliminationReason.Cull, decoded.Entries[0].Reason);
        Assert.Equal(1, decoded.Entries[0].FinalRank);
    }

    [Fact]
    public void ラウンドトリップ_PersonalResult()
    {
        var original = new PersonalResult
        {
            FinalRank = 42,
            Score = 1_234,
            TakoyakiCount = 56,
            SurvivedMs = 78_000,
            Stats = new MatchStats { ServedCount = 12, TotalMisses = 7, AvgAccuracy = 0.94 },
        };

        var json = _codec.EncodeEnvelope(MessageType.PersonalResult, original);
        var decoded = _codec.DecodePayload<PersonalResult>(_codec.DecodeEnvelope(json)!);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.FinalRank);
        Assert.Equal(1_234, decoded.Score);
        Assert.Equal(56, decoded.TakoyakiCount);
        Assert.Equal(78_000, decoded.SurvivedMs);
        // 総ミス数は Stats 側にだけ持つ（PersonalResult に重複させない）。
        Assert.Equal(7, decoded.Stats.TotalMisses);
    }

    /// <summary>MatchEnd はペイロードを持たない空クラス（v0.8.0 最大の破壊的変更）。</summary>
    [Fact]
    public void MatchEndはペイロードが空でも往復できる()
    {
        var json = _codec.EncodeEnvelope(MessageType.MatchEnd, new MatchEnd());

        Assert.Equal("""{"type":"MatchEnd","payload":{}}""", json);
        Assert.NotNull(_codec.DecodePayload<MatchEnd>(_codec.DecodeEnvelope(json)!));
        Assert.NotNull(_codec.DecodePayload<MatchEnd>(
            _codec.DecodeEnvelope("""{"type":"MatchEnd","payload":{}}""")!));
    }

    /// <summary>Proto が「null で届き得る」と明記しているコレクション（contract/01 §5）。</summary>
    [Theory]
    [InlineData("""{"entries":null}""")]
    [InlineData("{}")]
    public void entriesがnullや欠落のRankingSnapshotもdecodeできる(string payload)
    {
        var envelope = _codec.DecodeEnvelope($$"""{"type":"RankingSnapshot","payload":{{payload}}}""");

        var decoded = _codec.DecodePayload<RankingSnapshot>(envelope!);

        Assert.NotNull(decoded);
        // 空リストへの正規化は Dispatcher.OrEmpty が行うため、ここでは null のままで構わない。
    }
}
