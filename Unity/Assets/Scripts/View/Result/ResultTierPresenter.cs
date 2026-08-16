// 仕様書: Unity/docs/.sdd/result-view/02-result-rank-tier.md §3
// 1つの Tier の演出とジングル。Prefab として4つ用意し、Show はどれを再生するか選ぶだけにする
// （if の中に演出を書き分けると、アートの差し替えが Show の改修になってしまう）。

using TMPro;
using Takoda99.Client.State;
using UnityEngine;

namespace Takoda99.View.Result
{
    /// <summary>1つの Tier（Champion / Podium / Finalist / Standard）の演出。</summary>
    public sealed class ResultTierPresenter : MonoBehaviour
    {
        private const string Blank = "--";

        [SerializeField] private GameObject presenterRoot;
        [SerializeField] private AudioSource jingle;
        [SerializeField] private AudioClip jingleClip;

        [Header("Tier 共通の表示")]
        [SerializeField] private TMP_Text finalRankText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text takoyakiCountText;
        [SerializeField] private TMP_Text totalMissesText;

        /// <summary>この Tier の演出を再生する。result が null なら数字を空欄にして成立させる。</summary>
        public void Play(PersonalResultState result)
        {
            if (presenterRoot != null)
            {
                presenterRoot.SetActive(true);
            }

            if (jingle != null && jingleClip != null)
            {
                jingle.PlayOneShot(jingleClip);
            }

            if (result == null)
            {
                SetText(finalRankText, Blank);
                SetText(scoreText, Blank);
                SetText(takoyakiCountText, Blank);
                SetText(totalMissesText, Blank);
                return;
            }

            SetText(finalRankText, result.FinalRank >= 1 ? result.FinalRank + "位" : Blank);

            // 試合中は順位が主役だったが、リザルトでは具体的な数字が達成感になる（企画書 3.8）。
            SetText(scoreText, result.Score.ToString());
            SetText(takoyakiCountText, result.TakoyakiCount.ToString());
            SetText(totalMissesText, result.Stats != null ? result.Stats.TotalMisses.ToString() : Blank);
        }

        /// <summary>選ばれなかった Tier を畳む。</summary>
        public void Hide()
        {
            if (presenterRoot != null)
            {
                presenterRoot.SetActive(false);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
