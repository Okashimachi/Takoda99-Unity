// 仕様書: Unity/docs/.sdd/ranking-view/02-cull-countdown-panel.md
// 足切り秒読みパネル（常設UI）。予選の「予告時だけ出るポップアップ」を格上げしたもの。
//
// 残り時間を ClientState へ書き戻さない（毎フレームの Store 通知を作らない）。
// Rank と CutLineRank を比較して自分が危険かを判定しない（SelfAtRisk がサーバーから届く）。

using System.Collections.Generic;
using TMPro;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>次の足切りまでの秒読みと、脱落予定の店を出す常設パネル。</summary>
    public sealed class CullCountdownPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text cutLineText;

        [Header("脱落予定の店")]
        [SerializeField] private RankingRowView rowPrefab;   // 01 と同じ行Prefabを再利用
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private int maxCutRows = 5;

        /// <summary>maxCutRows に収まりきらない件数を添える欄。</summary>
        [SerializeField] private TMP_Text overflowText;

        [Header("SelfAtRisk の警告")]
        [SerializeField] private CanvasGroup alertOverlay;
        [SerializeField] private AudioSource alertSe;
        [SerializeField] private AudioClip atRiskClip;

        private CullWarning warning;
        private IReadOnlyDictionary<string, string> displayNames;
        private CullCountdownState current;
        private bool hasCurrent;

        /// <summary>直前の SelfAtRisk。状態が変わった瞬間だけSEを鳴らすために持つ。</summary>
        private bool lastSelfAtRisk;

        private RankingRowPool pool;
        private readonly HashSet<string> visibleIds = new HashSet<string>();
        private readonly List<string> cutRows = new List<string>();

        private void Awake()
        {
            pool = new RankingRowPool(rowPrefab, rowsRoot);
            SetPanelVisible(false);
        }

        /// <summary>受信値の差し替え。Renderer が state 変化のたびに呼ぶ。</summary>
        public void SetWarning(CullWarning next, ClientState state)
        {
            warning = next;
            displayNames = state?.DisplayNames;

            // C5: 未受信の間はパネルを非表示にする（0秒と区別する）。
            if (warning == null)
            {
                SetPanelVisible(false);
                hasCurrent = false;
                lastSelfAtRisk = false;
                pool?.ReleaseAll();
                return;
            }

            SetPanelVisible(true);
            ApplyCutRows();

            // C3: 新しい予告は即座に上書きする。補間中の値を優先しない（サーバー値が常に正）。
            hasCurrent = false;
            UpdateTexts();
        }

        /// <summary>受信の瞬間だけ必要な演出の契機（IRenderer.OnCullWarning から）。</summary>
        public void OnWarningReceived(CullWarning received)
        {
            if (received == null)
            {
                return;
            }

            // SelfAtRisk は 1〜2Hz で届き続ける。**状態が変わった瞬間だけ**鳴らす。
            if (received.SelfAtRisk && !lastSelfAtRisk && alertSe != null && atRiskClip != null)
            {
                alertSe.PlayOneShot(atRiskClip);
            }

            lastSelfAtRisk = received.SelfAtRisk;
        }

        private void Update()
        {
            if (warning == null)
            {
                return;
            }

            // C1: Update() で数字だけ更新する。ClientState を触らない。
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            // Time.time を使わない（タイムスケールの影響を受けるため）。Renderer と同じ式に揃える。
            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            var state = CullCountdownState.From(warning, nowMs);

            // AlertIntensity は毎フレーム変わるので Equals の外側で適用する。
            // 02 §5「残り秒が少ないほど強くする」を、単純フェードではなく明滅（パルス）で表現する。
            // intensity が上がるほど明滅を速く・深くし、「もっと激しく」に応える。
            if (alertOverlay != null)
            {
                var intensity = state.AlertIntensity;
                if (intensity > 0f)
                {
                    var pulseHz = Mathf.Lerp(1.5f, 6f, intensity);
                    var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f);
                    alertOverlay.alpha = intensity * Mathf.Lerp(0.55f, 1f, pulse);
                }
                else
                {
                    alertOverlay.alpha = 0f;
                }
            }

            // C2: 表示秒が変わったフレームだけ TMP.text に代入する。
            if (hasCurrent && current.Equals(state))
            {
                return;
            }

            current = state;
            hasCurrent = true;

            if (remainingText != null)
            {
                remainingText.text = state.RemainingText;
            }

            if (stageText != null)
            {
                stageText.text = state.StageText;
            }

            if (cutLineText != null)
            {
                cutLineText.text = state.CutLineText;
            }
        }

        private void ApplyCutRows()
        {
            if (pool == null || warning == null)
            {
                return;
            }

            var ids = warning.CutStoreIds;
            cutRows.Clear();
            visibleIds.Clear();

            var shown = ids.Count < maxCutRows ? ids.Count : maxCutRows;
            for (var i = 0; i < shown; i++)
            {
                cutRows.Add(ids[i]);
                visibleIds.Add(ids[i]);
            }

            pool.ReleaseAllExcept(visibleIds);

            for (var i = 0; i < cutRows.Count; i++)
            {
                var storeId = cutRows[i];
                var row = pool.Acquire(storeId);
                if (row == null)
                {
                    continue;
                }

                row.SetNameOnly(storeId, ResolveName(storeId));
                row.transform.SetSiblingIndex(i);
            }

            // サーバーの送信件数が maxCutRows より多い可能性がある。多い分は件数で添える。
            if (overflowText != null)
            {
                var rest = ids.Count - shown;
                overflowText.text = rest > 0 ? "他" + rest + "店" : string.Empty;
            }
        }

        /// <summary>解決できなければ storeId をそのまま出す（空欄にしない）。</summary>
        private string ResolveName(string storeId)
        {
            if (displayNames != null && displayNames.TryGetValue(storeId, out var n) && !string.IsNullOrEmpty(n))
            {
                return n;
            }

            return storeId;
        }

        public void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }
    }
}
