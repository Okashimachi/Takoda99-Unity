using System;
using System.Drawing;
using System.Windows.Forms;
using Takoda99.Client;
using Takoda99.Client.Contract;
using Takoda99.Client.Typing;

namespace Takoda99.GUITest;

/// <summary>お題サンプル固定3語でのタイピング確認（02/03）＋ Envelope エンコード確認（01）。</summary>
public sealed class MainForm : Form
{
    private static readonly string[] SampleWords = { "たこ", "しゃしん", "かんい" };

    private readonly IRomajiTable _table = new DefaultRomajiTable();
    private readonly SystemClock _clock = new();
    private readonly ITypingJudge _judge;
    private readonly IEnvelopeCodec _codec = new EnvelopeCodec();

    private readonly Label _wordLabel = new() { Left = 20, Top = 20, Width = 400, Font = new Font("Yu Gothic", 20) };
    private readonly Label _progressLabel = new() { Left = 20, Top = 70, Width = 400 };
    private readonly Label _statusLabel = new() { Left = 20, Top = 100, Width = 400 };
    private readonly TextBox _input = new() { Left = 20, Top = 130, Width = 300 };
    private readonly Button _resetButton = new() { Left = 20, Top = 170, Width = 100, Text = "リセット" };
    private readonly TextBox _envelopeOutput = new() { Left = 20, Top = 220, Width = 400, Height = 80, Multiline = true, ReadOnly = true };

    public MainForm()
    {
        Text = "pureC# GUITest（01/02/03 動作確認）";
        Width = 480;
        Height = 360;

        _judge = new TypingJudge(_table, _clock);

        Controls.Add(_wordLabel);
        Controls.Add(_progressLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_input);
        Controls.Add(_resetButton);
        Controls.Add(_envelopeOutput);

        _input.KeyPress += OnKeyPress;
        _resetButton.Click += (_, _) => BeginOrder();

        BeginOrder();
    }

    private void BeginOrder()
    {
        _judge.BeginOrder("c-sample", SampleWords);
        _input.Clear();
        Render();
        ShowEnvelopeDemo();
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        var result = _judge.PressKey(e.KeyChar);
        e.Handled = true;

        if (result == KeyResult.OrderCleared)
        {
            var report = _judge.BuildReport();
            _statusLabel.Text = $"完了！ missCount={report?.MissCount} elapsedMs={report?.ElapsedMs}";
            _wordLabel.Text = "(完了)";
            _progressLabel.Text = "";
            return;
        }

        Render(result);
    }

    private void Render(KeyResult? last = null)
    {
        var view = _judge.CurrentView;
        _wordLabel.Text = view.CurrentWord;
        _progressLabel.Text = $"x/N = {view.WordIndex}/{view.OrderCount}  打鍵済み={view.TypedKanaLength}  入力中={view.PendingInput}";
        _statusLabel.Text = $"直前判定: {last} / missCount={view.MissCount}";
    }

    private void ShowEnvelopeDemo()
    {
        // 01-contract の EnvelopeCodec を叩くだけの確認（実際の通信はしない）。
        var json = _codec.EncodeEnvelope("MatchmakingJoin", new Takoda99.Proto.MatchmakingJoin());
        var decoded = _codec.DecodeEnvelope(json);
        _envelopeOutput.Text = $"Encode: {json}\r\nDecode: type={decoded?.Type}";
    }

    private sealed class SystemClock : IClock
    {
        private readonly DateTime _start = DateTime.UtcNow;

        public long MonotonicMs => (long)(DateTime.UtcNow - _start).TotalMilliseconds;
        public long WallClockUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
