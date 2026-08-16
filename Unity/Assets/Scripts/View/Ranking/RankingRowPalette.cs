// 仕様書: Unity/docs/.sdd/value-objects/12-ranking-row-style.md §3.1
// RankingRowTone → Color の対応。コードに直書きせず ScriptableObject で外に出す
// （金銀銅はアートの調整対象であり、色を変えるたびにコードを触る形にしない）。

using System;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    [CreateAssetMenu(menuName = "Takoda99/Ranking Row Palette")]
    public sealed class RankingRowPalette : ScriptableObject
    {
        [SerializeField] private Color gold = new Color(1f, 0.84f, 0.20f);
        [SerializeField] private Color silver = new Color(0.78f, 0.80f, 0.84f);
        [SerializeField] private Color bronze = new Color(0.80f, 0.52f, 0.25f);
        [SerializeField] private Color upper = Color.white;
        [SerializeField] private Color normal = Color.white;
        [SerializeField] private Color atRisk = new Color(1f, 0.65f, 0.20f);   // 警告（暖色）
        [SerializeField] private Color doomed = new Color(0.85f, 0.15f, 0.15f); // 脱落確定（強い警告色）
        [SerializeField] private Color dead = new Color(0.4f, 0.4f, 0.4f);      // 脱落済み

        public Color Of(RankingRowTone tone)
        {
            switch (tone)
            {
                case RankingRowTone.Gold:
                    return gold;
                case RankingRowTone.Silver:
                    return silver;
                case RankingRowTone.Bronze:
                    return bronze;
                case RankingRowTone.Upper:
                    return upper;
                case RankingRowTone.Normal:
                    return normal;
                case RankingRowTone.AtRisk:
                    return atRisk;
                case RankingRowTone.Doomed:
                    return doomed;
                case RankingRowTone.Dead:
                    return dead;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tone), tone, null);
            }
        }
    }
}
