using EmrDemo.Core;

namespace EmrDemo.App;

/// <summary>
/// 직접 그리는 편집 그리드. 셀을 고르면 내용 전체가 선택된 편집 상태가 되고(입력하면 교체 —
/// DataGridView와 같다) Enter·Tab·↑↓·다른 셀 클릭·포커스 이탈 때 값을 확정한다.
/// <see cref="CustomTextBox"/>와 같은 이유로 Text·Name을 쓰지 않는다.
/// </summary>
sealed class CustomGrid : Control
{
    const int HeaderHeight = GridMetrics.HeaderHeight;
    const int RowHeight = GridMetrics.RowHeight;
    const int CellPad = 3;
    const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
        | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

    readonly IReadOnlyList<GridColumn> _columns;
    string[][] _cells = [];
    int _row = -1;
    int _column = -1;
    TextBuffer? _edit;

    public CustomGrid(IReadOnlyList<GridColumn> columns)
    {
        _columns = columns;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = Theme.Font;
        TabStop = true;
    }

    public event Action<int, int, string>? CellCommitted;

    protected override bool CanEnableIme => true;

    protected override AccessibleObject CreateAccessibilityInstance() => new OpaqueAccessibleObject(this);

    public void Load(IReadOnlyList<string[]> rows)
    {
        _cells = rows.Select(r => r.ToArray()).ToArray();
        _row = _column = -1;
        _edit = null;
        Invalidate();
    }

    public void CommitEdit()
    {
        if (_edit is null)
        {
            return;
        }

        var value = _edit.Text;
        _edit = null;
        if (value != _cells[_row][_column])
        {
            _cells[_row][_column] = value;
            CellCommitted?.Invoke(_row, _column, value);
        }

        Invalidate();
    }

    void Select(int row, int column)
    {
        CommitEdit();
        if (_cells.Length == 0)
        {
            return;
        }

        _row = Math.Clamp(row, 0, _cells.Length - 1);
        _column = Math.Clamp(column, 0, _columns.Count - 1);
        _edit = new TextBuffer(_cells[_row][_column], singleLine: true);
        _edit.SelectAll();
        Invalidate();
    }

    int[] ColumnWidths() => GridMetrics.FitWidths(_columns, ClientSize.Width - 1);

    Rectangle CellBounds(int row, int column)
    {
        var widths = ColumnWidths();
        var x = widths[..column].Sum();
        return new Rectangle(x, HeaderHeight + row * RowHeight, widths[column], RowHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var widths = ColumnWidths();
        using var headBrush = new SolidBrush(Theme.TableHead);
        using var headPen = new Pen(Theme.Line2);
        using var cellPen = new Pen(Theme.CellLine);
        using var selectBrush = new SolidBrush(Color.FromArgb(0xdd, 0xe8, 0xf8));

        var x = 0;
        for (var c = 0; c < _columns.Count; c++)
        {
            var header = new Rectangle(x, 0, widths[c], HeaderHeight);
            g.FillRectangle(headBrush, header);
            g.DrawRectangle(headPen, header.X, header.Y, header.Width - 1, header.Height - 1);
            TextRenderer.DrawText(g, _columns[c].Header, Font, header, ForeColor, Flags | TextFormatFlags.HorizontalCenter);
            x += widths[c];
        }

        for (var r = 0; r < _cells.Length; r++)
        {
            for (var c = 0; c < _columns.Count; c++)
            {
                var bounds = CellBounds(r, c);
                var selected = r == _row && c == _column;
                if (selected)
                {
                    g.FillRectangle(selectBrush, bounds);
                }

                g.DrawRectangle(cellPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                var text = selected && _edit is not null ? _edit.Text : _cells[r][c];
                var textBounds = Rectangle.Inflate(bounds, -CellPad, 0);
                if (selected && _edit is not null)
                {
                    PaintEditing(g, textBounds);
                }
                else
                {
                    TextRenderer.DrawText(g, text, Font, textBounds, ForeColor, Flags);
                }
            }
        }

        ControlPaint.DrawBorder(g, ClientRectangle, Theme.Line, ButtonBorderStyle.Solid);
    }

    void PaintEditing(Graphics g, Rectangle bounds)
    {
        var edit = _edit!;
        var flags = Flags & ~TextFormatFlags.EndEllipsis;
        var state = g.Save();
        g.SetClip(bounds);
        var caretX = bounds.X + Measure(edit.Text[..edit.Caret]);
        var shift = Math.Max(0, caretX - bounds.Right + 2);
        var origin = bounds with { X = bounds.X - shift, Width = bounds.Width + shift };
        TextRenderer.DrawText(g, edit.Text, Font, origin, ForeColor, flags);
        if (edit.HasSelection)
        {
            var (start, end) = edit.Selection;
            var x1 = origin.X + Measure(edit.Text[..start]);
            var x2 = origin.X + Measure(edit.Text[..end]);
            var highlight = new Rectangle(x1, bounds.Y + 2, Math.Max(1, x2 - x1), bounds.Height - 4);
            using var selection = new SolidBrush(Theme.Selection);
            g.FillRectangle(selection, highlight);
            g.SetClip(Rectangle.Intersect(highlight, bounds));
            TextRenderer.DrawText(g, edit.Text, Font, origin, Color.White, flags);
        }

        g.Restore(state);
        if (Focused)
        {
            g.DrawLine(Pens.Black, caretX - shift, bounds.Y + 3, caretX - shift, bounds.Bottom - 4);
        }
    }

    int Measure(string text) =>
        text.Length == 0 ? 0 : TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Y < HeaderHeight)
        {
            return;
        }

        var row = (e.Y - HeaderHeight) / RowHeight;
        if (row >= _cells.Length)
        {
            return;
        }

        var widths = ColumnWidths();
        var x = 0;
        for (var c = 0; c < widths.Length; c++)
        {
            if (e.X < x + widths[c])
            {
                if (row != _row || c != _column || _edit is null)
                {
                    Select(row, c);
                }

                return;
            }

            x += widths[c];
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        if (_row < 0 && _cells.Length > 0)
        {
            Select(0, 0);
        }
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        CommitEdit();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Tab)
        {
            return !TabLeavesGrid((keyData & Keys.Shift) != 0);
        }

        return (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter
                or Keys.Home or Keys.End or Keys.Escape
            || base.IsInputKey(keyData);
    }

    bool TabLeavesGrid(bool backward)
    {
        if (_cells.Length == 0 || _row < 0)
        {
            return true;
        }

        var index = _row * _columns.Count + _column;
        return backward ? index == 0 : index == _cells.Length * _columns.Count - 1;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_edit is null)
        {
            if (e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Tab && _cells.Length > 0)
            {
                Select(Math.Max(_row, 0), Math.Max(_column, 0));
                e.Handled = e.SuppressKeyPress = true;
            }

            return;
        }

        var handled = true;
        switch (e.KeyCode)
        {
            case Keys.Left: _edit.MoveLeft(e.Shift); break;
            case Keys.Right: _edit.MoveRight(e.Shift); break;
            case Keys.Home: _edit.MoveLineStart(e.Shift); break;
            case Keys.End: _edit.MoveLineEnd(e.Shift); break;
            case Keys.Delete: _edit.Delete(); break;
            case Keys.Up: Select(_row - 1, _column); break;
            case Keys.Down or Keys.Enter: Select(_row + 1, _column); break;
            case Keys.Tab: MoveTab(e.Shift ? -1 : 1); break;
            case Keys.Escape: _edit = null; break;
            case Keys.A when e.Control: _edit.SelectAll(); break;
            case Keys.C when e.Control:
                if (_edit.HasSelection)
                {
                    Clipboard.SetText(_edit.SelectedText);
                }

                break;
            case Keys.X when e.Control:
                if (_edit.HasSelection)
                {
                    Clipboard.SetText(_edit.SelectedText);
                    _edit.Insert("");
                }

                break;
            case Keys.V when e.Control:
                if (Clipboard.ContainsText())
                {
                    _edit.Insert(Clipboard.GetText());
                }

                break;
            default: handled = false; break;
        }

        if (handled)
        {
            e.Handled = e.SuppressKeyPress = true;
            Invalidate();
        }
    }

    void MoveTab(int direction)
    {
        var index = _row * _columns.Count + _column + direction;
        Select(index / _columns.Count, index % _columns.Count);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (_edit is null)
        {
            if (char.IsControl(e.KeyChar) || _cells.Length == 0)
            {
                return;
            }

            Select(Math.Max(_row, 0), Math.Max(_column, 0));
        }

        switch (e.KeyChar)
        {
            case '\b': _edit!.Backspace(); break;
            case var c when !char.IsControl(c): _edit!.Insert(c.ToString()); break;
            default: return;
        }

        e.Handled = true;
        Invalidate();
    }
}
