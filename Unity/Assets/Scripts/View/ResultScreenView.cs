// 仕様書: Unity/docs/.sdd/foundation/02-scene-composition.md §7
// Result シーンの入口。テストモードの切り替えをここ1箇所に持ち、
// たこ焼き生成・成績一覧の両方へ同じ出所のデータを流す。
//
// ⚠ このシーンに来た時点で MatchEnd が届いているとは限らない。自店が脱落した場合、
// 試合はまだ続いており（自店は Spectating）、MainGame の脱落モーダルの Next ボタンから
// GameBootstrapper.GoToResult() で先にこの画面へ来る。MatchEnd はそのあと届く。
// そのため一度読むだけにせず、Store を購読して結果の到着を待つ。
//
// ただし本選（v0.8.0）では **PersonalResult が脱落した瞬間に届いて state に保持されている**ため、
// 「成績が無いまま画面が出る」のは通信の取りこぼし時だけになった。それでも待ち表示の経路は残す
// （試合が終わったのに画面から出られない状態を作らないため）。

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

        [Header("順位別の演出（result-view/02）")]
        [Tooltip("4つの Tier をそれぞれ Prefab として持つ。Show はどれを再生するか選ぶだけにする。")]
        [SerializeField] private Result.ResultTierPresenter championPresenter;
        [SerializeField] private Result.ResultTierPresenter podiumPresenter;
        [SerializeField] private Result.ResultTierPresenter finalistPresenter;
        [SerializeField] private Result.ResultTierPresenter standardPresenter;

        [Tooltip("ResultCanvas/Result/Rank 配下、自店の最終順位を表示する数値テキスト（「位」ラベルと兄弟にある数値の方）。")]
        [SerializeField] private TMP_Text rankText;

        /// <summary>MatchEnd 待ちのあいだ順位の代わりに出す文字（ResultStatsBoardView と揃える）。</summary>
        private const string RankPending = "…";

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
        private PersonalResultState latestResult;
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
            var ranking = ResultSampleData.CreateRanking();
            latestResult = result;
            latestStoreName = FindSelfName(ranking, ResultSampleData.SelfStoreId);

            if (statsBoard != null)
            {
                statsBoard.Show(result, ranking, ResultSampleData.SelfStoreId);
            }

            ApplyRankText(result);
            PlayTier(result);

            if (takoyakiCreator != null)
            {
                takoyakiCreator.SetTakoyakiCount(result.TakoyakiCount);
            }
        }

        /// <summary>
        /// 最終順位に応じた Tier の演出を1つだけ再生する。
        /// **分岐の基準は <c>FinalRank</c> だけ**（途中の StoreEliminatedBatch を使わない）。
        /// result が null なら Standard 相当で成立させる。
        /// </summary>
        private void PlayTier(PersonalResultState result)
        {
            var tier = ValueObjects.ResultTierRule.From(result?.FinalRank ?? 0);

            // 選ばれなかった Tier は畳む。重複再生させない。
            SetTier(championPresenter, tier == ValueObjects.ResultTier.Champion, result);
            SetTier(podiumPresenter, tier == ValueObjects.ResultTier.Podium, result);
            SetTier(finalistPresenter, tier == ValueObjects.ResultTier.Finalist, result);
            SetTier(standardPresenter, tier == ValueObjects.ResultTier.Standard, result);
        }

        private static void SetTier(Result.ResultTierPresenter presenter, bool selected, PersonalResultState result)
        {
            if (presenter == null)
            {
                return;
            }

            if (selected)
            {
                presenter.Play(result);
            }
            else
            {
                presenter.Hide();
            }
        }

        /// <summary>
        /// 順位テキストを反映する。**MatchEnd 未着（<paramref name="result"/> が null）でも必ず書く。**
        ///
        /// 書かずに素通りすると、シーンに残っているプレースホルダー（過去に "99" が直書きされていた）が
        /// そのまま画面に出てしまい、「何位でも99位になる」ように見える。シーンの初期値に依存しないよう、
        /// 待ち状態も含めて常にこちらから上書きする。
        /// </summary>
        private void ApplyRankText(PersonalResultState result)
        {
            if (rankText == null)
            {
                // 黙って表示だけ変わらないと原因の特定が難しい。名指しで知らせる。
                Debug.LogError($"{nameof(ResultScreenView)}: {nameof(rankText)} が未割り当てです。順位が表示されません（ResultCanvas/Result/Rank 配下の数値テキストを割り当ててください）。", this);
                return;
            }

            rankText.text = result != null ? result.FinalRank.ToString() : RankPending;
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

            var result = state.PersonalResult;
            latestResult = result;
            latestStoreName = FindSelfName(state.Ranking, state.SelfStoreId);

            // 待ち表示は最初の1回だけ。観戦中は他店の更新が流れ続けるので、そのたびに組み直すと
            // TakoyakiCreator の表示演出が毎回リセットされ、いつまでも何も出てこなくなる。
            if (result == null && hasRenderedPending)
            {
                return;
            }

            if (statsBoard != null)
            {
                statsBoard.Show(result, state.Ranking, state.SelfStoreId);
            }

            ApplyRankText(result);

            // 成績が届いてから演出を選ぶ。未着の間は Standard で枠だけ出す。
            PlayTier(result);

            if (takoyakiCreator != null)
            {
                // Rank / Others / Buttons の表示は TakoyakiCreator の生成完了が起点なので、
                // MatchEnd 待ちのあいだも 0 個で呼んでおく。呼ばないと Title ボタンごと出てこない。
                // スコア（提供数）が 0 の店もいるため、0 でも必ず呼ぶ点は結果到着後も同じ。
                takoyakiCreator.SetTakoyakiCount(result?.TakoyakiCount ?? 0);
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

        /// <summary>自店の表示名。ランキング表の行から引く（RankingRow.DisplayName は解決済み）。</summary>
        private static string FindSelfName(RankingTable ranking, string selfStoreId)
        {
            if (ranking == null || string.IsNullOrEmpty(selfStoreId))
            {
                return "-";
            }

            var row = ranking.Find(selfStoreId);
            return row != null && !string.IsNullOrEmpty(row.DisplayName) ? row.DisplayName : "-";
        }
    }
}
