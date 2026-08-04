// 仕様書: pureC#/docs/.sdd/value-objects/03-customer-state.md
// 自店の行列に居る客1体ぶんの「サーバーから受信した事実」。我慢ゲージ残量・ムードは持たない。

using System.Collections.Generic;
using Takoda99.Proto;

namespace Takoda99.Client.State
{
    /// <summary>
    /// 自店の行列に存在する客1体。<c>CustomerArrived</c>（= <see cref="CustomerView"/>）の受信値を保持する。
    /// </summary>
    /// <remarks>
    /// 我慢ゲージの残量は保持しない。契約に <c>patienceLeftMs</c> を運ぶメッセージが存在せず（SV-03）、
    /// 残量はクライアントの推定値にしかならないため、表示用の算出は Unity 側 <c>PatienceTimer</c> が行う。
    /// <see cref="CustomerAttribute"/> は仕様書の定義（Proto と同一）に従い、Proto の列挙をそのまま使う。
    /// </remarks>
    public readonly record struct CustomerState(
        string CustomerId,
        CustomerAttribute Attribute,
        int PatienceMaxMs,           // CustomerView.patienceMaxMs
        int OrderCount,              // = 打つ単語数
        IReadOnlyList<string> Words, // お題単語。サーバー発行
        long ArrivedAtElapsedMs      // 来店時点の MatchState.ElapsedMs。クライアントの推定値（Proto に無い）
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
                PatienceMaxMs: view.PatienceMaxMs,
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
