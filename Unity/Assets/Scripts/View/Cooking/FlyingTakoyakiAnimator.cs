// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md（企画書 8番）
// 完成したたこ焼きが窪みから舟皿へ弧を描いて移動する演出。
//
// 窪みの子のままアニメさせると行オブジェクトの前後関係で他の穴に潜り込むため、
// 飛行中の玉だけをこのレイヤー（FlyLayer）で預かる。
// どの玉を飛ばすか・どこへ着地させるかは決めない（TakoyakiStandView が渡す）。

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Cooking
{
    /// <summary>窪み→舟皿の弧移動。root/.../MainStore/FlyLayer にアタッチする。</summary>
    public sealed class FlyingTakoyakiAnimator : MonoBehaviour
    {
        [SerializeField] private CookingAnimationSettings settings;

        private RectTransform selfRect;

        /// <summary>使い回す玉の入れ物。1単語ごとに Instantiate/Destroy しないための貯め置き。</summary>
        private readonly Stack<RectTransform> pool = new Stack<RectTransform>();

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();

            if (settings == null)
            {
                Debug.LogError($"{nameof(FlyingTakoyakiAnimator)}.{nameof(settings)} が未割り当てです。玉が飛びません。", this);
            }

            if (selfRect == null)
            {
                Debug.LogError($"{nameof(FlyingTakoyakiAnimator)}: RectTransform が必要です（Canvas 配下に置いてください）。", this);
            }
        }

        /// <summary>
        /// <paramref name="from"/> の位置に見えている玉を <paramref name="to"/> へ飛ばす。
        /// 着地した瞬間に <paramref name="onLanded"/> を呼ぶ。
        /// 注文ぶんを一斉に盛るため、**同時に複数が飛ぶ**。玉は使い回すので個数ぶん生成はしない。
        /// </summary>
        public void Fly(Sprite sprite, RectTransform from, RectTransform to, Action onLanded)
        {
            if (settings == null || selfRect == null || from == null || to == null)
            {
                onLanded?.Invoke();
                return;
            }

            var ball = Rent(sprite, from);
            StartCoroutine(FlyRoutine(ball, from, to, onLanded));
        }

        private RectTransform Rent(Sprite sprite, RectTransform from)
        {
            RectTransform ball;
            if (pool.Count > 0)
            {
                ball = pool.Pop();
                ball.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("FlyingTakoyaki", typeof(RectTransform), typeof(Image));
                ball = go.GetComponent<RectTransform>();
                ball.SetParent(selfRect, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
            }

            var ballImage = ball.GetComponent<Image>();
            ballImage.sprite = sprite;
            ballImage.enabled = sprite != null;

            // 見た目を窪みの玉に合わせる。アンカーを中央に寄せて anchoredPosition だけで動かせるようにする。
            ball.anchorMin = new Vector2(0.5f, 0.5f);
            ball.anchorMax = new Vector2(0.5f, 0.5f);
            ball.pivot = new Vector2(0.5f, 0.5f);
            ball.sizeDelta = from.rect.size;
            ball.localScale = Vector3.one;
            ball.anchoredPosition = ToLocal(from);
            ball.SetAsLastSibling();

            return ball;
        }

        private IEnumerator FlyRoutine(RectTransform ball, RectTransform from, RectTransform to, Action onLanded)
        {
            var start = ToLocal(from);
            var end = ToLocal(to);

            var rise = CookingAnimationSettings.ToSeconds(settings.FlyRiseMs);
            var arc = CookingAnimationSettings.ToSeconds(settings.FlyArcMs);
            var land = CookingAnimationSettings.ToSeconds(settings.FlyLandMs);

            // 頂点の高さは窪みの高さに対する倍率で決める（企画書 8番）。
            var apexHeight = from.rect.height * settings.FlyApexHeightScale;
            var apex = new Vector2(start.x, start.y + apexHeight);

            // 上昇：窪みから真上へ浮き上がる。
            yield return Move(ball, start, apex, rise, EaseOut);

            // 弧移動：頂点から皿の手前上空まで、山なりに滑る。
            var overTarget = new Vector2(end.x, end.y + apexHeight * 0.5f);
            yield return MoveArc(ball, apex, overTarget, apexHeight * 0.25f, arc);

            // 着地：落ちて収まる。
            yield return Move(ball, overTarget, end, land, EaseIn);

            ball.gameObject.SetActive(false);
            pool.Push(ball);

            onLanded?.Invoke();
        }

        /// <summary>他の RectTransform の中心を、このレイヤーのローカル座標へ変換する。</summary>
        private Vector2 ToLocal(RectTransform target)
        {
            var world = target.TransformPoint(target.rect.center);
            return selfRect.InverseTransformPoint(world);
        }

        private IEnumerator Move(RectTransform ball, Vector2 from, Vector2 to, float duration, Func<float, float> ease)
        {
            if (duration <= 0f)
            {
                ball.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Normalize(elapsed, duration);
                ball.anchoredPosition = Vector2.LerpUnclamped(from, to, ease(t));
                yield return null;
            }

            ball.anchoredPosition = to;
        }

        /// <summary>直線移動に上向きの膨らみ（<paramref name="bulge"/>）を足して山なりにする。</summary>
        private IEnumerator MoveArc(RectTransform ball, Vector2 from, Vector2 to, float bulge, float duration)
        {
            if (duration <= 0f)
            {
                ball.anchoredPosition = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Normalize(elapsed, duration);

                var linear = Vector2.LerpUnclamped(from, to, t);
                // 4t(1-t) は t=0,1 で 0、t=0.5 で 1 になる山。
                linear.y += bulge * 4f * t * (1f - t);
                ball.anchoredPosition = linear;
                yield return null;
            }

            ball.anchoredPosition = to;
        }

        private static float Normalize(float elapsed, float duration)
        {
            var t = elapsed / duration;
            return t > 1f ? 1f : t;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseIn(float t) => t * t;
    }
}
