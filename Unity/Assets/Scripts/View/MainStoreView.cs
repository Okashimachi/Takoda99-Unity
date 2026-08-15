// 仕様書: Unity/docs/.sdd/match-view/02-main-store-view.md
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
        // 屋台の見た目（暖簾・土台）。v0.8.0 では信用ライフが廃止されたため、
        // ライフ段階で差し替えず固定の絵を出す。
        [SerializeField] private Image noren;
        [SerializeField] private Image stand;

        // ---- お題単語 ----
        [SerializeField] private TextMeshProUGUI wordHiragana;   // WordPanel/Hiragana
        [SerializeField] private TextMeshProUGUI wordRoma;       // WordPanel/Roma

        // ---- お題の大型化（hud/02-order-word-emphasis.md §3.1）----
        // TextMeshPro の Auto Size を使わず、単語長から段階的に決める。
        // 閾値・実サイズは実機で詰めるため Inspector 公開値にしている。
        [Header("お題の文字サイズ段階")]
        [Tooltip("この文字数までが最大サイズ（L）。")]
        [SerializeField] private int wordLargeMaxLength = OrderWordSizeRule.DefaultLargeMaxLength;

        [Tooltip("この文字数までが中サイズ（M）。これを超えると小サイズ（S）。")]
        [SerializeField] private int wordMediumMaxLength = OrderWordSizeRule.DefaultMediumMaxLength;

        [SerializeField] private float wordFontSizeLarge = 140f;
        [SerializeField] private float wordFontSizeMedium = 100f;
        [SerializeField] private float wordFontSizeSmall = 72f;

        [Tooltip("ローマ字行はかな行に対するこの倍率で描く。")]
        [SerializeField] private float romaFontScale = 0.45f;
        [SerializeField, Range(0f, 1f)] private float typedAlpha = 0.35f;

        // ---- 注文カウンタ（OrderCounter） ----
        [SerializeField] private TextMeshProUGUI orderNumeratorText;    // OrderCounter/NumeratorText（準備できた数）
        [SerializeField] private TextMeshProUGUI orderDenominatorText;  // OrderCounter/DenominatorText（注文個数）

        // ---- 屋号（PlayerName） ----
        [SerializeField] private TextMeshProUGUI playerNameLeftText;    // PlayerName/LeftText
        [SerializeField] private TextMeshProUGUI playerNameMiddleText;  // PlayerName/MiddleText
        [SerializeField] private TextMeshProUGUI playerNameRightText;   // PlayerName/RightText

        private string currentHiragana = string.Empty;
        private string currentRoma = string.Empty;

        // 直近に描いた値。同じ値での再描画（と文字列の割り当て）を避けるために持つ。
        // -1 は「まだ一度も描いていない」ことを表し、初回の 0/0 を必ず反映させる。
        private int currentPrepared = -1;
        private int currentOrderCount = -1;
        private string currentPlayerName;

        private void Awake()
        {
            CheckReference(noren, nameof(noren));
            CheckReference(stand, nameof(stand));
            CheckReference(wordHiragana, nameof(wordHiragana));
            CheckReference(wordRoma, nameof(wordRoma));
        }

        private void Start()
        {
            SetWord(string.Empty, string.Empty);
            SetOrderProgress(0, 0);
            SetPlayerName(string.Empty);
        }

        /// <summary>
        /// 注文カウンタを反映する。分子は準備できたたこ焼きの数（＝打ち終えた単語数）、分母は注文個数。
        /// </summary>
        /// <param name="preparedCount">準備できた数。0..<paramref name="orderCount"/> にクランプする。</param>
        /// <param name="orderCount">注文個数（サーバー値）。</param>
        public void SetOrderProgress(int preparedCount, int orderCount)
        {
            var total = Math.Max(orderCount, 0);
            var prepared = Clamp(preparedCount, 0, total);

            // Renderer は state 変化のたびに呼ぶ（打鍵1回ごと・毎ティック）。
            // 変わっていなければ ToString の割り当てごと省く。
            if (prepared == currentPrepared && total == currentOrderCount)
            {
                return;
            }

            currentPrepared = prepared;
            currentOrderCount = total;

            if (orderNumeratorText != null)
            {
                orderNumeratorText.text = prepared.ToString();
            }

            if (orderDenominatorText != null)
            {
                orderDenominatorText.text = total.ToString();
            }
        }

        /// <summary>屋号（表示名）を3枠へ割って反映する。割り方は <see cref="PlayerNameLayout"/> の担当。</summary>
        public void SetPlayerName(string displayName)
        {
            var name = displayName ?? string.Empty;
            if (name == currentPlayerName)
            {
                return;
            }

            currentPlayerName = name;
            var layout = PlayerNameLayout.From(name);

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


        /// <summary>お題単語を差し替える。typedRomaLength = 0 の未入力状態にリセットされる。</summary>
        public void SetWord(string hiragana, string roma)
        {
            currentHiragana = hiragana ?? string.Empty;
            currentRoma = roma ?? string.Empty;
            ApplyWordFontSize();
            SetTypedProgress(0, 0);
        }

        /// <summary>
        /// 単語長からサイズ段階を決めて反映する。**単語が変わったときだけ**呼ぶ
        /// （打鍵ごとの SetTypedProgress では呼ばない。同じ単語の途中でサイズが動かないようにする）。
        /// </summary>
        private void ApplyWordFontSize()
        {
            var tier = OrderWordSizeRule.From(currentHiragana, wordLargeMaxLength, wordMediumMaxLength);

            float size;
            switch (tier)
            {
                case OrderWordSizeTier.Large:
                    size = wordFontSizeLarge;
                    break;
                case OrderWordSizeTier.Medium:
                    size = wordFontSizeMedium;
                    break;
                default:
                    size = wordFontSizeSmall;
                    break;
            }

            if (wordHiragana != null)
            {
                wordHiragana.enableAutoSizing = false;
                wordHiragana.fontSize = size;
            }

            if (wordRoma != null)
            {
                wordRoma.enableAutoSizing = false;
                wordRoma.fontSize = size * romaFontScale;
            }
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
