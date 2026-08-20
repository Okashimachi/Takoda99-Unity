// SE の再生窓口。呼び出し側は SoundPlayer.Play(SoundId) だけを知る。
//
// シーンに置かず、起動時に自分で生成して DontDestroyOnLoad で生き残る。
// SE は Title / MatchMaking / MainGame / Result の全シーンから鳴らすため、シーンごとに
// AudioSource を置いて Inspector で結線する形にすると、4シーンぶん結線がずれる余地ができる。
// SoundLibrary は Resources から引く（結線対象がゼロになる）。

using UnityEngine;

namespace Takoda99.Sound
{
    /// <summary>SE の再生窓口。シーンをまたいで唯一生存する。</summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        /// <summary>Resources 直下に置く SoundLibrary の名前。拡張子は付けない。</summary>
        public const string LibraryResourcePath = "SoundLibrary";

        /// <summary>同時に鳴らせる本数。足りないと直前のSEが切れるため、秒読みと打鍵が重なる分を見て多めに取る。</summary>
        private const int VoiceCount = 8;

        private SoundLibrary library;
        private AudioSource[] voices;
        private int nextVoice;

        public static SoundPlayer Instance { get; private set; }

        /// <summary>
        /// 最初のシーンがロードされる前に自分を生成する。
        /// これにより Boot シーンを経由しないエディタ再生（Result シーン単体の確認など）でも SE が鳴る。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(SoundPlayer));
            DontDestroyOnLoad(go);
            go.AddComponent<SoundPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            library = Resources.Load<SoundLibrary>(LibraryResourcePath);
            if (library == null)
            {
                // 鳴らないだけで進行は止まらないが、原因の特定が難しいので名指しで知らせる。
                Debug.LogWarning(
                    $"{nameof(SoundPlayer)}: Resources/{LibraryResourcePath} が見つかりません。SE は鳴りません。",
                    this);
            }

            voices = new AudioSource[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;

                // UI音なので距離減衰を掛けない（2D）。掛けるとカメラ位置で音量が変わる。
                source.spatialBlend = 0f;
                voices[i] = source;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>SE を1回鳴らす。未登録・未割り当て・音量0なら何もしない。</summary>
        /// <param name="id">鳴らすSE。</param>
        /// <param name="volumeScale">その場かぎりの音量倍率（自店が含まれるときだけ大きく、など）。</param>
        /// <returns>鳴らしたクリップの長さ（秒）。鳴らさなかった場合は0。呼び終わりを待ちたい呼び出し側が使う。</returns>
        public static float Play(SoundId id, float volumeScale = 1f)
        {
            return Instance != null ? Instance.PlayInternal(id, volumeScale) : 0f;
        }

        private float PlayInternal(SoundId id, float volumeScale)
        {
            if (library == null || voices == null)
            {
                return 0f;
            }

            if (!library.TryResolve(id, out var clip, out var volume))
            {
                return 0f;
            }

            var finalVolume = Mathf.Clamp01(volume * Mathf.Max(volumeScale, 0f));
            if (finalVolume <= 0f)
            {
                return 0f;
            }

            // 使う AudioSource を順に回す。PlayOneShot は重ねられるが、同じ Source に
            // 積みすぎると音が濁るため、鳴らすたびに別の Source へ散らす。
            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.PlayOneShot(clip, finalVolume);
            return clip.length;
        }
    }
}
