using System.Collections.Generic;

namespace Takoda99.Client.Typing;

public enum KeyResult
{
    Ignored,      // Idle 中・対象外キー（missCount を増やさない）
    Correct,      // 受理（単語継続中）
    Miss,         // 不一致（missCount++・バッファは巻き戻さない）
    WordCleared,  // 現在単語を打ち切った（wordIndex++）
    OrderCleared, // 最終単語を打ち切った（注文完了）
}

/// <summary>OrderServed の材料。BuildReport() が返す。</summary>
public readonly struct OrderReport
{
    public OrderReport(string customerId, int elapsedMs, int missCount, long clientTimestamp)
    {
        CustomerId = customerId;
        ElapsedMs = elapsedMs;
        MissCount = missCount;
        ClientTimestamp = clientTimestamp;
    }

    public string CustomerId { get; }
    public int ElapsedMs { get; }        // 対応開始 → OrderCleared
    public int MissCount { get; }        // 1注文通算
    public long ClientTimestamp { get; } // OrderCleared 時点の壁時計
}

/// <summary>表示用スナップショット（Renderer がハイライトに使う）。</summary>
public readonly struct TypingView
{
    public TypingView(string currentWord, int typedKanaLength, string pendingInput, int wordIndex, int orderCount, int missCount)
    {
        CurrentWord = currentWord;
        TypedKanaLength = typedKanaLength;
        PendingInput = pendingInput;
        WordIndex = wordIndex;
        OrderCount = orderCount;
        MissCount = missCount;
    }

    public static TypingView Empty { get; } = new(string.Empty, 0, string.Empty, 0, 0, 0);

    public string CurrentWord { get; }   // 現在のお題単語（かな原文）
    public int TypedKanaLength { get; }  // 確定済みかなの文字数（ハイライト幅）
    public string PendingInput { get; }  // 現在かなの未確定入力バッファ
    public int WordIndex { get; }        // x（0 起点）
    public int OrderCount { get; }       // N
    public int MissCount { get; }
}

public interface ITypingJudge
{
    /// <summary>客が行列先頭になり対応を開始する。ここが elapsedMs の起点（最初の打鍵ではない）。</summary>
    void BeginOrder(string customerId, IReadOnlyList<string> words);

    /// <summary>文字キー1つを与えて判定を進める。</summary>
    KeyResult PressKey(char c);

    /// <summary>CustomerLeft 受信時の中断。計測値は破棄し、OrderServed を送らない。</summary>
    void AbortOrder();

    /// <summary>OrderCleared 直後に呼ぶ。Idle 中・未完了時は null。</summary>
    OrderReport? BuildReport();

    /// <summary>現在の表示用状態。Idle 中は既定値。</summary>
    TypingView CurrentView { get; }

    /// <summary>Idle かどうか（Spectating 中は Idle に固定される）。</summary>
    bool IsIdle { get; }
}
