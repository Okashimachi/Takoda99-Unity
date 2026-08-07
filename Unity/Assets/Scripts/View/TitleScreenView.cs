// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Title シーン。Start ボタンをマッチングシーンへの遷移に繋ぐだけ。
// ★ここでは接続しない。BeginPlay() は呼ばない（接続は表示名確定後。matchmaking/01-matchmaking-flow.md §8.5）。

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Title シーン。Start ボタンをマッチングシーンへの遷移に繋ぐだけ。どのキー入力でも次シーンへ進む。</summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private Button startButton;

        [Header("特設ページ")]
        [Tooltip("特設ページボタンで開くURL。")]
        [SerializeField] private Button descriptionButton;
        [SerializeField] private string descriptionPageUrl = "https://takoda99-description.vercel.app/";

        private bool hasAdvanced;

        private void OnEnable()
        {
            hasAdvanced = false;

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

            if (descriptionButton != null)
            {
                descriptionButton.onClick.AddListener(OnDescriptionClicked);
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (descriptionButton != null)
            {
                descriptionButton.onClick.RemoveListener(OnDescriptionClicked);
            }
        }

        // 特設ページボタンとの誤爆を避けるため、そちらのクリックはここでは拾わず、
        // キーボード入力のみを「どのキーでも次へ進む」の対象にする。
        private void Update()
        {
            if (hasAdvanced)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                hasAdvanced = true;
                OnStartClicked();
            }
        }

        private void OnStartClicked()
        {
            Bootstrap.GameBootstrapper.Instance.GoToMatchmaking();
        }

        private void OnDescriptionClicked()
        {
            Application.OpenURL(descriptionPageUrl);
        }
    }
}
