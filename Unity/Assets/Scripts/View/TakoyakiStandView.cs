// 仕様書: Unity/docs/.sdd/09-takoyaki-stand-view.md
// たこ焼き台全体（6列×4行＝24穴）。root/MainStoreCanvas/Main/MainStore/Takoyakis にアタッチする。
//
// slots / mainStore は Inspector で手動配線しない。Takoyakis 直下の4つの行オブジェクト
// （各6個の TakoyakiSlotView）と、親階層の MainStoreView を実行時に自動収集する。
// Takoyakis への参照だけで全24穴を操作できるようにするための設計。

using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>たこ焼き台全体（6列×4行＝24穴）。root/.../MainStore/Takoyakis にアタッチする。</summary>
    public sealed class TakoyakiStandView : MonoBehaviour
    {
        private MainStoreView mainStore;
        private TakoyakiSlotView[] slots; // 長さ24。行優先・左上原点

        private int typedWordCount;

        private void Awake()
        {
            CollectSlots();
            mainStore = GetComponentInParent<MainStoreView>();

            if (slots.Length != TakoyakiStandState.StandCapacity)
            {
                Debug.LogError(
                    $"{nameof(TakoyakiStandView)}: 子階層から集めた穴の数が {TakoyakiStandState.StandCapacity} ではありません（{slots.Length}個）。"
                    + " Takoyakis 直下に4つの行オブジェクト（各6個の TakoyakiSlotView）があるか確認してください。",
                    this);
            }

            if (mainStore == null)
            {
                Debug.LogError($"{nameof(TakoyakiStandView)}: 親階層に {nameof(MainStoreView)} が見つかりません。", this);
            }
        }

        /// <summary>
        /// Takoyakis の直接の子（行オブジェクト）を上から順に走査し、
        /// 各行の子に付いた TakoyakiSlotView を左から順に集める。行数・列数は問わない。
        /// </summary>
        private void CollectSlots()
        {
            var collected = new System.Collections.Generic.List<TakoyakiSlotView>(TakoyakiStandState.StandCapacity);

            foreach (Transform line in transform)
            {
                foreach (Transform slot in line)
                {
                    var view = slot.GetComponent<TakoyakiSlotView>();
                    if (view != null)
                    {
                        collected.Add(view);
                    }
                }
            }

            slots = collected.ToArray();
        }

        private void OnEnable()
        {
            if (mainStore == null)
            {
                return;
            }

            mainStore.EvalLevelChanged += OnEvalLevelChanged;
            Apply(mainStore.EvalLevel, typedWordCount);
        }

        private void OnDisable()
        {
            if (mainStore != null)
            {
                mainStore.EvalLevelChanged -= OnEvalLevelChanged;
            }
        }

        /// <summary>いま対応中の客のノルマのうち、入力を終えた語数。</summary>
        public void SetTypedWordCount(int newTypedWordCount)
        {
            typedWordCount = newTypedWordCount < 0 ? 0 : newTypedWordCount;
            var evalLevel = mainStore != null ? mainStore.EvalLevel : StoreEvalLevel.Low;
            Apply(evalLevel, typedWordCount);
        }

        private void OnEvalLevelChanged(StoreEvalLevel evalLevel)
        {
            Apply(evalLevel, typedWordCount);
        }

        private void Apply(StoreEvalLevel evalLevel, int currentTypedWordCount)
        {
            if (slots == null)
            {
                return;
            }

            var state = TakoyakiStandState.From(evalLevel, currentTypedWordCount);

            // 24穴すべてを再適用する（差分更新はしない。24個なのでコストは無視できる）。
            for (var i = 0; i < slots.Length && i < state.Slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].SetState(state.Slots[i]);
                }
            }
        }
    }
}
