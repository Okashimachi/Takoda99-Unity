// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Result シーン。Title ボタンをタイトルシーンへの遷移に繋ぐだけ。

using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Result シーン。Title ボタンをタイトルシーンへの遷移に繋ぐだけ。</summary>
    public sealed class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private Button titleButton;

        private void OnEnable()
        {
            if (Bootstrap.GameBootstrapper.Instance == null)
            {
                Debug.LogError($"{nameof(ResultScreenView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Boot シーンから再生してください。", this);
                if (titleButton != null)
                {
                    titleButton.interactable = false;
                }
                return;
            }

            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnTitleClicked);
            }
        }

        private void OnDisable()
        {
            if (titleButton != null)
            {
                titleButton.onClick.RemoveListener(OnTitleClicked);
            }
        }

        private void OnTitleClicked()
        {
            Bootstrap.GameBootstrapper.Instance.BackToTitle();
        }
    }
}
