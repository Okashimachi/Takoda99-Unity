// 仕様書: Unity/docs/.sdd/value-objects/01-store-visual-state.md
// 評価3段階＋脱落の表示用状態。閾値判定のみを行い、色・演出は持たない。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    public enum StoreEvalLevel { High, Mid, Low }

    /// <summary>
    /// 店の評価を「高中低」3段階＋「脱落」に分類した表示用状態。
    /// 自店（主画面のアラート）と他店（99店ミニ盤面のセル色）で同じ変換規則を共有する。
    /// </summary>
    public readonly record struct StoreVisualState(
        string StoreId,
        StoreEvalLevel EvalLevel,
        bool Eliminated
    )
    {
        /// <summary>
        /// <c>StoreState</c> / <c>StoreSummaryState</c>（どちらも <c>EvalNormalized</c>(0..1) と
        /// <c>Alive</c> を持つ）から変換する。pureC# 側の型を Unity から参照する方法が未確定のため、
        /// 入力は素の値で受ける（pureC#/README.md §3）。
        /// </summary>
        public static StoreVisualState From(string storeId, double evalNormalized, bool alive, StoreEvalThresholds thresholds)
        {
            return From(storeId, evalNormalized, alive, thresholds, null);
        }

        /// <param name="previous">
        /// 直前の変換結果。脱落後は <c>EvalLevel</c> を再計算せず、直近生存時点の値を保持するために使う。
        /// 初回変換時は null を渡す。
        /// </param>
        public static StoreVisualState From(
            string storeId,
            double evalNormalized,
            bool alive,
            StoreEvalThresholds thresholds,
            StoreVisualState? previous)
        {
            if (!alive)
            {
                // 脱落後は評価の更新イベントが来ても EvalLevel を再計算しない（凍結）。
                var frozen = previous.HasValue ? previous.Value.EvalLevel : Classify(evalNormalized, thresholds);
                return new StoreVisualState(storeId, frozen, true);
            }

            return new StoreVisualState(storeId, Classify(evalNormalized, thresholds), false);
        }

        /// <summary>99店ミニ盤面用。全店ぶんをまとめて変換する。</summary>
        public static IReadOnlyList<StoreVisualState> FromAll(
            IReadOnlyList<StoreVisualSource> stores, StoreEvalThresholds thresholds)
        {
            return FromAll(stores, thresholds, new Dictionary<string, StoreVisualState>());
        }

        /// <param name="previous">
        /// 直前の変換結果を <c>StoreId</c> で引ける辞書。脱落店の <c>EvalLevel</c> 凍結に使う。
        /// </param>
        public static IReadOnlyList<StoreVisualState> FromAll(
            IReadOnlyList<StoreVisualSource> stores,
            StoreEvalThresholds thresholds,
            IReadOnlyDictionary<string, StoreVisualState> previous)
        {
            var result = new List<StoreVisualState>(stores.Count);
            foreach (var store in stores)
            {
                StoreVisualState? before = null;
                if (previous != null && previous.TryGetValue(store.StoreId, out var found))
                {
                    before = found;
                }

                result.Add(From(store.StoreId, store.EvalNormalized, store.Alive, thresholds, before));
            }

            return result;
        }

        private static StoreEvalLevel Classify(double evalNormalized, StoreEvalThresholds thresholds)
        {
            if (evalNormalized >= thresholds.High)
            {
                return StoreEvalLevel.High;
            }

            return evalNormalized >= thresholds.Mid ? StoreEvalLevel.Mid : StoreEvalLevel.Low;
        }
    }

    /// <summary>変換の入力（<c>StoreState</c> / <c>StoreSummaryState</c> のうち分類に使う値だけ）。</summary>
    public readonly record struct StoreVisualSource(string StoreId, double EvalNormalized, bool Alive);

    /// <summary>
    /// 評価3段階の閾値。<c>EvalNormalized</c> は生存店内のパーセンタイル(0..1)であり、
    /// ここに固定閾値を引くことで「常に生存店の一定割合が緑/黄/赤になる」相対表示になる（仕様書 §3）。
    /// </summary>
    /// <remarks>
    /// 実値は未確定（仕様書 §7）。<c>Default</c> は仮置きで、確定したら差し替える。
    /// 下位淘汰閾値 <c>stormThresholdPct</c> と赤帯の下限を揃える案が候補。
    /// </remarks>
    public readonly record struct StoreEvalThresholds(double High, double Mid)
    {
        /// <summary>仮置きの閾値（High: 上位1/3、Mid: 中位1/3）。演出確定時に差し替える。</summary>
        public static StoreEvalThresholds Default => new StoreEvalThresholds(High: 2d / 3d, Mid: 1d / 3d);
    }
}
