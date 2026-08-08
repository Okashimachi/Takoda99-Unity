// 仕様書: Unity/docs/.sdd/match-view/05-patience-timer.md
// 我慢ゲージの表示専用カウントダウン。我慢切れの判定はサーバー権威（CustomerLeft待ち）で、ここでは行わない。

using System;
using Takoda99.View.ValueObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.Timer
{
    /// <summary>我慢ゲージの表示専用カウントダウン（05-patience-timer.md）。</summary>
    public sealed class PatienceTimer : MonoBehaviour
    {
        [Tooltip("バー本体（PatientGage/Gage）。右端は動かさず、左端と色だけを書き換える。")]
        [SerializeField] private Image gauge;

        [Tooltip("残量に応じた3段階の色。")]
        [SerializeField] private PatienceGaugePalette palette;

        [SerializeField] private TextMeshProUGUI remainingSecondsText;

        private long deadlineLocalMonotonicMs;
        private long totalMs;
        private bool running;

        private void Awake()
        {
            if (gauge == null)
            {
                Debug.LogError($"{nameof(PatienceTimer)}.{nameof(gauge)} が未設定です。", this);
                return;
            }

            if (palette == null)
            {
                Debug.LogError($"{nameof(PatienceTimer)}.{nameof(palette)} が未設定です。ゲージの色が変化しません。", this);
            }

            // 右端固定・左端可変は「右端が anchorMax に張り付いていて、左右のオフセットが0」でしか成立しない。
            // ここが崩れているとバーが左右両方から縮む等の見た目になるため、実行時に気づけるようにする（仕様書 §3）。
            var rect = gauge.rectTransform;
            if (!Mathf.Approximately(rect.anchorMax.x, 1f)
                || !Mathf.Approximately(rect.anchoredPosition.x, 0f)
                || !Mathf.Approximately(rect.sizeDelta.x, 0f))
            {
                Debug.LogError(
                    $"{nameof(PatienceTimer)}.{nameof(gauge)} の RectTransform が右端固定の前提を満たしていません"
                    + "（anchorMax.x=1 / anchoredPosition.x=0 / sizeDelta.x=0 が必要）。",
                    this);
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

        /// <summary>
        /// 対応終了・客の離脱時に呼ぶ。カウントダウンを止め、ゲージを満タン（待機状態）へ戻す。
        /// 客が居ない間に「残り0・赤」を出しっぱなしにすると、次の客の我慢が尽きたように見えるため。
        /// </summary>
        public void Stop()
        {
            running = false;
            totalMs = Math.Max(totalMs, 1); // Begin 前に呼ばれても 0 除算相当（空・赤）にしない。
            Apply(totalMs);
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
            var thresholds = palette != null ? palette.Thresholds : PatienceGaugeThresholds.Default;
            var state = PatienceGaugeState.From(remainingMs, totalMs, thresholds);

            if (gauge != null)
            {
                // 右端（満タン側）は anchorMax のまま触らず、左端だけを右へ寄せて残量を表す。
                var rect = gauge.rectTransform;
                var anchorMin = rect.anchorMin;
                anchorMin.x = (float)state.LeftEdgeAnchorX;
                rect.anchorMin = anchorMin;

                if (palette != null)
                {
                    gauge.color = palette.Resolve(state.Stage);
                }
            }

            if (remainingSecondsText != null)
            {
                var clamped = Math.Max(0, Math.Min(remainingMs, totalMs));
                remainingSecondsText.text = clamped <= 0 ? "0" : Math.Ceiling(clamped / 1000d).ToString();
            }
        }
    }
}
