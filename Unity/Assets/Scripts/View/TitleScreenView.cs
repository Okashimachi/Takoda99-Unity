// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Title シーン。Start ボタンをマッチングシーンへの遷移に繋ぐだけ。
// ★ここでは接続しない。BeginPlay() は呼ばない（接続は表示名確定後。matchmaking/01-matchmaking-flow.md §8.5）。

using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Title シーン。Start ボタンをマッチングシーンへの遷移に繋ぐだけ。</summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        private void OnEnable()
        {
            if (Bootstrap.GameBootstrapper.Instance == null)
            {
                Debug.LogError($"{nameof(TitleScreenView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Boot シーンから再生してください。", this);
                if (startButton != null)
                {
                    startButton.interactable = false;
                }
                return;
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
            }
        }

        private void OnStartClicked()
        {
            Bootstrap.GameBootstrapper.Instance.GoToMatchmaking();
        }
    }
}
