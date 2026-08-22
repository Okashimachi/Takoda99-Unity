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
using System.Collections;
using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.Proto;
using Takoda99.Sound;
using Takoda99.View.ValueObjects;
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

        [Header("順位ごとのネオン色（ResultCanvas/Result/Rank/Panel/NeonFrame）")]
        [Tooltip("Rank/Panel の縁取り。順位（Tier）に応じて色だけ差し替える。")]
        [SerializeField] private Image rankNeonFrame;
        [SerializeField] private Color championNeonColor = new Color(1f, 0.84f, 0.2f);   // 金（1位）
        [SerializeField] private Color podiumNeonColor = new Color(0.78f, 0.86f, 0.95f); // 銀（2〜3位）
        [SerializeField] private Color finalistNeonColor = new Color(1f, 0.55f, 0.15f);  // 銅（4〜10位）
        [SerializeField] private Color standardNeonColor = new Color(0.3f, 0.75f, 1f);   // それ以外

        [Header("暖簾のプレイヤー名（試合画面 MainStoreView と同じ組み方）")]
        [Tooltip("ResultCanvas/Noren/PlayerName/LeftText")]
        [SerializeField] private TMP_Text playerNameLeftText;
        [Tooltip("ResultCanvas/Noren/PlayerName/MiddleText")]
        [SerializeField] private TMP_Text playerNameMiddleText;
        [Tooltip("ResultCanvas/Noren/PlayerName/RightText")]
        [SerializeField] private TMP_Text playerNameRightText;

        /// <summary>MatchEnd 待ちのあいだ順位の代わりに出す文字（ResultStatsBoardView と揃える）。</summary>
        private const string RankPending = "…";

        [Header("X 投稿")]
        [SerializeField] private Button xButton;

        [Header("順位表示SE（SoundLibrary の Result グループ）")]
        [Tooltip("この順位までを「上位」とし、全パネルが出そろった瞬間に上位用のSEを鳴らす。")]
        [SerializeField] private int topRankCount = ResultRankSoundRule.DefaultTopCount;

        [Tooltip("最下位からこの件数までを「下位」とする。")]
        [SerializeField] private int bottomRankCount = ResultRankSoundRule.DefaultBottomCount;

        [Tooltip("1試合の参加店数。下位の境目を出すのに使う。")]
        [SerializeField] private int storeCount = ResultRankSoundRule.DefaultStoreCount;

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

        /// <summary>順位表示SEを鳴らしたか。表示完了は1度きりだが、二重再生を構造で封じる。</summary>
        private bool hasPlayedRankRevealSe;

        private void OnEnable()
        {
            hasPlayedRankRevealSe = false;

            // たこ焼きの生成が終わり、順位・成績・次へボタンが出そろった瞬間に鳴らす。
            // 購読は SetTakoyakiCount より前に済ませる（生成が 0 個だとすぐ表示完了まで進むため）。
            if (takoyakiCreator != null)
            {
                takoyakiCreator.RevealCompleted -= OnRevealCompleted;
                takoyakiCreator.RevealCompleted += OnRevealCompleted;
            }

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
            if (takoyakiCreator != null)
            {
                takoyakiCreator.RevealCompleted -= OnRevealCompleted;
            }

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
            ApplyPlayerName(latestStoreName);

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

            ApplyRankNeonColor(tier);
        }

        /// <summary>Rank/Panel の縁取りを、最終順位の Tier に応じたネオン色へ差し替える。</summary>
        private void ApplyRankNeonColor(ValueObjects.ResultTier tier)
        {
            if (rankNeonFrame == null)
            {
                return;
            }

            switch (tier)
            {
                case ValueObjects.ResultTier.Champion:
                    rankNeonFrame.color = championNeonColor;
                    break;
                case ValueObjects.ResultTier.Podium:
                    rankNeonFrame.color = podiumNeonColor;
                    break;
                case ValueObjects.ResultTier.Finalist:
                    rankNeonFrame.color = finalistNeonColor;
                    break;
                default:
                    rankNeonFrame.color = standardNeonColor;
                    break;
            }
        }

        /// <summary>暖簾のプレイヤー名を、試合画面（MainStoreView）と同じ3分割の組み方で反映する。</summary>
        private void ApplyPlayerName(string displayName)
        {
            var layout = PlayerNameLayout.From(displayName);

            if (playerNameLeftText != null)
            {
                playerNameLeftText.text = layout.Left;
            }

            if (playerNameMiddleText != null)
            {
                playerNameMiddleText.text = layout.Middle;
            }

            if (playerNameRightText != null)
            {
                playerNameRightText.text = layout.Right;
            }
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
            ApplyPlayerName(latestStoreName);

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

        /// <summary>
        /// 順位・成績・次へボタンがすべて出そろった瞬間。最終順位で3種類を鳴らし分ける。
        /// **順位はここで初めて読む**（表示完了は MatchEnd の到着より後になることがある）。
        /// </summary>
        private void OnRevealCompleted()
        {
            if (hasPlayedRankRevealSe)
            {
                return;
            }

            hasPlayedRankRevealSe = true;

            var finalRank = latestResult?.FinalRank ?? 0;
            var sound = ResultRankSoundRule.From(finalRank, topRankCount, bottomRankCount, storeCount);

            SoundId seId;
            switch (sound)
            {
                case ResultRankSound.Top:
                    seId = SoundId.ResultRankRevealTop;
                    break;
                case ResultRankSound.Bottom:
                    seId = SoundId.ResultRankRevealBottom;
                    break;
                default:
                    seId = SoundId.ResultRankRevealNormal;
                    break;
            }

            var seLength = SoundPlayer.Play(seId);

            // リザルトBGM。パネル表示完了SEが鳴り終わってから流し始める。
            StartCoroutine(PlayResultBgmAfter(seLength));
        }

        private IEnumerator PlayResultBgmAfter(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            BgmPlayer.PlayLoop(BgmId.Result);
        }

        private void OnTitleClicked()
        {
            SoundPlayer.Play(SoundId.ButtonTap);

            // Title へ戻る＝リザルトBGMの役目が終わる瞬間。ここで完全に止める。
            BgmPlayer.Stop();

            Bootstrap.GameBootstrapper.Instance.BackToTitle();
        }

        /// <summary>成績とハッシュタグを添えて、X の投稿画面をブラウザで開く。上位3位は専用の煽り文にする。</summary>
        private void OnXClicked()
        {
            SoundPlayer.Play(SoundId.ButtonTap);

            var stats = latestResult?.Stats ?? new MatchStats();

            // 正確率は**打鍵数とミス数から導く**（stats.AvgAccuracy は使わない）。
            // AvgAccuracy は1注文ごとの正確率の平均で、リザルト画面の「平均正確率」がそれにあたる。
            // ここは真上に「打鍵数」「ミス数」を並べるので、読み手が 1 - ミス数 ÷ 打鍵数 を検算できる値でないと
            // 数字が食い違って見える。
            var accuracy = stats.TotalKeystrokes > 0
                ? ((stats.TotalKeystrokes - stats.TotalMisses) * 100.0 / stats.TotalKeystrokes)
                    .ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                : null;

            var finalRank = latestResult?.FinalRank ?? 0;

            // 一打も叩いていない（打鍵数 0）なら正確率の行ごと落とす。
            // 0.0% は不当に低く、100.0% は不当に高い。どちらも書かないのが正しい。
            var accuracyLine = accuracy != null ? $"\n正確率：{accuracy}%" : string.Empty;

            // 順位は見出し側（BuildHeadline）が書く。ここで繰り返さない。
            var body =
                $"打鍵数：{stats.TotalKeystrokes}\n" +
                $"ミス数：{stats.TotalMisses}" +
                accuracyLine;

            // ★「作ったたこ焼きの数」は PersonalResult.TakoyakiCount。
            // stats.ServedCount は**提供した「客」の数**で、1客が複数個を持っていくため桁が変わる
            // （ResultStatsBoardView の「たこ焼き数」も TakoyakiCount 側を出している）。
            // ここで ServedCount を渡すと、画面の数字と投稿の数字が食い違う。
            var takoyakiCount = latestResult?.TakoyakiCount ?? 0;
            var headline = BuildHeadline(finalRank, latestStoreName, takoyakiCount);
            var text = $"{headline}\n\n{body}\n\n#たこ打99 #THEHACK2026";

            var url = "https://x.com/intent/post?text=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(text);
            Application.OpenURL(url);
        }

        /// <summary>
        /// 投稿の見出し（3行）。X のタイムラインは先頭数行しか見えないので、
        /// 一番の話題性がある「順位」を独立した行で先に見せ、そのあと個人成績へ移る。
        ///
        /// <code>
        /// 🏆優勝🏆
        /// たこ焼き店Aは1位でした！
        /// 123個のたこ焼きを作りました！
        /// </code>
        ///
        /// 4位以下は称号の行を出さず2行になる。MatchEnd 未着（<paramref name="finalRank"/> が 0）のときは
        /// 順位を書かず、たこ焼きの数だけを出す。
        ///
        /// <paramref name="takoyakiCount"/> は <c>PersonalResult.TakoyakiCount</c>（作ったたこ焼きの総数）。
        /// **<c>MatchStats.ServedCount</c>（提供した客の数）を渡さないこと。**
        /// </summary>
        private static string BuildHeadline(int finalRank, string storeName, int takoyakiCount)
        {
            // 上位3位だけ称号を独立した1行に置く（それだけで一度改行する）。
            string titleLine;
            switch (finalRank)
            {
                case 1:
                    titleLine = "🏆優勝🏆\n";
                    break;
                case 2:
                    titleLine = "🥈準優勝🥈\n";
                    break;
                case 3:
                    titleLine = "🥉3位入賞🥉\n";
                    break;
                default:
                    titleLine = string.Empty;
                    break;
            }

            var rankLine = finalRank > 0 ? $"{storeName}は{finalRank}位でした！\n" : string.Empty;

            // 順位が無いときだけ、たこ焼きの行に店名を添える（誰の投稿か分からなくなるため）。
            var countLine = finalRank > 0
                ? $"{takoyakiCount}個のたこ焼きを作りました！"
                : $"{storeName}は{takoyakiCount}個のたこ焼きを作りました！";

            return titleLine + rankLine + countLine;
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
