// 調査用の簡易イベントログ。Envelope の生JSON（IEnvelopeLog）とは別に、
// 「客が流れてきた／去った」といったクライアント側の離散イベントを時系列で残す。
//
// 目的: 自店舗が脱落したあとも客が流れ続けるバグの切り分け。
// 見るべき点は「その客がどこから来たか」なので、各行に発生源を必ず付ける:
//   NET  … サーバー由来（IRenderer 通知 = RendererProxy）
//   VIEW … 表示側で実際に増減した客（CustomerQueueView）
// NET が止まっているのに VIEW が動き続けていれば、原因は表示側にある。
//
// 併せて自店の生存状況（alive/phase/queue）を添える。
// DLL 側（Takoda99.Client）には手を入れられないため、Unity 側の静的リングバッファとして持つ。
//
// 連続する同種イベントは 1 行にまとめる（脱落連鎖で 98 行流れるとログが読めなくなるため）。

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Takoda99.DebugUI
{
    /// <summary>イベントの発生源。ログの読み分けの軸。</summary>
    public enum ClientEventSource
    {
        /// <summary>サーバー由来（IRenderer 通知）。</summary>
        Net,

        /// <summary>表示側の増減（CustomerQueueView）。</summary>
        View,
    }

    /// <summary>クライアント側の離散イベントを一定件数だけ保持するリングバッファ（調査用）。</summary>
    public static class ClientEventLog
    {
        private const int Capacity = 200;

        private static readonly LinkedList<Entry> entries = new();

        /// <summary>1件ぶんの記録。連続する同種イベントは Count でまとめる。</summary>
        private sealed class Entry
        {
            public float FirstAt;
            public float LastAt;
            public ClientEventSource Source;
            public string Tag;
            public string FirstDetail;
            public string LastDetail;
            public int Count;
        }

        /// <summary>1件記録する。</summary>
        public static void Add(ClientEventSource source, string tag, string detail)
        {
            var now = Time.realtimeSinceStartup;
            var head = entries.First?.Value;

            // 直前と同じ発生源・同じ種別なら、行を増やさず件数だけ伸ばす。
            if (head != null && head.Source == source && head.Tag == tag)
            {
                head.Count++;
                head.LastAt = now;
                head.LastDetail = detail;
                return;
            }

            entries.AddFirst(new Entry
            {
                FirstAt = now,
                LastAt = now,
                Source = source,
                Tag = tag,
                FirstDetail = detail,
                LastDetail = detail,
                Count = 1,
            });

            while (entries.Count > Capacity)
            {
                entries.RemoveLast();
            }
        }

        public static void Clear() => entries.Clear();

        /// <summary>
        /// 再生開始時に必ず捨てる。エディタで Domain Reload を切っていると static がそのまま残り、
        /// 前回の再生のイベントが混ざって時刻が逆行する（実際に "Boot -&gt; Title" が二重に出た）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => entries.Clear();

        /// <summary>新しい順に整形した本文を返す。limit は行数（まとめ後）の上限。</summary>
        public static string BuildText(int limit)
        {
            var builder = new StringBuilder();
            builder.Append("time     src  event        detail\n");
            builder.Append("-------- ---- ------------ ------------------------------\n");

            var lines = 0;
            foreach (var entry in entries)
            {
                if (lines >= limit)
                {
                    break;
                }

                AppendEntry(builder, entry);
                lines++;
            }

            if (lines == 0)
            {
                builder.Append("(イベントなし)\n");
            }

            return builder.ToString();
        }

        private static void AppendEntry(StringBuilder builder, Entry entry)
        {
            // 時刻: まとまっている場合は「開始→終了」の区間で出す。
            var time = entry.Count == 1
                ? Format(entry.FirstAt)
                : $"{Format(entry.FirstAt)}-{Format(entry.LastAt)}";

            builder.Append(time.PadRight(18));
            builder.Append(entry.Source == ClientEventSource.Net ? "NET  " : "VIEW ");
            builder.Append(entry.Tag.PadRight(12));

            if (entry.Count == 1)
            {
                builder.Append(entry.FirstDetail);
            }
            else
            {
                // まとめた行は「何回・最初と最後だけ」。中間は追う価値がない。
                builder.Append('x').Append(entry.Count).Append("  first: ").Append(entry.FirstDetail);
                builder.Append("  last: ").Append(entry.LastDetail);
            }

            builder.Append('\n');
        }

        private static string Format(float seconds) => $"{seconds:F2}s";
    }
}
