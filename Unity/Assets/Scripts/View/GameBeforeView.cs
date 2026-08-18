// 仕様書: Unity/docs/.sdd/match-view/08-game-before.md
// 試合シーンへ移ってから実際に打ち始めるまでの待機（GameBefore/CountDownPanel）。
// カウントダウンは表示専用で、試合の開始を決めるのはサーバー（MatchStart）である。

using System;
using Takoda99.Sound;
using TMPro;
using UnityEngine;

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

            gameObject.SetActive(true);
            ApplyText();
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
    }
}
