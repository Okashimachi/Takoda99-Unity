// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md（企画書 1, 2, 3番。ひっくり返しは本実装の追加分）
// 千枚通しを持つ手。打鍵ごとの縦揺れ（2番）・ミス時の横揺れ（3番）・
// 単語を打ち切った瞬間にそのたこ焼きまで動く「ひっくり返し」を持つ。
//
// **「定位置」はお題が変わるたびに動く。** 使う穴が固定ではなく巡回するため（cooking-anim/01
// §4.1）、手を毎回同じ場所へ戻すと、次のお題の穴から離れた位置で打鍵ごとの反応だけが起き続けて
// 不自然になる。ひっくり返し演出の最後に、次のお題が使う穴の位置を新しい定位置として覚え直す。
//
// 待機時の呼吸（1番）は企画判断で実装しない。
// 打鍵の正誤判定はしない（Renderer / TakoyakiStandView から結果を受け取るだけ）。

using System.Collections;
using UnityEngine;

namespace Takoda99.View.Cooking
{
    /// <summary>
    /// 手の演出。root/.../MainStore/HandRoot にアタッチする。
    /// 起動時の定位置は HandRoot が持つが、ひっくり返し演出のたびにその時点のお題の穴へ動き直す
    /// （<see cref="restPosition"/> が可変。固定値ではない）。揺れは <c>pivot</c>（HandPivot）の
    /// anchoredPosition だけを動かす。画像の左右反転が要る場合は Hand（pivot の子）の Scale X を
    /// -1 にする。pivot の符号には影響しない。
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private CookingAnimationSettings settings;

        [Tooltip("揺れの対象。HandRoot 直下の HandPivot を割り当てる。")]
        [SerializeField] private RectTransform pivot;

        [Tooltip("手の画像本体（pivot の子）。ひっくり返し演出で「手の左下角」をたこ焼きへ合わせる際の、手の大きさの参照に使う。未割り当てなら中心を合わせる。")]
        [SerializeField] private RectTransform handImage;

        /// <summary>
        /// 揺れの基準位置。Awake 時は pivot の起動時位置（HandRoot が定める待機位置）。
        /// 以後はひっくり返し演出のたびに「次のお題が使う穴」の位置へ更新される。
        /// </summary>
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

        /// <summary>
        /// ひっくり返し演出。単語を打ち切った瞬間、手の左下角が <paramref name="completedSlotRect"/>
        /// （打ち終えたたこ焼きの位置）に重なる位置まで動き、続けて <paramref name="nextSlotRect"/>
        /// （次のお題が使う穴）まで動く。**そこで止まり、以後はそこが新しい定位置になる**
        /// （元の位置へは戻らない）。<paramref name="nextSlotRect"/> が null なら起動時の定位置へ戻る。
        /// 通常反応・ミス反応より後に呼ばれた場合はそちらを上書きする（<see cref="Play"/> と同じ規則）。
        /// </summary>
        public void PlayFlipReaction(RectTransform completedSlotRect, RectTransform nextSlotRect)
        {
            if (settings == null || pivot == null || completedSlotRect == null)
            {
                return;
            }

            Play(Flip(completedSlotRect, nextSlotRect, CookingAnimationSettings.ToSeconds(settings.HandFlipDurationMs)));
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

        /// <summary>
        /// 打ち終えた穴まで動いてひっくり返し、続けて次のお題の穴へ動く。
        /// 最後にいた位置（次のお題の穴、無ければ起動時の定位置）を新しい <see cref="restPosition"/> にする。
        /// </summary>
        private IEnumerator Flip(RectTransform completedSlotRect, RectTransform nextSlotRect, float duration)
        {
            var home = nextSlotRect != null ? ResolveFlipTarget(nextSlotRect) : restPosition;

            if (duration <= 0f)
            {
                pivot.anchoredPosition = home;
                restPosition = home;
                playing = null;
                yield break;
            }

            var completed = ResolveFlipTarget(completedSlotRect);
            var half = duration / 2f;
            yield return Move(restPosition, completed, half);
            yield return Move(completed, home, half);

            pivot.anchoredPosition = home;
            restPosition = home;
            playing = null;
        }

        /// <summary>
        /// 対象の穴の位置を、pivot の親（HandRoot）のローカル座標に変換し、
        /// 手の左下角がそこに来るよう、手の半サイズぶんだけ右上へずらす。
        /// </summary>
        private Vector2 ResolveFlipTarget(RectTransform targetSlotRect)
        {
            var parent = pivot.parent as RectTransform;
            if (parent == null)
            {
                return restPosition;
            }

            var world = targetSlotRect.TransformPoint(targetSlotRect.rect.center);
            var local = (Vector2)parent.InverseTransformPoint(world);

            if (handImage != null)
            {
                var half = handImage.rect.size * 0.5f;
                local += half;
            }

            return local;
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
