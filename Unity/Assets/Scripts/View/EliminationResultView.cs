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
        private readonly Dictionary<string, int> finalRanks = new Dictionary<string, int>();

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

        /// <summary>自店が脱落した際、Renderer.OnStoreEliminated から呼ぶ。モーダルを表示する。</summary>
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
        /// まだ出していなければ出す。Renderer.OnMatchEnd から呼ぶ。
        /// 優勝（最後まで残った）場合は自店に対して OnStoreEliminated が来ないため、
        /// MatchEnd が唯一のモーダル表示の契機になる。既に脱落で出ている場合は順位を上書きしない。
        /// </summary>
        public void ShowIfHidden(int selfFinalRank)
        {
            if (IsShown)
            {
                return;
            }

            Show(selfFinalRank);
        }

        /// <summary>
        /// 全店ぶんの脱落イベントを Renderer から転送してもらう（自店以外も含む）。
        /// <c>ClientState.Stores</c> は脱落時に FinalRank を保持しないため、ここで直接記録する。
        /// </summary>
        public void RecordElimination(string storeId, int finalRank)
        {
            finalRanks[storeId] = finalRank;

            if (lastState != null && gameObject.activeInHierarchy)
            {
                RefreshRankList(lastState);
            }
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
        /// 指定順位の店名を返す。脱落済みで順位が確定していればそれを、まだなら現在生存中の店の
        /// 現在順位から取得する（脱落時点で未確定な上位もリアルタイムに反映するため）。
        /// </summary>
        private string FindDisplayNameAtRank(ClientState state, int place)
        {
            foreach (var pair in finalRanks)
            {
                if (pair.Value != place)
                {
                    continue;
                }

                var eliminatedStore = state.Stores.FirstOrDefault(s => s.StoreId == pair.Key);
                if (eliminatedStore != null)
                {
                    return eliminatedStore.DisplayName;
                }
            }

            var aliveStore = state.Stores.FirstOrDefault(s => s.Alive && s.Rank == place);
            return aliveStore?.DisplayName;
        }

        private void OnNextClicked()
        {
            Bootstrap.GameBootstrapper.Instance.GoToResult();
        }
    }
}
