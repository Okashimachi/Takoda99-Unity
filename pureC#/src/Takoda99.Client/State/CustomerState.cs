// 仕様書: pureC#/docs/.sdd/value-objects/03-customer-state.md
// 自店の行列に居る客1体ぶんの「サーバーから受信した事実」。
// v0.8.0（本選）では我慢ゲージ・離脱が廃止されたため、我慢に関する値を一切持たない。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>
    /// 自店の行列に存在する客1体。<c>CustomerArrived</c>（= <see cref="CustomerView"/>）の受信値を保持する。
    /// </summary>
    /// <remarks>
    /// 我慢ゲージに関する値は保持しない。v0.8.0（本選）で客が逃げなくなり、
    /// <c>patienceMaxMs</c> / <c>patienceStartedAtServerMs</c> は Obsolete（0 が届く）になったため。
    /// **一度出たお題は必ず打ち切られる。**
    /// <see cref="CustomerAttribute"/> は仕様書の定義（Proto と同一）に従い、Proto の列挙をそのまま使う。
    /// v0.8.0 では見た目の出し分け専用で、スコアには影響しない。
    /// </remarks>
    public readonly record struct CustomerState(
        string CustomerId,
        CustomerAttribute Attribute,
        int OrderCount,              // = 打つ単語数 = たこ焼きの個数
        IReadOnlyList<string> Words, // お題単語。サーバー発行
        long ArrivedAtElapsedMs      // 受信時点の MatchState.ElapsedMs。サーバー時刻の対応づけに使う（表示の起点には使わない）
    )
    {
        /// <summary>
        /// <c>CustomerArrived</c> のペイロードから生成する。
        /// <paramref name="arrivedAtElapsedMs"/> には受信時点の <see cref="MatchState.ElapsedMs"/> を渡す。
        /// </summary>
        public static CustomerState FromCustomerView(CustomerView view, long arrivedAtElapsedMs)
        {
            return new CustomerState(
                CustomerId: view.CustomerId,
                Attribute: view.Attribute,
                OrderCount: view.OrderCount,
                Words: view.Words ?? new List<string>(),
                ArrivedAtElapsedMs: arrivedAtElapsedMs);
        }

        /// <summary>
        /// 用語集4章の不変条件（注文個数 = タイプする単語数）を満たすか。
        /// 満たさない場合はサーバー側の不整合として扱い、クライアントでは補正しない（判定のみ提供する）。
        /// </summary>
        public bool HasConsistentWordCount => Words.Count == OrderCount;
    }
}
