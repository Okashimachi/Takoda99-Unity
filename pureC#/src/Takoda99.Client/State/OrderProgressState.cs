// 仕様書: pureC#/docs/.sdd/value-objects/04-order-progress-state.md
// 対応中の客への注文進捗。サーバーへ送らないクライアントローカル状態。判定ロジック自体は TypingJudge の責務。

using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>
    /// 行列先頭の客に対する注文の進捗（タイプ済み単語数・ミス数・所要時間）。
    /// <c>TypingJudge</c> の判定結果を蓄積する入れ物であり、判定そのものは行わない。
    /// </summary>
    public readonly record struct OrderProgressState(
        string StoreId,
        string CustomerId,  // 対応中の客
        int OrderCount,     // CustomerState.OrderCount のコピー（対応開始時点でスナップショット）
        int TypedWordCount, // 何単語ぶんタイプし終えたか（0..OrderCount）
        int MissCount,      // この Order における累計ミスタイプ数
        long StartedAtMs,   // 対応開始時刻（MatchState.ElapsedMs 基準）
        long ElapsedMs      // 現在までの所要時間
    )
    {
        /// <summary>
        /// 行列先頭の客が確定した時点（新規到着 or 前客の提供完了による繰り上がり）で生成する。
        /// </summary>
        public static OrderProgressState Start(string storeId, CustomerState customer, long matchElapsedMs)
        {
            return new OrderProgressState(
                StoreId: storeId,
                CustomerId: customer.CustomerId,
                OrderCount: customer.OrderCount,
                TypedWordCount: 0,
                MissCount: 0,
                StartedAtMs: matchElapsedMs,
                ElapsedMs: 0);
        }

        /// <summary>1単語ぶんタイプし終えた瞬間に呼ぶ。<c>OrderCount</c> を超えて進まない。</summary>
        public OrderProgressState WithWordTyped() =>
            TypedWordCount >= OrderCount ? this : this with { TypedWordCount = TypedWordCount + 1 };

        /// <summary>
        /// 誤入力1文字ごとに呼ぶ。集計粒度（1文字ミスごとに +1）は SV-13 で確認中の仮置き。
        /// </summary>
        public OrderProgressState WithMiss() => this with { MissCount = MissCount + 1 };

        /// <summary>表示用の所要時間更新。</summary>
        public OrderProgressState WithElapsed(long matchElapsedMs)
        {
            var elapsed = matchElapsedMs - StartedAtMs;
            return this with { ElapsedMs = elapsed > 0 ? elapsed : 0 };
        }

        /// <summary>注文個数ぶんタイプし終えたか。true になったら <c>OrderServed</c> をトリガーする。</summary>
        public bool IsComplete => TypedWordCount >= OrderCount;

        /// <summary>
        /// <c>OrderServed</c> のペイロードを生成する。送信自体は <c>MatchClientController</c> の責務。
        /// <paramref name="clientTimestamp"/> は同時脱落のタイブレーク用のクライアント時刻。
        /// </summary>
        public OrderServed ToOrderServed(long clientTimestamp)
        {
            return new OrderServed
            {
                CustomerId = CustomerId,
                ElapsedMs = (int)ElapsedMs,
                MissCount = MissCount,
                ClientTimestamp = clientTimestamp,
            };
        }
    }
}
