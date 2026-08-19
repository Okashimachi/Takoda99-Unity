// BGM の再生窓口。呼び出し側は BgmPlayer.PlayLoop / PlayMatchHalves / Stop だけを知る。
//
// SoundPlayer と同じ理由（4シーンをまたいで鳴らす）で、シーンに置かず起動時に自分で
// 生成して DontDestroyOnLoad で生き残る。BGM は同時に1曲しか鳴らないため AudioSource は1つで足りる。

using System.Collections;
using UnityEngine;

namespace Takoda99.Sound
{
    /// <summary>BGM の再生窓口。シーンをまたいで唯一生存する。</summary>
    public sealed class BgmPlayer : MonoBehaviour
    {
        /// <summary>Resources 直下に置く BgmLibrary の名前。拡張子は付けない。</summary>
        public const string LibraryResourcePath = "BgmLibrary";

        private BgmLibrary library;
        private AudioSource source;
        private Coroutine pendingSecondHalf;

        public static BgmPlayer Instance { get; private set; }

        /// <summary>
        /// 最初のシーンがロードされる前に自分を生成する。SoundPlayer と同じ理由
        /// （Boot シーンを経由しないエディタ再生でも鳴らすため）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(BgmPlayer));
            DontDestroyOnLoad(go);
            go.AddComponent<BgmPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            library = Resources.Load<BgmLibrary>(LibraryResourcePath);
            if (library == null)
            {
                Debug.LogWarning(
                    $"{nameof(BgmPlayer)}: Resources/{LibraryResourcePath} が見つかりません。BGM は鳴りません。",
                    this);
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>ループ再生に切り替える。Title / Matchmaking の通常BGM、リザルトBGM用。</summary>
        public static void PlayLoop(BgmId id)
        {
            Instance?.PlayLoopInternal(id);
        }

        /// <summary>
        /// 試合前半BGMを流し、鳴り終わったら自動で後半BGMへつなぐ。
        /// 前半・後半とも1分尺のためループはしない。
        /// </summary>
        public static void PlayMatchHalves()
        {
            Instance?.PlayMatchHalvesInternal();
        }

        /// <summary>再生中のBGMを完全に止める。後半BGMへの繋ぎ予約も取り消す。</summary>
        public static void Stop()
        {
            Instance?.StopInternal();
        }

        private void PlayLoopInternal(BgmId id)
        {
            CancelPendingSecondHalf();

            if (library == null || !library.TryResolve(id, out var clip, out var volume))
            {
                source.Stop();
                source.clip = null;
                return;
            }

            source.loop = true;
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        private void PlayMatchHalvesInternal()
        {
            CancelPendingSecondHalf();

            if (library == null || !library.TryResolve(BgmId.MatchFirstHalf, out var clip, out var volume))
            {
                source.Stop();
                source.clip = null;
                return;
            }

            source.loop = false;
            source.clip = clip;
            source.volume = volume;
            source.Play();

            pendingSecondHalf = StartCoroutine(PlaySecondHalfAfter(clip.length));
        }

        private IEnumerator PlaySecondHalfAfter(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            pendingSecondHalf = null;

            if (library == null || !library.TryResolve(BgmId.MatchSecondHalf, out var clip, out var volume))
            {
                yield break;
            }

            source.loop = false;
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        private void StopInternal()
        {
            CancelPendingSecondHalf();
            source.Stop();
            source.clip = null;
        }

        private void CancelPendingSecondHalf()
        {
            if (pendingSecondHalf == null)
            {
                return;
            }

            StopCoroutine(pendingSecondHalf);
            pendingSecondHalf = null;
        }
    }
}
