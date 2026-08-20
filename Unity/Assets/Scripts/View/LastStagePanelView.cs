// MainGame。足切りの最終ステージ（StageIndex == StageTotal、残り20秒の最後の段階）に
// 入った瞬間だけ1回出す「ラスト20秒！」ポップアップ。Renderer.OnCullWarning から呼ぶ。

using DG.Tweening;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>最終ステージ突入演出。フェードイン→表示保持→フェードアウトで自動的に消える。</summary>
    public sealed class LastStagePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup panelGroup; // 未設定なら panel から取る

        [Header("フェード（秒）")]
        [SerializeField] private float fadeInSeconds = 0.3f;
        [SerializeField] private float holdSeconds = 1f;
        [SerializeField] private float fadeOutSeconds = 0.5f;

        private Sequence fadeSequence;

        private void Awake()
        {
            if (panelGroup == null && panel != null)
            {
                panelGroup = panel.GetComponent<CanvasGroup>();
            }

            SetPanelActive(false);
        }

        private void OnDestroy()
        {
            fadeSequence?.Kill();
        }

        /// <summary>最終ステージに入った瞬間、Renderer から1回だけ呼ぶ。</summary>
        public void Show()
        {
            SetPanelActive(true);
            PlayFade();
        }

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
                .Append(panelGroup.DOFade(0f, fadeOutSeconds))
                .OnComplete(() => SetPanelActive(false));
        }

        private void SetPanelActive(bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }
    }
}
