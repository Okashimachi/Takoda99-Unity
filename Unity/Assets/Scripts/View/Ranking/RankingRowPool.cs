// 仕様書: Unity/docs/.sdd/ranking-view/01-ranking-panel.md §5 A1 / 03-spectator-ranking-view.md §4 P1
// 行 GameObject を storeId でプールする。99店ぶんの生成破棄は WebGL で詰まるため、
// 生成・破棄ではなく「使い回して位置だけ動かす」。

using System.Collections.Generic;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>
    /// <see cref="RankingRowView"/> を storeId で引き当てるプール。
    /// 同じ店には同じ GameObject を割り当て続けるので、行の移動アニメーションが成立する。
    /// </summary>
    public sealed class RankingRowPool
    {
        private readonly Dictionary<string, RankingRowView> active = new Dictionary<string, RankingRowView>();
        private readonly Stack<RankingRowView> idle = new Stack<RankingRowView>();
        private readonly List<string> releaseBuffer = new List<string>();

        private readonly RankingRowView prefab;
        private readonly Transform root;

        public RankingRowPool(RankingRowView prefab, Transform root)
        {
            this.prefab = prefab;
            this.root = root;
        }

        public IReadOnlyDictionary<string, RankingRowView> Active => active;

        /// <summary>storeId に対応する行を返す。無ければプールから取り出す（無ければ生成）。</summary>
        public RankingRowView Acquire(string storeId)
        {
            if (prefab == null || root == null)
            {
                return null;
            }

            if (active.TryGetValue(storeId, out var existing))
            {
                return existing;
            }

            var row = idle.Count > 0 ? idle.Pop() : Object.Instantiate(prefab, root);
            row.gameObject.SetActive(true);
            active[storeId] = row;
            return row;
        }

        /// <summary>
        /// <paramref name="keep"/> に含まれない行をプールへ戻す。
        /// リストから出ていった行の後始末を、呼び出し側が集合演算で書かなくて済むようにする。
        /// </summary>
        public void ReleaseAllExcept(ICollection<string> keep)
        {
            releaseBuffer.Clear();
            foreach (var pair in active)
            {
                if (!keep.Contains(pair.Key))
                {
                    releaseBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < releaseBuffer.Count; i++)
            {
                var storeId = releaseBuffer[i];
                var row = active[storeId];
                active.Remove(storeId);

                if (row == null)
                {
                    continue;
                }

                row.Recycle();
                idle.Push(row);
            }
        }

        public void ReleaseAll()
        {
            releaseBuffer.Clear();
            foreach (var pair in active)
            {
                if (pair.Value != null)
                {
                    pair.Value.Recycle();
                    idle.Push(pair.Value);
                }
            }

            active.Clear();
        }
    }
}
