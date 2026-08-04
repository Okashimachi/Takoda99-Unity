using System;
using System.Collections.Generic;
using System.Linq;

namespace Takoda99.Client.Typing;

/// <summary>
/// <see cref="ITypingJudge"/> の実装。クライアント唯一のローカルドメイン（03-typing-judge.md）。
/// </summary>
public sealed class TypingJudge : ITypingJudge
{
    private enum Phase { Idle, Typing }

    private readonly IRomajiTable _romajiTable;
    private readonly IClock _clock;

    private Phase _phase = Phase.Idle;
    private OrderReport? _pendingReport;

    private string _customerId = "";
    private IReadOnlyList<string> _words = Array.Empty<string>();
    private int _wordIndex;
    private IReadOnlyList<KanaUnit> _units = Array.Empty<KanaUnit>();
    private int _unitIndex;
    private string _buffer = "";
    private int _missCount;
    private long _startedAtMonotonicMs;

    public TypingJudge(IRomajiTable romajiTable, IClock clock)
    {
        _romajiTable = romajiTable;
        _clock = clock;
    }

    public bool IsIdle => _phase == Phase.Idle;

    public void BeginOrder(string customerId, IReadOnlyList<string> words)
    {
        if (words is null || words.Count == 0)
        {
            // words が空：Idle のまま（OrderCleared を即発火させない。03-typing-judge.md §3.5）。
            return;
        }

        _customerId = customerId;
        _words = words;
        _wordIndex = 0;
        _units = _romajiTable.Segment(words[0]);
        _unitIndex = 0;
        _buffer = "";
        _missCount = 0;
        _startedAtMonotonicMs = _clock.MonotonicMs;
        _pendingReport = null;
        _phase = Phase.Typing;
    }

    public KeyResult PressKey(char c)
    {
        if (_phase == Phase.Idle)
        {
            return KeyResult.Ignored;
        }

        if (char.IsControl(c))
        {
            return KeyResult.Ignored;
        }

        return ProcessChar(char.ToLowerInvariant(c));
    }

    private KeyResult ProcessChar(char c)
    {
        var unit = _units[_unitIndex];
        var candidate = _buffer + c;

        var anyPrefix = unit.Patterns.Any(p => p.StartsWith(candidate, StringComparison.Ordinal));

        if (anyPrefix)
        {
            var exact = unit.Patterns.Contains(candidate);
            var longerExists = unit.Patterns.Any(p => p.Length > candidate.Length && p.StartsWith(candidate, StringComparison.Ordinal));

            if (exact && !longerExists)
            {
                return ConfirmUnit();
            }

            _buffer = candidate;
            return KeyResult.Correct;
        }

        if (_buffer.Length > 0 && unit.Patterns.Contains(_buffer))
        {
            // バッファは既に候補と完全一致：単位を確定し、この文字を次の単位で再処理する
            // （03-typing-judge.md §3.2 手順4。"ん" の n/nn prefix 競合をこれで解消する）。
            var confirmResult = ConfirmUnit();
            return confirmResult == KeyResult.OrderCleared ? confirmResult : ProcessChar(c);
        }

        _missCount++;
        return KeyResult.Miss;
    }

    private KeyResult ConfirmUnit()
    {
        _unitIndex++;
        _buffer = "";

        if (_unitIndex < _units.Count)
        {
            return KeyResult.Correct;
        }

        if (_wordIndex + 1 < _words.Count)
        {
            _wordIndex++;
            _units = _romajiTable.Segment(_words[_wordIndex]);
            _unitIndex = 0;
            return KeyResult.WordCleared;
        }

        _pendingReport = new OrderReport(
            _customerId,
            (int)(_clock.MonotonicMs - _startedAtMonotonicMs),
            _missCount,
            _clock.WallClockUnixMs);
        _phase = Phase.Idle;
        return KeyResult.OrderCleared;
    }

    public void AbortOrder()
    {
        _phase = Phase.Idle;
        _pendingReport = null;
    }

    public OrderReport? BuildReport() => _pendingReport;

    public TypingView CurrentView
    {
        get
        {
            if (_phase == Phase.Idle)
            {
                return TypingView.Empty;
            }

            var typedKanaLength = 0;
            for (var i = 0; i < _unitIndex; i++)
            {
                typedKanaLength += _units[i].Kana.Length;
            }

            return new TypingView(
                _words[_wordIndex],
                typedKanaLength,
                _buffer,
                _wordIndex,
                _words.Count,
                _missCount);
        }
    }
}
