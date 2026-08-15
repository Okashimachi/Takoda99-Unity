// MainGame/ResultCanvas。自店が脱落した瞬間に Renderer から Show() され、
// 自分の最終順位と上位10店（未確定分はリアルタイムの現在順位）を表示する。

using System;
using System.Collections.Generic;
using System.Linq;
using Takoda99.Client.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>MainGame シーンの脱落リザルトモーダル（ResultCanvas）。</summary>
    public sealed class EliminationResultView : MonoBehaviour
    {
        private static readonly string[] PlaceNames =
        {
            "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th",
        };

        [SerializeField] private TMP_Text rankText;
        [SerializeField] private Transform rankListRoot; // RankList。子（1st..10th想定）から自動解決する
        [SerializeField] private Button nextButton;

        private TMP_Text[] rankListTexts = new TMP_Text[10]; // 1st..10th

        // storeId -> 脱落時に確定した最終順位。Renderer が全店ぶんの OnStoreEliminated を転送する。

        private IStore store;
        private IDisposable subscription;
        private ClientState lastState;
        private bool initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (rankText == null)
            {
                rankText = transform.Find("Result/Rank/Rank")?.GetComponent<TMP_Text>();
            }

            if (rankListRoot == null)
            {
                rankListRoot = transform.Find("Result/RankList");
            }

            rankListTexts = new TMP_Text[PlaceNames.Length];
            if (rankListRoot != null)
            {
                for (var i = 0; i < PlaceNames.Length && i < rankListRoot.childCount; i++)
                {
                    // 子は 1st..10th の順で並んでいる前提。子の名前ではなく並び順で解決する。
                    var row = rankListRoot.GetChild(i);
                    rankListTexts[i] = row.GetComponentInChildren<TMP_Text>();
                }
            }

            if (nextButton == null)
            {
                nextButton = transform.Find("Result/NextButton")?.GetComponent<Button>();
            }

            if (rankText == null || rankListTexts.Any(t => t == null) || nextButton == null)
            {
                Debug.LogError($"{nameof(EliminationResultView)}: ResultCanvas 配下の参照が見つかりません。階層を確認してください。", this);
            }
        }

        private void OnEnable()
        {
            EnsureInitialized();

            var bootstrap = Bootstrap.GameBootstrapper.Instance;
            if (bootstrap == null)
            {
                Debug.LogError($"{nameof(EliminationResultView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。", this);
                return;
            }

            store = bootstrap.Store;
            subscription = store.Subscribe(HandleStateChanged);
            HandleStateChanged(store.State);

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextClicked);
            }
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextClicked);
            }
        }

        /// <summary>モーダルを表示済みか。MatchEnd で二重に出さないための判定に使う。</summary>
        public bool IsShown { get; private set; }

        /// <summary>自店が脱落した際、Renderer.OnStoreEliminatedBatch から呼ぶ。モーダルを表示する。</summary>
        public void Show(int selfFinalRank)
        {
            EnsureInitialized();

            if (rankText != null)
            {
                rankText.text = selfFinalRank.ToString();
            }

            IsShown = true;
            gameObject.SetActive(true);

            // 出た／出ないの切り分けを実機（WebGL のブラウザコンソール含む）で行えるようにする。
            // 1試合に1回しか出ないためログとしても静か。
            Debug.Log($"{nameof(EliminationResultView)}: リザルトモーダルを表示 rank={selfFinalRank}", this);
        }

        /// <summary>
        /// まだ出していなければ出す。Renderer.OnMatchEnd と state 駆動の両方から呼ぶ。
        /// 既に脱落で出ている場合は順位を上書きしない（冪等）。
        /// </summary>
        public void ShowIfHidden(int selfFinalRank)
        {
            if (IsShown)
            {
                return;
            }

            Show(selfFinalRank);
        }

        private void HandleStateChanged(ClientState state)
        {
            lastState = state;
            RefreshRankList(state);
        }

        private void RefreshRankList(ClientState state)
        {
            for (var i = 0; i < rankListTexts.Length; i++)
            {
                var text = rankListTexts[i];
                if (text == null)
                {
                    continue;
                }

                var place = i + 1;
                var displayName = FindDisplayNameAtRank(state, place);
                text.text = displayName != null ? $"{PlaceNames[i]}. {displayName}" : $"{PlaceNames[i]}. ";
            }
        }

        /// <summary>
        /// 指定順位の店名を返す。
        /// </summary>
        /// <remarks>
        /// v0.8.0 の <c>RankingRow.Rank</c> は**生存店なら現在順位・脱落店なら確定順位**を1本で持つため、
        /// 生死で分岐せずそのまま引ける。予選版にあった「脱落店の確定順位を自前の辞書に記録しておく」
        /// 処理（<c>RecordElimination</c>）は不要になった。
        /// </remarks>
        private static string FindDisplayNameAtRank(ClientState state, int place)
        {
            var row = state.Ranking.Rows.FirstOrDefault(r => r.Rank == place);
            return row != null && !string.IsNullOrEmpty(row.DisplayName) ? row.DisplayName : null;
        }

        private void OnNextClicked()
        {
            var bootstrap = Bootstrap.GameBootstrapper.Instance;
            if (bootstrap == null)
            {
                // ここが null だと押しても無反応になり「遷移できない」に見える。原因を名指しで残す。
                Debug.LogError($"{nameof(EliminationResultView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が null のため Result へ遷移できません。", this);
                return;
            }

            Debug.Log($"{nameof(EliminationResultView)}: NextButton → Result シーンへ遷移します。", this);
            bootstrap.GoToResult();
        }
    }
}
