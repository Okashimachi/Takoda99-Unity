// 仕様書: Unity/docs/.sdd/match-view/08-game-before.md
// 試合シーンへ移ってから実際に打ち始めるまでの待機（GameBefore/CountDownPanel）。
// カウントダウンは表示専用で、試合の開始を決めるのはサーバー（MatchStart）である。

using System;
using Takoda99.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>
    /// 試合開始前のカウントダウン。数え終わり、かつサーバーの試合開始が届いていれば自分を畳む。
    /// 畳むまでの間は <see cref="IsHolding"/> が true で、<c>Renderer</c> はお題と行列を出さない。
    /// </summary>
    public sealed class GameBeforeView : MonoBehaviour
    {
        [Tooltip("カウントダウンの数値を出すテキスト（CountDownPanel/CountText）。")]
        [SerializeField] private TextMeshProUGUI countText;

        [Tooltip("この画面に移ってから待つ秒数。")]
        [SerializeField] private float countdownSeconds = 5f;

        [Header("ネオンパネル（CountPanel）")]
        [Tooltip("数字を囲むネオンの縁取り（CountDownPanel/CountPanel/NeonFrame）。Images/UI/NeonFrame.png を Sliced・FillCenter オフで敷く。")]
        [SerializeField] private Image neonFrame;

        [Tooltip("パネル全体のフェードに使う（CountDownPanel/CountPanel の CanvasGroup）。")]
        [SerializeField] private CanvasGroup countPanelGroup;

        [Tooltip("拡大の起点（CountDownPanel/CountPanel）。未設定なら拡大しない。")]
        [SerializeField] private RectTransform scaleRoot;

        [Tooltip("5〜2秒台のネオン色。")]
        [SerializeField] private Color neonNormalColor = new Color(0.3f, 0.75f, 1f, 1f);

        [Tooltip("最後の1秒のネオン色。開始が近いことを色で示す。")]
        [SerializeField] private Color neonFinalColor = new Color(1f, 0.55f, 0.15f, 1f);

        [Header("アニメーション")]
        [Tooltip("数字が出はじめる大きさ（1.0 = シーンで組んだ大きさ）。ここから等倍へ広がる。")]
        [SerializeField, Range(0.1f, 1.5f)] private float popStartScale = 1.35f;

        [Tooltip("等倍に届くまでの時間（秒）。1秒より短くする（次の数字に食い込ませない）。")]
        [SerializeField, Range(0.05f, 0.9f)] private float popSeconds = 0.25f;

        [Tooltip("不透明になるまでの時間（秒）。")]
        [SerializeField, Range(0.02f, 0.5f)] private float fadeInSeconds = 0.1f;

        [Tooltip("次の数字へ変わる前に薄くなるまでの時間（秒）。")]
        [SerializeField, Range(0.05f, 0.9f)] private float fadeOutSeconds = 0.25f;

        [Tooltip("ネオンが1秒のあいだに脈打つ深さ（0で脈動なし）。")]
        [SerializeField, Range(0f, 1f)] private float neonPulseDepth = 0.45f;

        /// <summary>ネオンの拡大の起点。<see cref="scaleRoot"/> の authored なスケール。</summary>
        private Vector3 baseScale = Vector3.one;

        private float remainingSec;
        private bool matchStarted;
        private bool finished;

        /// <summary>直近に秒読みSEを鳴らした秒数。数字が変わったフレームだけ鳴らすために持つ。</summary>
        private int lastTickSecond = -1;

        /// <summary>まだ待機中か。true の間、お題と客の行列は出さない。</summary>
        public bool IsHolding => !finished;

        /// <summary>待機が明けた瞬間に1度だけ発火する。<c>Renderer</c> が描き直しの起点に使う。</summary>
        public event Action Finished;

        private void Awake()
        {
            if (countText == null)
            {
                Debug.LogError($"{nameof(GameBeforeView)}.{nameof(countText)} が未設定です。カウントダウンの数値は出ません。", this);
            }

            // 未設定でも自身（GameBeforeCanvas）へフォールバックしない。Canvas ごと拡大すると
            // 背景の暗幕まで一緒に伸び縮みしてしまう。拡大したい枠だけをシーンで指す。
            // 縮んだ値を等倍として覚えないよう、localScale を書き換える前に採る。
            if (scaleRoot != null)
            {
                baseScale = scaleRoot.localScale;
            }

            if (countPanelGroup != null)
            {
                // 開始前の全画面に被さるので、入力は絶対に食わせない。
                countPanelGroup.blocksRaycasts = false;
                countPanelGroup.interactable = false;
            }
        }

        /// <summary>
        /// カウントダウンを開始する。<c>Renderer</c> の結線時に呼ぶ。
        /// GameObject が非アクティブで置かれていても、ここで起こす。
        /// </summary>
        public void Begin()
        {
            remainingSec = countdownSeconds;
            matchStarted = false;
            finished = false;
            lastTickSecond = -1;

            // 試合前半BGM。カウントダウンが始まった瞬間に流す（前半→後半は BgmPlayer 側で自動でつなぐ）。
            BgmPlayer.PlayMatchHalves();

            gameObject.SetActive(true);
            ApplyText();
            ApplyAnimation();
        }

        /// <summary>
        /// サーバーの試合開始（<c>MatchStart</c> 受信＝<c>ClientPhase.InMatch</c> 到達）を伝える。
        /// カウントダウンが 0 になっていても、これが false の間は畳まない。
        /// </summary>
        public void SetMatchStarted(bool started)
        {
            matchStarted = started;
            TryFinish();
        }

        private void Update()
        {
            if (finished)
            {
                return;
            }

            if (remainingSec > 0f)
            {
                remainingSec -= Time.deltaTime;
                ApplyText();
            }

            ApplyAnimation();
            TryFinish();
        }

        /// <summary>数え終わり、かつサーバーの合図が出ていれば畳む。片方だけでは畳まない。</summary>
        private void TryFinish()
        {
            if (finished || remainingSec > 0f || !matchStarted)
            {
                return;
            }

            finished = true;

            // 待機が明ける＝サーバーの試合開始が届いた瞬間。ここが開始の合図。
            SoundPlayer.Play(SoundId.MatchStart);

            gameObject.SetActive(false);
            Finished?.Invoke();
        }

        private void ApplyText()
        {
            if (countText == null)
            {
                return;
            }

            // 5.0秒残っていれば「5」、0.1秒でも残っていれば「1」。0 は畳む直前の一瞬だけ。
            var second = Mathf.CeilToInt(Mathf.Max(remainingSec, 0f));
            countText.text = second.ToString();

            // 数字が変わったフレームだけ1回鳴らす。0（畳む直前の一瞬）では鳴らさない。
            if (second != lastTickSecond)
            {
                lastTickSecond = second;
                if (second > 0)
                {
                    SoundPlayer.Play(SoundId.MatchCountdown);
                }
            }
        }

        /// <summary>
        /// 1秒ぶんの演出。大きいところから等倍へ縮みながら現れ、次の数字へ移る前に薄くなる。
        /// ネオンの縁取りは同じ1秒のあいだに脈打ち、最後の1秒だけ色が変わる。
        ///
        /// <para>
        /// **時間はすべて残り秒数から引く**（自前のタイマーを別に走らせない）ので、
        /// フレームレートが落ちても数字と演出がずれない。
        /// 数え終わって <c>MatchStart</c> を待っている間は、脈動を止めて出しっぱなしにする。
        /// </para>
        /// </summary>
        private void ApplyAnimation()
        {
            // 表示中の数字が出てからの経過（0→1）。remainingSec の小数部の裏返し。
            var counting = remainingSec > 0f;

            // 小数部が 0 のちょうどの秒（Begin 直後の 5.0 秒）は「新しい数字が出た瞬間」。
            // そのまま裏返すと経過 1（＝出し終わり）になり、最初の1つだけ演出が出ない。
            var fraction = remainingSec - Mathf.Floor(remainingSec);
            if (fraction <= 0f)
            {
                fraction = 1f;
            }

            var elapsed = counting ? Mathf.Clamp01(1f - fraction) : 1f;

            if (scaleRoot != null)
            {
                // EaseOut（1-(1-t)^3）。勢いよく縮んでから静かに止まる。
                var t = popSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / popSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                scaleRoot.localScale = baseScale * Mathf.LerpUnclamped(popStartScale, 1f, eased);
            }

            var alpha = 1f;
            if (counting)
            {
                var fadeIn = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInSeconds);

                // 残り時間側から測る。fadeOutSeconds が長くても頭のフェードインを潰さない。
                var fadeOut = fadeOutSeconds <= 0f ? 1f : Mathf.Clamp01((1f - elapsed) / fadeOutSeconds);
                alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
            }

            if (countPanelGroup != null)
            {
                countPanelGroup.alpha = alpha;
            }

            if (neonFrame == null)
            {
                return;
            }

            // 最後の1秒（残り1秒台）だけ色を変え、開始が目前であることを見せる。
            var color = counting && remainingSec <= 1f ? neonFinalColor : neonNormalColor;

            // 1秒で1往復。出た瞬間が最も明るく、次の数字へ向かって落ち着く。
            var pulse = counting
                ? 1f - neonPulseDepth * (1f - Mathf.Cos(elapsed * Mathf.PI * 2f)) * 0.5f
                : 1f;
            color.a *= Mathf.Clamp01(pulse);
            neonFrame.color = color;
        }
    }
}
