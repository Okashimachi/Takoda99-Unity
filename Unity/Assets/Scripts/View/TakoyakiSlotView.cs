// 仕様書: Unity/docs/.sdd/match-view/03-takoyaki-stand-view.md
//         Unity/docs/.sdd/cooking-anim/01-cooking-animation.md（企画書 5, 6番）
//
// 状態は Empty / Batter / Cooked の3つだけ。**打鍵の出来では見た目が変わらない**
// （盛り付けの出来は舟皿だけが表す。cooking-anim/01 §4.3）。
// たこ焼き1個ぶんの穴の見た目。Assets/MainStoreCanvas/Takoyaki.prefab にアタッチする。
//
// 自分がどの状態になるべきかは判断しない（TakoyakiStandView が決める）。
// ここは「言われた状態へ、言われた尺で見た目を変える」だけを持つ。

using System.Collections;
using Takoda99.View.Cooking;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>たこ焼き1個ぶんの穴の見た目。Assets/MainStoreCanvas/Takoyaki.prefab にアタッチする。</summary>
    public sealed class TakoyakiSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject raw;   // TakoyakiRaw（生地）
        [SerializeField] private GameObject done;  // TakoyakiDone（焼き）

        public TakoyakiSlotState State { get; private set; }

        private CookingAnimationSettings settings;

        private Image doneImage;
        private CanvasGroup rawGroup;
        private CanvasGroup doneGroup;
        private RectTransform rawRect;
        private RectTransform selfRect;

        /// <summary>生地の基準スケール。投入アニメで触るため、Awake 時の値を原点として覚える。</summary>
        private Vector3 rawRestScale = Vector3.one;

        private Coroutine playing;

        /// <summary>焼き上がりの見た目（飛ばす玉の複製元）。</summary>
        public Sprite CookedSprite => doneImage != null ? doneImage.sprite : null;

        /// <summary>焼き上がりの矩形。飛行の開始位置・サイズに使う。</summary>
        public RectTransform CookedRect => doneImage != null ? doneImage.rectTransform : null;

        /// <summary>この穴そのものの矩形。「そのたこ焼きの位置」として、手のひっくり返し演出の目印に使う。</summary>
        public RectTransform SlotRect => selfRect;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();

            if (raw == null)
            {
                Debug.LogError($"{nameof(TakoyakiSlotView)}.{nameof(raw)} が未設定です。", this);
            }
            else
            {
                rawRect = raw.GetComponent<RectTransform>();
                rawGroup = EnsureCanvasGroup(raw);
                if (rawRect != null)
                {
                    rawRestScale = rawRect.localScale;
                }
            }

            if (done == null)
            {
                Debug.LogError($"{nameof(TakoyakiSlotView)}.{nameof(done)} が未設定です。", this);
            }
            else
            {
                doneImage = done.GetComponent<Image>();
                doneGroup = EnsureCanvasGroup(done);
            }
        }

        private void Start()
        {
            SetState(TakoyakiSlotState.Empty);
        }

        /// <summary>調整値を渡す。<see cref="TakoyakiStandView"/> が Awake で全穴へ配る。</summary>
        public void Bind(CookingAnimationSettings boundSettings)
        {
            settings = boundSettings;
        }

        /// <summary>アニメーションなしで状態を切り替える。台の一括再適用（穴数の増減など）に使う。</summary>
        public void SetState(TakoyakiSlotState state)
        {
            StopPlaying();
            State = state;

            switch (state)
            {
                case TakoyakiSlotState.Empty:
                    Show(raw, rawGroup, false);
                    Show(done, doneGroup, false);
                    break;
                case TakoyakiSlotState.Batter:
                    Show(raw, rawGroup, true);
                    Show(done, doneGroup, false);
                    break;
                case TakoyakiSlotState.Cooked:
                    // Cooked で生地パネルを消してから焼きパネルを出す（重ね表示にしない）。
                    Show(raw, rawGroup, false);
                    Show(done, doneGroup, true);
                    break;
            }

            ResetTransforms();
        }

        /// <summary>生地投入（企画書 5番）。落ちて広がる。</summary>
        public void PourBatter()
        {
            if (settings == null || raw == null)
            {
                SetState(TakoyakiSlotState.Batter);
                return;
            }

            StopPlaying();
            State = TakoyakiSlotState.Batter;
            Show(done, doneGroup, false);
            playing = StartCoroutine(PourRoutine());
        }

        /// <summary>
        /// 焼き上がり（企画書 6番）。生地→焼きのクロスフェードと、ひっくり返す回転。
        /// **単語を打ち切った瞬間にだけ呼ぶ**（TakoyakiStandView.OnWordCleared）。打鍵の途中経過では呼ばない。
        /// </summary>
        public void Cook()
        {
            if (State == TakoyakiSlotState.Cooked)
            {
                return;
            }

            if (settings == null || raw == null || done == null)
            {
                SetState(TakoyakiSlotState.Cooked);
                return;
            }

            StopPlaying();
            State = TakoyakiSlotState.Cooked;
            playing = StartCoroutine(CookRoutine());
        }

        /// <summary>完成した玉を取り外して穴を空にする（企画書 8番の飛行開始時に呼ぶ）。</summary>
        public void TakeCooked()
        {
            SetState(TakoyakiSlotState.Empty);
        }

        private IEnumerator PourRoutine()
        {
            var fall = CookingAnimationSettings.ToSeconds(settings.BatterFallMs);
            var spread = CookingAnimationSettings.ToSeconds(settings.BatterSpreadMs);

            Show(raw, rawGroup, true);
            SetAlpha(rawGroup, 0f);

            // 落下：上から降ってくるように、大きいスケールから基準へ縮めながら現れる。
            var startScale = rawRestScale * settings.BatterFallStartScale;
            yield return Tween(fall, t =>
            {
                SetAlpha(rawGroup, t);
                SetRawScale(Vector3.LerpUnclamped(startScale, rawRestScale, EaseOut(t)));
            });

            // 広がり：基準を少し越えてから戻す。
            var overshoot = rawRestScale * 1.08f;
            yield return Tween(spread, t =>
            {
                SetRawScale(t < 0.5f
                    ? Vector3.LerpUnclamped(rawRestScale, overshoot, t * 2f)
                    : Vector3.LerpUnclamped(overshoot, rawRestScale, (t - 0.5f) * 2f));
            });

            SetAlpha(rawGroup, 1f);
            SetRawScale(rawRestScale);
            playing = null;
        }

        private IEnumerator CookRoutine()
        {
            var duration = CookingAnimationSettings.ToSeconds(settings.CookedFadeMs);
            var rotation = settings.TakoyakiFlipRotationDegrees;

            Show(done, doneGroup, true);
            SetAlpha(doneGroup, 0f);

            // ひっくり返す動き。1回転して元の向きに戻る（回転が残ると次に並ぶ生地と傾きがずれて見える）。
            yield return Tween(duration, t =>
            {
                SetAlpha(rawGroup, 1f - t);
                SetAlpha(doneGroup, t);
                SetRotation(rotation * t);
            });

            // 重ね表示にしない（match-view/03 §4.1）。焼きが出そろってから生地を消す。
            Show(raw, rawGroup, false);
            SetAlpha(doneGroup, 1f);
            SetRotation(0f);
            playing = null;
        }

        private IEnumerator Tween(float duration, System.Action<float> apply)
        {
            if (duration <= 0f)
            {
                apply(1f);
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

                apply(t);
                yield return null;
            }

            apply(1f);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private void StopPlaying()
        {
            if (playing != null)
            {
                StopCoroutine(playing);
                playing = null;
            }
        }

        private void ResetTransforms()
        {
            SetRawScale(rawRestScale);
            SetAlpha(rawGroup, 1f);
            SetAlpha(doneGroup, 1f);
            SetRotation(0f);
        }

        private void SetRotation(float degrees)
        {
            if (selfRect != null)
            {
                selfRect.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }
        }

        private void SetRawScale(Vector3 scale)
        {
            if (rawRect != null)
            {
                rawRect.localScale = scale;
            }
        }

        private static void Show(GameObject target, CanvasGroup group, bool visible)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(visible);
            if (visible && group != null)
            {
                group.alpha = 1f;
            }
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        /// <summary>
        /// フェードのために CanvasGroup を要求する。Prefab 側に無くても実行時に足す
        /// （Prefab の手入れを増やさないための保険。事前に付けてあればそれを使う）。
        /// </summary>
        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }
    }
}
