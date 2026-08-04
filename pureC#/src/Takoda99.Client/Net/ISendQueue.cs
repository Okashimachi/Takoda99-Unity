namespace Takoda99.Client.Net;

/// <summary>送信キュー。順序保証と再送方針を1箇所に閉じる。</summary>
public interface ISendQueue
{
    void Enqueue(string type, object payload);

    /// <summary>接続確立時に呼ぶ。キュー順で flush する。</summary>
    void Flush();

    /// <summary>切断時に呼ぶ。OrderServed を破棄する（§3.3）。</summary>
    void OnDisconnected();
}
