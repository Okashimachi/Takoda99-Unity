// リザルト画面の成績一覧。root/ResultCanvas/Result/Othes にアタッチする。
// Othes を埋める子コンテナを作り、ResultUnitPanel を格子状に並べて項目を流し込む。
//
// 格子は縦3 x 横6。優先度の高い順に上から詰め、
//   1段目 … 優先度「高」を2つ（各3マス幅）＝ 一番大きく、一番先に目に入る
//   2段目 … 優先度「中」を3つ（各2マス幅）
//   3段目 … 優先度「低」を3つ（各2マス幅）
// パネルの幅と内容の文字サイズの両方で優先度を表す。

using System.Collections.Generic;
using System.Globalization;
using Takoda99.Client.State;
using Takoda99.Proto;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>成績項目を ResultUnitPanel の格子として構築する。</summary>
    public sealed class ResultStatsBoardView : MonoBehaviour
    {
        /// <summary>横6マスを何分割で使うかの定義。段ごとにパネル1枚が占めるマス数が変わる。</summary>
        private const int GridColumns = 6;
        private const int GridRows = 3;

        /// <summary>MatchEnd 待ちのあいだ、値の代わりに出す文字。</summary>
        private const string Pending = "…";

        [Header("パネル")]
        [SerializeField] private ResultUnitPanelView unitPanelPrefab;

        [Header("レイアウト（縦3 x 横6）")]
        [Tooltip("マス目の間隔（px）。")]
        [SerializeField] private Vector2 spacing = new Vector2(8f, 8f);
        [Tooltip("外周の余白（px）。")]
        [SerializeField] private float padding = 4f;

        [Header("優先度ごとの内容の文字サイズ倍率（プレハブの文字サイズに対する倍率）")]
        [SerializeField] private float highFontScale = 1.6f;
        [SerializeField] private float middleFontScale = 1.2f;
        [SerializeField] private float lowFontScale = 1f;

        [Header("優先度ごとの項目名の文字サイズ倍率")]
        [Tooltip("項目名は内容ほど大きくしない。大きくしすぎると主役の数字が負けるため。")]
        [SerializeField] private float highTitleFontScale = 1.4f;
        [SerializeField] private float middleTitleFontScale = 1f;
        [SerializeField] private float lowTitleFontScale = 1f;

        private GameObject grid;

        /// <summary>成績を構築して表示する。データが無い場合は空表示になる。</summary>
        public void Show(PersonalResultState result, RankingTable ranking, string selfStoreId)
        {
            Clear();

            if (unitPanelPrefab == null)
            {
                Debug.LogError($"{nameof(ResultStatsBoardView)}: {nameof(unitPanelPrefab)} が未設定です。Inspector で ResultUnitPanel プレハブをアタッチしてください。", this);
                return;
            }

            var items = BuildItems(result, ranking, selfStoreId);
            var parent = CreateGrid();
            var area = ((RectTransform)transform).rect;
            var cellWidth = CellLength(area.width, GridColumns, spacing.x, padding);
            var rowHeight = CellLength(area.height, GridRows, spacing.y, padding);

            foreach (var item in items)
            {
                var panel = Instantiate(unitPanelPrefab, parent);
                Place((RectTransform)panel.transform, item, cellWidth, rowHeight);
                panel.SetValue(item.Title, item.Param, ResolveFontScale(item.Priority), ResolveTitleFontScale(item.Priority));
            }
        }

        /// <summary>生成済みの格子とパネルをすべて破棄する。</summary>
        public void Clear()
        {
            if (grid != null)
            {
                Destroy(grid);
                grid = null;
            }
        }

        /// <summary>段・開始マス・占有マス数から、パネルを左上原点で配置する。</summary>
        private void Place(RectTransform panelRect, Item item, float cellWidth, float rowHeight)
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);

            // 占有マス数ぶんの幅＋その間に挟まる間隔ぶんだけ、パネルを横に伸ばす。
            var width = (cellWidth * item.ColumnSpan) + (spacing.x * (item.ColumnSpan - 1));
            panelRect.sizeDelta = new Vector2(width, rowHeight);
            panelRect.anchoredPosition = new Vector2(
                padding + (item.Column * (cellWidth + spacing.x)),
                -(padding + (item.Row * (rowHeight + spacing.y))));
        }

        private float ResolveFontScale(Priority priority)
        {
            switch (priority)
            {
                case Priority.High: return highFontScale;
                case Priority.Middle: return middleFontScale;
                default: return lowFontScale;
            }
        }

        private float ResolveTitleFontScale(Priority priority)
        {
            switch (priority)
            {
                case Priority.High: return highTitleFontScale;
                case Priority.Middle: return middleTitleFontScale;
                default: return lowTitleFontScale;
            }
        }

        private RectTransform CreateGrid()
        {
            var go = new GameObject("StatsGrid", typeof(RectTransform));
            go.layer = gameObject.layer;

            // Othes いっぱいに広げる。マス目の大きさは Othes の実サイズから割り出すので、
            // Othes の幅・高さを変えれば格子もそのまま追従する。
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            grid = go;
            return rect;
        }

        /// <summary>余白と間隔を差し引いた残りを等分し、1マスぶんの長さを出す。</summary>
        private static float CellLength(float total, int count, float gap, float outerPadding)
        {
            var usable = total - (outerPadding * 2f) - (gap * (count - 1));
            return usable <= 0f ? 1f : usable / count;
        }

        /// <summary>項目の優先度。上の段ほど、また文字が大きいほど優先度が高い。</summary>
        private enum Priority
        {
            High,
            Middle,
            Low,
        }

        private readonly struct Item
        {
            public Item(string title, string param, Priority priority, int row, int column, int columnSpan)
            {
                Title = title;
                Param = param;
                Priority = priority;
                Row = row;
                Column = column;
                ColumnSpan = columnSpan;
            }

            public string Title { get; }
            public string Param { get; }
            public Priority Priority { get; }
            public int Row { get; }
            public int Column { get; }
            public int ColumnSpan { get; }
        }

        /// <summary>
        /// 表示する8項目を、優先度の高い順（上の段から）に組み立てる。
        /// 成績がまだ届いていない（<paramref name="result"/> が null の）間も、
        /// 枠だけは組んで待ち表示にする。ここで空リストを返すと画面が真っ白になってしまう。
        /// </summary>
        private static List<Item> BuildItems(PersonalResultState result, RankingTable ranking, string selfStoreId)
        {
            var items = new List<Item>(8);

            if (result == null)
            {
                items.Add(new Item("スコア", Pending, Priority.High, 0, 0, 3));
                items.Add(new Item("たこ焼き数", Pending, Priority.High, 0, 3, 3));
                items.Add(new Item("総打鍵数", Pending, Priority.Middle, 1, 0, 2));
                items.Add(new Item("ミス打鍵数", Pending, Priority.Middle, 1, 2, 2));
                items.Add(new Item("平均正確率", Pending, Priority.Middle, 1, 4, 2));
                items.Add(new Item("店の名前", FindSelfName(ranking, selfStoreId), Priority.Low, 2, 0, 2));
                items.Add(new Item("提供数", Pending, Priority.Low, 2, 2, 2));
                items.Add(new Item("生存時間", Pending, Priority.Low, 2, 4, 2));
                return items;
            }

            var stats = result.Stats ?? new MatchStats();

            // 1段目（優先度 高）：2つで横6マスを折半する。
            // リザルトではスコアを大きく出す（試合中は順位が主役だが、ここでは具体的な数字が達成感になる）。
            // 「来客数」は LeftCount（常に 0）に依存していたため撤去した。
            items.Add(new Item("スコア", $"{result.Score}", Priority.High, 0, 0, 3));
            // ★たこ焼きの個数。stats.ServedCount（提供した「客」の数）とは別物。
            items.Add(new Item("たこ焼き数", $"{result.TakoyakiCount} 個", Priority.High, 0, 3, 3));

            // 2段目（優先度 中）：3つで横6マスを3等分する。
            items.Add(new Item("総打鍵数", $"{stats.TotalKeystrokes} 打", Priority.Middle, 1, 0, 2));
            items.Add(new Item("ミス打鍵数", $"{stats.TotalMisses} 打", Priority.Middle, 1, 2, 2));
            items.Add(new Item("平均正確率", Percent(stats.AvgAccuracy), Priority.Middle, 1, 4, 2));

            // 3段目（優先度 低）。
            items.Add(new Item("店の名前", FindSelfName(ranking, selfStoreId), Priority.Low, 2, 0, 2));
            items.Add(new Item("提供数", $"{stats.ServedCount} 人", Priority.Low, 2, 2, 2));
            // 「終わり方」は撤去した。v0.8.0 では脱落経路が足切りの1本だけになり、
            // 判別する意味が無くなった（MatchEnd.reason も消えている）。
            items.Add(new Item("生存時間", FormatDuration(result.SurvivedMs), Priority.Low, 2, 4, 2));

            return items;
        }

        private static string FindSelfName(RankingTable ranking, string selfStoreId)
        {
            if (ranking != null && !string.IsNullOrEmpty(selfStoreId))
            {
                foreach (var store in ranking.Rows)
                {
                    if (store.StoreId == selfStoreId)
                    {
                        return store.DisplayName;
                    }
                }
            }

            return "-";
        }

        private static string Percent(double value01) =>
            (value01 * 100.0).ToString("F1", CultureInfo.InvariantCulture) + "%";

        private static string FormatDuration(long milliseconds)
        {
            var totalSeconds = milliseconds / 1000;
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }
    }
}
