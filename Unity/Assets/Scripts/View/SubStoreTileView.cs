// 仕様書: Unity/docs/.sdd/10-sub-store-board-view.md
// 1店舗ぶんのタイルの見た目（SubStorePanel Prefab）。脱落の判定・順位の算出はしない。

using Takoda99.View.ValueObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View
{
    public sealed class SubStoreTileView : MonoBehaviour
    {
        [SerializeField] private Image booth;              // SubStore
        [SerializeField] private GameObject rankPanel;      // SubStoreRankPanel（既定で非アクティブ）
        [SerializeField] private TextMeshProUGUI rankText;  // SubStoreRankPanel/Text
        [SerializeField] private Sprite boothLife0;         // minitile_booth_life0
        [SerializeField] private Sprite boothLife1;
        [SerializeField] private Sprite boothLife2;
        [SerializeField] private Sprite boothLife3;
        [SerializeField] private float eliminationRevealDelaySec = 3f;

        private int creditLife;
        private bool alive = true;
        private float elapsedSinceEliminatedSec;
        private int? rank;
        private bool bound;

        public string StoreId { get; private set; }

        public SubStoreTileState State { get; private set; }

        private void Awake()
        {
            if (booth == null)
            {
                Debug.LogError($"{nameof(SubStoreTileView)}.{nameof(booth)} が未設定です。", this);
            }

            if (rankPanel != null)
            {
                rankPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Alive == false かつ JustEliminated の間だけ経過時間を加算する。
            if (!alive && State == SubStoreTileState.JustEliminated)
            {
                elapsedSinceEliminatedSec += Time.deltaTime;
                Recompute();
            }
        }

        public void Bind(string storeId)
        {
            StoreId = storeId;
            creditLife = 3;
            alive = true;
            elapsedSinceEliminatedSec = 0f;
            rank = null;
            bound = true;

            if (rankPanel != null)
            {
                rankPanel.SetActive(false);
            }

            Recompute();
        }

        /// <summary>StoreListUpdate 由来の値を反映する。</summary>
        public void SetSummary(int newCreditLife, bool newAlive)
        {
            if (!bound)
            {
                return;
            }

            // 一度 Eliminated になったタイルは、以後の SetSummary で Life* へ戻らない（脱落は不可逆）。
            if (State == SubStoreTileState.Eliminated)
            {
                return;
            }

            if (alive && !newAlive)
            {
                elapsedSinceEliminatedSec = 0f;
            }

            creditLife = newCreditLife;
            alive = newAlive;
            Recompute();
        }

        /// <summary>完全脱落時に表示する順位。未確定なら null を渡す（順位テキストを空にする）。</summary>
        public void SetRank(int? newRank)
        {
            rank = newRank;
            if (State == SubStoreTileState.Eliminated)
            {
                ApplyRankText();
            }
        }

        private void Recompute()
        {
            var next = SubStoreTileStateCalculator.From(creditLife, alive, elapsedSinceEliminatedSec, eliminationRevealDelaySec);
            if (next == State)
            {
                return;
            }

            State = next;
            Apply();
        }

        private void Apply()
        {
            switch (State)
            {
                case SubStoreTileState.Life3:
                    ShowBooth(boothLife3);
                    break;
                case SubStoreTileState.Life2:
                    ShowBooth(boothLife2);
                    break;
                case SubStoreTileState.Life1:
                    ShowBooth(boothLife1);
                    break;
                case SubStoreTileState.JustEliminated:
                    ShowBooth(boothLife0);
                    break;
                case SubStoreTileState.Eliminated:
                    if (booth != null)
                    {
                        booth.enabled = false;
                    }

                    if (rankPanel != null)
                    {
                        rankPanel.SetActive(true);
                    }

                    ApplyRankText();
                    break;
            }
        }

        private void ShowBooth(Sprite sprite)
        {
            if (booth != null)
            {
                booth.enabled = true;
                booth.sprite = sprite;
            }

            if (rankPanel != null)
            {
                rankPanel.SetActive(false);
            }
        }

        private void ApplyRankText()
        {
            if (rankText != null)
            {
                rankText.text = rank.HasValue ? rank.Value.ToString() : string.Empty;
            }
        }
    }
}
