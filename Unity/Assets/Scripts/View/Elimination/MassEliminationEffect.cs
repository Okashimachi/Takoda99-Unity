// 仕様書: Unity/docs/.sdd/elimination/01-mass-elimination-effect.md
// 一斉脱落の集約演出。1回の StoreEliminatedBatch を1つの演出として再生する。
//
// 6回再生され、1回あたり最大24件（サーバー上限側は49件）が同時に届く。
// **1件ずつ再生する設計は成立しない。**

using Takoda99.Sound;
using TMPro;
using UnityEngine;

namespace Takoda99.View.Elimination
{
    /// <summary>1ステージぶんの一斉脱落をまとめて見せる。</summary>
    public sealed class MassEliminationEffect : MonoBehaviour
    {
        [Header("「今、◯店が一斉に閉店した」")]
        [SerializeField] private GameObject effectRoot;
        [SerializeField] private TMP_Text countText;

        /// <summary>演出時間。**2秒以内**（6回再生されるため、そのたび試合が止まって見えると尺を食う）。</summary>
        [SerializeField] private float durationSeconds = 1.2f;

        /// <summary>全ステージ数。stageIndex から演出の強度を出すのに使う。</summary>
        [SerializeField] private int stageTotal = 6;

        /// <summary>自店が含まれるときの音量倍率。</summary>
        [SerializeField] private float selfVolumeScale = 1.0f;

        private float hideAtRealtime;
        private bool playing;

        /// <summary>1ステージぶんの一斉脱落を1つの演出として再生する。</summary>
        /// <param name="stageIndex">第何段階か（1始まり）。演出の強度に使う。</param>
        /// <param name="count">脱落した店の数。演出の規模に使う。</param>
        /// <param name="includesSelf">自店が含まれるか。</param>
        public void Play(int stageIndex, int count, bool includesSelf)
        {
            if (count <= 0)
            {
                return;
            }

            // E3: stageIndex が進むほど強くする（1回目は控えめ、6回目が最大）。
            var progress = stageTotal > 1
                ? Mathf.Clamp01((stageIndex - 1) / (float)(stageTotal - 1))
                : 1f;
            var intensity = Mathf.Lerp(0.5f, 1f, progress) * (includesSelf ? selfVolumeScale : 1f);

            // E1: SEは1回。予選の「他店脱落音（都度再生版）」をそのまま使うと24〜49回同時に鳴る。
            // 段階が進むほど、また自店が含まれるときほど大きく鳴らす（音量差だけで区別する）。
            SoundPlayer.Play(SoundId.Eliminated, Mathf.Clamp01(intensity));

            // E4: count を演出に反映してよいが、件数に比例した数のオブジェクトを出さない（WebGL）。
            // 数字を出すのが最も確実で、LTのプレゼン中に「今20人減りました」と言える。
            if (countText != null)
            {
                countText.text = count + "店 閉店";
            }

            if (effectRoot != null)
            {
                effectRoot.SetActive(true);
                effectRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.15f, progress);
            }

            playing = true;
            hideAtRealtime = Time.realtimeSinceStartup + durationSeconds;
        }

        private void Update()
        {
            if (!playing || Time.realtimeSinceStartup < hideAtRealtime)
            {
                return;
            }

            playing = false;
            if (effectRoot != null)
            {
                effectRoot.SetActive(false);
            }
        }
    }
}
