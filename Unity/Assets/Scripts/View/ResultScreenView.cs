// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Result シーンの入口。テストモードの切り替えをここ1箇所に持ち、
// たこ焼き生成・成績一覧の両方へ同じ出所のデータを流す。
//
// ⚠ このシーンに来た時点で MatchEnd が届いているとは限らない。自店が脱落した場合、
// 試合はまだ続いており（自店は Spectating）、MainGame の脱落モーダルの Next ボタンから
// GameBootstrapper.GoToResult() で先にこの画面へ来る。MatchEnd はそのあと届く。
// そのため一度読むだけにせず、Store を購読して結果の到着を待つ。

using System;
using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>Result シーン。成績データの注入と、Title ボタンのシーン遷移を担う。</summary>
    public sealed class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private Button titleButton;
        [SerializeField] private TakoyakiCreator takoyakiCreator;
        [SerializeField] private ResultStatsBoardView statsBoard;

        [Tooltip("ResultCanvas/Result/Rank 配下、自店の最終順位を表示する数値テキスト（99 のプレースホルダーが入っている方）。")]
        [SerializeField] private TMP_Text rankText;

        [Header("X 投稿")]
        [SerializeField] private Button xButton;

        [Header("テストモード")]
        [Tooltip("ON にすると、サーバーの受信値ではなく ResultSampleData のサンプルを全要素へ注入する（たこ焼き生成を含む）。")]
        [SerializeField] private bool testMode;

        [Tooltip("テストモードで生成するたこ焼きの個数（＝提供数）。成績表示もこの値を基準に組み立てる。")]
        [SerializeField] private int testTakoyakiCount = ResultSampleData.DefaultServedCount;

        private IDisposable subscription;
        private bool hasRenderedResult;
        private bool hasRenderedPending;
        private MatchResult latestResult;
        private string latestStoreName = "-";

        private void OnEnable()
        {
            if (testMode)
            {
                ApplySample();
                BindTitleButton();
                return;
            }

            if (Bootstrap.GameBootstrapper.Instance == null)
            {
                Debug.LogError($"{nameof(ResultScreenView)}: {nameof(Bootstrap.GameBootstrapper)}.Instance が見つかりません。Boot シーンから再生するか、テストモードを ON にしてください。", this);
                if (titleButton != null)
                {
                    titleButton.interactable = false;
                }
                return;
            }

            var store = Bootstrap.GameBootstrapper.Instance.Store;
            subscription = store.Subscribe(HandleStateChanged);
            HandleStateChanged(store.State);

            BindTitleButton();
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;

            if (titleButton != null)
            {
                titleButton.onClick.RemoveListener(OnTitleClicked);
            }

            if (xButton != null)
            {
                xButton.onClick.RemoveListener(OnXClicked);
            }
        }

        private void BindTitleButton()
        {
            if (titleButton != null && Bootstrap.GameBootstrapper.Instance != null)
            {
                titleButton.onClick.AddListener(OnTitleClicked);
            }

            if (xButton != null)
            {
                xButton.onClick.AddListener(OnXClicked);
            }
        }

        private void ApplySample()
        {
            var result = ResultSampleData.CreateResult(testTakoyakiCount);
            var stores = ResultSampleData.CreateStores();
            latestResult = result;
            latestStoreName = FindSelfName(stores, ResultSampleData.SelfStoreId);

            if (statsBoard != null)
            {
                statsBoard.Show(result, stores, ResultSampleData.SelfStoreId);
            }

            if (rankText != null)
            {
                rankText.text = result.FinalRank.ToString();
            }

            if (takoyakiCreator != null)
            {
                takoyakiCreator.SetTakoyakiCount(result.Stats.ServedCount);
            }
        }

        /// <summary>
        /// MatchEnd がまだなら枠だけ（待ち表示）を出し、届いた時点で本番の値へ差し替える。
        /// 差し替えは1度だけ。以降の状態変化でたこ焼きの生成が最初からやり直しになるのを防ぐ。
        /// </summary>
        private void HandleStateChanged(ClientState state)
        {
            if (hasRenderedResult)
            {
                return;
            }

            var result = state.Result;
            latestResult = result;
            latestStoreName = FindSelfName(state.Stores, state.SelfStoreId);

            // 待ち表示は最初の1回だけ。観戦中は他店の更新が流れ続けるので、そのたびに組み直すと
            // TakoyakiCreator の表示演出が毎回リセットされ、いつまでも何も出てこなくなる。
            if (result == null && hasRenderedPending)
            {
                return;
            }

            if (statsBoard != null)
            {
                statsBoard.Show(result, state.Stores, state.SelfStoreId);
            }

            if (rankText != null && result != null)
            {
                rankText.text = result.FinalRank.ToString();
            }

            if (takoyakiCreator != null)
            {
                // Rank / Others / Buttons の表示は TakoyakiCreator の生成完了が起点なので、
                // MatchEnd 待ちのあいだも 0 個で呼んでおく。呼ばないと Title ボタンごと出てこない。
                // スコア（提供数）が 0 の店もいるため、0 でも必ず呼ぶ点は結果到着後も同じ。
                takoyakiCreator.SetTakoyakiCount(result?.Stats.ServedCount ?? 0);
            }

            if (result == null)
            {
                hasRenderedPending = true;
            }
            else
            {
                hasRenderedResult = true;
            }
        }

        private void OnTitleClicked()
        {
            Bootstrap.GameBootstrapper.Instance.BackToTitle();
        }

        /// <summary>成績とハッシュタグを添えて、X の投稿画面をブラウザで開く。上位3位は専用の煽り文にする。</summary>
        private void OnXClicked()
        {
            var stats = latestResult?.Stats ?? new MatchStats();
            var missRate = stats.TotalKeystrokes > 0
                ? (stats.TotalMisses * 100.0 / stats.TotalKeystrokes).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                : "0.0";

            var finalRank = latestResult?.FinalRank ?? 0;

            // 順位は本文の先頭に置く。X のタイムラインは先頭数行しか見えないことが多く、
            // 一番の話題性がある数字を打鍵数より下へ埋めない。
            // MatchEnd 未着（finalRank が 0）のときだけ順位行を落とす。
            var rankLine = finalRank > 0 ? $"順位：{finalRank}位 / 99店\n" : string.Empty;

            var body =
                rankLine +
                $"打鍵数：{stats.TotalKeystrokes}\n" +
                $"ミス数：{stats.TotalMisses}\n" +
                $"ミス率：{missRate}%";

            var headline = BuildHeadline(finalRank, latestStoreName, stats.ServedCount);
            var text = $"{headline}\n{body}\n#たこ打99 #THEHACK2026";

            var url = "https://x.com/intent/post?text=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(text);
            Application.OpenURL(url);
        }

        /// <summary>上位3位専用の煽り文。それ以外は通常の見出し。</summary>
        private static string BuildHeadline(int finalRank, string storeName, int servedCount)
        {
            switch (finalRank)
            {
                case 1:
                    return $"🏆優勝🏆 {storeName}は堂々の1位！{servedCount}個のたこ焼きを作りました！";
                case 2:
                    return $"🥈準優勝🥈 {storeName}は2位に輝きました！{servedCount}個のたこ焼きを作りました！";
                case 3:
                    return $"🥉3位入賞🥉 {storeName}は見事3位！{servedCount}個のたこ焼きを作りました！";
                default:
                    return $"{storeName}は{servedCount}個のたこ焼きを作りました！";
            }
        }

        private static string FindSelfName(IReadOnlyList<StoreSummary> stores, string selfStoreId)
        {
            if (stores != null && !string.IsNullOrEmpty(selfStoreId))
            {
                foreach (var store in stores)
                {
                    if (store.StoreId == selfStoreId)
                    {
                        return store.DisplayName;
                    }
                }
            }

            return "-";
        }
    }
}
