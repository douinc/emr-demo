namespace EmrDemo.Core;

/// <summary>
/// custom 모드 컨트롤의 편집 상태. 줄바꿈은 항상 "\n"으로 보관한다.
/// </summary>
public sealed class TextBuffer
{
    readonly bool _singleLine;
    string _text = "";
    int _anchor;

    public TextBuffer(string text, bool singleLine = false)
    {
        _singleLine = singleLine;
        SetText(text);
    }

    public string Text => _text;
    public int Caret { get; private set; }
    public bool HasSelection => _anchor != Caret;
    public (int Start, int End) Selection => (Math.Min(_anchor, Caret), Math.Max(_anchor, Caret));
    public string SelectedText => _text[Selection.Start..Selection.End];

    public void SetText(string text)
    {
        _text = Normalize(text);
        Caret = _anchor = _text.Length;
    }

    public void Insert(string value)
    {
        var (start, end) = Selection;
        var normalized = Normalize(value);
        _text = _text[..start] + normalized + _text[end..];
        Caret = _anchor = start + normalized.Length;
    }

    public void Backspace()
    {
        if (!HasSelection)
        {
            _anchor = Previous(Caret);
        }

        Insert("");
    }

    public void Delete()
    {
        if (!HasSelection)
        {
            _anchor = Next(Caret);
        }

        Insert("");
    }

    public void MoveLeft(bool extend = false) =>
        MoveTo(!extend && HasSelection ? Selection.Start : Previous(Caret), extend);

    public void MoveRight(bool extend = false) =>
        MoveTo(!extend && HasSelection ? Selection.End : Next(Caret), extend);

    public void MoveLineStart(bool extend = false) =>
        MoveTo(Caret == 0 ? 0 : _text.LastIndexOf('\n', Caret - 1) + 1, extend);

    public void MoveLineEnd(bool extend = false)
    {
        var end = _text.IndexOf('\n', Caret);
        MoveTo(end < 0 ? _text.Length : end, extend);
    }

    public void MoveTo(int position, bool extend = false)
    {
        Caret = Math.Clamp(position, 0, _text.Length);
        if (Caret > 0 && Caret < _text.Length && char.IsSurrogatePair(_text[Caret - 1], _text[Caret]))
        {
            Caret--;
        }

        if (!extend)
        {
            _anchor = Caret;
        }
    }

    public void SelectAll()
    {
        _anchor = 0;
        Caret = _text.Length;
    }

    string Normalize(string value)
    {
        var text = value.Replace("\r\n", "\n").Replace('\r', '\n');
        return _singleLine ? text.Replace('\n', ' ') : text;
    }

    int Previous(int position)
    {
        if (position <= 0)
        {
            return 0;
        }

        return position >= 2 && char.IsSurrogatePair(_text[position - 2], _text[position - 1])
            ? position - 2
            : position - 1;
    }

    int Next(int position)
    {
        if (position >= _text.Length)
        {
            return _text.Length;
        }

        return position + 1 < _text.Length && char.IsSurrogatePair(_text[position], _text[position + 1])
            ? position + 2
            : position + 1;
    }
}
