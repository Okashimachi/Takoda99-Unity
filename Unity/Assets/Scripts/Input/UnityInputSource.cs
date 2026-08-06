// 仕様書: Unity/docs/.sdd/platform/02-input-source.md
// IInputSource の Unity 実体。Input System の文字入力イベントを TypingJudge.PressKey(char) にそのまま渡す。

using System;
using Takoda99.Client.Lifecycle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Takoda99.InputSource
{
    /// <summary><see cref="IInputSource"/> の Unity 実体（02-input-source.md）。</summary>
    public sealed class UnityInputSource : MonoBehaviour, IInputSource
    {
        public event Action<char> OnCharKey;

        private void OnEnable()
        {
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput += HandleTextInput;
            }
        }

        private void OnDisable()
        {
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= HandleTextInput;
            }
        }

        private void HandleTextInput(char c)
        {
            OnCharKey?.Invoke(c);
        }
    }
}
