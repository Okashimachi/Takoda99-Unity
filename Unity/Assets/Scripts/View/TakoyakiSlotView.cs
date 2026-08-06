// 仕様書: Unity/docs/.sdd/09-takoyaki-stand-view.md
// たこ焼き1個ぶんの穴の見た目。Assets/Prefabs/MainStoreCanvas/Takoyaki.prefab にアタッチする。

using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>たこ焼き1個ぶんの穴の見た目。Assets/Prefabs/MainStoreCanvas/Takoyaki.prefab にアタッチする。</summary>
    public sealed class TakoyakiSlotView : MonoBehaviour
    {
        [SerializeField] private GameObject raw;   // TakoyakiRaw（生地）
        [SerializeField] private GameObject done;  // TakoyakiDone（焼き）

        public TakoyakiSlotState State { get; private set; }

        private void Awake()
        {
            if (raw == null)
            {
                Debug.LogError($"{nameof(TakoyakiSlotView)}.{nameof(raw)} が未設定です。", this);
            }

            if (done == null)
            {
                Debug.LogError($"{nameof(TakoyakiSlotView)}.{nameof(done)} が未設定です。", this);
            }
        }

        private void Start()
        {
            SetState(TakoyakiSlotState.Empty);
        }

        public void SetState(TakoyakiSlotState state)
        {
            State = state;

            switch (state)
            {
                case TakoyakiSlotState.Empty:
                    SetActive(raw, false);
                    SetActive(done, false);
                    break;
                case TakoyakiSlotState.Batter:
                    SetActive(raw, true);
                    SetActive(done, false);
                    break;
                case TakoyakiSlotState.Cooked:
                    // Cooked で生地パネルを消してから焼きパネルを出す（重ね表示にしない）。
                    SetActive(raw, false);
                    SetActive(done, true);
                    break;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
