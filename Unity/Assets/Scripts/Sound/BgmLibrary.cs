// 全BGMの実体（AudioClip）と音量を ScriptableObject 1つで一括管理する（SoundLibrary と同じ方針）。
// 再生側は BgmId だけを知り、どのファイルをどの音量で鳴らすかは一切持たない。
//
// SE と違って意味単位のまとまり（カテゴリ）は持たない。BGM は同時に1つしか鳴らないため、
// 「マスター × 個別」の2段で足りる。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Takoda99.Sound
{
    /// <summary>全BGMの実体と音量を一括管理する ScriptableObject。</summary>
    [CreateAssetMenu(fileName = "BgmLibrary", menuName = "Takoda99/Bgm Library")]
    public sealed class BgmLibrary : ScriptableObject
    {
        [Header("マスター")]
        [Tooltip("全BGMに掛かる音量。0 で完全に無音。")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        [Tooltip("OFF にすると BGM を一切鳴らさない。デバッグ用。")]
        [SerializeField] private bool muted;

        [Header("BGM（曲ごとに音量を均す）")]
        [SerializeField]
        private List<BgmEntry> entries = new List<BgmEntry>();

        /// <summary>BgmId → エントリの索引。初回参照時に組み、以降は辞書引きで済ませる。</summary>
        private Dictionary<BgmId, ResolvedEntry> index;

        public float MasterVolume => masterVolume;
        public bool Muted => muted;

        /// <summary>
        /// 鳴らすべき実体と音量を引く。未登録・クリップ未割り当て・音量0のいずれでも
        /// <c>false</c> を返し、呼び出し側は何もしない。
        /// </summary>
        public bool TryResolve(BgmId id, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 0f;

            if (muted || id == BgmId.None)
            {
                return false;
            }

            EnsureIndex();

            if (!index.TryGetValue(id, out var entry) || entry.Clip == null)
            {
                return false;
            }

            volume = Mathf.Clamp01(masterVolume * entry.Volume);
            if (volume <= 0f)
            {
                return false;
            }

            clip = entry.Clip;
            return true;
        }

        /// <summary>Inspector で値を触ったら索引を作り直す（再生中に音量スライダーを動かせるように）。</summary>
        private void OnValidate()
        {
            index = null;
        }

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<BgmId, ResolvedEntry>();

            foreach (var entry in entries)
            {
                if (entry == null || entry.Id == BgmId.None)
                {
                    continue;
                }

                if (index.ContainsKey(entry.Id))
                {
                    Debug.LogWarning($"{nameof(BgmLibrary)}: {entry.Id} が複数登録されています。先に定義された方を使います。", this);
                    continue;
                }

                index[entry.Id] = new ResolvedEntry(entry.Clip, entry.Volume);
            }
        }

        private readonly struct ResolvedEntry
        {
            public ResolvedEntry(AudioClip clip, float volume)
            {
                Clip = clip;
                Volume = volume;
            }

            public AudioClip Clip { get; }
            public float Volume { get; }
        }

        /// <summary>BGM 1曲ぶん。素材ごとの録音レベルの差はここの音量で均す。</summary>
        [Serializable]
        public sealed class BgmEntry
        {
            [SerializeField] private BgmId id = BgmId.None;
            [SerializeField] private AudioClip clip;

            [Tooltip("この曲個別の音量。素材ごとの音量差を均すために使う。")]
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            public BgmId Id => id;
            public AudioClip Clip => clip;
            public float Volume => volume;
        }
    }
}
