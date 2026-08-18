// SE の識別子。**再生側はファイル名もパスも知らない**。ここの識別子だけを指す。
// 実体（AudioClip）と音量の対応は SoundLibrary（ScriptableObject）が一手に持つ。
//
// 値を明示的に振っているのは、並び替え・追加でシーンや .asset の参照がずれないようにするため。
// **既存の値は変更しない**（変えると SoundLibrary.asset の割り当てが別のSEにずれる）。

namespace Takoda99.Sound
{
    /// <summary>ゲーム内で鳴らす SE の識別子。分類は <see cref="SoundCategory"/> を参照。</summary>
    public enum SoundId
    {
        None = 0,

        // ── UI ───────────────────────────────────────────────────────
        /// <summary>ボタン押下。画面を問わず、押した瞬間に鳴らす。</summary>
        ButtonTap = 100,

        // ── マッチング ───────────────────────────────────────────────
        /// <summary>マッチング成立（カウントダウンが尽きて MatchingComplete が出た瞬間）。</summary>
        MatchmakingComplete = 200,

        // ── 試合の進行 ───────────────────────────────────────────────
        /// <summary>試合開始前のカウントダウン（GameBefore の秒読み開始時に1回）。</summary>
        MatchCountdown = 300,

        /// <summary>試合開始の合図（サーバーの MatchStart で待機が明けた瞬間）。</summary>
        MatchStart = 301,

        /// <summary>試合終了（MatchEnd 受信）。</summary>
        MatchEnd = 302,

        // ── 打鍵 ─────────────────────────────────────────────────────
        /// <summary>通常の打鍵成功。</summary>
        KeyHit = 400,

        /// <summary>ノーミスで1単語を打ち切った。</summary>
        KeyPerfect = 401,

        /// <summary>ミス打鍵。</summary>
        KeyMiss = 402,

        // ── 下位淘汰 ─────────────────────────────────────────────────
        /// <summary>淘汰直前の秒読み（1秒ごとに1回）。自店が安全圏にいるとき。</summary>
        CullCountdownTick = 500,

        /// <summary>淘汰直前の秒読み（1秒ごとに1回）。自店が淘汰圏にいるとき。</summary>
        CullCountdownWarningTick = 501,

        /// <summary>脱落（一斉閉店の発生。自店を含むかは音量差で表す）。</summary>
        Eliminated = 502,

        // ── 順位の変動 ───────────────────────────────────────────────
        /// <summary>自店の順位が上位圏（既定10位以内）に入った。</summary>
        RankEnteredTop = 600,

        /// <summary>
        /// 自店が次の淘汰圏内に落ちた（このままなら切られる）。
        /// 「下位ランク入り」の強い方。ギリギリ圏外とは音量を別に振るため SoundId を分けている。
        /// </summary>
        RankEnteredCullRange = 601,

        /// <summary>
        /// 自店が淘汰圏のすぐ上（ギリギリ圏外）に落ちた。
        /// 「下位ランク入り」の弱い方。
        /// </summary>
        RankEnteredCullMargin = 602,

        // ── リザルト ─────────────────────────────────────────────────
        /// <summary>リザルトのたこ焼き生成（1個ごとに1回）。</summary>
        ResultTakoyakiSpawn = 700,

        /// <summary>リザルトの全パネル表示完了（上位3位）。</summary>
        ResultRankRevealTop = 701,

        /// <summary>リザルトの全パネル表示完了（下位20位）。</summary>
        ResultRankRevealBottom = 702,

        /// <summary>リザルトの全パネル表示完了（上位・下位のいずれでもない）。</summary>
        ResultRankRevealNormal = 703,
    }

    /// <summary>SE の意味的なまとまり。音量スライダーはこの単位でも持つ。</summary>
    public enum SoundCategory
    {
        /// <summary>UI 操作。</summary>
        Ui = 0,

        /// <summary>マッチング。</summary>
        Matchmaking = 1,

        /// <summary>試合の進行（開始・終了の節目）。</summary>
        MatchFlow = 2,

        /// <summary>打鍵のフィードバック。最も高頻度に鳴るため、既定音量は控えめにする。</summary>
        Typing = 3,

        /// <summary>下位淘汰（秒読み・脱落）。</summary>
        Cull = 4,

        /// <summary>順位の変動。</summary>
        Ranking = 5,

        /// <summary>リザルト。</summary>
        Result = 6,
    }
}
