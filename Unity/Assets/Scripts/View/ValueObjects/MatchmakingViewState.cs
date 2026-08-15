// 仕様書: Unity/docs/.sdd/matchmaking/01-matchmaking-flow.md §2, §3
// 試合前（マッチング画面）の状態区分と、その画面が必要とする表示用の値。
//
// Unity は C# 9 までのため record struct（C# 10）を使わない（Unity のスクリプティングランタイム制約）。
// pureC# 側の型（ConnectionState / ClientPhase 等）を Unity から参照する方法が未確定のため、
// 入力は素の値で受ける（pureC#/README.md §3）。

using System.Collections.Generic;

namespace Takoda99.View.ValueObjects
{
    /// <summary>参加者一覧の1件ぶんの表示用の値。<c>IsSelf</c> は自店強調表示に使う。</summary>
    public readonly struct MatchmakingParticipantView
    {
        public string StoreId { get; }

        public string DisplayName { get; }

        public bool IsSelf { get; }

        public MatchmakingParticipantView(string storeId, string displayName, bool isSelf)
        {
            StoreId = storeId;
            DisplayName = displayName;
            IsSelf = isSelf;
        }
    }

    public enum MatchmakingScreenState
    {
        NameEntry,    // 表示名の入力中。★まだ接続していない（02-display-name.md §5）
        Connecting,   // 名前確定後。WebSocket 接続中
        Joining,      // MatchmakingJoin 送信済み。MatchmakingStatus をまだ受けていない
        Waiting,      // 待機中。WaitingCount / MinPlayers を表示
        CountingDown, // カウントダウン中。CountdownMs を表示
        Starting,     // MatchStart 受信。試合シーンへ遷移中
        Rejected,     // 接続を拒否された（同時接続上限など）
    }

    /// <summary>
    /// MatchMakingCanvas 直下の3パネルのうち、いま表示すべきもの。
    /// シーン階層の WriteNameModal / WaitingPanel / MatchingPanel に対応する。
    /// </summary>
    public enum MatchmakingPanel
    {
        None,            // どのパネルも出さない（シーン遷移中・未確定の Rejected）
        WriteNameModal,
        WaitingPanel,
        MatchingPanel,
    }

    /// <summary>マッチング画面が描画に必要とする値。01-matchmaking-flow.md §3 の C# シグネチャに対応する。</summary>
    public readonly struct MatchmakingViewState
    {
        public MatchmakingScreenState State { get; }

        public int WaitingCount { get; }

        public int MinPlayers { get; }

        /// <summary>カウントダウン中のみ値を持つ。null は「カウントダウンしていない」（§3 の注記）。</summary>
        public int? CountdownMs { get; }

        /// <summary>待機中の参加者一覧。Bot は含まない。MatchmakingStatus.participants から作る（v0.5.0 / REQ-03）。</summary>
        public IReadOnlyList<MatchmakingParticipantView> Participants { get; }

        public MatchmakingViewState(MatchmakingScreenState State, int WaitingCount, int MinPlayers, int? CountdownMs)
            : this(State, WaitingCount, MinPlayers, CountdownMs, System.Array.Empty<MatchmakingParticipantView>())
        {
        }

        public MatchmakingViewState(
            MatchmakingScreenState State,
            int WaitingCount,
            int MinPlayers,
            int? CountdownMs,
            IReadOnlyList<MatchmakingParticipantView> Participants)
        {
            this.State = State;
            this.WaitingCount = WaitingCount;
            this.MinPlayers = MinPlayers;
            this.CountdownMs = CountdownMs;
            this.Participants = Participants;
        }

        /// <summary>
        /// いま表示すべきパネル。3パネルの遷移が不可逆（§8.4）なのは、この対応表と、
        /// <c>nameDecided</c> / <c>hasReceivedStatus</c> が一度 true になったら戻らないことの帰結であり、
        /// 別途ラッチを持つ必要はない。
        /// </summary>
        public MatchmakingPanel Panel
        {
            get
            {
                switch (State)
                {
                    case MatchmakingScreenState.NameEntry:
                        return MatchmakingPanel.WriteNameModal;
                    case MatchmakingScreenState.Connecting:
                    case MatchmakingScreenState.Joining:
                    // 接続失敗も WaitingPanel に留めて文言を出す。503 専用UIは作らないが、
                    // 何も出さないと「Decide を押したのに無反応」に見えて原因が分からなくなる（§8.2）。
                    case MatchmakingScreenState.Rejected:
                        return MatchmakingPanel.WaitingPanel;
                    case MatchmakingScreenState.Waiting:
                    case MatchmakingScreenState.CountingDown:
                        return MatchmakingPanel.MatchingPanel;
                    default:
                        // Starting（シーン遷移中）のみ。
                        return MatchmakingPanel.None;
                }
            }
        }

        /// <summary>
        /// 接続・参加・状態受信・試合開始・拒否の各シグナルから、いま画面に出すべき区分と値を導出する。
        /// 一度 <see cref="MatchmakingScreenState.Starting"/> になったら、以降 <paramref name="hasReceivedStatus"/> 等が
        /// 変化しても巻き戻らない（§10 テスト観点：「以降 MatchmakingStatus を受けても状態が巻き戻らない」）。
        /// </summary>
        /// <param name="connectionFailed">同時接続上限超過等でサーバーに拒否されたか（§8.2）。</param>
        /// <param name="nameDecided">
        /// WriteNameModal の Decide が押され、表示名が確定したか。**これが false の間は接続していない。**
        /// 接続してから名前を入力させると、サーバーの3秒の待ち受けを超えて名前が失われる
        /// （02-display-name.md §5 ★）。
        /// </param>
        /// <param name="connected">WebSocket 接続が確立し、MatchmakingJoin を送信済みか。</param>
        /// <param name="matchStarted">MatchStart を受信済みか。</param>
        /// <param name="hasReceivedStatus">
        /// MatchmakingStatus を一度でも受信したか。接続直後の1秒間は届かないため（§5.1）、
        /// この値で「まだ人数が不明」を区別する。<c>WaitingCount = 0</c> を「誰もいない」と誤読させないための入力。
        /// </param>
        /// <param name="waitingCount">直近の <c>MatchmakingStatus.waitingCount</c>。</param>
        /// <param name="minPlayers">直近の <c>MatchmakingStatus.minPlayers</c>。</param>
        /// <param name="countdownMs">
        /// 直近の <c>MatchmakingStatus.countdownMs</c>。キーごと欠落している間は null（§3・§5.2）。
        /// </param>
        /// <param name="selfStoreId">
        /// 直近の <c>MatchmakingStatus.selfStoreId</c>。参加者一覧の中で自店を強調表示するための識別子。
        /// </param>
        /// <param name="participants">
        /// 直近の <c>MatchmakingStatus.participants</c>（storeId, displayName）。Bot は含まない。
        /// </param>
        public static MatchmakingViewState From(
            bool connectionFailed,
            bool nameDecided,
            bool connected,
            bool matchStarted,
            bool hasReceivedStatus,
            int waitingCount,
            int minPlayers,
            int? countdownMs,
            string selfStoreId = null,
            IReadOnlyList<(string StoreId, string DisplayName)> participants = null)
        {
            if (matchStarted)
            {
                return new MatchmakingViewState(MatchmakingScreenState.Starting, waitingCount, minPlayers, countdownMs);
            }

            if (connectionFailed)
            {
                return new MatchmakingViewState(MatchmakingScreenState.Rejected, 0, 0, null);
            }

            if (!nameDecided)
            {
                // 名前入力中。接続前なので人数も分からない。
                return new MatchmakingViewState(MatchmakingScreenState.NameEntry, 0, 0, null);
            }

            if (!connected)
            {
                return new MatchmakingViewState(MatchmakingScreenState.Connecting, 0, 0, null);
            }

            if (!hasReceivedStatus)
            {
                // 接続はできたが人数がまだ不明（§5.1）。数値を出さない。
                return new MatchmakingViewState(MatchmakingScreenState.Joining, 0, 0, null);
            }

            var state = countdownMs.HasValue ? MatchmakingScreenState.CountingDown : MatchmakingScreenState.Waiting;
            return new MatchmakingViewState(state, waitingCount, minPlayers, countdownMs, ToParticipantViews(participants, selfStoreId));
        }

        private static IReadOnlyList<MatchmakingParticipantView> ToParticipantViews(
            IReadOnlyList<(string StoreId, string DisplayName)> participants,
            string selfStoreId)
        {
            if (participants == null || participants.Count == 0)
            {
                return System.Array.Empty<MatchmakingParticipantView>();
            }

            var result = new MatchmakingParticipantView[participants.Count];
            for (var i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                result[i] = new MatchmakingParticipantView(p.StoreId, p.DisplayName, p.StoreId == selfStoreId);
            }

            return result;
        }
    }
}
