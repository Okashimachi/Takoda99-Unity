// 仕様書: Unity/docs/.sdd/match-view/05-patience-timer.md §2.1
// 我慢ゲージの3段階の色をまとめた ScriptableObject。段階の数は3で固定し、差し替えるのは色と閾値だけ。
// 段階がいつ成立するかの規則は PatienceGaugeState 側が持つ（ここは演出の実値だけを持つ）。

using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.Timer
{
    /// <summary>我慢ゲージのカラーパレット（05-patience-timer.md §2.1）。</summary>
    [CreateAssetMenu(fileName = "PatienceGaugePalette", menuName = "Takoda99/Patience Gauge Palette")]
    public sealed class PatienceGaugePalette : ScriptableObject
    {
        [Tooltip("余裕（残量が「注意」閾値以上）のときのバー色。")]
        [SerializeField] private Color _safe = new Color(0.247f, 0.749f, 0.373f, 1f);

        [Tooltip("注意（残量が「危険」閾値以上、「注意」閾値未満）のときのバー色。")]
        [SerializeField] private Color _caution = new Color(1f, 0.596f, 0f, 1f);

        [Tooltip("危険（残量が「危険」閾値未満）のときのバー色。")]
        [SerializeField] private Color _danger = new Color(0.898f, 0.224f, 0.208f, 1f);

        [Tooltip("ここを下回ると「注意」色に変わる残量比。")]
        [Range(0f, 1f)]
        [SerializeField] private float _cautionThreshold = 0.5f;

        [Tooltip("ここを下回ると「危険」色に変わる残量比。「注意」閾値より大きくはできない。")]
        [Range(0f, 1f)]
        [SerializeField] private float _dangerThreshold = 0.25f;

        /// <summary>段階分類に渡す閾値。</summary>
        public PatienceGaugeThresholds Thresholds => new PatienceGaugeThresholds(_cautionThreshold, _dangerThreshold);

        private void OnValidate()
        {
            // 逆転すると「注意」の帯が消えて3段階が2段階に潰れる。
            if (_dangerThreshold > _cautionThreshold)
            {
                _dangerThreshold = _cautionThreshold;
            }
        }

        /// <summary>段階に対応する色を返す。</summary>
        public Color Resolve(PatienceGaugeStage stage)
        {
            switch (stage)
            {
                case PatienceGaugeStage.Safe: return _safe;
                case PatienceGaugeStage.Caution: return _caution;
                case PatienceGaugeStage.Danger: return _danger;
                default: return _safe;
            }
        }
    }
}
