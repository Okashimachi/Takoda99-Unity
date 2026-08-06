using System;
using System.Windows.Forms;

namespace Takoda99.GUITest;

/// <summary>
/// pureC# の動作確認用の簡易GUI。仕様書 01(Contract) / 02(RomajiTable) / 03(TypingJudge) を
/// 手で触って確認するためだけの使い捨てハーネス（本番コードではない）。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
