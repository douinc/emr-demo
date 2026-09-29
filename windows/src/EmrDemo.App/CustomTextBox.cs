using EmrDemo.Core;

namespace EmrDemo.App;

/// <summary>
/// 직접 그리는 여러 줄 편집기. 내용은 <see cref="Buffer"/>에만 있고 Control.Text(창 텍스트)에는
/// 넣지 않는다 — 넣으면 WM_GETTEXT·UIA Name으로 읽혀 custom 모드의 전제가 깨진다.
/// 같은 이유로 Control.Name도 비워 둔다(UIA AutomationId로 노출된다).
/// </summary>
sealed class CustomTextBox : Control
{
    const int Pad = 6;
    const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    readonly List<(int Start, int Length)> _lines = [];
    int _lineHeight;
    int _scroll;
    bool _layoutDirty = true;
    bool _mouseSelecting;
    int? _preferredX;

    public CustomTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = Theme.Font;
        Cursor = Cursors.IBeam;
        TabStop = true;
    }

    public TextBuffer Buffer { get; } = new("");

    protected override bool CanEnableIme => true;

    protected override AccessibleObject CreateAccessibilityInstance() => new OpaqueAccessibleObject(this);

    public void SetText(string text)
    {
        Buffer.SetText(text);
        Buffer.MoveTo(0);
        _scroll = 0;
        Changed();
    }

    void Changed()
    {
        _layoutDirty = true;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _layoutDirty = true;
    }

    void EnsureLayout()
    {
        if (!_layoutDirty)
        {
            return;
        }

        _layoutDirty = false;
        _lines.Clear();
        _lineHeight = TextRenderer.MeasureText("가", Font, Size.Empty, Flags).Height + 2;
        var text = Buffer.Text;
        var width = Math.Max(20, ClientSize.Width - Pad * 2);
        var start = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline;
            WrapParagraph(text, start, end, width);
            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
        }
    }

    void WrapParagraph(string text, int start, int end, int width)
    {
        if (start == end)
        {
            _lines.Add((start, 0));
            return;
        }

        while (start < end)
        {
            var length = 1;
            while (start + length < end && Measure(text.Substring(start, length + 1)) <= width)
            {
                length++;
            }

            if (start + length < end)
            {
                if (text[start + length] == ' ')
                {
                    length++;
                }
                else
                {
                    var space = text.LastIndexOf(' ', start + length - 1, length);
                    if (space > start)
                    {
                        length = space - start + 1;
                    }
                    else if (char.IsHighSurrogate(text[start + length - 1]) && length > 1)
                    {
                        length--;
                    }
                }
            }

            _lines.Add((start, length));
            start += length;
        }
    }

    int Measure(string text) => text.Length == 0 ? 0 : TextRenderer.MeasureText(text, Font, Size.Empty, Flags).Width;

    int LineOf(int position)
    {
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            if (position >= _lines[i].Start)
            {
                return i;
            }
        }

        return 0;
    }

    Point PointOf(int position)
    {
        var line = LineOf(position);
        var (start, _) = _lines[line];
        var x = Pad + Measure(Buffer.Text[start..Math.Min(position, Buffer.Text.Length)]);
        return new Point(x, Pad + line * _lineHeight - _scroll);
    }

    int PositionAt(Point point)
    {
        EnsureLayout();
        var line = Math.Clamp((point.Y + _scroll - Pad) / Math.Max(1, _lineHeight), 0, _lines.Count - 1);
        var (start, length) = _lines[line];
        var text = Buffer.Text;
        var visibleEnd = start + length;
        if (visibleEnd > start && text[visibleEnd - 1] == ' ' && line + 1 < _lines.Count && _lines[line + 1].Start == visibleEnd)
        {
            visibleEnd--;
        }

        var best = start;
        var bestDistance = int.MaxValue;
        for (var p = start; p <= visibleEnd; p++)
        {
            var distance = Math.Abs(Pad + Measure(text[start..p]) - point.X);
            if (distance < bestDistance)
            {
                best = p;
                bestDistance = distance;
            }
        }

        return best;
    }

    void ScrollToCaret()
    {
        EnsureLayout();
        var y = LineOf(Buffer.Caret) * _lineHeight;
        var visible = ClientSize.Height - Pad * 2 - _lineHeight;
        if (y < _scroll)
        {
            _scroll = y;
        }
        else if (y > _scroll + visible)
        {
            _scroll = y - visible;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureLayout();
        var g = e.Graphics;
        g.Clear(BackColor);
        var text = Buffer.Text;
        var (selStart, selEnd) = Buffer.Selection;

        for (var i = 0; i < _lines.Count; i++)
        {
            var y = Pad + i * _lineHeight - _scroll;
            if (y + _lineHeight < 0 || y > ClientSize.Height)
            {
                continue;
            }

            var (start, length) = _lines[i];
            var end = start + length;
            var lineText = text.Substring(start, length);
            TextRenderer.DrawText(g, lineText, Font, new Point(Pad, y), ForeColor, Flags);
            if (Buffer.HasSelection && selStart < end + 1 && selEnd > start)
            {
                var from = Math.Max(selStart, start);
                var to = Math.Min(selEnd, end);
                var x1 = Pad + Measure(text[start..from]);
                var x2 = Pad + Measure(text[start..to]) + (selEnd > end ? 4 : 0);
                var highlight = new Rectangle(x1, y, Math.Max(1, x2 - x1), _lineHeight);
                using var selection = new SolidBrush(Theme.Selection);
                g.FillRectangle(selection, highlight);
                var state = g.Save();
                g.SetClip(highlight);
                TextRenderer.DrawText(g, lineText, Font, new Point(Pad, y), Color.White, Flags);
                g.Restore(state);
            }
        }

        if (Focused)
        {
            var caret = PointOf(Buffer.Caret);
            g.DrawLine(Pens.Black, caret.X, caret.Y + 1, caret.X, caret.Y + _lineHeight - 2);
        }

        ControlPaint.DrawBorder(g, ClientRectangle, Theme.Line, ButtonBorderStyle.Solid);
        var contentHeight = _lines.Count * _lineHeight + Pad * 2;
        if (contentHeight > ClientSize.Height)
        {
            var track = ClientSize.Height - 4;
            var thumb = Math.Max(16, track * ClientSize.Height / contentHeight);
            var top = 2 + (track - thumb) * _scroll / Math.Max(1, contentHeight - ClientSize.Height);
            using var thumbBrush = new SolidBrush(Theme.Line2);
            g.FillRectangle(thumbBrush, ClientSize.Width - 6, top, 4, thumb);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            Buffer.MoveTo(PositionAt(e.Location), extend: ModifierKeys.HasFlag(Keys.Shift));
            _mouseSelecting = true;
            _preferredX = null;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_mouseSelecting)
        {
            Buffer.MoveTo(PositionAt(e.Location), extend: true);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _mouseSelecting = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        EnsureLayout();
        var max = Math.Max(0, _lines.Count * _lineHeight + Pad * 2 - ClientSize.Height);
        _scroll = Math.Clamp(_scroll - e.Delta / 120 * _lineHeight * 3, 0, max);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var extend = e.Shift;
        var handled = true;
        switch (e.KeyCode)
        {
            case Keys.Left: Buffer.MoveLeft(extend); break;
            case Keys.Right: Buffer.MoveRight(extend); break;
            case Keys.Home: Buffer.MoveLineStart(extend); break;
            case Keys.End: Buffer.MoveLineEnd(extend); break;
            case Keys.Up: MoveVertical(-1, extend); break;
            case Keys.Down: MoveVertical(1, extend); break;
            case Keys.Delete: Buffer.Delete(); _layoutDirty = true; break;
            case Keys.A when e.Control: Buffer.SelectAll(); break;
            case Keys.C when e.Control: CopySelection(); break;
            case Keys.X when e.Control: CopySelection(); Buffer.Insert(""); _layoutDirty = true; break;
            case Keys.V when e.Control: Paste(); break;
            default: handled = false; break;
        }

        if (!handled)
        {
            return;
        }

        if (e.KeyCode is not (Keys.Up or Keys.Down))
        {
            _preferredX = null;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        ScrollToCaret();
        Invalidate();
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        switch (e.KeyChar)
        {
            case '\b': Buffer.Backspace(); break;
            case '\r': Buffer.Insert("\n"); break;
            case var c when !char.IsControl(c): Buffer.Insert(c.ToString()); break;
            default: return;
        }

        e.Handled = true;
        _preferredX = null;
        Changed();
        ScrollToCaret();
    }

    void MoveVertical(int direction, bool extend)
    {
        EnsureLayout();
        var caret = PointOf(Buffer.Caret);
        _preferredX ??= caret.X;
        var line = LineOf(Buffer.Caret) + direction;
        if (line < 0 || line >= _lines.Count)
        {
            Buffer.MoveTo(line < 0 ? 0 : Buffer.Text.Length, extend);
            return;
        }

        var y = Pad + line * _lineHeight - _scroll + _lineHeight / 2;
        Buffer.MoveTo(PositionAt(new Point(_preferredX.Value, y)), extend);
    }

    void CopySelection()
    {
        if (Buffer.HasSelection)
        {
            Clipboard.SetText(Buffer.SelectedText.Replace("\n", "\r\n"));
        }
    }

    void Paste()
    {
        if (Clipboard.ContainsText())
        {
            Buffer.Insert(Clipboard.GetText());
            Changed();
        }
    }
}

/// <summary>
/// custom 컨트롤을 UIA/MSAA에 이름·값·자식 없는 Client 하나로만 보이게 한다.
/// </summary>
sealed class OpaqueAccessibleObject(Control owner) : Control.ControlAccessibleObject(owner)
{
    public override AccessibleRole Role => AccessibleRole.Client;

    public override string? Name
    {
        get => null;
        set { }
    }

    public override string? Value
    {
        get => null;
        set { }
    }

    public override string? Description => null;

    public override int GetChildCount() => 0;

    public override AccessibleObject? GetChild(int index) => null;
}
