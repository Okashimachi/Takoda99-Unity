// MainGame/MatchFinishCanvas。試合が完全終了（MatchEnd）した瞬間だけ出す演出。
// 1〜10位も含めて「試合が終わったこと」を伝えるためのもので、脱落表現（グレーアウト）とは別物。
// Panel（Glow/FinishText）はフェードイン→数秒表示→フェードアウトで消えるが、
// NextButton はそれとは別に常時押せる（ResultCanvas/NextButton と同じ遷移）。

using DG.Tweening;
using Takoda99.Sound;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>MainGame シーンの試合完全終了演出（MatchFinishCanvas）。</summary>
    public sealed class MatchFinishView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup panelGroup; // 未設定なら panel から取る
        [SerializeField] private Button nextButton;

        [Header("Panel のフェード（秒）")]
        [SerializeField] private float fadeInSeconds = 0.5f;
        [SerializeField] private float holdSeconds = 2f;
        [SerializeField] private float fadeOutSeconds = 0.5f;

        private Sequence fadeSequence;

        private void Awake()
        {
            if (panelGroup == null && panel != null)
            {
                panelGroup = panel.GetComponent<CanvasGroup>();
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextClicked);
            }
        }

        private void OnDestroy()
        {
            fadeSequence?.Kill();

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextClicked);
            }
        }

        /// <summary>試合終了時に Renderer.OnMatchEnd から呼ぶ。</summary>
        public void Show()
        {
            gameObject.SetActive(true);

            if (panel != null)
            {
                panel.SetActive(true);
            }

            PlayFade();
        }

        /// <summary>フェードイン → 表示保持 → フェードアウト。呼び直されても最初からやり直す。</summary>
        private void PlayFade()
        {
            fadeSequence?.Kill();

            if (panelGroup == null)
            {
                return;
            }

            panelGroup.alpha = 0f;
            fadeSequence = DOTween.Sequence()
                .Append(panelGroup.DOFade(1f, fadeInSeconds))
                .AppendInterval(holdSeconds)
                .Append(panelGroup.DOFade(0f, fadeOutSeconds));
        }

        /// <summary>
        /// ResultCanvas の NextButton（EliminationResultView.OnNextClicked）と同じ振る舞い。
        /// GameBootstrapper.GoToResult 側でBGM停止も行うため、ここでは呼び出すだけでよい。
        /// </summary>
        private void OnNextClicked()
        {
            SoundPlayer.Play(SoundId.ButtonTap);

            var bootstrap = Bootstrap.GameBootstrapper.Instance;
            if (bootstrap == null)
            {
                Debug.LogError($"{nameof(MatchFinishView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が null のため Result へ遷移できません。", this);
                return;
            }

            bootstrap.GoToResult();
        }
    }
}
