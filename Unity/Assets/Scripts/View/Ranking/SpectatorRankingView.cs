// 仕様書: Unity/docs/.sdd/ranking-view/03-spectator-ranking-view.md
// 観戦画面の全プレイヤー順位一覧。99人中89人は120秒より前に脱落して観戦側にいるため、
// 「多数派が最も長く見る画面」であり優先度は低くない。
//
// 精度は追わない（眺めるためのもの）。差分の取りこぼしは定期的な全量配信で直る。

using System.Collections.Generic;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Ranking
{
    /// <summary>全99店を1本のリストとして描く観戦画面。</summary>
    public sealed class SpectatorRankingView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RankingRowView rowPrefab;   // 01 と同じ行Prefabを再利用
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private float rowHeight = 56f;

        private RankingRowPool pool;
        private readonly HashSet<string> visibleIds = new HashSet<string>();

        private bool isOpen;

        private void Awake()
        {
            pool = new RankingRowPool(rowPrefab, content);
            SetPanelVisible(false);
        }

        /// <summary>画面を開く。自分の行までスクロールする。</summary>
        public void Open(ClientState state)
        {
            isOpen = true;
            SetPanelVisible(true);
            ApplyCore(state);
            ScrollToSelf(state);
        }

        /// <summary>state 変化のたびに呼ぶ。開いていなければ何もしない。</summary>
        public void Apply(ClientState state)
        {
            if (!isOpen)
            {
                return;
            }

            // S6: 以降のスクロール位置はユーザーの操作を優先する。ここでは戻さない。
            ApplyCore(state);
        }

        public void Close()
        {
            isOpen = false;
            SetPanelVisible(false);
            pool?.ReleaseAll();
        }

        private void ApplyCore(ClientState state)
        {
            if (state == null || pool == null)
            {
                return;
            }

            // S1: Ranking.Rows は Rank 昇順で保持されている。そのままの順で描く（再ソートしない）。
            // S2: 自分の行は state.Rank / state.Score の権威値で上書きされる。
            var rows = RankingRowsBuilder.BuildAll(state.Ranking, state.SelfStoreId, state.Rank, state.Score);

            if (content != null)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, rowHeight * rows.Count);
            }

            // 位置移動のアニメーションは付けない（99行が一斉に動くと読めなくなる）。
            RankingRowLayout.Apply(pool, rows, visibleIds, rowHeight, 0f);
        }

        /// <summary>S5: Open した瞬間、自分の行が画面中央に来る位置へスクロールする。</summary>
        private void ScrollToSelf(ClientState state)
        {
            if (scroll == null || state == null)
            {
                return;
            }

            var rows = state.Ranking.Rows;
            var index = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].StoreId == state.SelfStoreId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || rows.Count <= 1)
            {
                return;
            }

            // ScrollRect の verticalNormalizedPosition は 1 が先頭・0 が末尾。
            var ratio = (float)index / (rows.Count - 1);
            scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - ratio);
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }
    }
}
