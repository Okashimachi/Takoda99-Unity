// 仕様書: Unity/docs/.sdd/match-view/03-takoyaki-stand-view.md
// たこ焼き台全体（6列×4行＝24穴）。root/MainStoreCanvas/Main/MainStore/Takoyakis にアタッチする。
//
// slots は Inspector で手動配線しない。Takoyakis 直下の4つの行オブジェクト
// （各6個の TakoyakiSlotView）を実行時に自動収集する。
// Takoyakis への参照だけで全24穴を操作できるようにするための設計。

using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>たこ焼き台全体（6列×4行＝24穴）。root/.../MainStore/Takoyakis にアタッチする。</summary>
    public sealed class TakoyakiStandView : MonoBehaviour
    {
        private TakoyakiSlotView[] slots; // 長さ24。行優先・左上原点

        private int typedWordCount;
        private int orderCount;

        private void Awake()
        {
            CollectSlots();

            if (slots.Length != TakoyakiStandState.StandCapacity)
            {
                Debug.LogError(
                    $"{nameof(TakoyakiStandView)}: 子階層から集めた穴の数が {TakoyakiStandState.StandCapacity} ではありません（{slots.Length}個）。"
                    + " Takoyakis 直下に4つの行オブジェクト（各6個の TakoyakiSlotView）があるか確認してください。",
                    this);
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
            Apply(orderCount, typedWordCount);
        }

        /// <summary>いま対応中の客のノルマのうち、入力を終えた語数。</summary>
        public void SetTypedWordCount(int newTypedWordCount)
        {
            typedWordCount = newTypedWordCount < 0 ? 0 : newTypedWordCount;
            Apply(orderCount, typedWordCount);
        }

        /// <summary>いま対応中の客の注文個数（＝台に並べるたこ焼きの個数）。</summary>
        public void SetOrderCount(int newOrderCount)
        {
            orderCount = newOrderCount < 0 ? 0 : newOrderCount;
            Apply(orderCount, typedWordCount);
        }

        private void Apply(int currentOrderCount, int currentTypedWordCount)
        {
            if (slots == null)
            {
                return;
            }

            var state = TakoyakiStandState.From(currentOrderCount, currentTypedWordCount);

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
