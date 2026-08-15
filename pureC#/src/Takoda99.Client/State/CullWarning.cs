using System;
using System.Collections.Generic;

namespace Takoda99.Client.State;

/// <summary>
/// 次の足切りの予告。予選の StormWarning を置き換える（match-state/03-cull-warning.md §2）。
/// **経過秒を state に持たない。** 秒読みの数値は受信値＋ローカル経過から描画時に計算する。
/// </summary>
public sealed class CullWarning
{
    /// <summary>受信時点での「次の足切りまでの残りミリ秒」（サーバー値そのまま）。</summary>
    public int UntilMs { get; init; }

    /// <summary>この予告を受信したローカル単調時刻（ms）。補間の起点。</summary>
    public long ReceivedAtLocalMs { get; init; }

    /// <summary>第何段階か（1始まり）。</summary>
    public int StageIndex { get; init; }

    /// <summary>全何段階か。</summary>
    public int StageTotal { get; init; }

    /// <summary>この順位より下が切られる境界。最終ステージのみ 2 が届く（企画意図）。</summary>
    public int CutLineRank { get; init; }

    /// <summary>切られる予定の店（サーバーが表示件数ぶんに上限を切っている）。null では入らない。</summary>
    public IReadOnlyList<string> CutStoreIds { get; init; } = Array.Empty<string>();

    /// <summary>自店が淘汰の対象圏内か。**クライアントで rank と比較しない**（サーバー値をそのまま使う）。</summary>
    public bool SelfAtRisk { get; init; }

    /// <summary>
    /// 現在時刻における残りミリ秒。0 未満にはならない（足切り実行前後の揺れを負数で出さない）。
    /// 描画側が毎フレーム呼ぶ純関数。state は変化しない。
    /// </summary>
    public int RemainingMsAt(long nowLocalMs)
        => Math.Max(0, UntilMs - (int)(nowLocalMs - ReceivedAtLocalMs));
}
