using Takoda99.Proto;

namespace Takoda99.Client.Contract;

/// <summary>
/// Envelope とメッセージ DTO の相互変換。シリアライザ実装（System.Text.Json /
/// Newtonsoft.Json）をこのインターフェースの裏に隠す（01-contract.md §6 未確定事項）。
/// </summary>
public interface IEnvelopeCodec
{
    /// <summary>受信した生 JSON テキストを Envelope に復元する。</summary>
    /// <returns>復元できなければ null（例外を投げない。第5章 §3「1メッセージの失敗で接続を切らない」）。</returns>
    Envelope? DecodeEnvelope(string json);

    /// <summary>Envelope.Payload を指定の DTO 型へ復元する。</summary>
    /// <returns>必須フィールド欠落・型不一致なら null（例外を投げない）。</returns>
    T? DecodePayload<T>(Envelope envelope) where T : class;

    /// <summary>送信メッセージを Envelope に包んで JSON テキストにする。</summary>
    /// <remarks>payload が空のメッセージも "payload": {} を必ず出力する（第5章 §2）。</remarks>
    string EncodeEnvelope(string type, object payload);
}
