// 仕様書: Unity/docs/.sdd/ranking-view/02-cull-countdown-panel.md §6
// 淘汰直前（残り5秒）に画面中央へ出す大きなカウントダウン。EffectCanvas/CountDown にアタッチする。
//
// 残り時間を自分で持たない。CullCountdownPanelView が毎フレーム SetState で押し込む
// （同じ時計を2本走らせない）。誰に出すかも決めない（CullAlertTier をそのまま受ける）。

using TMPro;
using Takoda99.Sound;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>淘汰対象と、その直前（ぎりぎり圏外）の店に出す画面中央の秒読み。</summary>
    public sealed class CullFinalCountdownView : MonoBehaviour
    {
        /// <summary>数字を流し込む先。縁取り用に重ねた2枚など、複数まとめて同じ数字にする。</summary>
        [SerializeField] private TMP_Text[] numberTexts;

        /// <summary>フェードに使う。未設定なら自身から取る。</summary>
        [SerializeField] private CanvasGroup group;

        /// <summary>拡大に使う。未設定なら自身の RectTransform。子の相対スケールは保たれる。</summary>
        [SerializeField] private RectTransform scaleRoot;

        [Header("出現アニメーション")]
        [Tooltip("出はじめの大きさ（1.0 = Inspector で組んだ大きさ）。ここから等倍へ広がる。")]
        [SerializeField, Range(0.1f, 1f)] private float popStartScale = 0.6f;

        [Tooltip("等倍に届くまでの時間（秒）。1秒より短くする（次の数字に食い込ませない）。")]
        [SerializeField, Range(0.05f, 0.9f)] private float popSeconds = 0.28f;

        [Tooltip("不透明になるまでの時間（秒）。")]
        [SerializeField, Range(0.02f, 0.5f)] private float fadeInSeconds = 0.12f;

        [Tooltip("次の数字へ変わる前に消えるまでの時間（秒）。")]
        [SerializeField, Range(0.05f, 0.9f)] private float fadeOutSeconds = 0.3f;

        /// <summary>
        /// 表示中の秒。数字が変わったフレームだけ TMP.text に代入するために持つ
        /// （<see cref="CullFinalCountdownState.SecondProgress"/> は毎フレーム変わるので比較に使わない）。
        /// </summary>
        private CullFinalCountdownState current;
        private bool hasCurrent;

        private Vector3 baseScale = Vector3.one;

        /// <summary>直近に押し込まれた段階。秒読みSEを警告／通常のどちらで鳴らすかに使う。</summary>
        private CullAlertTier currentTier = CullAlertTier.None;

        /// <summary>
        /// 初期化済みか。**シーンでは非アクティブに置いてあるため <c>Awake</c> が走らない。**
        /// 初回の <see cref="SetState"/> で必ず一度通す（非アクティブな GameObject でも
        /// コンポーネントのメソッド呼び出し自体は届く）。
        /// </summary>
        private bool initialized;

        private void Awake() => EnsureInitialized();

        /// <summary>
        /// 参照の解決と初期状態。**`baseScale` は `localScale` を書き換える前に採る**
        /// （縮んだ値を等倍として覚えてしまうと、以後カウントダウンが小さいまま出る）。
        /// </summary>
        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (scaleRoot == null)
            {
                scaleRoot = transform as RectTransform;
            }

            if (scaleRoot != null)
            {
                baseScale = scaleRoot.localScale;
            }

            if (group != null)
            {
                // 全画面の最前面（EffectCanvas）に置くため、入力を絶対に食わせない。
                group.blocksRaycasts = false;
                group.interactable = false;
                group.alpha = 0f;
            }
        }

        /// <summary>毎フレーム呼ばれる。段階と残り時間だけを受け、表示は自分で決める。</summary>
        public void SetState(CullAlertTier tier, long remainingMs)
        {
            EnsureInitialized();
            currentTier = tier;
            Apply(CullFinalCountdownState.From(tier, remainingMs));
        }

        private void Apply(CullFinalCountdownState state)
        {
            if (!state.Visible)
            {
                hasCurrent = false;
                if (group != null)
                {
                    group.alpha = 0f;
                }

                SetRootActive(false);
                return;
            }

            // 秒読みが窓に入ったらここで初めてアクティブにする（シーンでは非表示で置いてある）。
            SetRootActive(true);

            // 数字が変わったフレームだけ文字列を差し替える（毎フレームの TMP 再構築を避ける）。
            if (!hasCurrent || !current.Equals(state))
            {
                var secondChanged = !hasCurrent || current.Text != state.Text;

                current = state;
                hasCurrent = true;
                ApplyText(state.Text);

                // 秒読みSEは数字が変わったときだけ。SecondProgress は毎フレーム変わるので、
                // state の等値だけを条件にすると毎フレーム鳴ってしまう。
                if (secondChanged)
                {
                    PlayTick();
                }
            }

            ApplyAnimation(state.SecondProgress);
        }

        /// <summary>
        /// 1秒ぶんの出現アニメーション。小さいところから等倍へ広がりながらフェードインし、
        /// 次の数字へ移る前にフェードアウトする。**時間はすべて秒読みの進み具合から引く**ので、
        /// フレームレートが落ちても数字と演出がずれない。
        /// </summary>
        private void ApplyAnimation(float secondProgress)
        {
            var elapsed = Mathf.Clamp01(secondProgress);

            if (scaleRoot != null)
            {
                // EaseOut（1-(1-t)^3）。勢いよく広がってから静かに止まる。
                var t = popSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / popSeconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                scaleRoot.localScale = baseScale * Mathf.LerpUnclamped(popStartScale, 1f, eased);
            }

            if (group == null)
            {
                return;
            }

            var fadeIn = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInSeconds);

            // 残り時間側から測る。fadeOutSeconds が長くても頭のフェードインを潰さない。
            var remain = 1f - elapsed;
            var fadeOut = fadeOutSeconds <= 0f ? 1f : Mathf.Clamp01(remain / fadeOutSeconds);

            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
        }

        /// <summary>
        /// 1秒ぶんの秒読み音。自店が淘汰圏内（Danger）なら警告側、ぎりぎり圏外（Caution）なら通常側。
        /// どちらを出すかは <see cref="CullAlertTier"/> がすでに決めているので、ここでは選ぶだけ。
        /// </summary>
        private void PlayTick()
        {
            SoundPlayer.Play(currentTier == CullAlertTier.Danger
                ? SoundId.CullCountdownWarningTick
                : SoundId.CullCountdownTick);
        }

        private void ApplyText(string text)
        {
            if (numberTexts == null)
            {
                return;
            }

            for (var i = 0; i < numberTexts.Length; i++)
            {
                if (numberTexts[i] != null)
                {
                    numberTexts[i].text = text;
                }
            }
        }

        /// <summary>
        /// 自分の GameObject を出し入れする。
        /// **`Update()` を持たないので、非アクティブにしても復帰できる**
        /// （表示の駆動は CullCountdownPanelView からの <see cref="SetState"/> であり、
        /// 非アクティブな GameObject のコンポーネントにもメソッド呼び出しは届く）。
        /// alpha 0 では TMP のメッシュが描かれ続けるため、出していない間は根ごと切る。
        /// </summary>
        private void SetRootActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }
}
