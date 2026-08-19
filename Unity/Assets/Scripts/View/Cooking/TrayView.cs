// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md（企画書 8番の着地先・9番）
// 舟皿。注文ぶんの玉が一斉に飛んできたあと、盛り付け済みの絵に切り替えて提供する。
//
// **玉を1個ずつ皿の上に並べない。** 盛り付けは1枚絵で表し、出来（きれい/ふつう/汚い）で差し替える。
// 提供の確定はしない（サーバー権威。ここは見た目だけ）。出来の計算もしない
// （TakoyakiStandView が打鍵ミス率から決めて渡す）。

using System.Collections;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Cooking
{
    /// <summary>舟皿の見た目。root/.../MainStore/TrayRoot にアタッチする。</summary>
    public sealed class TrayView : MonoBehaviour
    {
        [SerializeField] private CookingAnimationSettings settings;

        [Tooltip("提供演出でまとめて動かす対象。TrayRoot 自身の CanvasGroup を割り当てる。")]
        [SerializeField] private CanvasGroup trayGroup;

        [Tooltip("空の舟皿。素材が未入稿の間は未割り当てでよい（その場合は盛り付け済みの絵だけを出す）。")]
        [SerializeField] private Image trayEmpty;

        [Tooltip("盛り付け済みの舟皿。品質で差し替える。")]
        [SerializeField] private Image trayServed;

        [Header("盛り付け済みスプライト（品質別）")]
        [SerializeField] private Sprite trayCleanSprite;
        [SerializeField] private Sprite trayNormalSprite;
        [SerializeField] private Sprite trayDirtySprite;

        [Tooltip("玉が飛んでくる着地点。未割り当てなら皿の中心を使う。")]
        [SerializeField] private RectTransform landingPoint;

        /// <summary>この皿の出来。<see cref="Serve"/> で受け取った値。</summary>
        public TakoyakiQuality Quality { get; private set; }

        /// <summary>提供演出の再生中か。次の客の仕切り直しは、これが終わるまで待つ。</summary>
        public bool IsServing => serving != null;

        private RectTransform selfRect;
        private Vector2 restPosition;
        private Vector3 restScale;
        private Coroutine serving;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();

            if (settings == null)
            {
                Debug.LogError($"{nameof(TrayView)}.{nameof(settings)} が未割り当てです。提供演出は出ません。", this);
            }

            if (trayGroup == null)
            {
                trayGroup = GetComponent<CanvasGroup>();
                if (trayGroup == null)
                {
                    trayGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (selfRect != null)
            {
                restPosition = selfRect.anchoredPosition;
                restScale = selfRect.localScale;
            }
        }

        private void Start()
        {
            ResetTray();
        }

        /// <summary>
        /// 提供（企画書 9番）。盛り付け済みの絵へ切り替え、余韻を置いてから皿を送り出し、
        /// 空の皿を出し直す。<paramref name="quality"/> は 1注文ぶんの打鍵ミス率から決まった出来。
        /// </summary>
        public void Serve(TakoyakiQuality quality)
        {
            Quality = quality;

            if (settings == null || selfRect == null)
            {
                ResetTray();
                return;
            }

            if (serving != null)
            {
                StopCoroutine(serving);
            }

            serving = StartCoroutine(ServeRoutine());
        }

        /// <summary>皿を空の状態に戻す（客が入れ替わった・試合が始まった等の区切りで呼ぶ）。</summary>
        public void ResetTray()
        {
            if (serving != null)
            {
                StopCoroutine(serving);
                serving = null;
            }

            Quality = TakoyakiQuality.Clean;

            if (selfRect != null)
            {
                selfRect.anchoredPosition = restPosition;
                selfRect.localScale = restScale;
            }

            if (trayGroup != null)
            {
                trayGroup.alpha = 1f;
            }

            SetVisible(trayEmpty, true);
            SetVisible(trayServed, false);
        }

        private IEnumerator ServeRoutine()
        {
            // 玉が乗った直後に盛り付け済みの絵へ切り替える。
            yield return ApplyServedSprite();

            // 着地の余韻（企画書 9番）。
            yield return new WaitForSeconds(CookingAnimationSettings.ToSeconds(settings.ServeDelayMs));

            var serveDuration = CookingAnimationSettings.ToSeconds(settings.ServeMs);
            var overlap = CookingAnimationSettings.ToSeconds(settings.TrayCrossOverlapMs);

            var from = restPosition;
            var to = restPosition + settings.ServeSlideOffset;
            var toScale = restScale * settings.ServeEndScale;

            // 皿は1つを使い回すため、消えきる時刻を終了より overlap だけ手前に置く。
            // その瞬間から新しい皿のフェードインが始まり、スライドの残り overlap と重なる
            // （企画書 9番の「50ms 重ねてクロス」を、オブジェクトを増やさずに満たす）。
            var fadeOutDuration = serveDuration - overlap;
            if (fadeOutDuration <= 0f)
            {
                fadeOutDuration = serveDuration;
            }

            var elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / fadeOutDuration;
                if (t > 1f)
                {
                    t = 1f;
                }

                selfRect.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOut(t));
                selfRect.localScale = Vector3.LerpUnclamped(restScale, toScale, EaseOut(t));
                trayGroup.alpha = 1f - t;
                yield return null;
            }

            // 提供しきったので、この皿を空に戻してからフェードインで出し直す。
            Quality = TakoyakiQuality.Clean;
            SetVisible(trayEmpty, true);
            SetVisible(trayServed, false);
            selfRect.anchoredPosition = restPosition;
            selfRect.localScale = restScale;

            var fadeIn = CookingAnimationSettings.ToSeconds(settings.TrayFadeInMs);
            elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / fadeIn;
                trayGroup.alpha = t > 1f ? 1f : t;
                yield return null;
            }

            trayGroup.alpha = 1f;
            serving = null;
        }

        /// <summary>盛り付け済みの絵をフェードインで出す。空の皿はその裏で消す。</summary>
        private IEnumerator ApplyServedSprite()
        {
            if (trayServed == null)
            {
                yield break;
            }

            var sprite = ResolveTraySprite(Quality);
            if (sprite != null)
            {
                trayServed.sprite = sprite;
            }

            SetVisible(trayServed, true);
            SetVisible(trayEmpty, false);

            var duration = CookingAnimationSettings.ToSeconds(settings.TrayServedFadeMs);
            var color = trayServed.color;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                color.a = t > 1f ? 1f : t;
                trayServed.color = color;
                yield return null;
            }

            color.a = 1f;
            trayServed.color = color;
        }

        private Sprite ResolveTraySprite(TakoyakiQuality quality)
        {
            switch (quality)
            {
                case TakoyakiQuality.Clean:
                    return trayCleanSprite;
                case TakoyakiQuality.Normal:
                    return trayNormalSprite;
                default:
                    return trayDirtySprite;
            }
        }

        /// <summary>飛んできた玉の着地先。専用の目印が無ければ皿自身を返す。</summary>
        public RectTransform ResolveLandingRect() => landingPoint != null ? landingPoint : selfRect;

        private static void SetVisible(Image image, bool visible)
        {
            if (image != null)
            {
                image.gameObject.SetActive(visible);
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
