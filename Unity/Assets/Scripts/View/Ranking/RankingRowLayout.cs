// 仕様書: Unity/docs/.sdd/ranking-view/06-rank-swap-animation.md（01-ranking-panel.md §5 A1〜A5 の上に積む）
// 行を並べ替えて配置する。前回から順位が変わった行だけ強調しながら、新しいスロットへ移動させる。

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View.Ranking
{
    /// <summary>
    /// 行の並べ替えとアニメーション。パネル・観戦画面で同じ規則を使うため切り出している。
    /// </summary>
    internal static class RankingRowLayout
    {
        /// <summary>
        /// pool ごとの記憶。呼び出し元（上位パネル／下位パネル／観戦画面）はそれぞれ別の pool を持つため、
        /// static なこのクラスで前回配置を共有してしまわないよう pool をキーにする。
        /// </summary>
        private sealed class LayoutMemory
        {
            /// <summary>前回の配置（storeId → index）。06 §4.1 の「順位が変わった行」の判定に使う。</summary>
            public readonly Dictionary<string, int> Previous = new Dictionary<string, int>();

            /// <summary>移動完了後に描画順を張り直す予約（06 §4.4）。次の Apply で必ず取り消す。</summary>
            public Tween PendingOrder;

            /// <summary>
            /// 予約が参照する storeId の並び。次の Apply が必ず PendingOrder を Kill してから
            /// 書き換えるため、使い回しても古い予約が壊れた並びを見ることはない（WebGL の GC を避ける）。
            /// </summary>
            public readonly List<string> PendingOrderIds = new List<string>();

            /// <summary>スロット不足の警告を1回だけ出すためのフラグ（毎 Apply のログ洪水を防ぐ）。</summary>
            public bool WarnedSlotShortage;
        }

        private static readonly ConditionalWeakTable<RankingRowPool, LayoutMemory> Memories =
            new ConditionalWeakTable<RankingRowPool, LayoutMemory>();

        public static void Apply(
            RankingRowPool pool,
            IReadOnlyList<RankingRowViewState> rows,
            IReadOnlyList<RankingRowStyle> styles,
            HashSet<string> visibleIds,
            IRankingSlotSource slots,
            RankingRowPalette palette,
            RankingSwapSettings settings)
        {
            if (pool == null || slots == null || rows == null)
            {
                return;
            }

            var memory = Memories.GetOrCreateValue(pool);
            var previous = memory.Previous;

            // 配置できるのはスロットがある数まで。rows がそれを超える場合（01 §3 B2 の「自分を末尾に足す」で
            // visibleCount + 1 件になり得る）、あふれた行は表示できない。
            // visibleIds をあふれたぶんまで含めると、プールに残ったまま前の位置で固まる行が出るため、
            // **実際に配置する行だけ**を visibleIds に入れる。
            var count = rows.Count < slots.Count ? rows.Count : slots.Count;
            if (rows.Count > slots.Count && !memory.WarnedSlotShortage)
            {
                memory.WarnedSlotShortage = true;
                Debug.LogWarning(
                    $"{nameof(RankingRowLayout)}: 表示行 {rows.Count} 件に対しスロットが {slots.Count} 件しかありません。" +
                    $"あふれた {rows.Count - slots.Count} 件（自分の行を末尾に足した場合など）は描画されません。");
            }

            visibleIds.Clear();
            for (var i = 0; i < count; i++)
            {
                visibleIds.Add(rows[i].StoreId);
            }

            // A3: リストから出ていった行はプールへ戻す（破棄しない）。
            pool.ReleaseAllExcept(visibleIds);

            // 4.1: 前回に存在し、かつ index が変わった行を数える。新規行は含めない。
            var changedCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (previous.TryGetValue(rows[i].StoreId, out var prevIndex) && prevIndex != i)
                {
                    changedCount++;
                }
            }

            // 4.3: 変化した行数が上限を超えたら、強調をやめ移動と色の補間だけ行う。
            // RankingSnapshot 直後は10行すべてが変わり得るため、この分岐は毎試合必ず通る（例外パスではない）。
            var emphasize = changedCount > 0 && changedCount <= settings.maxEmphasisRows;
            var duration = settings.moveDuration;

            for (var i = 0; i < count; i++)
            {
                var state = rows[i];
                var row = pool.Acquire(state.StoreId);
                if (row == null)
                {
                    continue;
                }

                // A4 / 4.6: 順位の数字はアニメーションさせない。位置だけ動かし、数字は即時更新する。
                row.SetState(state);

                // 4.2: 新しい色・寸法は移動の開始と同時に補間を始める（04 §5.3）。
                // styles が null の呼び出し元（観戦画面）では SetStyle を呼ばず、Prefab の既定値のまま描く
                // （04 §6: 99行に順位別の寸法を適用するとスクロールの行高が揃わなくなるため）。
                // 件数が足りない場合に default（Size = (0,0)）を適用すると行が潰れるため、その行は触らない。
                if (styles != null && i < styles.Count)
                {
                    row.SetStyle(styles[i], palette, duration);
                }

                var hasPrev = previous.TryGetValue(state.StoreId, out var prevIndex);
                var indexChanged = hasPrev && prevIndex != i;
                var target = slots.PositionOf(i);

                if (!hasPrev)
                {
                    // 4.1: 前回に無い行（新しくリスト入り）は強調もスライドもしない（01 A3「フェードインのみ」）。
                    // プールから来た行は前の持ち主の位置に居るため、補間すると無関係な場所から滑り込む。
                    row.MoveTo(target, 0f);
                }
                else if (indexChanged || !row.IsAt(target))
                {
                    // 4.1: 前回と同じ index の行には Tween を張らない。
                    // ただしスロットをエディタで動かした場合（04 §7-2）に追従できるよう、
                    // 目標位置から実際にズレているときだけ張り直す。
                    row.MoveTo(target, duration);
                }

                if (indexChanged && emphasize && duration > 0f)
                {
                    // 4.2: 描画順は強調中だけ最前面へ。移動が終わったら 4.4 の順序へ戻す。
                    row.transform.SetAsLastSibling();
                    row.Emphasize(settings.emphasisScale, settings.emphasisDuration);
                }
                else
                {
                    // E2: 強調しない行は必ず等倍へ戻す。
                    // 直前の強調が中断された行がここを通るため、**これが無いと拡大したまま残る**
                    // （06 §4.5 が「最も出やすい不具合」と名指ししているケース）。
                    row.ResetScale();
                }
            }

            // 4.4: 移動が完了したら、rows の順に SetSiblingIndex(i) を張り直す（1位が最背面、末尾が最前面）。
            // 予約は pool ごとに1本だけ持つ。moveDuration より短い周期で Apply が来ても、
            // 古い rows を掴んだ予約が後から発火して並びを巻き戻さないようにする。
            KillTween(ref memory.PendingOrder);

            if (duration <= 0f)
            {
                ApplyFinalSiblingOrder(pool, rows, count);
            }
            else
            {
                var orderedIds = memory.PendingOrderIds;
                orderedIds.Clear();
                for (var i = 0; i < count; i++)
                {
                    orderedIds.Add(rows[i].StoreId);
                }

                memory.PendingOrder = DOVirtual.DelayedCall(
                    duration,
                    () => ApplyFinalSiblingOrder(pool, orderedIds),
                    false);
            }

            // 次回の Apply のために今回の配置を覚える。
            previous.Clear();
            for (var i = 0; i < count; i++)
            {
                previous[rows[i].StoreId] = i;
            }
        }

        private static void ApplyFinalSiblingOrder(RankingRowPool pool, IReadOnlyList<RankingRowViewState> rows, int count)
        {
            for (var i = 0; i < count; i++)
            {
                SetSibling(pool, rows[i].StoreId, i);
            }
        }

        private static void ApplyFinalSiblingOrder(RankingRowPool pool, IReadOnlyList<string> orderedIds)
        {
            for (var i = 0; i < orderedIds.Count; i++)
            {
                SetSibling(pool, orderedIds[i], i);
            }
        }

        private static void SetSibling(RankingRowPool pool, string storeId, int index)
        {
            if (pool.Active.TryGetValue(storeId, out var row) && row != null)
            {
                row.transform.SetSiblingIndex(index);
            }
        }

        /// <summary>予約済みの Tween を現在値のまま止める。完了済み Tween の使い回しを巻き添えにしない。</summary>
        private static void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill(false);
            }

            tween = null;
        }
    }
}
