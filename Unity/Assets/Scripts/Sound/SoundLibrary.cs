// 全SEの実体（AudioClip）と音量を ScriptableObject 1つで一括管理する（FontTheme / CustomerSpriteLibrary と同じ方針）。
// 再生側は SoundId だけを知り、どのファイルをどの音量で鳴らすかは一切持たない。
//
// 音量は「マスター × カテゴリ × 個別」の3段で決める。素材ごとの録音レベルの差は個別で均し、
// 「打鍵音がうるさい」のような意味単位の調整はカテゴリのスライダー1本で済ませられるようにする。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Takoda99.Sound
{
    /// <summary>全SEの実体と音量を一括管理する ScriptableObject。</summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Takoda99/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Header("マスター")]
        [Tooltip("全SEに掛かる音量。0 で完全に無音。")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        [Tooltip("OFF にすると SE を一切鳴らさない。デバッグ用。")]
        [SerializeField] private bool muted;

        [Header("意味単位のまとまり（上から順に並べる）")]
        [SerializeField]
        private List<SoundGroup> groups = new List<SoundGroup>();

        /// <summary>SoundId → エントリの索引。初回参照時に組み、以降は辞書引きで済ませる。</summary>
        private Dictionary<SoundId, ResolvedEntry> index;

        public float MasterVolume => masterVolume;
        public bool Muted => muted;
        public IReadOnlyList<SoundGroup> Groups => groups;

        /// <summary>
        /// 鳴らすべき実体と音量を引く。未登録・クリップ未割り当て・音量0のいずれでも
        /// <c>false</c> を返し、呼び出し側は何もしない。
        /// </summary>
        public bool TryResolve(SoundId id, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 0f;

            if (muted || id == SoundId.None)
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

        /// <summary>登録済みか。未登録のまま呼ばれている SoundId を起動時に洗い出すのに使う。</summary>
        public bool Contains(SoundId id)
        {
            EnsureIndex();
            return index.ContainsKey(id);
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

            index = new Dictionary<SoundId, ResolvedEntry>();

            foreach (var group in groups)
            {
                if (group == null || group.Entries == null)
                {
                    continue;
                }

                foreach (var entry in group.Entries)
                {
                    if (entry == null || entry.Id == SoundId.None)
                    {
                        continue;
                    }

                    if (index.ContainsKey(entry.Id))
                    {
                        Debug.LogWarning($"{nameof(SoundLibrary)}: {entry.Id} が複数登録されています。先に定義された方を使います。", this);
                        continue;
                    }

                    index[entry.Id] = new ResolvedEntry(entry.Clip, group.Volume * entry.Volume);
                }
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

        /// <summary>意味単位のまとまり1つぶん。まとめて音量を上げ下げできる。</summary>
        [Serializable]
        public sealed class SoundGroup
        {
            [Tooltip("Inspector 上の見出し。表示のみで、参照には使わない。")]
            [SerializeField] private string label = "";

            [SerializeField] private SoundCategory category = SoundCategory.Ui;

            [Tooltip("このまとまり全体に掛かる音量。")]
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            [SerializeField] private List<SoundEntry> entries = new List<SoundEntry>();

            public string Label => label;
            public SoundCategory Category => category;
            public float Volume => volume;
            public IReadOnlyList<SoundEntry> Entries => entries;
        }

        /// <summary>SE 1つぶん。素材ごとの録音レベルの差はここの音量で均す。</summary>
        [Serializable]
        public sealed class SoundEntry
        {
            [SerializeField] private SoundId id = SoundId.None;
            [SerializeField] private AudioClip clip;

            [Tooltip("このSE個別の音量。素材ごとの音量差を均すために使う。")]
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            public SoundId Id => id;
            public AudioClip Clip => clip;
            public float Volume => volume;
        }
    }
}
