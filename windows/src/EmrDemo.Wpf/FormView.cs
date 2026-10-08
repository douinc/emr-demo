using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using EmrDemo.Core.Forms;

namespace EmrDemo.Wpf;

static class FormMeasure
{
    /// <summary>서식 배치 폭. 창 크기와 무관하게 고정해 정답 좌표가 실행마다 같게 한다.</summary>
    public const int LayoutWidth = 860;

    static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");

    public static int Measure(string text) => text.Length == 0
        ? 0
        : (int)Math.Ceiling(new FormattedText(text, Korean, FlowDirection.LeftToRight, Theme.Regular, Theme.FontSize,
            Brushes.Black, pixelsPerDip: 1.0).WidthIncludingTrailingWhitespace);

    public static FormLayoutResult Layout(FormDefinition form) => FormLayout.Build(form, Measure, LayoutWidth);
}

/// <summary>
/// 서식을 표준 WPF 컨트롤로 배치한다(WinForms판 StandardFormView). AutomationId는 서식 키이고, 라디오·체크
/// 선택지는 "&lt;키&gt;#&lt;선택지 번호&gt;"다. 서식별 화면은 한 번 만들어 두고 값만 다시 채운다.
/// </summary>
sealed class FormView
{
    readonly Grid _host = new() { Background = Brushes.White };
    readonly Dictionary<string, FormPage> _pages = [];
    FormPage? _current;

    public FrameworkElement Element => _host;

    /// <summary>(키, 값). 값이 빈 문자열이면 입력이 지워진 것이다.</summary>
    public event Action<string, string>? Committed;

    public FormLayoutResult Show(FormDefinition form, IReadOnlyDictionary<string, string> values)
    {
        if (!_pages.TryGetValue(form.Id, out var page))
        {
            page = new FormPage(form, (key, value) => Committed?.Invoke(key, value));
            _pages[form.Id] = page;
            _host.Children.Add(page.Element);
        }

        foreach (var other in _pages.Values)
        {
            other.Element.Visibility = other == page ? Visibility.Visible : Visibility.Collapsed;
        }

        _current = page;
        page.Load(values);
        page.Element.ScrollToHome();
        return page.Layout;
    }

    public void CommitPending() => _current?.CommitPending();

    sealed class FormPage
    {
        readonly Canvas _canvas;
        readonly Action<string, string> _commit;
        readonly List<Action<IReadOnlyDictionary<string, string>>> _loaders = [];
        readonly List<Action> _pending = [];
        readonly FormCommits _commits = new();

        public FormPage(FormDefinition form, Action<string, string> commit)
        {
            _commit = commit;
            Layout = FormMeasure.Layout(form);
            _canvas = new Canvas { Width = Layout.Width, Height = Layout.Height, Background = Brushes.White };
            _canvas.Children.Add(new FormDecorations(Layout));
            Element = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                Focusable = false,
                Content = _canvas,
            };
            Build(form);
        }

        public ScrollViewer Element { get; }

        public FormLayoutResult Layout { get; }

        public void Load(IReadOnlyDictionary<string, string> values)
        {
            _commits.BeginLoad();
            foreach (var loader in _loaders)
            {
                loader(values);
            }

            _commits.EndLoad();
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
            if (_commits.ShouldCommit(key, value))
            {
                _commit(key, value);
            }
        }

        static string Value(IReadOnlyDictionary<string, string> values, string key) => values.GetValueOrDefault(key, "");

        void Build(FormDefinition form)
        {
            var optionGroups = new Dictionary<string, List<(ToggleButton Button, string Text)>>();
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
                        AddLabel(item, bordered: true, Brushes.Gray);
                        break;
                    case LayoutKind.Button or LayoutKind.GridButton:
                        Place(new Button
                        {
                            Content = Plain(item.Text ?? ""),
                            Focusable = false,
                            IsTabStop = false,
                            Padding = new Thickness(0),
                        }, item);
                        break;
                    case LayoutKind.Text:
                        AddText((TextControl)item.Control!, item);
                        break;
                    case LayoutKind.TextArea:
                        NewTextBox(item.Key!, item, multiline: true);
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

        void Place(FrameworkElement element, LayoutItem item)
        {
            Canvas.SetLeft(element, item.Bounds.X);
            Canvas.SetTop(element, item.Bounds.Y);
            element.Width = item.Bounds.W;
            element.Height = item.Bounds.H;
            _canvas.Children.Add(element);
        }

        /// <summary>글자 그대로 보이는 내용(문자열 Content는 '_'를 접근 키로 읽는다).</summary>
        static TextBlock Plain(string text) => new() { Text = text };

        void AddLabel(LayoutItem item, bool bordered = false, Brush? fore = null)
        {
            var bold = item.Kind is LayoutKind.Section or LayoutKind.Title;
            var text = new TextBlock
            {
                Text = item.Text ?? "",
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = fore ?? (item.Kind == LayoutKind.Section ? Theme.SectionText : Brushes.Black),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = item.Kind == LayoutKind.DrawArea ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                TextAlignment = item.Kind == LayoutKind.DrawArea ? TextAlignment.Center : TextAlignment.Left,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = item.Kind is LayoutKind.RowLabel or LayoutKind.SetHeader
                    ? TextTrimming.CharacterEllipsis
                    : TextTrimming.None,
            };
            Place(new Border
            {
                Child = text,
                Background = item.Kind is LayoutKind.SetHeader ? Theme.Face2 : Brushes.Transparent,
                BorderBrush = bordered ? Brushes.Black : null,
                BorderThickness = new Thickness(bordered ? 1 : 0),
            }, item);
        }

        TextBox NewTextBox(string key, LayoutItem item, bool multiline)
        {
            var box = TextField.NewBox(key, multiline);
            Place(box, item);
            _loaders.Add(values =>
            {
                _commits.Remember(key, values);
                box.Text = Value(values, key).Replace("\n", "\r\n");
            });
            void CommitBox() => Commit(key, TextCommit.Normalize(box.Text));
            box.LostKeyboardFocus += (_, _) => CommitBox();
            box.TextChanged += (_, _) =>
            {
                if (!box.IsKeyboardFocusWithin)
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
            box.IsReadOnly = control.ReadOnly;
            box.Background = control.ReadOnly ? Theme.ReadOnly : control.Highlight ? Theme.Highlight : Brushes.White;
        }

        /// <summary>
        /// 날짜: WPF DatePicker. 빈 값은 SelectedDate null이다. 글자로 친 날짜는 포커스를 잃거나 Enter를 칠 때
        /// SelectedDate가 되고, 그때(달력에서 고를 때도) 기록한다.
        /// </summary>
        void AddDate(LayoutItem item)
        {
            var key = item.Key!;
            var picker = new DatePicker
            {
                SelectedDateFormat = DatePickerFormat.Short,
                Padding = new Thickness(1, 0, 1, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = Theme.Line,
            };
            AutomationProperties.SetAutomationId(picker, key);
            Place(picker, item);
            _loaders.Add(values =>
            {
                _commits.Remember(key, values);
                picker.SelectedDate = KoreanCulture.ParseStored(Value(values, key));
            });
            picker.SelectedDateChanged += (_, _) => Commit(key, KoreanCulture.StoredDate(picker.SelectedDate));
            _pending.Add(() =>
            {
                // 포커스를 잃지 않은 채 저장(Ctrl+S)하면 친 글자가 아직 날짜가 아니다. 글자를 날짜로 읽힌 뒤 기록한다.
                if (FindDescendant<DatePickerTextBox>(picker) is { } textBox && textBox.Text != picker.Text)
                {
                    picker.Text = textBox.Text;
                }

                Commit(key, KoreanCulture.StoredDate(picker.SelectedDate));
            });
        }

        void AddSelect(SelectControl control, LayoutItem item)
        {
            var combo = new ComboBox { IsEditable = false, Padding = new Thickness(3, 0, 3, 0), VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(combo, control.Key);
            combo.Items.Add("");
            foreach (var option in control.Options)
            {
                combo.Items.Add(option);
            }

            Place(combo, item);
            _loaders.Add(values =>
            {
                _commits.Remember(control.Key, values);
                combo.SelectedIndex = Math.Max(0, combo.Items.IndexOf(Value(values, control.Key)));
            });
            combo.SelectionChanged += (_, _) => Commit(control.Key, combo.SelectedItem as string ?? "");
        }

        void AddOption(LayoutItem item, Dictionary<string, List<(ToggleButton Button, string Text)>> groups)
        {
            var options = (OptionControl)item.Control!;
            var key = options.Key;
            var text = item.Text ?? "";
            // 서식 키에 서식 id가 들어 있어 창 전체에서 그룹 이름이 겹치지 않는다.
            ToggleButton button = options is RadioControl ? new RadioButton { GroupName = key } : new CheckBox();
            AutomationProperties.SetAutomationId(button, $"{key}#{item.OptionIndex}");
            button.Content = Plain(text);
            button.VerticalContentAlignment = VerticalAlignment.Center;
            Place(button, item);

            if (!groups.TryGetValue(key, out var group))
            {
                groups[key] = group = [];
                var members = group;
                _loaders.Add(values =>
                {
                    _commits.Remember(key, values);
                    var value = Value(values, key);
                    var selected = options is RadioControl ? [value] : CheckValues.Split(value);
                    foreach (var (member, memberText) in members)
                    {
                        member.IsChecked = selected.Contains(memberText);
                    }
                });
            }

            group.Add((button, text));
            if (button is RadioButton radio)
            {
                radio.Checked += (_, _) => Commit(key, text);
            }
            else
            {
                void CommitChecks() => Commit(key, CheckValues.Join(options.AllOptions,
                    group.Where(m => m.Button.IsChecked == true).Select(m => m.Text)));
                button.Checked += (_, _) => CommitChecks();
                button.Unchecked += (_, _) => CommitChecks();
            }
        }

        void AddGrid(FormDefinition form, string gridKey, List<LayoutItem> cells)
        {
            var grid = FindGrid(form.Blocks, gridKey) ?? throw new KeyNotFoundException(gridKey);
            var firstCell = cells.First(c => c.Key == GridBlock.CellKey(gridKey, 0, 0));
            var top = firstCell.Bounds.Y - FormLayout.GridRowHeight;
            var left = firstCell.Bounds.X - FormLayout.GridRowNumberWidth;
            var widths = Enumerable.Range(0, grid.Columns.Count)
                .Select(c => cells.Single(i => i.Key == GridBlock.CellKey(gridKey, 0, c)).Bounds.W).ToArray();

            var view = GridField.NewGrid(Theme.GridHeader, Theme.GridLine);
            view.BorderThickness = new Thickness(0);
            AutomationProperties.SetAutomationId(view, gridKey);
            ScrollViewer.SetHorizontalScrollBarVisibility(view, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(view, ScrollBarVisibility.Disabled);
            view.Columns.Add(new DataGridTextColumn
            {
                Header = "",
                Binding = new Binding(nameof(GridRow.Number)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(FormLayout.GridRowNumberWidth),
                IsReadOnly = true,
            });
            for (var c = 0; c < widths.Length; c++)
            {
                view.Columns.Add(new DataGridTextColumn
                {
                    Header = grid.Columns[c].Header,
                    Binding = new Binding($"[{c}]"),
                    Width = new DataGridLength(widths[c]),
                });
            }

            var rows = new List<GridRow>();
            for (var r = 0; r < grid.Rows; r++)
            {
                var row = new GridRow((r + 1).ToString(), widths.Length);
                var rowIndex = r;
                row.CellChanged += (column, value) => Commit(GridBlock.CellKey(gridKey, rowIndex, column), value);
                rows.Add(row);
            }

            view.ItemsSource = rows;
            Canvas.SetLeft(view, left);
            Canvas.SetTop(view, top);
            view.Width = FormLayout.GridRowNumberWidth + widths.Sum() + 1;
            view.Height = FormLayout.GridRowHeight * (grid.Rows + 1) + 1;
            _canvas.Children.Add(view);

            _loaders.Add(values =>
            {
                view.CancelEdit(DataGridEditingUnit.Row);
                for (var r = 0; r < grid.Rows; r++)
                {
                    var rowValues = new string[widths.Length];
                    for (var c = 0; c < widths.Length; c++)
                    {
                        var key = GridBlock.CellKey(gridKey, r, c);
                        _commits.Remember(key, values);
                        rowValues[c] = Value(values, key);
                    }

                    rows[r].Load(rowValues);
                }

                view.UnselectAllCells();
            });
            _pending.Add(() => view.CommitEdit(DataGridEditingUnit.Row, true));
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

        static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    return match;
                }

                if (FindDescendant<T>(child) is { } deeper)
                {
                    return deeper;
                }
            }

            return null;
        }
    }
}

/// <summary>배경·테두리처럼 컨트롤이 아닌 장식(WinForms판 FormDecorations). 한 요소가 직접 그린다.</summary>
sealed class FormDecorations : FrameworkElement
{
    readonly FormLayoutResult _layout;

    public FormDecorations(FormLayoutResult layout)
    {
        _layout = layout;
        Width = layout.Width;
        Height = layout.Height;
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var line = new Pen(Theme.GridLine, 1);
        var section = new Pen(Theme.SectionText, 1);
        foreach (var item in _layout.Items)
        {
            var r = new Rect(item.Bounds.X, item.Bounds.Y, item.Bounds.W, item.Bounds.H);
            switch (item.Kind)
            {
                case LayoutKind.TitleBand:
                    dc.DrawRectangle(Theme.Band, null, r);
                    dc.DrawLine(line, new Point(r.Left, r.Bottom - 0.5), new Point(r.Right, r.Bottom - 0.5));
                    break;
                case LayoutKind.Section:
                    dc.DrawLine(section, new Point(r.Left, r.Bottom + 0.5), new Point(r.Right, r.Bottom + 0.5));
                    break;
                case LayoutKind.GridTitle:
                    dc.DrawRectangle(Theme.GridTitle, line, Inner(r));
                    break;
                case LayoutKind.GridRowNumber when item.Text == "1":
                    dc.DrawRectangle(Theme.GridHeader, line, Inner(r with { Y = r.Y - r.Height }));
                    break;
                case LayoutKind.SetBox or LayoutKind.SignBox:
                    dc.DrawRectangle(null, line, Inner(r));
                    break;
            }
        }
    }

    /// <summary>1px 선이 화소 경계에 맞도록 반 화소 안쪽으로 줄인 사각형.</summary>
    static Rect Inner(Rect r) => new(r.X + 0.5, r.Y + 0.5, Math.Max(0, r.Width - 1), Math.Max(0, r.Height - 1));
}
