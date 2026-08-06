using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Takoda99.Proto;

namespace Takoda99.Client.Contract;

/// <summary>
/// <see cref="IEnvelopeCodec"/> の System.Text.Json 実装。
/// シリアライザの選定は未確定（01-contract.md §6）だが、この実装をこのクラスの裏に
/// 閉じ込めてあるので差し替え時の影響範囲はこのファイルに限定される。
/// </summary>
public sealed class EnvelopeCodec : IEnvelopeCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Envelope? DecodeEnvelope(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (envelope is null || string.IsNullOrEmpty(envelope.Type))
        {
            return null;
        }

        return envelope;
    }

    public T? DecodePayload<T>(Envelope envelope) where T : class
    {
        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(envelope.Payload.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is null)
        {
            return null;
        }

        EnumFallbackSanitizer.Fix(node, typeof(T));

        JsonDocument fixedDocument;
        try
        {
            fixedDocument = JsonDocument.Parse(node.ToJsonString());
        }
        catch (JsonException)
        {
            return null;
        }

        using (fixedDocument)
        {
            if (!RequiredFieldValidator.HasAllRequiredFields(fixedDocument.RootElement, typeof(T)))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(fixedDocument.RootElement.GetRawText(), Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public string EncodeEnvelope(string type, object payload)
    {
        var payloadElement = JsonSerializer.SerializeToElement(payload, payload.GetType(), Options);
        var wire = new EnvelopeWire { Type = type, Payload = payloadElement };
        return JsonSerializer.Serialize(wire, Options);
    }

    private sealed class EnvelopeWire
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; }
    }
}
