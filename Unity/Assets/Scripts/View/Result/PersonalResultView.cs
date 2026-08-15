// 仕様書: Unity/docs/.sdd/result-view/01-personal-result-view.md
//
// ★この画面が守る一線：サーバーへ問い合わせない。保持しているデータを表示するだけ。
//
// 予選のバグは「画面遷移のタイミングとデータ受信のタイミングが結びついていた」こと（プレイヤーが
// ボタンを押す速さにデータの有無が依存した）。本選では PersonalResult が脱落した瞬間に届き、
// ClientState.PersonalResult に保持されている。**この画面はいつ開いても壊れない。**

using TMPro;
using Takoda99.Client.State;
using UnityEngine;

namespace Takoda99.View.Result
{
    /// <summary>保持している個人成績を表示するだけの画面。</summary>
    public sealed class PersonalResultView : MonoBehaviour
    {
        /// <summary>値が無いときの表記。ローディング表示は作らない（待つ設計こそが予選のバグ）。</summary>
        private const string Blank = "--";

        [SerializeField] private GameObject panelRoot;

        [Header("主役")]
        [SerializeField] private TMP_Text finalRankText;
        [SerializeField] private TMP_Text scoreText;

        [Header("内訳")]
        [SerializeField] private TMP_Text takoyakiCountText;
        [SerializeField] private TMP_Text totalMissesText;
        [SerializeField] private TMP_Text totalKeystrokesText;
        [SerializeField] private TMP_Text avgAccuracyText;
        [SerializeField] private TMP_Text servedCountText;
        [SerializeField] private TMP_Text survivedText;

        [Header("最速・最遅（提供0なら欄ごと隠す）")]
        [SerializeField] private GameObject fastestRow;
        [SerializeField] private TMP_Text fastestText;
        [SerializeField] private GameObject slowestRow;
        [SerializeField] private TMP_Text slowestText;

        [Header("客の属性別内訳（成績の彩り）")]
        [SerializeField] private TMP_Text normalServedText;
        [SerializeField] private TMP_Text bonusServedText;
        [SerializeField] private TMP_Text claimerServedText;
        [SerializeField] private TMP_Text buzzServedText;

        /// <summary>
        /// 保持データを表示する。<paramref name="result"/> が null でも例外を出さず、空欄で成立させる
        /// （画面から出る導線は必ず生きている）。何度呼んでも同じ内容になる（冪等）。
        /// </summary>
        public void Show(PersonalResultState result)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (result == null)
            {
                ShowBlank();
                return;
            }

            SetText(finalRankText, result.FinalRank >= 1 ? result.FinalRank + "位" : Blank);

            // 試合中は補助だったが、ここでは大きく出す。負値もそのまま（クランプしない）。
            SetText(scoreText, result.Score.ToString());

            // ★Stats.ServedCount（提供した「客」の数）と混同しない。
            SetText(takoyakiCountText, result.TakoyakiCount.ToString());
            SetText(survivedText, (result.SurvivedMs / 1000) + "秒");

            var stats = result.Stats;
            if (stats == null)
            {
                ShowStatsBlank();
                return;
            }

            // 総ミス数は PersonalResult 直下ではなく Stats 側にある。
            SetText(totalMissesText, stats.TotalMisses.ToString());
            SetText(totalKeystrokesText, stats.TotalKeystrokes.ToString());
            SetText(avgAccuracyText, Mathf.RoundToInt((float)stats.AvgAccuracy * 100f) + "%");
            SetText(servedCountText, stats.ServedCount.ToString());

            // 提供0なら 0 が届く。そのときは欄ごと出さない。
            SetOptional(fastestRow, fastestText, stats.FastestMs);
            SetOptional(slowestRow, slowestText, stats.SlowestMs);

            // 属性がスコアに影響しなくなっても、この数字は「成績の彩り」として出せる。
            // Left（取りこぼし）は常に 0 なので出さない。
            SetText(normalServedText, stats.Normal != null ? stats.Normal.Served.ToString() : Blank);
            SetText(bonusServedText, stats.Bonus != null ? stats.Bonus.Served.ToString() : Blank);
            SetText(claimerServedText, stats.Claimer != null ? stats.Claimer.Served.ToString() : Blank);
            SetText(buzzServedText, stats.Buzz != null ? stats.Buzz.Served.ToString() : Blank);
        }

        public void Close()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>データが無いことが分かる画面を出す（画面を出さない、はしない）。</summary>
        private void ShowBlank()
        {
            SetText(finalRankText, Blank);
            SetText(scoreText, Blank);
            SetText(takoyakiCountText, Blank);
            SetText(survivedText, Blank);
            ShowStatsBlank();
        }

        private void ShowStatsBlank()
        {
            SetText(totalMissesText, Blank);
            SetText(totalKeystrokesText, Blank);
            SetText(avgAccuracyText, Blank);
            SetText(servedCountText, Blank);
            SetText(normalServedText, Blank);
            SetText(bonusServedText, Blank);
            SetText(claimerServedText, Blank);
            SetText(buzzServedText, Blank);
            SetActive(fastestRow, false);
            SetActive(slowestRow, false);
        }

        private static void SetOptional(GameObject row, TMP_Text text, int valueMs)
        {
            var show = valueMs > 0;
            SetActive(row, show);
            if (show)
            {
                SetText(text, (valueMs / 1000f).ToString("0.0") + "秒");
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
