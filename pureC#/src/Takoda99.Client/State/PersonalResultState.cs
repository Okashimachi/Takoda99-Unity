using Takoda99.Proto;

namespace Takoda99.Client.State;

/// <summary>
/// 自店の脱落確定と同時に届く個人成績（result/01-personal-result.md §2）。
/// **保持して、任意のタイミングで画面に出す。** 予選の MatchResult を置き換える
/// （MatchEnd が空になったため、成績の供給源はこれだけ）。
/// </summary>
public sealed class PersonalResultState
{
    /// <summary>確定した最終順位（1始まり）。リザルト演出の分岐はこの値だけで行う。</summary>
    public int FinalRank { get; init; }

    /// <summary>最終スコア。順位を決めた値そのもの。負値もあり得る。</summary>
    public int Score { get; init; }

    /// <summary>作ったたこ焼きの総数（＝累計 orderCount）。Stats.ServedCount とは別物。</summary>
    public int TakoyakiCount { get; init; }

    /// <summary>試合開始から脱落までの積算ミリ秒。</summary>
    public long SurvivedMs { get; init; }

    /// <summary>提供数・精度・属性別内訳などの統計（Proto DTO をそのまま保持する）。総ミス数は Stats.TotalMisses。</summary>
    public MatchStats Stats { get; init; } = new();
}
