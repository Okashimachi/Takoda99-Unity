// 仕様書: Unity/docs/.sdd/match-view/06-view-sample-data.md
// 開発用。サンプル値で主画面・小画面のViewを駆動する。本番シーンには置かない。
// サーバーの挙動を模したシミュレーション（評価計算・客分配・脱落判定）は書かない。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Takoda99.View.Sample
{
    /// <summary>開発用。サンプル値で主画面・小画面のViewを駆動する。本番シーンには置かない。</summary>
    public sealed class MainGameViewSampleDriver : MonoBehaviour
    {
        [SerializeField] private MainStoreView mainStore;
        [SerializeField] private TakoyakiStandView takoyakiStand;
        [SerializeField] private SubStoreBoardView subStoreBoard;

        [Header("自店のサンプル値")]
        [SerializeField, Range(0, 3)] private int creditLife = 3;
        [SerializeField, Range(0f, 1f)] private float evalNormalized = 0.5f;
        [SerializeField] private bool alive = true;
        [SerializeField] private string sampleHiragana = "たこやき";
        [SerializeField] private string sampleRoma = "takoyaki";
        [SerializeField] private int typedHiraganaLength;
        [SerializeField] private int typedRomaLength;
        [SerializeField] private int typedWordCount;

        [Header("他店のサンプル値")]
        [SerializeField] private string selfStoreId = "50";
        [SerializeField] private int eliminateStoreCount;

        private List<string> otherStoreIds = new List<string>();

        private void Start()
        {
            BuildOtherStoreIds();

            if (subStoreBoard != null)
            {
                subStoreBoard.Bind(otherStoreIds);
            }

            Apply();
        }

        private void BuildOtherStoreIds()
        {
            otherStoreIds = new List<string>();
            for (var i = 1; i <= 99; i++)
            {
                var id = i.ToString();
                if (id != selfStoreId)
                {
                    otherStoreIds.Add(id);
                }
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                creditLife = Mathf.Clamp(creditLife - 1, 0, 3);
                Apply();
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                creditLife = Mathf.Clamp(creditLife + 1, 0, 3);
                Apply();
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                evalNormalized = Mathf.Clamp01(evalNormalized - 0.1f);
                Apply();
            }

            if (keyboard.digit4Key.wasPressedThisFrame)
            {
                evalNormalized = Mathf.Clamp01(evalNormalized + 0.1f);
                Apply();
            }

            if (keyboard.digit5Key.wasPressedThisFrame)
            {
                typedWordCount = Mathf.Max(0, typedWordCount - 1);
                Apply();
            }

            if (keyboard.digit6Key.wasPressedThisFrame)
            {
                typedWordCount = typedWordCount + 1;
                Apply();
            }

            if (keyboard.digit7Key.wasPressedThisFrame)
            {
                AdjustTypedRomaLength(-1);
                Apply();
            }

            if (keyboard.digit8Key.wasPressedThisFrame)
            {
                AdjustTypedRomaLength(1);
                Apply();
            }

            if (keyboard.digit9Key.wasPressedThisFrame)
            {
                EliminateNextStore();
            }

            if (keyboard.digit0Key.wasPressedThisFrame)
            {
                ResetSampleValues();
            }
        }

        private void AdjustTypedRomaLength(int delta)
        {
            var maxRoma = string.IsNullOrEmpty(sampleRoma) ? 0 : sampleRoma.Length;
            typedRomaLength = Mathf.Clamp(typedRomaLength + delta, 0, maxRoma);

            // ひらがなの確定文字数はローマ字との比率で連動させる（打鍵判定はしない）。
            var maxHiragana = string.IsNullOrEmpty(sampleHiragana) ? 0 : sampleHiragana.Length;
            typedHiraganaLength = maxRoma > 0
                ? Mathf.Clamp(Mathf.RoundToInt((float)typedRomaLength / maxRoma * maxHiragana), 0, maxHiragana)
                : 0;
        }

        private void EliminateNextStore()
        {
            if (subStoreBoard == null)
            {
                return;
            }

            if (eliminateStoreCount >= otherStoreIds.Count)
            {
                return;
            }

            var storeId = otherStoreIds[eliminateStoreCount];
            eliminateStoreCount++;

            subStoreBoard.SetSummary(storeId, 0, false);

            // サンプル専用の仮値（SV-15確定まで）。本番コードへ持ち込まない。
            var rank = 98 - eliminateStoreCount + 1;
            subStoreBoard.SetRank(storeId, rank);
        }

        private void ResetSampleValues()
        {
            creditLife = 3;
            evalNormalized = 0.5f;
            alive = true;
            typedHiraganaLength = 0;
            typedRomaLength = 0;
            typedWordCount = 0;
            eliminateStoreCount = 0;

            if (subStoreBoard != null)
            {
                subStoreBoard.Bind(otherStoreIds);
            }

            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            if (mainStore != null)
            {
                mainStore.SetCreditLife(creditLife);
                mainStore.SetEvaluation(evalNormalized, alive);
                mainStore.SetWord(sampleHiragana, sampleRoma);
                mainStore.SetTypedProgress(typedHiraganaLength, typedRomaLength);
            }

            if (takoyakiStand != null)
            {
                takoyakiStand.SetTypedWordCount(typedWordCount);
            }
        }
    }
}
