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
        var envelope = _codec.DecodeEnvelope("""{"type":"CustomerLeft","payload":{"reason":"Timeout"}}""");

        Assert.Null(_codec.DecodePayload<CustomerLeft>(envelope!));
    }

    [Fact]
    public void EncodeEnvelope_空payloadでもpayloadキーを出力する()
    {
        var json = _codec.EncodeEnvelope(MessageType.MatchmakingJoin, new MatchmakingJoin());

        Assert.Equal("""{"type":"MatchmakingJoin","payload":{}}""", json);
    }

    [Fact]
    public void EncodeEnvelope_nullのオプショナルフィールドは出力しない()
    {
        var json = _codec.EncodeEnvelope(MessageType.MatchmakingStatus, new MatchmakingStatus
        {
            WaitingCount = 3,
            MinPlayers = 10,
            CountdownMs = null,
        });

        Assert.DoesNotContain("countdownMs", json);
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
            PatienceMaxMs = 1000,
        });

        var envelope = _codec.DecodeEnvelope(json)!;
        var view = _codec.DecodePayload<CustomerView>(envelope);

        Assert.Equal(CustomerAttribute.Claimer, view!.Attribute);
    }

    [Fact]
    public void 未知enum値_全体が失敗せず既定値になる()
    {
        var envelope = _codec.DecodeEnvelope(
            """{"type":"CustomerArrived","payload":{"customerId":"c1","attribute":"Unknown","orderCount":1,"words":["たこ"],"patienceMaxMs":1000}}""");

        var view = _codec.DecodePayload<CustomerView>(envelope!);

        Assert.NotNull(view);
        Assert.Equal(CustomerAttribute.Normal, view!.Attribute);
    }

    [Fact]
    public void ラウンドトリップ_エンコードデコードで同値()
    {
        var original = new MatchStart
        {
            MatchId = "m1",
            SelfStoreId = "s1",
            Params = new GameParametersPublicSubset { MatchTimeLimitMs = 60000, InitialLife = 3, MaxStores = 99 },
            Phase = Phase.Mid,
            Stores = new List<StoreSummary>
            {
                new() { StoreId = "s1", DisplayName = "店1", EvalNormalized = 0.5, Rank = 1, CreditLife = 3, Alive = true },
            },
        };

        var json = _codec.EncodeEnvelope(MessageType.MatchStart, original);
        var envelope = _codec.DecodeEnvelope(json)!;
        var decoded = _codec.DecodePayload<MatchStart>(envelope);

        Assert.NotNull(decoded);
        Assert.Equal(original.MatchId, decoded!.MatchId);
        Assert.Equal(original.SelfStoreId, decoded.SelfStoreId);
        Assert.Equal(original.Phase, decoded.Phase);
        Assert.Equal(original.Stores[0].StoreId, decoded.Stores[0].StoreId);
    }
}
