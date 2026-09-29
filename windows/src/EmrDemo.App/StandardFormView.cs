using System.Globalization;
using EmrDemo.Core.Forms;

namespace EmrDemo.App;

interface IFormView
{
    Control Control { get; }

    /// <summary>(키, 값). 값이 빈 문자열이면 입력이 지워진 것이다.</summary>
    event Action<string, string>? Committed;

    FormLayoutResult Show(FormDefinition form, IReadOnlyDictionary<string, string> values);

    void CommitPending();
}

static class FormMetrics
{
    /// <summary>서식 배치 폭. 창 크기와 무관하게 고정해 정답 좌표가 실행마다 같게 한다.</summary>
    public const int LayoutWidth = 860;

    public static readonly Color Band = ColorTranslator.FromHtml("#ecebe6");
    public static readonly Color SectionText = ColorTranslator.FromHtml("#1a3c66");
    public static readonly Color GridTitle = ColorTranslator.FromHtml("#eef0f3");
    public static readonly Color GridHeader = ColorTranslator.FromHtml("#a8c6ea");
    public static readonly Color GridLine = ColorTranslator.FromHtml("#9aa4b1");
    public static readonly Color Highlight = ColorTranslator.FromHtml("#fff7d6");
    public static readonly Color ReadOnly = ColorTranslator.FromHtml("#f1f2f4");

    public static int Measure(string text) =>
        text.Length == 0 ? 0 : TextRenderer.MeasureText(text, Theme.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

    public static FormLayoutResult Layout(FormDefinition form) => FormLayout.Build(form, Measure, LayoutWidth);
}

/// <summary>
/// 서식을 표준 컨트롤로 배치한다. AutomationId(= Control.Name)는 서식 키이고, 라디오·체크 선택지는
/// "&lt;키&gt;#&lt;선택지 번호&gt;"다. 서식별 컨트롤 트리는 한 번 만들어 두고 값만 다시 채운다.
/// </summary>
sealed class StandardFormView : IFormView
{
    readonly Panel _host = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    readonly Dictionary<string, FormPage> _pages = [];
    FormPage? _current;

    public Control Control => _host;
    public event Action<string, string>? Committed;

    public FormLayoutResult Show(FormDefinition form, IReadOnlyDictionary<string, string> values)
    {
        if (!_pages.TryGetValue(form.Id, out var page))
        {
            page = new FormPage(form, (key, value) => Committed?.Invoke(key, value));
            _pages[form.Id] = page;
            _host.Controls.Add(page);
        }

        foreach (var other in _pages.Values)
        {
            other.Visible = other == page;
        }

        _current = page;
        page.Load(values);
        page.AutoScrollPosition = Point.Empty;
        return page.Layout;
    }

    public void CommitPending() => _current?.CommitPending();

    sealed class FormPage : Panel
    {
        readonly Action<string, string> _commit;
        readonly List<Action<IReadOnlyDictionary<string, string>>> _loaders = [];
        readonly List<Action> _pending = [];
        readonly Dictionary<string, string> _committed = [];
        readonly Dictionary<string, string> _initial = [];
        bool _loading;

        public FormPage(FormDefinition form, Action<string, string> commit)
        {
            _commit = commit;
            Layout = FormMetrics.Layout(form);
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Color.White;
            DoubleBuffered = true;
            AutoScrollMinSize = new Size(Layout.Width, Layout.Height);
            SuspendLayout();
            Build(form);
            ResumeLayout();
        }

        public new FormLayoutResult Layout { get; }

        public void Load(IReadOnlyDictionary<string, string> values)
        {
            _loading = true;
            _committed.Clear();
            foreach (var loader in _loaders)
            {
                loader(values);
            }

            _loading = false;
        }

        public void CommitPending()
        {
            foreach (var pending in _pending)
            {
                pending();
            }
        }

        void Commit(string key, string value)
        {
            if (_loading || _committed.GetValueOrDefault(key, Initial(key)) == value)
            {
                return;
            }

            _committed[key] = value;
            _commit(key, value);
        }

        string Initial(string key) => _initial.GetValueOrDefault(key, "");

        void Remember(string key, IReadOnlyDictionary<string, string> values) =>
            _initial[key] = values.GetValueOrDefault(key, "");

        static string Value(IReadOnlyDictionary<string, string> values, string key) => values.GetValueOrDefault(key, "");

        void Build(FormDefinition form)
        {
            var optionGroups = new Dictionary<string, List<ButtonBase>>();
            var grids = new Dictionary<string, List<LayoutItem>>();
            foreach (var item in Layout.Items)
            {
                switch (item.Kind)
                {
                    case LayoutKind.Title or LayoutKind.Department or LayoutKind.Section or LayoutKind.Sub
                        or LayoutKind.Note or LayoutKind.Static or LayoutKind.DrawLabel or LayoutKind.RowLabel
                        or LayoutKind.Label or LayoutKind.SetHeader:
                        AddLabel(item);
                        break;
                    case LayoutKind.DrawArea:
                        AddLabel(item, BorderStyle.FixedSingle, Color.Gray);
                        break;
                    case LayoutKind.Button or LayoutKind.GridButton:
                        Place(new Button { Text = item.Text, FlatStyle = FlatStyle.System, Font = Theme.Font, TabStop = false }, item);
                        break;
                    case LayoutKind.Text:
                        AddText((TextControl)item.Control!, item);
                        break;
                    case LayoutKind.TextArea:
                        AddTextArea(item);
                        break;
                    case LayoutKind.Date:
                        AddDate(item);
                        break;
                    case LayoutKind.Select:
                        AddSelect((SelectControl)item.Control!, item);
                        break;
                    case LayoutKind.Option:
                        AddOption(item, optionGroups);
                        break;
                    case LayoutKind.GridHeader or LayoutKind.GridRowNumber or LayoutKind.GridCell:
                        var gridKey = item.Key is { } cell ? cell[..cell.IndexOf('[')] : null;
                        if (gridKey is null)
                        {
                            break;
                        }

                        if (!grids.TryGetValue(gridKey, out var cells))
                        {
                            grids[gridKey] = cells = [];
                        }

                        cells.Add(item);
                        break;
                }
            }

            foreach (var (key, cells) in grids)
            {
                AddGrid(form, key, cells);
            }
        }

        void Place(Control control, LayoutItem item)
        {
            control.Bounds = new Rectangle(item.Bounds.X, item.Bounds.Y, item.Bounds.W, item.Bounds.H);
            control.Margin = Padding.Empty;
            Controls.Add(control);
        }

        void AddLabel(LayoutItem item, BorderStyle border = BorderStyle.None, Color? fore = null)
        {
            var bold = item.Kind is LayoutKind.Section or LayoutKind.Title;
            Place(new Label
            {
                Text = item.Text,
                Font = bold ? Theme.Bold : Theme.Font,
                ForeColor = fore ?? (item.Kind == LayoutKind.Section ? FormMetrics.SectionText : Color.Black),
                BackColor = item.Kind is LayoutKind.SetHeader ? Theme.Face2 : Color.Transparent,
                TextAlign = item.Kind == LayoutKind.DrawArea ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                AutoEllipsis = item.Kind is LayoutKind.RowLabel or LayoutKind.SetHeader,
                Padding = Padding.Empty,
                BorderStyle = border,
                UseMnemonic = false,
            }, item);
        }

        TextBox NewTextBox(string key, LayoutItem item, bool multiline)
        {
            var box = new TextBox
            {
                Name = key,
                Font = Theme.Font,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = multiline,
                AcceptsReturn = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            };
            Place(box, item);
            _loaders.Add(values =>
            {
                Remember(key, values);
                box.Text = Value(values, key).Replace("\n", "\r\n");
            });
            void CommitBox() => Commit(key, box.Text.Replace("\r\n", "\n"));
            box.Leave += (_, _) => CommitBox();
            box.TextChanged += (_, _) =>
            {
                if (!box.Focused)
                {
                    CommitBox();
                }
            };
            _pending.Add(CommitBox);
            return box;
        }

        void AddText(TextControl control, LayoutItem item)
        {
            var box = NewTextBox(control.Key, item, multiline: false);
            box.ReadOnly = control.ReadOnly;
            box.BackColor = control.ReadOnly ? FormMetrics.ReadOnly : control.Highlight ? FormMetrics.Highlight : Color.White;
        }

        void AddTextArea(LayoutItem item) => NewTextBox(item.Key!, item, multiline: true);

        void AddDate(LayoutItem item)
        {
            var key = item.Key!;
            var picker = new DateTimePicker
            {
                Name = key,
                Font = Theme.Font,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
            };
            Place(picker, item);
            string Current() => picker.Checked ? picker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
            _loaders.Add(values =>
            {
                Remember(key, values);
                var value = Value(values, key);
                if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    picker.Value = date;
                    picker.Checked = true;
                }
                else
                {
                    picker.Value = new DateTime(2026, 7, 28);
                    picker.Checked = false;
                }
            });
            picker.ValueChanged += (_, _) => Commit(key, Current());
            _pending.Add(() => Commit(key, Current()));
        }

        void AddSelect(SelectControl control, LayoutItem item)
        {
            var combo = new ComboBox { Name = control.Key, Font = Theme.Font, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.Add("");
            combo.Items.AddRange([.. control.Options]);
            Place(combo, item);
            _loaders.Add(values =>
            {
                Remember(control.Key, values);
                var index = combo.Items.IndexOf(Value(values, control.Key));
                combo.SelectedIndex = Math.Max(0, index);
            });
            combo.SelectedIndexChanged += (_, _) => Commit(control.Key, combo.SelectedItem as string ?? "");
        }

        void AddOption(LayoutItem item, Dictionary<string, List<ButtonBase>> groups)
        {
            var options = (OptionControl)item.Control!;
            var key = options.Key;
            ButtonBase button = options is RadioControl
                ? new RadioButton { AutoCheck = false }
                : new CheckBox { AutoCheck = true };
            button.Name = $"{key}#{item.OptionIndex}";
            button.Text = item.Text;
            button.Font = Theme.Font;
            button.UseMnemonic = false;
            Place(button, item);

            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = group = [];
                _loaders.Add(values =>
                {
                    Remember(key, values);
                    var value = Value(values, key);
                    var selected = options is RadioControl ? [value] : CheckValues.Split(value);
                    for (var i = 0; i < group.Count; i++)
                    {
                        SetChecked(group[i], selected.Contains(options.AllOptions[i]));
                    }
                });
            }

            group.Add(button);
            switch (button)
            {
                case RadioButton radio:
                    radio.Click += (_, _) =>
                    {
                        foreach (var sibling in group)
                        {
                            SetChecked(sibling, sibling == radio);
                        }

                        Commit(key, item.Text!);
                    };
                    break;
                case CheckBox check:
                    check.CheckedChanged += (_, _) => Commit(key, CheckValues.Join(options.AllOptions,
                        group.Where(IsChecked).Select(b => b.Text)));
                    break;
            }
        }

        static bool IsChecked(ButtonBase button) => button switch
        {
            RadioButton r => r.Checked,
            CheckBox c => c.Checked,
            _ => false,
        };

        static void SetChecked(ButtonBase button, bool value)
        {
            switch (button)
            {
                case RadioButton r: r.Checked = value; break;
                case CheckBox c: c.Checked = value; break;
            }
        }

        void AddGrid(FormDefinition form, string gridKey, List<LayoutItem> cells)
        {
            var grid = FindGrid(form.Blocks, gridKey) ?? throw new KeyNotFoundException(gridKey);
            var firstCell = cells.First(c => c.Key == GridBlock.CellKey(gridKey, 0, 0));
            var top = firstCell.Bounds.Y - FormLayout.GridRowHeight;
            var left = firstCell.Bounds.X - FormLayout.GridRowNumberWidth;
            var widths = grid.Columns.Select(c => c.W ?? 80).ToArray();
            var view = new DataGridView
            {
                Name = gridKey,
                Font = Theme.Font,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.None,
                GridColor = FormMetrics.GridLine,
                BackgroundColor = Color.White,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = FormLayout.GridRowHeight,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
            };
            view.ColumnHeadersDefaultCellStyle.BackColor = FormMetrics.GridHeader;
            view.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            view.RowTemplate.Height = FormLayout.GridRowHeight;
            view.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "#", HeaderText = "", Width = FormLayout.GridRowNumberWidth, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
            for (var c = 0; c < widths.Length; c++)
            {
                view.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = $"c{c}", HeaderText = grid.Columns[c].Header, Width = widths[c],
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            for (var r = 0; r < grid.Rows; r++)
            {
                view.Rows.Add([(r + 1).ToString(), .. Enumerable.Repeat<object>("", widths.Length)]);
            }

            view.Bounds = new Rectangle(left, top, FormLayout.GridRowNumberWidth + widths.Sum() + 1,
                FormLayout.GridRowHeight * (grid.Rows + 1) + 1);
            Controls.Add(view);

            _loaders.Add(values =>
            {
                for (var r = 0; r < grid.Rows; r++)
                {
                    for (var c = 0; c < widths.Length; c++)
                    {
                        var key = GridBlock.CellKey(gridKey, r, c);
                        Remember(key, values);
                        view.Rows[r].Cells[c + 1].Value = Value(values, key);
                    }
                }

                view.CurrentCell = null;
            });
            view.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex > 0)
                {
                    Commit(GridBlock.CellKey(gridKey, e.RowIndex, e.ColumnIndex - 1),
                        view.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string ?? "");
                }
            };
            _pending.Add(() =>
            {
                if (view.IsCurrentCellInEditMode)
                {
                    view.EndEdit();
                }
            });
        }

        static GridBlock? FindGrid(IEnumerable<FormBlock> blocks, string key)
        {
            foreach (var block in blocks)
            {
                var found = block switch
                {
                    GridBlock g when g.Key == key => g,
                    SetBlock s => FindGrid(s.Blocks, key),
                    SignBlock s => FindGrid(s.Blocks, key),
                    _ => null,
                };
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            FormDecorations.Paint(g, Layout);
        }
    }
}

/// <summary>배경·테두리처럼 컨트롤이 아닌 장식. 두 모드가 같은 모양을 쓴다.</summary>
static class FormDecorations
{
    public static void Paint(Graphics g, FormLayoutResult layout)
    {
        using var line = new Pen(FormMetrics.GridLine);
        using var band = new SolidBrush(FormMetrics.Band);
        using var gridTitle = new SolidBrush(FormMetrics.GridTitle);
        foreach (var item in layout.Items)
        {
            var r = new Rectangle(item.Bounds.X, item.Bounds.Y, item.Bounds.W, item.Bounds.H);
            switch (item.Kind)
            {
                case LayoutKind.TitleBand:
                    g.FillRectangle(band, r);
                    g.DrawLine(line, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
                    break;
                case LayoutKind.Section:
                    using (var sectionPen = new Pen(FormMetrics.SectionText))
                    {
                        g.DrawLine(sectionPen, r.Left, r.Bottom, r.Right, r.Bottom);
                    }

                    break;
                case LayoutKind.GridTitle:
                    g.FillRectangle(gridTitle, r);
                    g.DrawRectangle(line, r.X, r.Y, r.Width - 1, r.Height - 1);
                    break;
                case LayoutKind.SetBox or LayoutKind.SignBox:
                    g.DrawRectangle(line, r.X, r.Y, r.Width - 1, r.Height - 1);
                    break;
            }
        }
    }
}
