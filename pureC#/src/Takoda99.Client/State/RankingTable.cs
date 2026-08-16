using System;
using System.Collections.Generic;

namespace Takoda99.Client.State;

/// <summary>ランキング表の1行。表示に必要な値をすべて持つ（描画側が他を引かなくて済む形）。</summary>
public sealed class RankingRow
{
    public string StoreId { get; init; } = "";

    /// <summary>MatchStart のキャッシュから解決済みの表示名。未解決なら空文字。</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>生存店は現在順位、脱落店は確定順位（以後不変）。1始まり。</summary>
    public int Rank { get; init; }

    public int Score { get; init; }

    public bool Alive { get; init; }
}

/// <summary>全店のランキング。Rows は常に Rank の昇順（1位が先頭）で保持する。</summary>
public sealed class RankingTable
{
    public IReadOnlyList<RankingRow> Rows { get; init; } = Array.Empty<RankingRow>();

    /// <summary>上位 n 件（不足分は詰めて返す。n &lt;= 0 なら空）。</summary>
    public IReadOnlyList<RankingRow> Top(int n)
    {
        if (n <= 0)
        {
            return Array.Empty<RankingRow>();
        }

        var count = n < Rows.Count ? n : Rows.Count;
        var result = new List<RankingRow>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(Rows[i]);
        }

        return result;
    }

    /// <summary>storeId で1行引く。無ければ null。</summary>
    public RankingRow? Find(string storeId)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (string.Equals(Rows[i].StoreId, storeId, StringComparison.Ordinal))
            {
                return Rows[i];
            }
        }

        return null;
    }
}
