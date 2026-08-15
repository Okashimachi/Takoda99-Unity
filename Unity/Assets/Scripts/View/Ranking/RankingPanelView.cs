// 仕様書: Unity/docs/.sdd/ranking-view/01-ranking-panel.md
// 試合中のランキングパネル（上位N＋自分）。順位・スコアは計算せず、Ranking が持つ値を描くだけ。

using System.Collections.Generic;
using DG.Tweening;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>上位N名＋自分を描くランキングパネル。</summary>
    public sealed class RankingPanelView : MonoBehaviour
    {
        [SerializeField] private RankingRowView rowPrefab;
        [SerializeField] private RectTransform rowsRoot;

        /// <summary>
        /// 表示件数。**10 を下回る値を設定しない**。100秒時点の生存数が10人＝
        /// 上位10名リストがそのまま生存者全員になるため（ranking-view/01 §4）。
        /// </summary>
        [SerializeField] private int visibleCount = 10;

        /// <summary>行の移動にかける秒数。0 で即時。</summary>
        [SerializeField] private float rowMoveDuration = 0.25f;

        /// <summary>1行ぶんの高さ（px）。行の目標位置の算出に使う。</summary>
        [SerializeField] private float rowHeight = 56f;

        private RankingRowPool pool;
        private readonly HashSet<string> visibleIds = new HashSet<string>();

        private void Awake()
        {
            if (visibleCount < RankingRowsBuilder.MinVisibleCount)
            {
                Debug.LogWarning(
                    $"{nameof(RankingPanelView)}.{nameof(visibleCount)} が {visibleCount} でした。" +
                    $"決勝では上位{RankingRowsBuilder.MinVisibleCount}名＝生存者全員になるため、" +
                    $"{RankingRowsBuilder.MinVisibleCount} にクランプします。",
                    this);
                visibleCount = RankingRowsBuilder.MinVisibleCount;
            }

            pool = new RankingRowPool(rowPrefab, rowsRoot);
        }

        /// <summary>state から表示行を組み立てて反映する。Renderer が state 変化のたびに呼ぶ。</summary>
        public void Apply(ClientState state)
        {
            if (state == null)
            {
                return;
            }

            var rows = RankingRowsBuilder.Build(
                state.Ranking,
                state.SelfStoreId,
                state.Rank,
                state.Score,
                visibleCount);

            // Ranking.Rows が空ならパネルごと非表示。空リストの枠だけ出さない。
            if (rows.Count == 0)
            {
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(true);
            RankingRowLayout.Apply(pool, rows, visibleIds, rowHeight, rowMoveDuration);
        }

        /// <summary>待機中・リザルトで畳む。</summary>
        public void SetPanelVisible(bool visible)
        {
            if (rowsRoot != null && rowsRoot.gameObject.activeSelf != visible)
            {
                rowsRoot.gameObject.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// 行の並べ替えとアニメーション。パネル・観戦画面で同じ規則を使うため切り出している。
    /// </summary>
    internal static class RankingRowLayout
    {
        public static void Apply(
            RankingRowPool pool,
            IReadOnlyList<RankingRowViewState> rows,
            HashSet<string> visibleIds,
            float rowHeight,
            float moveDuration)
        {
            if (pool == null)
            {
                return;
            }

            visibleIds.Clear();
            for (var i = 0; i < rows.Count; i++)
            {
                visibleIds.Add(rows[i].StoreId);
            }

            // リストから出ていった行はプールへ戻す（破棄しない）。
            pool.ReleaseAllExcept(visibleIds);

            for (var i = 0; i < rows.Count; i++)
            {
                var state = rows[i];
                var row = pool.Acquire(state.StoreId);
                if (row == null)
                {
                    continue;
                }

                // A4: 順位の数字はアニメーションさせない。位置だけ動かし、数字は即時更新する
                // （読めない時間を作らない）。
                row.SetState(state);

                var rect = row.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                row.transform.SetSiblingIndex(i);
                var target = new Vector2(rect.anchoredPosition.x, -rowHeight * i);

                // A2 / A5: 次の Apply が来たら現在位置から追従する。Tween を Kill してから張り直すので
                // 積み重ならない。
                rect.DOKill();
                if (moveDuration <= 0f)
                {
                    rect.anchoredPosition = target;
                }
                else
                {
                    rect.DOAnchorPos(target, moveDuration).SetEase(Ease.OutCubic);
                }
            }
        }
    }
}
