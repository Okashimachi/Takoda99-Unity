// BGM の識別子。**再生側はファイル名もパスも知らない**。ここの識別子だけを指す。
// 実体（AudioClip）と音量の対応は BgmLibrary（ScriptableObject）が一手に持つ。
//
// 値を明示的に振っているのは、並び替え・追加でシーンや .asset の参照がずれないようにするため。
// **既存の値は変更しない**（変えると BgmLibrary.asset の割り当てが別のBGMにずれる）。

namespace Takoda99.Sound
{
    /// <summary>ゲーム内で流す BGM の識別子。</summary>
    public enum BgmId
    {
        None = 0,

        /// <summary>通常。Title / Matchmaking で流す。</summary>
        Normal = 100,

        /// <summary>試合前半（1分）。試合前カウントダウンが始まった瞬間に流す。</summary>
        MatchFirstHalf = 200,

        /// <summary>試合後半（1分）。前半を最後まで鳴らし切ってから流す。</summary>
        MatchSecondHalf = 300,

        /// <summary>リザルト。個人成績パネルの表示完了SEが鳴り終わってから流す。</summary>
        Result = 400,
    }
}
