namespace EmrDemo.Core.Forms;

public readonly record struct LayoutRect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public bool Intersects(LayoutRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public LayoutRect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };
}

public enum LayoutKind
{
    TitleBand,
    Title,
    Department,
    Section,
    Sub,
    Note,
    Static,
    DrawLabel,
    DrawArea,
    RowLabel,
    Label,
    Button,
    Text,
    Date,
    TextArea,
    Select,
    Option,
    GridTitle,
    GridButton,
    GridHeader,
    GridRowNumber,
    GridCell,
    SetHeader,
    SetBox,
    SignBox,
}

/// <summary>
/// 서식 한 요소의 배치. Option은 Key(그룹 키)와 OptionIndex(<see cref="OptionControl.AllOptions"/> 순서)로
/// 식별하고, 입력 요소는 Key로 식별한다.
/// </summary>
public sealed record LayoutItem(
    LayoutKind Kind,
    LayoutRect Bounds,
    string? Key = null,
    string? Text = null,
    int OptionIndex = -1,
    FormControl? Control = null)
{
    public bool IsInteractive => Key is not null;
}

public sealed record FormLayoutResult(IReadOnlyList<LayoutItem> Items, int Width, int Height)
{
    public LayoutItem? HitTest(int x, int y) =>
        Items.LastOrDefault(i => i.IsInteractive && i.Bounds.Contains(x, y));
}

/// <summary>
/// standard·custom 두 모드가 같은 좌표를 쓰도록 서식을 배치한다(웹 ob-forms.css를 단순화한 규칙).
/// </summary>
public static class FormLayout
{
    public const int Pad = 8;
    public const int ControlHeight = 20;
    public const int Gap = 4;
    public const int IndentStep = 14;
    public const int OptionBox = 13;
    public const int GridRowHeight = 20;
    public const int GridRowNumberWidth = 24;
    public const int DateWidth = 110;

    public static FormLayoutResult Build(FormDefinition form, Func<string, int> measure, int width)
    {
        var builder = new Builder(form, measure, Math.Max(width, 200));
        builder.Run();
        return new FormLayoutResult(builder.Items, builder.MaxRight, builder.Y + Pad);
    }

    sealed class Builder(FormDefinition form, Func<string, int> measure, int width)
    {
        public readonly List<LayoutItem> Items = [];
        public int Y;
        public int MaxRight = width;

        void Add(LayoutItem item)
        {
            Items.Add(item);
            MaxRight = Math.Max(MaxRight, item.Bounds.Right + Pad);
        }

        public void Run()
        {
            const int band = 26;
            Add(new LayoutItem(LayoutKind.TitleBand, new LayoutRect(0, 0, width, band)));
            var dateLabel = "작성일 :";
            var labelW = measure(dateLabel) + 2;
            Add(new LayoutItem(LayoutKind.Label, new LayoutRect(Pad, 3, labelW, ControlHeight), Text: dateLabel));
            Add(new LayoutItem(LayoutKind.Date, new LayoutRect(Pad + labelW + Gap, 3, DateWidth, ControlHeight),
                form.WrittenOnKey, Control: new DateControl(form.WrittenOnKey)));
            var titleW = measure(form.Title) + 4;
            var deptW = measure(form.Department) + 4;
            var titleX = Math.Max(Pad + labelW + Gap + DateWidth + Gap, (width - titleW) / 2);
            Add(new LayoutItem(LayoutKind.Title, new LayoutRect(titleX, 3, titleW, ControlHeight), Text: form.Title));
            var deptX = Math.Max(titleX + titleW + Gap, width - Pad - deptW);
            Add(new LayoutItem(LayoutKind.Department, new LayoutRect(deptX, 3, deptW, ControlHeight), Text: form.Department));
            Y = band + Gap;
            Blocks(form.Blocks, Pad, width - Pad);
        }

        void Blocks(IEnumerable<FormBlock> blocks, int left, int right)
        {
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case SectionBlock s:
                        Y += 6;
                        Add(new LayoutItem(LayoutKind.Section, new LayoutRect(left, Y, right - left, 18), Text: s.Text));
                        Y += 18 + 2;
                        break;
                    case SubBlock s:
                        Add(new LayoutItem(LayoutKind.Sub, new LayoutRect(left + IndentStep, Y, right - left - IndentStep, 18), Text: s.Text));
                        Y += 18 + 2;
                        break;
                    case NoteBlock n:
                        Add(new LayoutItem(LayoutKind.Note, new LayoutRect(left, Y, right - left, 18), Text: n.Text));
                        Y += 18 + 2;
                        break;
                    case StaticBlock s:
                        Add(new LayoutItem(LayoutKind.Static, new LayoutRect(left, Y, right - left, 18), Text: s.Text));
                        Y += 18 + 2;
                        break;
                    case DrawBlock d:
                        Add(new LayoutItem(LayoutKind.DrawLabel, new LayoutRect(left, Y, right - left, 18), Text: d.Text));
                        Y += 18 + 2;
                        Add(new LayoutItem(LayoutKind.DrawArea, new LayoutRect(left, Y, right - left, 80),
                            Text: "[ 그림 작성 영역 - 데모에서는 비활성 ]"));
                        Y += 80 + Gap;
                        break;
                    case RowBlock row:
                        Row(row, left, right);
                        break;
                    case GridBlock grid:
                        Grid(grid, left, right);
                        break;
                    case SetBlock set:
                        Container(LayoutKind.SetBox, set.Text, set.Blocks, left, right);
                        break;
                    case SignBlock sign:
                        Container(LayoutKind.SignBox, null, sign.Blocks, left, right);
                        break;
                }
            }
        }

        void Container(LayoutKind kind, string? header, IReadOnlyList<FormBlock> blocks, int left, int right)
        {
            var top = Y;
            var box = Items.Count;
            Items.Add(null!);
            if (header is not null)
            {
                Add(new LayoutItem(LayoutKind.SetHeader, new LayoutRect(left + 1, Y + 1, right - left - 2, 18), Text: header));
                Y += 20;
            }

            Y += 4;
            Blocks(blocks, left + 8, right - 8);
            Y += 4;
            Items[box] = new LayoutItem(kind, new LayoutRect(left, top, right - left, Y - top));
            Y += Gap;
        }

        void Row(RowBlock row, int left, int right)
        {
            var indent = row.Indent * IndentStep;
            var labelWidth = form.LabelWidth;
            if (row.Label.Length > 0)
            {
                Add(new LayoutItem(LayoutKind.RowLabel,
                    new LayoutRect(left + indent, Y, Math.Max(20, labelWidth - indent - Gap), ControlHeight), Text: row.Label));
            }

            var flow = new Flow(this, left + labelWidth + 2, right, Y);
            foreach (var item in row.Items)
            {
                Control(item, flow);
            }

            Y = flow.Bottom + 2;
        }

        void Control(FormControl control, Flow flow)
        {
            switch (control)
            {
                case BreakControl:
                    flow.NewLine();
                    break;
                case OptionControl options when options.Stack:
                    StackedOptions(options, flow);
                    break;
                case OptionControl options:
                    for (var i = 0; i < options.AllOptions.Count; i++)
                    {
                        flow.Place(OptionItem(options, i));
                        foreach (var inline in InlineFor(options, i))
                        {
                            Control(inline, flow);
                        }
                    }

                    if (options.Tail is { } tail)
                    {
                        Control(tail, flow);
                    }

                    break;
                default:
                    flow.Place(ShrinkFill([Leaf(control)], flow.Available)[0]);
                    break;
            }
        }

        IEnumerable<FormControl> InlineFor(OptionControl options, int index)
        {
            if (index >= options.Options.Count)
            {
                return options.Other is { } other ? [new TextControl(other.Key, other.W, Fill: true)] : [];
            }

            return options.Inline is not null && options.Inline.TryGetValue(options.Options[index], out var items) ? items : [];
        }

        void StackedOptions(OptionControl options, Flow outer)
        {
            var lines = new List<List<LayoutItem>>();
            for (var i = 0; i < options.AllOptions.Count; i++)
            {
                var line = new List<LayoutItem> { OptionItem(options, i) };
                line.AddRange(InlineFor(options, i).Select(Leaf));
                lines.Add(line);
            }

            if (options.Tail is { } tail)
            {
                lines.Add([Leaf(tail)]);
            }

            for (var l = 0; l < lines.Count; l++)
            {
                lines[l] = ShrinkFill(lines[l], outer.Available);
            }

            var groupWidth = lines.Max(l => l.Sum(i => i.Bounds.W) + Gap * (l.Count - 1));
            var groupHeight = lines.Sum(l => l.Max(i => i.Bounds.H)) + 2 * (lines.Count - 1);
            var origin = outer.Reserve(groupWidth, groupHeight);
            var y = origin.Y;
            foreach (var line in lines)
            {
                var x = origin.X;
                foreach (var item in line)
                {
                    Add(item with { Bounds = item.Bounds with { X = x, Y = y } });
                    x += item.Bounds.W + Gap;
                }

                y += line.Max(i => i.Bounds.H) + 2;
            }
        }

        /// <summary>웹의 .fill처럼, 줄이 가용 폭을 넘으면 fill 입력칸을 줄인다(최소 40px).</summary>
        static List<LayoutItem> ShrinkFill(List<LayoutItem> line, int available)
        {
            var overflow = line.Sum(i => i.Bounds.W) + Gap * (line.Count - 1) - available;
            return line.Select(item =>
            {
                if (overflow <= 0 || item.Control is not TextControl { Fill: true })
                {
                    return item;
                }

                var shrink = Math.Min(overflow, item.Bounds.W - 40);
                overflow -= shrink;
                return item with { Bounds = item.Bounds with { W = item.Bounds.W - shrink } };
            }).ToList();
        }

        LayoutItem OptionItem(OptionControl options, int index)
        {
            var text = options.AllOptions[index];
            return new LayoutItem(LayoutKind.Option, new LayoutRect(0, 0, OptionBox + 4 + measure(text) + 6, ControlHeight),
                options.Key, text, index, options);
        }

        LayoutItem Leaf(FormControl control) => control switch
        {
            LabelControl l => new LayoutItem(LayoutKind.Label, new LayoutRect(0, 0, measure(l.Text) + 2, ControlHeight), Text: l.Text),
            ButtonControl b => new LayoutItem(LayoutKind.Button, new LayoutRect(0, 0, measure(b.Text) + 16, ControlHeight), Text: b.Text),
            TextControl t => new LayoutItem(LayoutKind.Text, new LayoutRect(0, 0, t.W, ControlHeight), t.Key, Control: t),
            DateControl d => new LayoutItem(LayoutKind.Date, new LayoutRect(0, 0, DateWidth, ControlHeight), d.Key, Control: d),
            TextAreaControl a => new LayoutItem(LayoutKind.TextArea, new LayoutRect(0, 0, a.W ?? 300, a.H), a.Key, Control: a),
            SelectControl s => new LayoutItem(LayoutKind.Select,
                new LayoutRect(0, 0, s.W ?? s.Options.Max(o => measure(o)) + 28, ControlHeight), s.Key, Control: s),
            _ => throw new InvalidOperationException($"배치할 수 없는 컨트롤: {control}"),
        };

        void Grid(GridBlock grid, int left, int right)
        {
            var widths = grid.Columns.Select(c => c.W ?? 80).ToArray();
            var tableWidth = GridRowNumberWidth + widths.Sum();
            var boxWidth = Math.Max(right - left, tableWidth + 2);
            Add(new LayoutItem(LayoutKind.GridTitle, new LayoutRect(left, Y, boxWidth, 22), Text: grid.Title));
            var bx = left + boxWidth - 2;
            foreach (var button in grid.Buttons.Reverse())
            {
                var w = measure(button) + 16;
                bx -= w;
                Add(new LayoutItem(LayoutKind.GridButton, new LayoutRect(bx, Y + 1, w, ControlHeight), Text: button));
                bx -= Gap;
            }

            Y += 22;
            var x = left + 1 + GridRowNumberWidth;
            for (var c = 0; c < widths.Length; c++)
            {
                Add(new LayoutItem(LayoutKind.GridHeader, new LayoutRect(x, Y, widths[c], GridRowHeight), Text: grid.Columns[c].Header));
                x += widths[c];
            }

            Y += GridRowHeight;
            for (var r = 0; r < grid.Rows; r++)
            {
                Add(new LayoutItem(LayoutKind.GridRowNumber, new LayoutRect(left + 1, Y, GridRowNumberWidth, GridRowHeight),
                    Text: (r + 1).ToString()));
                x = left + 1 + GridRowNumberWidth;
                for (var c = 0; c < widths.Length; c++)
                {
                    var key = GridBlock.CellKey(grid.Key, r, c);
                    Add(new LayoutItem(LayoutKind.GridCell, new LayoutRect(x, Y, widths[c], GridRowHeight), key,
                        Control: new TextControl(key, widths[c])));
                    x += widths[c];
                }

                Y += GridRowHeight;
            }

            Y += 6;
        }

        sealed class Flow(Builder owner, int left, int right, int top)
        {
            int _x = left;
            int _lineTop = top;
            int _lineHeight = ControlHeight;

            public int Bottom => _lineTop + _lineHeight;
            public int Available => right - left;

            public void NewLine()
            {
                _lineTop += _lineHeight + 2;
                _lineHeight = ControlHeight;
                _x = left;
            }

            public (int X, int Y) Reserve(int w, int h)
            {
                if (_x > left && _x + w > right)
                {
                    NewLine();
                }

                var origin = (_x, _lineTop);
                _x += w + Gap;
                _lineHeight = Math.Max(_lineHeight, h);
                return origin;
            }

            public void Place(LayoutItem item)
            {
                var (x, y) = Reserve(item.Bounds.W, item.Bounds.H);
                owner.Add(item with { Bounds = item.Bounds with { X = x, Y = y } });
            }
        }
    }
}
