// 仕様書: Unity/docs/.sdd/08-main-store-view.md
// 主画面の自店舗（root/MainStoreCanvas/Main/MainStore）の表示を一括管理する。
// 信用ライフ・評価・お題単語の決定はしない（サーバー権威。受け取って描くだけ）。

using System;
using TMPro;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    /// <summary>主画面の自店舗（root/MainStoreCanvas/Main/MainStore）の表示を一括管理する。</summary>
    public sealed class MainStoreView : MonoBehaviour
    {
        // ---- 信用ライフ（暖簾・屋台土台・提灯） ----
        [SerializeField] private Image noren;
        [SerializeField] private Sprite norenLife1;      // stall_noren_life1
        [SerializeField] private Sprite norenLife2;      // stall_noren_life2
        [SerializeField] private Sprite norenLife3;      // stall_noren_life3

        [SerializeField] private Image stand;
        [SerializeField] private Sprite standLife0;      // stall_booth_life0
        [SerializeField] private Sprite standLife1;
        [SerializeField] private Sprite standLife2;
        [SerializeField] private Sprite standLife3;

        [SerializeField] private Image[] lanterns;       // 添字 0 = Lantern1, 1 = Lantern2, 2 = Lantern3
        [SerializeField] private Sprite lanternOn;       // stall_lantern_on
        [SerializeField] private Sprite lanternOff;      // stall_lantern_off

        // ---- 評価（鉄板） ----
        [SerializeField] private Image griddle;
        [SerializeField] private Sprite griddleNormal;   // stall_griddle_normal
        [SerializeField] private Sprite griddleHot;      // stall_griddle_hot

        // ---- お題単語 ----
        [SerializeField] private TextMeshProUGUI wordHiragana;   // WordPanel/Hiragana
        [SerializeField] private TextMeshProUGUI wordRoma;       // WordPanel/Roma
        [SerializeField, Range(0f, 1f)] private float typedAlpha = 0.35f;

        private string currentHiragana = string.Empty;
        private string currentRoma = string.Empty;
        private StoreVisualState? currentVisualState;

        /// <summary>評価3段階が変化したときに発火する。Takoyakis が購読する。</summary>
        public event Action<StoreEvalLevel> EvalLevelChanged;

        /// <summary>現在の評価3段階。購読開始直後の初期化に使う。</summary>
        public StoreEvalLevel EvalLevel { get; private set; }

        private void Awake()
        {
            CheckReference(noren, nameof(noren));
            CheckReference(stand, nameof(stand));
            CheckReference(griddle, nameof(griddle));
            CheckReference(wordHiragana, nameof(wordHiragana));
            CheckReference(wordRoma, nameof(wordRoma));

            if (lanterns == null || lanterns.Length == 0)
            {
                Debug.LogError($"{nameof(MainStoreView)}.{nameof(lanterns)} が未設定です。", this);
            }
        }

        private void Start()
        {
            SetCreditLife(3);
            SetEvaluation(0d, true);
            SetWord(string.Empty, string.Empty);
        }

        /// <summary>信用ライフ（提灯・暖簾・屋台土台）を反映する。</summary>
        public void SetCreditLife(int creditLife)
        {
            var initialLife = lanterns != null ? lanterns.Length : 3;
            var clamped = Clamp(creditLife, 0, Math.Max(initialLife, 3));

            ApplyNoren(clamped);
            ApplyStand(clamped);
            ApplyLanterns(clamped);
        }

        private void ApplyNoren(int creditLife)
        {
            if (noren == null)
            {
                return;
            }

            if (creditLife <= 0)
            {
                // 暖簾に life0 の画像は存在しないため、非表示にする。
                noren.enabled = false;
                return;
            }

            noren.enabled = true;
            noren.sprite = creditLife switch
            {
                1 => norenLife1,
                2 => norenLife2,
                _ => norenLife3,
            };
        }

        private void ApplyStand(int creditLife)
        {
            if (stand == null)
            {
                return;
            }

            stand.sprite = creditLife switch
            {
                0 => standLife0,
                1 => standLife1,
                2 => standLife2,
                _ => standLife3,
            };
        }

        private void ApplyLanterns(int creditLife)
        {
            if (lanterns == null)
            {
                return;
            }

            // 番号の大きい方から消灯する。GameObject の破棄・非アクティブ化は行わない。
            for (var i = 0; i < lanterns.Length; i++)
            {
                var lantern = lanterns[i];
                if (lantern == null)
                {
                    continue;
                }

                lantern.sprite = i < creditLife ? lanternOn : lanternOff;
            }
        }

        /// <summary>評価を反映する。evalNormalized は 0..1（生存店内パーセンタイル）。</summary>
        public void SetEvaluation(double evalNormalized, bool alive)
        {
            var next = StoreVisualState.From(
                gameObject.name,
                evalNormalized,
                alive,
                StoreEvalThresholds.Default,
                currentVisualState);

            var levelChanged = !currentVisualState.HasValue || currentVisualState.Value.EvalLevel != next.EvalLevel;
            currentVisualState = next;
            EvalLevel = next.EvalLevel;

            ApplyGriddle(next.EvalLevel);

            if (levelChanged)
            {
                EvalLevelChanged?.Invoke(next.EvalLevel);
            }
        }

        private void ApplyGriddle(StoreEvalLevel evalLevel)
        {
            if (griddle == null)
            {
                return;
            }

            griddle.sprite = evalLevel == StoreEvalLevel.High ? griddleHot : griddleNormal;
        }

        /// <summary>お題単語を差し替える。typedRomaLength = 0 の未入力状態にリセットされる。</summary>
        public void SetWord(string hiragana, string roma)
        {
            currentHiragana = hiragana ?? string.Empty;
            currentRoma = roma ?? string.Empty;
            SetTypedProgress(0, 0);
        }

        /// <summary>入力進捗を反映する。引数はいずれも「確定した先頭からの文字数」。</summary>
        public void SetTypedProgress(int typedHiraganaLength, int typedRomaLength)
        {
            if (wordHiragana != null)
            {
                wordHiragana.text = BuildProgressText(currentHiragana, typedHiraganaLength);
            }

            if (wordRoma != null)
            {
                wordRoma.text = BuildProgressText(currentRoma, typedRomaLength);
            }
        }

        private string BuildProgressText(string source, int typedLength)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            if (typedLength <= 0)
            {
                return source;
            }

            if (typedLength >= source.Length)
            {
                return $"<alpha=#{AlphaHex()}>{source}";
            }

            var typed = source.Substring(0, typedLength);
            var rest = source.Substring(typedLength);
            return $"<alpha=#{AlphaHex()}>{typed}<alpha=#FF>{rest}";
        }

        private string AlphaHex()
        {
            var value = Clamp(Mathf.RoundToInt(typedAlpha * 255f), 0, 255);
            return value.ToString("X2");
        }

        private void CheckReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                Debug.LogError($"{nameof(MainStoreView)}.{fieldName} が未設定です。", this);
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
