// 仕様書: Unity/docs/.sdd/match-view/05-patience-timer.md
// 我慢ゲージの表示専用カウントダウン。我慢切れの判定はサーバー権威（CustomerLeft待ち）で、ここでは行わない。

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.Timer
{
    /// <summary>我慢ゲージの表示専用カウントダウン（05-patience-timer.md）。</summary>
    public sealed class PatienceTimer : MonoBehaviour
    {
        [SerializeField] private Image gauge;
        [SerializeField] private TextMeshProUGUI remainingSecondsText;

        private long deadlineLocalMonotonicMs;
        private long totalMs;
        private bool running;

        private void Awake()
        {
            if (gauge == null)
            {
                Debug.LogError($"{nameof(PatienceTimer)}.{nameof(gauge)} が未設定です。", this);
            }
        }

        /// <summary>対応開始。arrivedAtLocalMs は IClock.MonotonicMs 基準。</summary>
        public void Begin(long arrivedAtLocalMs, int patienceMaxMs)
        {
            totalMs = Math.Max(patienceMaxMs, 1);
            deadlineLocalMonotonicMs = arrivedAtLocalMs + patienceMaxMs;
            running = true;
            Apply(totalMs);
        }

        /// <summary>対応終了・客の離脱時に呼ぶ。ゲージを空にする。</summary>
        public void Stop()
        {
            running = false;
            Apply(0);
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            var remaining = deadlineLocalMonotonicMs - nowMs;
            Apply(remaining);

            if (remaining <= 0)
            {
                running = false;
            }
        }

        private void Apply(long remainingMs)
        {
            var clamped = Math.Max(0, Math.Min(remainingMs, totalMs));

            if (gauge != null)
            {
                gauge.fillAmount = totalMs > 0 ? (float)clamped / totalMs : 0f;
            }

            if (remainingSecondsText != null)
            {
                remainingSecondsText.text = clamped <= 0 ? "0" : Math.Ceiling(clamped / 1000d).ToString();
            }
        }
    }
}
