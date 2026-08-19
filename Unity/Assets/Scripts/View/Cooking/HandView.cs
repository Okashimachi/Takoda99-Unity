// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md（企画書 1, 2, 3番）
// 千枚通しを持つ手。打鍵ごとの縦揺れ（2番）とミス時の横揺れ（3番）だけを持つ。
//
// 待機時の呼吸（1番）は企画判断で実装しない。
// 打鍵の正誤判定はしない（Renderer から結果を受け取るだけ）。

using System.Collections;
using UnityEngine;

namespace Takoda99.View.Cooking
{
    /// <summary>
    /// 手の演出。root/.../MainStore/HandRoot にアタッチする。
    /// 定位置は HandRoot が持ち、揺れは <c>pivot</c>（HandPivot）の anchoredPosition だけを動かす。
    /// 画像の左右反転が要る場合は Hand（pivot の子）の Scale X を -1 にする。pivot の符号には影響しない。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private CookingAnimationSettings settings;

        [Tooltip("揺れの対象。HandRoot 直下の HandPivot を割り当てる。")]
        [SerializeField] private RectTransform pivot;

        /// <summary>揺れの基準位置。Awake 時の pivot の位置を原点として覚える。</summary>
        private Vector2 restPosition;

        private Coroutine playing;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(HandView)}.{nameof(settings)} が未割り当てです。手の演出は動きません。", this);
            }

            if (pivot == null)
            {
                Debug.LogError($"{nameof(HandView)}.{nameof(pivot)} が未割り当てです。手の演出は動きません。", this);
                return;
            }

            restPosition = pivot.anchoredPosition;
        }

        /// <summary>打鍵ごとの反応（2番）。縦に小さく1往復する。</summary>
        public void PlayKeyReaction()
        {
            if (settings == null || pivot == null)
            {
                return;
            }

            Play(Shake(
                new Vector2(0f, settings.HandKeyOffsetY),
                CookingAnimationSettings.ToSeconds(settings.HandKeyDurationMs),
                returnThroughOpposite: false));
        }

        /// <summary>
        /// ミス反応（3番）。横に小さく1往復する。
        /// 企画書どおり通常反応（2番）より優先し、同時に起きたらこちらだけを流す
        /// （<see cref="Play"/> が再生中のコルーチンを止めるため、後から呼ぶ側が勝つ。
        /// Renderer は Miss のとき <see cref="PlayKeyReaction"/> を呼ばない）。
        /// </summary>
        public void PlayMissReaction()
        {
            if (settings == null || pivot == null)
            {
                return;
            }

            Play(Shake(
                new Vector2(-settings.HandMissOffsetX, 0f),
                CookingAnimationSettings.ToSeconds(settings.HandMissDurationMs),
                returnThroughOpposite: true));
        }

        private void Play(IEnumerator routine)
        {
            if (playing != null)
            {
                StopCoroutine(playing);
                pivot.anchoredPosition = restPosition;
            }

            playing = StartCoroutine(routine);
        }

        /// <summary>
        /// 基準位置から <paramref name="offset"/> まで動いて戻る。
        /// <paramref name="returnThroughOpposite"/> が true なら反対側まで振ってから中央へ戻る
        /// （企画書 3番の「左28ms・右28ms・中央14ms」＝ 2:2:1 の配分）。
        /// </summary>
        private IEnumerator Shake(Vector2 offset, float duration, bool returnThroughOpposite)
        {
            if (duration <= 0f)
            {
                pivot.anchoredPosition = restPosition;
                playing = null;
                yield break;
            }

            if (returnThroughOpposite)
            {
                var unit = duration / 5f;
                yield return Move(restPosition, restPosition + offset, unit * 2f);
                yield return Move(restPosition + offset, restPosition - offset, unit * 2f);
                yield return Move(restPosition - offset, restPosition, unit);
            }
            else
            {
                var half = duration / 2f;
                yield return Move(restPosition, restPosition + offset, half);
                yield return Move(restPosition + offset, restPosition, half);
            }

            pivot.anchoredPosition = restPosition;
            playing = null;
        }

        private IEnumerator Move(Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                pivot.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                if (t > 1f)
                {
                    t = 1f;
                }

                // ease-out（企画書 2番）。短い尺でも動き出しが見えるようにする。
                pivot.anchoredPosition = Vector2.LerpUnclamped(from, to, 1f - (1f - t) * (1f - t));
                yield return null;
            }

            pivot.anchoredPosition = to;
        }
    }
}
