using System.Globalization;
using EmrDemo.Core;
using EmrDemo.Core.Forms;

namespace EmrDemo.App;

/// <summary>
/// 서식 전체를 컨트롤 하나에 직접 그린다(PowerBuilder DataWindow와 같은 모양). 값은 이 객체 안에만 있고
/// Text·Name을 쓰지 않으며 접근성 객체도 비워 둔다(<see cref="CustomTextBox"/>와 같은 이유).
/// </summary>
sealed class CustomFormCanvas : Control, IFormView
{
    const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
        | TextFormatFlags.VerticalCenter;

    FormLayoutResult _layout = new([], 0, 0);
    List<LayoutItem> _focusable = [];
    readonly Dictionary<string, string> _values = [];
    readonly Dictionary<string, string> _committed = [];
    int _focus = -1;
    TextBuffer? _edit;
    int _dropdownHighlight = -1;
    bool _dropdownOpen;
    int _scrollX;
    int _scrollY;
    int _editShift;

    public CustomFormCanvas()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = Theme.Font;
        TabStop = true;
        Dock = DockStyle.Fill;
    }

    public Control Control => this;
    public event Action<string, string>? Committed;

    protected override bool CanEnableIme => true;

    protected override AccessibleObject CreateAccessibilityInstance() => new OpaqueAccessibleObject(this);

    public FormLayoutResult Show(FormDefinition form, IReadOnlyDictionary<string, string> values)
    {
        _layout = FormMetrics.Layout(form);
        _focusable = _layout.Items.Where(i => i.IsInteractive && i.Kind != LayoutKind.Option || IsFirstOption(i)).ToList();
        _values.Clear();
        _committed.Clear();
        foreach (var (key, value) in values)
        {
            _values[key] = value;
            _committed[key] = value;
        }

        _focus = -1;
        _edit = null;
        _dropdownOpen = false;
        _scrollX = _scrollY = 0;
        Invalidate();
        return _layout;
    }

    /// <summary>
    /// Tab 순서에는 선택지 그룹마다 첫 선택지 하나만 두고, 그룹 안은 화살표로 옮긴다. standard 모드는 선택지마다
    /// Tab 정지점이 있어 두 모드의 Tab 횟수가 다르다.
    /// </summary>
    static bool IsFirstOption(LayoutItem item) => item.Kind == LayoutKind.Option && item.OptionIndex == 0;

    public void CommitPending() => CommitEdit();

    string Value(string key) => _values.GetValueOrDefault(key, "");

    void SetValue(string key, string value)
    {
        if (value.Length == 0)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }

        if (_committed.GetValueOrDefault(key, "") != value)
        {
            _committed[key] = value;
            Committed?.Invoke(key, value);
        }
    }

    LayoutItem? FocusedItem => _focus >= 0 && _focus < _focusable.Count ? _focusable[_focus] : null;

    static bool IsTextLike(LayoutItem item) =>
        item.Kind is LayoutKind.Text or LayoutKind.Date or LayoutKind.TextArea or LayoutKind.GridCell;

    static bool IsReadOnly(LayoutItem item) => item.Control is TextControl { ReadOnly: true };

    void FocusItem(LayoutItem? item, int? caret = null)
    {
        CommitEdit();
        _dropdownOpen = false;
        if (item is null)
        {
            _focus = -1;
            Invalidate();
            return;
        }

        var target = item.Kind == LayoutKind.Option
            ? _focusable.First(f => f.Kind == LayoutKind.Option && f.Key == item.Key)
            : item;
        _focus = _focusable.IndexOf(target);
        if (IsTextLike(item) && !IsReadOnly(item))
        {
            _edit = new TextBuffer(Value(item.Key!), singleLine: item.Kind != LayoutKind.TextArea);
            if (caret is { } position)
            {
                _edit.MoveTo(position);
            }
            else if (item.Kind == LayoutKind.TextArea)
            {
                _edit.MoveTo(_edit.Text.Length);
            }
            else
            {
                _edit.SelectAll();
            }
        }

        ScrollIntoView(item.Bounds);
        Invalidate();
    }

    void CommitEdit()
    {
        if (_edit is null || FocusedItem is not { } item)
        {
            _edit = null;
            return;
        }

        var value = _edit.Text;
        _edit = null;
        if (item.Kind == LayoutKind.Date && value.Length > 0
            && !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            Invalidate();
            return;
        }

        SetValue(item.Key!, value);
        Invalidate();
    }

    void Toggle(LayoutItem option)
    {
        var control = (OptionControl)option.Control!;
        var text = control.AllOptions[option.OptionIndex];
        if (control is RadioControl)
        {
            SetValue(control.Key, text);
        }
        else
        {
            var selected = CheckValues.Split(Value(control.Key)).ToHashSet();
            if (!selected.Remove(text))
            {
                selected.Add(text);
            }

            SetValue(control.Key, CheckValues.Join(control.AllOptions, selected));
        }

        Invalidate();
    }

    bool IsSelected(LayoutItem option)
    {
        var control = (OptionControl)option.Control!;
        var text = control.AllOptions[option.OptionIndex];
        var value = Value(control.Key);
        return control is RadioControl ? value == text : CheckValues.Split(value).Contains(text);
    }

    IReadOnlyList<string> DropdownOptions(LayoutItem select) => ["", .. ((SelectControl)select.Control!).Options];

    Rectangle DropdownBounds(LayoutItem select)
    {
        var count = DropdownOptions(select).Count;
        return new Rectangle(select.Bounds.X, select.Bounds.Bottom, Math.Max(select.Bounds.W, 80),
            count * FormLayout.ControlHeight + 2);
    }

    void OpenDropdown(LayoutItem select)
    {
        _dropdownOpen = true;
        _dropdownHighlight = DropdownOptions(select).ToList().IndexOf(Value(select.Key!));
        ScrollIntoView(ToLayoutRect(DropdownBounds(select)));
        Invalidate();
    }

    void ChooseDropdown(LayoutItem select, int index)
    {
        _dropdownOpen = false;
        var options = DropdownOptions(select);
        if (index >= 0 && index < options.Count)
        {
            SetValue(select.Key!, options[index]);
        }

        Invalidate();
    }

    static LayoutRect ToLayoutRect(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    void ScrollIntoView(LayoutRect bounds)
    {
        if (bounds.Y < _scrollY)
        {
            _scrollY = Math.Max(0, bounds.Y - 8);
        }
        else if (bounds.Bottom > _scrollY + ClientSize.Height)
        {
            _scrollY = bounds.Bottom - ClientSize.Height + 8;
        }

        if (bounds.X < _scrollX)
        {
            _scrollX = Math.Max(0, bounds.X - 8);
        }
        else if (bounds.Right > _scrollX + ClientSize.Width)
        {
            _scrollX = bounds.Right - ClientSize.Width + 8;
        }

        ClampScroll();
    }

    void ClampScroll()
    {
        var height = _layout.Height;
        if (_dropdownOpen && FocusedItem is { Kind: LayoutKind.Select } select)
        {
            height = Math.Max(height, DropdownBounds(select).Bottom + 8);
        }

        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, _layout.Width - ClientSize.Width));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, height - ClientSize.Height));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampScroll();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var state = g.Save();
        g.TranslateTransform(-_scrollX, -_scrollY);
        FormDecorations.Paint(g, _layout);
        var visible = new Rectangle(_scrollX, _scrollY, ClientSize.Width, ClientSize.Height);
        foreach (var item in _layout.Items)
        {
            var r = new Rectangle(item.Bounds.X, item.Bounds.Y, item.Bounds.W, item.Bounds.H);
            if (r.IntersectsWith(visible))
            {
                PaintItem(g, item, r);
            }
        }

        if (_dropdownOpen && FocusedItem is { Kind: LayoutKind.Select } select)
        {
            PaintDropdown(g, select);
        }

        g.Restore(state);
        PaintScrollbars(g);
        ControlPaint.DrawBorder(g, ClientRectangle, Theme.Line, ButtonBorderStyle.Solid);
    }

    void PaintItem(Graphics g, LayoutItem item, Rectangle r)
    {
        var focused = FocusedItem is { } f && (f.Kind == LayoutKind.Option
            ? item.Kind == LayoutKind.Option && f.Key == item.Key && item.OptionIndex == _optionCursor
            : f == item);
        switch (item.Kind)
        {
            case LayoutKind.Title:
            case LayoutKind.Section:
                TextRenderer.DrawText(g, item.Text, Theme.Bold, r,
                    item.Kind == LayoutKind.Section ? FormMetrics.SectionText : Color.Black, Flags | TextFormatFlags.EndEllipsis);
                break;
            case LayoutKind.SetHeader:
                using (var back = new SolidBrush(Theme.Face2))
                {
                    g.FillRectangle(back, r);
                }

                TextRenderer.DrawText(g, item.Text, Theme.Font, r, Color.Black, Flags | TextFormatFlags.EndEllipsis);
                break;
            case LayoutKind.DrawArea:
                using (var dash = new Pen(Color.Gray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawRectangle(dash, r.X, r.Y, r.Width - 1, r.Height - 1);
                }

                TextRenderer.DrawText(g, item.Text, Theme.Font, r, Color.Gray, Flags | TextFormatFlags.HorizontalCenter);
                break;
            case LayoutKind.Department or LayoutKind.Sub or LayoutKind.Note or LayoutKind.Static or LayoutKind.DrawLabel
                or LayoutKind.RowLabel or LayoutKind.Label or LayoutKind.GridRowNumber:
                TextRenderer.DrawText(g, item.Text, Theme.Font, r, Color.Black,
                    Flags | TextFormatFlags.EndEllipsis | (item.Kind == LayoutKind.GridRowNumber ? TextFormatFlags.HorizontalCenter : 0));
                if (item.Kind == LayoutKind.GridRowNumber)
                {
                    using var cellLine = new Pen(FormMetrics.GridLine);
                    g.DrawRectangle(cellLine, r.X, r.Y, r.Width - 1, r.Height - 1);
                }

                break;
            case LayoutKind.Button or LayoutKind.GridButton:
                ControlPaint.DrawButton(g, r, ButtonState.Normal);
                TextRenderer.DrawText(g, item.Text, Theme.Font, r, Color.Black, Flags | TextFormatFlags.HorizontalCenter);
                break;
            case LayoutKind.GridHeader:
                using (var head = new SolidBrush(FormMetrics.GridHeader))
                using (var headLine = new Pen(FormMetrics.GridLine))
                {
                    g.FillRectangle(head, r);
                    g.DrawRectangle(headLine, r.X, r.Y, r.Width - 1, r.Height - 1);
                }

                TextRenderer.DrawText(g, item.Text, Theme.Font, r, Color.Black,
                    Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                break;
            case LayoutKind.Text or LayoutKind.Date or LayoutKind.TextArea or LayoutKind.GridCell:
                PaintTextInput(g, item, r, focused);
                break;
            case LayoutKind.Select:
                PaintSelect(g, item, r, focused);
                break;
            case LayoutKind.Option:
                PaintOption(g, item, r, focused);
                break;
        }
    }

    void PaintTextInput(Graphics g, LayoutItem item, Rectangle r, bool focused)
    {
        var back = IsReadOnly(item) ? FormMetrics.ReadOnly
            : item.Control is TextControl { Highlight: true } ? FormMetrics.Highlight : Color.White;
        using (var brush = new SolidBrush(back))
        {
            g.FillRectangle(brush, r);
        }

        using (var border = new Pen(item.Kind == LayoutKind.GridCell ? FormMetrics.GridLine : Theme.Line))
        {
            g.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        var inner = Rectangle.Inflate(r, -3, 0);
        var state = g.Save();
        g.SetClip(inner);
        var multiline = item.Kind == LayoutKind.TextArea;
        var editing = focused && _edit is not null;
        var text = editing ? _edit!.Text : Value(item.Key!);
        if (text.Length == 0 && !editing && item.Kind == LayoutKind.Date)
        {
            TextRenderer.DrawText(g, "YYYY-MM-DD", Theme.Font, inner, Color.Silver, Flags);
        }
        else if (multiline)
        {
            PaintMultiline(g, text, inner, editing);
        }
        else
        {
            PaintSingleLine(g, text, inner, editing);
        }

        g.Restore(state);
        if (focused && Focused)
        {
            using var focusPen = new Pen(Theme.Selection);
            g.DrawRectangle(focusPen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
    }

    void PaintSingleLine(Graphics g, string text, Rectangle inner, bool editing)
    {
        var shift = 0;
        if (editing)
        {
            var caretX = FormMetrics.Measure(text[.._edit!.Caret]);
            shift = Math.Max(0, caretX - inner.Width + 2);
            _editShift = shift;
        }

        var origin = inner with { X = inner.X - shift, Width = inner.Width + shift };
        TextRenderer.DrawText(g, text, Theme.Font, origin, Color.Black, Flags);
        if (!editing)
        {
            return;
        }

        if (_edit!.HasSelection)
        {
            var (start, end) = _edit.Selection;
            var x1 = origin.X + FormMetrics.Measure(text[..start]);
            var x2 = origin.X + FormMetrics.Measure(text[..end]);
            var highlight = new Rectangle(x1, inner.Y + 2, Math.Max(1, x2 - x1), inner.Height - 4);
            using var selection = new SolidBrush(Theme.Selection);
            g.FillRectangle(selection, highlight);
            var clip = g.Save();
            g.SetClip(Rectangle.Intersect(highlight, inner));
            TextRenderer.DrawText(g, text, Theme.Font, origin, Color.White, Flags);
            g.Restore(clip);
        }

        if (FocusedItem is not null && Focused)
        {
            var x = origin.X + FormMetrics.Measure(text[.._edit.Caret]);
            g.DrawLine(Pens.Black, x, inner.Y + 3, x, inner.Bottom - 4);
        }
    }

    void PaintMultiline(Graphics g, string text, Rectangle inner, bool editing)
    {
        const TextFormatFlags lineFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var lineHeight = FormMetrics.Measure("가") > 0 ? TextRenderer.MeasureText("가", Theme.Font).Height : 14;
        var y = inner.Y + 2;
        var start = 0;
        var caretPoint = Point.Empty;
        foreach (var line in text.Split('\n'))
        {
            TextRenderer.DrawText(g, line, Theme.Font, new Point(inner.X, y), Color.Black, lineFlags);
            if (editing && _edit!.HasSelection)
            {
                var (selStart, selEnd) = _edit.Selection;
                var from = Math.Clamp(selStart - start, 0, line.Length);
                var to = Math.Clamp(selEnd - start, 0, line.Length);
                if (selStart <= start + line.Length && selEnd >= start && to >= from)
                {
                    var x1 = inner.X + FormMetrics.Measure(line[..from]);
                    var x2 = inner.X + FormMetrics.Measure(line[..to]) + (selEnd > start + line.Length ? 4 : 0);
                    var highlight = new Rectangle(x1, y, Math.Max(1, x2 - x1), lineHeight);
                    using var selection = new SolidBrush(Theme.Selection);
                    g.FillRectangle(selection, highlight);
                    var clip = g.Save();
                    g.SetClip(highlight, System.Drawing.Drawing2D.CombineMode.Intersect);
                    TextRenderer.DrawText(g, line, Theme.Font, new Point(inner.X, y), Color.White, lineFlags);
                    g.Restore(clip);
                }
            }

            if (editing && _edit!.Caret >= start && _edit.Caret <= start + line.Length)
            {
                caretPoint = new Point(inner.X + FormMetrics.Measure(line[..(_edit.Caret - start)]), y);
            }

            start += line.Length + 1;
            y += lineHeight;
        }

        if (editing && Focused)
        {
            g.DrawLine(Pens.Black, caretPoint.X, caretPoint.Y + 1, caretPoint.X, caretPoint.Y + lineHeight - 2);
        }
    }

    void PaintSelect(Graphics g, LayoutItem item, Rectangle r, bool focused)
    {
        g.FillRectangle(Brushes.White, r);
        using (var border = new Pen(focused ? Theme.Selection : Theme.Line))
        {
            g.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        var arrow = new Rectangle(r.Right - 17, r.Y + 1, 16, r.Height - 2);
        ControlPaint.DrawComboButton(g, arrow, ButtonState.Normal);
        TextRenderer.DrawText(g, Value(item.Key!), Theme.Font, new Rectangle(r.X + 3, r.Y, r.Width - 22, r.Height),
            Color.Black, Flags | TextFormatFlags.EndEllipsis);
    }

    void PaintDropdown(Graphics g, LayoutItem select)
    {
        var bounds = DropdownBounds(select);
        g.FillRectangle(Brushes.White, bounds);
        g.DrawRectangle(Pens.Black, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        var options = DropdownOptions(select);
        for (var i = 0; i < options.Count; i++)
        {
            var row = new Rectangle(bounds.X + 1, bounds.Y + 1 + i * FormLayout.ControlHeight, bounds.Width - 2, FormLayout.ControlHeight);
            var highlighted = i == _dropdownHighlight;
            if (highlighted)
            {
                using var brush = new SolidBrush(Theme.Selection);
                g.FillRectangle(brush, row);
            }

            TextRenderer.DrawText(g, options[i], Theme.Font, Rectangle.Inflate(row, -3, 0),
                highlighted ? Color.White : Color.Black, Flags);
        }
    }

    int _optionCursor;

    void PaintOption(Graphics g, LayoutItem item, Rectangle r, bool focused)
    {
        var box = new Rectangle(r.X, r.Y + (r.Height - FormLayout.OptionBox) / 2, FormLayout.OptionBox, FormLayout.OptionBox);
        var selected = IsSelected(item);
        if (item.Control is RadioControl)
        {
            ControlPaint.DrawRadioButton(g, box, selected ? ButtonState.Checked : ButtonState.Normal);
        }
        else
        {
            ControlPaint.DrawCheckBox(g, box, selected ? ButtonState.Checked : ButtonState.Normal);
        }

        var textBounds = new Rectangle(box.Right + 4, r.Y, r.Width - FormLayout.OptionBox - 4, r.Height);
        TextRenderer.DrawText(g, item.Text, Theme.Font, textBounds, Color.Black, Flags | TextFormatFlags.EndEllipsis);
        if (focused && Focused)
        {
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(textBounds, 1, -1));
        }
    }

    void PaintScrollbars(Graphics g)
    {
        using var thumb = new SolidBrush(Theme.Line2);
        if (_layout.Height > ClientSize.Height && ClientSize.Height > 0)
        {
            var track = ClientSize.Height - 4;
            var size = Math.Max(16, track * ClientSize.Height / _layout.Height);
            var top = 2 + (track - size) * _scrollY / Math.Max(1, _layout.Height - ClientSize.Height);
            g.FillRectangle(thumb, ClientSize.Width - 6, top, 4, size);
        }

        if (_layout.Width > ClientSize.Width && ClientSize.Width > 0)
        {
            var track = ClientSize.Width - 4;
            var size = Math.Max(16, track * ClientSize.Width / _layout.Width);
            var left = 2 + (track - size) * _scrollX / Math.Max(1, _layout.Width - ClientSize.Width);
            g.FillRectangle(thumb, left, ClientSize.Height - 6, size, 4);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var x = e.X + _scrollX;
        var y = e.Y + _scrollY;
        var wasOpen = _dropdownOpen;
        if (_dropdownOpen && FocusedItem is { Kind: LayoutKind.Select } select)
        {
            var bounds = DropdownBounds(select);
            if (bounds.Contains(x, y))
            {
                ChooseDropdown(select, (y - bounds.Y - 1) / FormLayout.ControlHeight);
                return;
            }

            _dropdownOpen = false;
            Invalidate();
        }

        var hit = _layout.HitTest(x, y);
        if (hit is null || hit.Kind == LayoutKind.GridRowNumber)
        {
            return;
        }

        switch (hit.Kind)
        {
            case LayoutKind.Option:
                FocusItem(hit);
                _optionCursor = hit.OptionIndex;
                Toggle(hit);
                break;
            case LayoutKind.Select:
                var reopen = FocusedItem != hit || !wasOpen;
                FocusItem(hit);
                if (reopen)
                {
                    OpenDropdown(hit);
                }

                break;
            case var _ when hit == FocusedItem && _edit is not null:
                if (hit.Kind != LayoutKind.TextArea)
                {
                    _edit.MoveTo(CaretAt(hit, x));
                }

                Invalidate();
                break;
            default:
                FocusItem(hit, IsTextLike(hit) && hit.Kind != LayoutKind.TextArea ? CaretAt(hit, x) : null);
                if (hit.Kind == LayoutKind.TextArea)
                {
                    _edit?.MoveTo(_edit.Text.Length);
                }

                break;
        }
    }

    int CaretAt(LayoutItem item, int x)
    {
        var editing = item == FocusedItem && _edit is not null;
        var text = editing ? _edit!.Text : Value(item.Key!);
        var local = x - item.Bounds.X - 3 + (editing ? _editShift : 0);
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var p = 0; p <= text.Length; p++)
        {
            var distance = Math.Abs(FormMetrics.Measure(text[..p]) - local);
            if (distance < bestDistance)
            {
                best = p;
                bestDistance = distance;
            }
        }

        return best;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var step = e.Delta / 120 * FormLayout.ControlHeight * 3;
        if (ModifierKeys.HasFlag(Keys.Shift))
        {
            _scrollX -= step;
        }
        else
        {
            _scrollY -= step;
        }

        ClampScroll();
        Invalidate();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        if (MouseButtons == MouseButtons.None && _focusable.Count > 0)
        {
            _optionCursor = 0;
            FocusItem(ModifierKeys.HasFlag(Keys.Shift) ? _focusable[^1] : _focusable[0]);
        }
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        CommitEdit();
        _dropdownOpen = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        if (code == Keys.Tab)
        {
            var backward = (keyData & Keys.Shift) != 0;
            return backward ? _focus > 0 : _focus < _focusable.Count - 1;
        }

        return code is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Home or Keys.End
                or Keys.Escape or Keys.Space
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var handled = HandleKey(e);
        if (handled)
        {
            e.Handled = e.SuppressKeyPress = true;
            Invalidate();
        }
    }

    bool HandleKey(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Tab)
        {
            MoveFocus(e.Shift ? -1 : 1);
            return true;
        }

        if (FocusedItem is not { } item)
        {
            if (e.KeyCode is Keys.Down or Keys.Right && _focusable.Count > 0)
            {
                FocusItem(_focusable[0]);
                return true;
            }

            return false;
        }

        if (item.Kind == LayoutKind.Select)
        {
            return SelectKey(item, e.KeyCode);
        }

        if (item.Kind == LayoutKind.Option)
        {
            return OptionKey(item, e.KeyCode);
        }

        if (_edit is null)
        {
            return false;
        }

        switch (e.KeyCode)
        {
            case Keys.Left: _edit.MoveLeft(e.Shift); return true;
            case Keys.Right: _edit.MoveRight(e.Shift); return true;
            case Keys.Home: _edit.MoveLineStart(e.Shift); return true;
            case Keys.End: _edit.MoveLineEnd(e.Shift); return true;
            case Keys.Delete: _edit.Delete(); return true;
            case Keys.Escape:
                _edit = new TextBuffer(Value(item.Key!), singleLine: item.Kind != LayoutKind.TextArea);
                return true;
            case Keys.Enter when item.Kind != LayoutKind.TextArea:
                MoveFocus(1);
                return true;
            case Keys.A when e.Control: _edit.SelectAll(); return true;
            case Keys.C when e.Control:
                if (_edit.HasSelection)
                {
                    Clipboard.SetText(_edit.SelectedText.Replace("\n", "\r\n"));
                }

                return true;
            case Keys.X when e.Control:
                if (_edit.HasSelection)
                {
                    Clipboard.SetText(_edit.SelectedText.Replace("\n", "\r\n"));
                    _edit.Insert("");
                }

                return true;
            case Keys.V when e.Control:
                if (Clipboard.ContainsText())
                {
                    _edit.Insert(Clipboard.GetText());
                }

                return true;
            default:
                return false;
        }
    }

    bool SelectKey(LayoutItem item, Keys key)
    {
        var count = DropdownOptions(item).Count;
        switch (key)
        {
            case Keys.Space or Keys.Enter when !_dropdownOpen:
                OpenDropdown(item);
                return true;
            case Keys.Enter:
                ChooseDropdown(item, _dropdownHighlight);
                return true;
            case Keys.Escape:
                _dropdownOpen = false;
                return true;
            case Keys.Down or Keys.Up when _dropdownOpen:
                _dropdownHighlight = Math.Clamp(_dropdownHighlight + (key == Keys.Down ? 1 : -1), 0, count - 1);
                return true;
            default:
                return false;
        }
    }

    bool OptionKey(LayoutItem item, Keys key)
    {
        var options = _layout.Items.Where(i => i.Kind == LayoutKind.Option && i.Key == item.Key).OrderBy(i => i.OptionIndex).ToList();
        switch (key)
        {
            case Keys.Space:
                Toggle(options[Math.Clamp(_optionCursor, 0, options.Count - 1)]);
                return true;
            case Keys.Left or Keys.Up:
                _optionCursor = Math.Max(0, _optionCursor - 1);
                return true;
            case Keys.Right or Keys.Down:
                _optionCursor = Math.Min(options.Count - 1, _optionCursor + 1);
                return true;
            default:
                return false;
        }
    }

    void MoveFocus(int direction)
    {
        var next = Math.Clamp(_focus + direction, 0, _focusable.Count - 1);
        _optionCursor = 0;
        FocusItem(_focusable[next]);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (_edit is null || FocusedItem is not { } item || IsReadOnly(item))
        {
            return;
        }

        switch (e.KeyChar)
        {
            case '\b': _edit.Backspace(); break;
            case '\r' when item.Kind == LayoutKind.TextArea: _edit.Insert("\n"); break;
            case var c when !char.IsControl(c): _edit.Insert(c.ToString()); break;
            default: return;
        }

        e.Handled = true;
        Invalidate();
    }
}
