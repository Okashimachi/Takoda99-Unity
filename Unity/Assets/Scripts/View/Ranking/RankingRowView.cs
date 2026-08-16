// 仕様書: Unity/docs/.sdd/ranking-view/01-ranking-panel.md §2
// ランキング1行（順位・名前・スコア）。ランキングパネル・秒読みパネル・観戦画面で共用する。

using DG.Tweening;
using TMPro;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Ranking
{
    /// <summary>1行。順位・名前・スコアの3点セット。</summary>
    public sealed class RankingRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;

        [Header("見た目（value-objects/12）")]
        [SerializeField] private Image panelImage;

        [Header("強調・減光")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject selfHighlight;

        /// <summary>脱落済みの行の不透明度。**リストからは消さない**（順位は確定値として並び続ける）。</summary>
        [SerializeField] private float deadAlpha = 0.4f;

        private RankingRowViewState current;
        private bool hasCurrent;

        private RankingRowStyle currentStyle;
        private bool hasStyle;

        // 06 §4.5 E1/E2: 位置・スケール・寸法はいずれも RectTransform を DOTween の target として共有する。
        // target 単位の DOKill では3つまとめて消えてしまうため、Tween の参照を保持して**個別に**Kill する。
        private Tween moveTween;
        private Tween scaleTween;
        private Tween sizeTween;

        /// <summary>この行が今どの店を描いているか。プールの引き当てに使う。</summary>
        public string StoreId => hasCurrent ? current.StoreId : null;

        public void SetState(RankingRowViewState state)
        {
            // 99行のリストで「値が変わった行だけ TMP を更新する」ための早期リターン
            // （ranking-view/03 §4 P2）。WebGL では 1〜2Hz でも無視できない差になる。
            if (hasCurrent && current.Equals(state))
            {
                return;
            }

            current = state;
            hasCurrent = true;

            if (rankText != null)
            {
                rankText.text = state.RankText;
            }

            if (nameText != null)
            {
                nameText.text = state.NameText;
            }

            if (scoreText != null)
            {
                scoreText.text = state.ScoreText;
            }

            if (selfHighlight != null)
            {
                selfHighlight.SetActive(state.IsSelf);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = state.IsAlive ? 1f : deadAlpha;
            }
        }

        /// <summary>スコア欄を持たない用途（脱落予定リスト等）で、名前だけを差し替える。</summary>
        public void SetNameOnly(string storeId, string displayName)
        {
            SetState(RankingRowViewState.SelfOnly(storeId, 0, 0, displayName));

            if (rankText != null)
            {
                rankText.text = string.Empty;
            }

            if (scoreText != null)
            {
                scoreText.text = string.Empty;
            }

            if (selfHighlight != null)
            {
                selfHighlight.SetActive(false);
            }
        }

        /// <summary>
        /// 目標位置へ動かす（06 §4.2）。duration &lt;= 0 で即時。
        /// E1/E2: 既存の移動 Tween だけを現在値保持で Kill してから張り直す（寸法・スケールは巻き込まない）。
        /// </summary>
        public void MoveTo(Vector2 target, float duration)
        {
            if (!(transform is RectTransform rect))
            {
                return;
            }

            KillTween(ref moveTween);

            if (duration <= 0f)
            {
                rect.anchoredPosition = target;
                return;
            }

            moveTween = rect.DOAnchorPos(target, duration).SetEase(Ease.OutCubic);
        }

        /// <summary>この行がすでに目標位置に居るか（06 §4.1「index が変わっていない行に Tween を張らない」判定用）。</summary>
        public bool IsAt(Vector2 target)
        {
            return transform is RectTransform rect
                && (rect.anchoredPosition - target).sqrMagnitude <= 0.01f;
        }

        /// <summary>順位が変わった行の強調（06 §4.2）。1.0 → scale → 1.0 の往復。</summary>
        public void Emphasize(float scale, float duration)
        {
            if (!(transform is RectTransform rect))
            {
                return;
            }

            // E2: 前回の強調が途中でも、必ず等倍から始め直す（拡大したまま残さない）。
            KillTween(ref scaleTween);
            rect.localScale = Vector3.one;

            if (scale <= 1f || duration <= 0f)
            {
                return;
            }

            var half = duration * 0.5f;
            scaleTween = DOTween.Sequence()
                .SetTarget(rect)
                .Append(rect.DOScale(scale, half))
                .Append(rect.DOScale(1f, half));
        }

        /// <summary>
        /// 強調を打ち切って等倍へ戻す（06 §4.5 E2）。
        /// 強調中に「その行は動かない」Apply が来ても、拡大したまま残る行を作らないための保険。
        /// </summary>
        public void ResetScale()
        {
            if (!(transform is RectTransform rect))
            {
                return;
            }

            // 何も起きていないなら触らない（毎 Apply の無駄な書き込みを避ける）。
            if (scaleTween == null && rect.localScale == Vector3.one)
            {
                return;
            }

            KillTween(ref scaleTween);
            rect.localScale = Vector3.one;
        }

        /// <summary>
        /// 見た目を適用する（ranking-view/04 §5.4）。duration &lt;= 0 で即時。
        /// S2: 前回と同じ RankingRowStyle なら何もしない（毎フレームの Tween 張り直しを防ぐ）。
        /// </summary>
        public void SetStyle(RankingRowStyle style, RankingRowPalette palette, float duration)
        {
            if (hasStyle && currentStyle.Equals(style))
            {
                return;
            }

            hasStyle = true;
            currentStyle = style;

            var color = palette != null ? palette.Of(style.Tone) : Color.white;
            var rect = transform as RectTransform;

            // S3: RectTransform（sizeDelta）ぶんは、位置・スケールを巻き込まないよう参照で個別に Kill する。
            KillTween(ref sizeTween);

            if (duration <= 0f)
            {
                if (rect != null)
                {
                    rect.sizeDelta = style.Size;
                }

                if (panelImage != null)
                {
                    panelImage.DOKill();
                    panelImage.color = color;
                }

                ApplyFontSizeImmediate(rankText, style.RankFontSize);
                ApplyFontSizeImmediate(nameText, style.NameFontSize);
                ApplyFontSizeImmediate(scoreText, style.ScoreFontSize);

                ApplyTextLayoutImmediate(rankText, style.RankOffset, style.RankSize);
                ApplyTextLayoutImmediate(nameText, style.NameOffset, style.NameSize);
                ApplyTextLayoutImmediate(scoreText, style.ScoreOffset, style.ScoreSize);
                return;
            }

            if (rect != null)
            {
                sizeTween = rect.DOSizeDelta(style.Size, duration).SetEase(Ease.OutCubic);
            }

            if (panelImage != null)
            {
                panelImage.DOKill();
                panelImage.DOColor(color, duration);
            }

            // S4: フォントサイズの補間は DOTween.To で TMP_Text.fontSize を動かす（Auto Size は使わない）。
            TweenFontSize(rankText, style.RankFontSize, duration);
            TweenFontSize(nameText, style.NameFontSize, duration);
            TweenFontSize(scoreText, style.ScoreFontSize, duration);

            // パネルの Size だけでなく、各テキストの位置・幅も順位段階に合わせて動かす。
            TweenTextLayout(rankText, style.RankOffset, style.RankSize, duration);
            TweenTextLayout(nameText, style.NameOffset, style.NameSize, duration);
            TweenTextLayout(scoreText, style.ScoreOffset, style.ScoreSize, duration);
        }

        private static void ApplyTextLayoutImmediate(TMP_Text text, Vector2 offset, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.DOKill();
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void TweenTextLayout(TMP_Text text, Vector2 offset, Vector2 size, float duration)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.DOKill();
            rect.DOAnchorPos(offset, duration).SetEase(Ease.OutCubic);
            rect.DOSizeDelta(size, duration).SetEase(Ease.OutCubic);
        }

        private static void ApplyFontSizeImmediate(TMP_Text text, float size)
        {
            if (text == null || size <= 0f)
            {
                return;
            }

            text.DOKill();
            text.fontSize = size;
        }

        private static void TweenFontSize(TMP_Text text, float size, float duration)
        {
            if (text == null || size <= 0f)
            {
                return;
            }

            text.DOKill();
            DOTween.To(() => text.fontSize, v => text.fontSize = v, size, duration)
                .SetTarget(text)
                .SetEase(Ease.OutCubic);
        }

        /// <summary>
        /// 保持している Tween を現在値のまま止める（06 §4.5 E2: complete: false）。
        /// DOTween は完了した Tween を内部プールへ返すため、Kill 前に <see cref="Tween.IsActive"/> で
        /// 生きているものだけを対象にする（使い回された別 Tween を巻き添えにしない）。
        /// </summary>
        private static void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill(false);
            }

            tween = null;
        }

        /// <summary>プールへ戻すときに呼ぶ。次に使い回すとき確実に描き直されるようにする。</summary>
        public void Recycle()
        {
            hasCurrent = false;
            hasStyle = false;

            moveTween = null;
            scaleTween = null;
            sizeTween = null;

            var rect = transform as RectTransform;
            rect?.DOKill();
            panelImage?.DOKill();
            rankText?.DOKill();
            nameText?.DOKill();
            scoreText?.DOKill();

            // ApplyTextLayoutImmediate/TweenTextLayout は TMP_Text ではなく rectTransform を target にするため、
            // 上の DOKill(text) だけでは殺せない。放置すると使い回した行が前の入れ替え中の位置から動き出す。
            rankText?.rectTransform.DOKill();
            nameText?.rectTransform.DOKill();
            scoreText?.rectTransform.DOKill();

            // 強調の途中で戻された行が、次に使い回されたとき拡大したまま出てこないようにする。
            if (rect != null)
            {
                rect.localScale = Vector3.one;
            }

            gameObject.SetActive(false);
        }
    }
}
