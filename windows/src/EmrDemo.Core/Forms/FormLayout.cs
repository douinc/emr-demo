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
    Heading,
    TableCell,
    PatientField,
    Image,
    Chart,
    Legend,
}

/// <summary>종이 서식 표 칸(<see cref="LayoutKind.TableCell"/>)의 모양.</summary>
[Flags]
public enum CellStyle
{
    None = 0,

    /// <summary>회색 바탕의 라벨 칸.</summary>
    Label = 1,
    Left = 2,
    Bold = 4,
    Big = 8,
    NoBorder = 16,

    /// <summary>상자 머리줄(흰 바탕, 굵게, 왼쪽 정렬).</summary>
    Head = 32,
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
    FormControl? Control = null,
    CellStyle Style = CellStyle.None)
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

    /// <summary>네이티브 Label·RadioButton·CheckBox가 글자 폭 외에 쓰는 여백. 두 모드가 같은 폭을 쓴다.</summary>
    public const int TextSlack = 8;

    /// <summary>행 라벨이 라벨 열보다 길 때 추가하는 줄의 높이(최대 3줄).</summary>
    public const int LabelLineHeight = 14;

    public const int HeadingHeight = 30;
    public const int CellPadX = 4;
    public const int CellPadY = 2;

    /// <summary>머리글 환자 칸의 폭. 값은 환자마다 다르므로 배치는 필드별 고정 폭을 쓴다.</summary>
    public static int PatientFieldWidth(string field) => field switch
    {
        "id" => 70,
        "name" => 84,
        "dept" => 104,
        "sex" or "age" => 34,
        "sexAge" => 60,
        _ => 80,
    };

    public static FormLayoutResult Build(FormDefinition form, Func<string, int> measure, int width)
    {
        var builder = new Builder(form, measure, Math.Max(width, 200));
        builder.Run();
        var band = builder.Items[0];
        builder.Items[0] = band with { Bounds = band.Bounds with { W = builder.MaxRight } };
        return new FormLayoutResult(builder.Items, builder.MaxRight, builder.Y + Pad);
    }

    sealed class Builder(FormDefinition form, Func<string, int> measure, int width)
    {
        public readonly List<LayoutItem> Items = [];
        public int Y;
        public int MaxRight;

        void Add(LayoutItem item)
        {
            Items.Add(item);
            MaxRight = Math.Max(MaxRight, item.Kind == LayoutKind.TitleBand ? item.Bounds.Right : item.Bounds.Right + Pad);
        }

        public void Run()
        {
            const int band = 26;
            MaxRight = width;
            Add(new LayoutItem(LayoutKind.TitleBand, new LayoutRect(0, 0, width, band)));
            var dateLabel = "작성일 :";
            var labelW = measure(dateLabel) + TextSlack;
            Add(new LayoutItem(LayoutKind.Label, new LayoutRect(Pad, 3, labelW, ControlHeight), Text: dateLabel));
            Add(new LayoutItem(LayoutKind.Date, new LayoutRect(Pad + labelW + Gap, 3, DateWidth, ControlHeight),
                form.WrittenOnKey, Control: new DateControl(form.WrittenOnKey)));
            var titleW = measure(form.Title) + TextSlack * 2;
            var deptW = measure(form.Department) + TextSlack * 2;
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
                    case HeadingBlock h:
                        Add(new LayoutItem(LayoutKind.Heading, new LayoutRect(left, Y, right - left, HeadingHeight), Text: h.Text));
                        Y += HeadingHeight + 4;
                        break;
                    case LineBlock line:
                        var placed = Content(line.Items, right - left);
                        Emit(placed, left, Y);
                        Y += placed.H + 4;
                        break;
                    case GapBlock gap:
                        Y += gap.H;
                        break;
                    case TableBlock table:
                        Table(table, left);
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
            var childRight = Items.Skip(box + 1).Where(i => i.Kind != LayoutKind.SetHeader)
                .Select(i => i.Bounds.Right + 8).DefaultIfEmpty(right).Max();
            var boxRight = Math.Max(right, childRight);
            Items[box] = new LayoutItem(kind, new LayoutRect(left, top, boxRight - left, Y - top));
            MaxRight = Math.Max(MaxRight, boxRight + Pad);
            Y += Gap;
        }

        void Row(RowBlock row, int left, int right)
        {
            var indent = row.Indent * IndentStep;
            var labelWidth = form.LabelWidth;
            var labelBottom = Y;
            if (row.Label.Length > 0)
            {
                var labelW = Math.Max(20, labelWidth - indent - Gap);
                var lines = Math.Clamp((measure(row.Label) + labelW - 1) / labelW, 1, 3);
                var labelH = ControlHeight + (lines - 1) * LabelLineHeight;
                Add(new LayoutItem(LayoutKind.RowLabel, new LayoutRect(left + indent, Y, labelW, labelH), Text: row.Label));
                labelBottom = Y + labelH;
            }

            var flow = new Flow(this, left + labelWidth + 2, right, Y);
            foreach (var item in row.Items)
            {
                Control(item, flow);
            }

            Y = Math.Max(flow.Bottom, labelBottom) + 2;
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
                        List<LayoutItem> unit = [OptionItem(options, i), .. InlineFor(options, i).Select(Leaf)];
                        flow.PlaceTogether(ShrinkFill(unit, flow.Available));
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

        /// <summary>
        /// 줄이 가용 폭을 넘으면 fill 입력칸을 최소 40px까지 줄인다. 웹 CSS의 .fill은 배경색만 바꾸며,
        /// 이 축소는 이 앱의 규칙이다.
        /// </summary>
        static List<LayoutItem> ShrinkFill(List<LayoutItem> line, int available)
        {
            var overflow = line.Sum(i => i.Bounds.W) + Gap * (line.Count - 1) - available;
            return line.Select(item =>
            {
                if (overflow <= 0 || item.Control is not TextControl { Fill: true })
                {
                    return item;
                }

                var shrink = Math.Max(0, Math.Min(overflow, item.Bounds.W - 40));
                overflow -= shrink;
                return item with { Bounds = item.Bounds with { W = item.Bounds.W - shrink } };
            }).ToList();
        }

        LayoutItem OptionItem(OptionControl options, int index)
        {
            var text = options.AllOptions[index];
            return new LayoutItem(LayoutKind.Option, new LayoutRect(0, 0, OptionBox + 6 + measure(text) + TextSlack * 2, ControlHeight),
                options.Key, text, index, options);
        }

        LayoutItem Leaf(FormControl control) => control switch
        {
            LabelControl l => new LayoutItem(LayoutKind.Label,
                new LayoutRect(0, 0, l.W ?? measure(l.Text) + TextSlack, ControlHeight), Text: l.Text),
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
            var widths = GridColumnWidths(grid, right - left);
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

        /// <summary>너비가 없는 열은 웹의 width:100% 표처럼 남은 폭을 나눠 갖는다(최소 80px).</summary>
        static int[] GridColumnWidths(GridBlock grid, int boxWidth)
        {
            var fixedSum = grid.Columns.Sum(c => c.W ?? 0);
            var flexible = grid.Columns.Count(c => c.W is null);
            if (flexible == 0)
            {
                return grid.Columns.Select(c => c.W!.Value).ToArray();
            }

            var remaining = boxWidth - 2 - GridRowNumberWidth - fixedSum;
            var share = Math.Max(80, remaining / flexible);
            var widths = grid.Columns.Select(c => c.W ?? share).ToArray();
            var lastFlexible = Array.FindLastIndex(grid.Columns.ToArray(), c => c.W is null);
            if (share * flexible < remaining)
            {
                widths[lastFlexible] += remaining - share * flexible;
            }

            return widths;
        }

        /* ---------------- 종이 서식 : 표와 칸 내용 ---------------- */

        /// <summary>칸 내용의 배치 결과. 좌표는 내용 원점(0,0) 기준이다.</summary>
        sealed record Placed(List<LayoutItem> Items, int W, int H);

        static Placed Single(LayoutItem item) => new([item], item.Bounds.W, item.Bounds.H);

        void Emit(Placed placed, int x, int y)
        {
            foreach (var item in placed.Items)
            {
                Add(item with { Bounds = item.Bounds.Offset(x, y) });
            }
        }

        void Table(TableBlock table, int left)
        {
            var colX = new int[table.Columns.Count + 1];
            for (var c = 0; c < table.Columns.Count; c++)
            {
                colX[c + 1] = colX[c] + table.Columns[c];
            }

            // 병합 칸이 차지한 자리를 건너뛰며 칸마다 시작 열을 정한다
            var occupied = new HashSet<(int, int)>();
            var cells = new List<(TableCell Cell, int R, int C)>();
            for (var r = 0; r < table.Rows.Count; r++)
            {
                var c = 0;
                foreach (var cell in table.Rows[r])
                {
                    while (occupied.Contains((r, c)))
                    {
                        c++;
                    }

                    if (c + cell.ColSpan > table.Columns.Count)
                    {
                        throw new InvalidDataException($"표 칸이 열 수를 넘습니다: {r}행 {c}열");
                    }

                    cells.Add((cell, r, c));
                    for (var rr = r; rr < r + cell.RowSpan; rr++)
                    {
                        for (var cc = c; cc < c + cell.ColSpan; cc++)
                        {
                            occupied.Add((rr, cc));
                        }
                    }

                    c += cell.ColSpan;
                }
            }

            var rowCount = Math.Max(table.Rows.Count, cells.Max(x => x.R + x.Cell.RowSpan));
            var rowH = new int[rowCount];
            var contents = new Placed?[cells.Count];
            var needs = new int[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                var (cell, r, c) = cells[i];
                var inner = colX[c + cell.ColSpan] - colX[c] - 2 * PadOf(cell);
                int need;
                if (cell.Label is { } label)
                {
                    need = cell.Has("big") ? HeadingHeight : LabelHeight(label, inner) + 2 * CellPadY;
                }
                else
                {
                    var placed = Content(cell.Items ?? [], inner, cell.Has("spread"), cell.Has("grid5"));
                    contents[i] = placed;
                    need = placed.H + 2 * CellPadY;
                }

                var min = cell.H ?? (cell.Has("compact") || cell.Has("timeGrid") ? ControlHeight + 2 : ControlHeight + 4);
                needs[i] = Math.Max(need, min);
                if (cell.RowSpan == 1)
                {
                    rowH[r] = Math.Max(rowH[r], needs[i]);
                }
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var (cell, r, _) = cells[i];
                var spanned = Enumerable.Range(r, cell.RowSpan).Sum(x => rowH[x]);
                if (spanned < needs[i])
                {
                    rowH[r + cell.RowSpan - 1] += needs[i] - spanned;
                }
            }

            var rowY = new int[rowCount + 1];
            for (var r = 0; r < rowCount; r++)
            {
                rowY[r + 1] = rowY[r] + rowH[r];
            }

            var top = Y;
            for (var i = 0; i < cells.Count; i++)
            {
                var (cell, r, c) = cells[i];
                var rect = new LayoutRect(left + colX[c], top + rowY[r], colX[c + cell.ColSpan] - colX[c],
                    rowY[r + cell.RowSpan] - rowY[r]);
                Add(new LayoutItem(LayoutKind.TableCell, rect, Text: cell.Label, Style: StyleOf(cell)));
                if (contents[i] is not { } placed)
                {
                    continue;
                }

                if (placed.Items is [{ Kind: LayoutKind.TextArea } area] && cell.Has("fill"))
                {
                    // 칸을 가득 채우는 입력(마취기록지 REMARK 등)
                    Add(area with { Bounds = new LayoutRect(rect.X + 2, rect.Y + 2, rect.W - 4, rect.H - 4) });
                }
                else if (placed.Items is [{ Kind: LayoutKind.Text } time] && cell.Has("timeGrid"))
                {
                    Add(time with { Bounds = new LayoutRect(rect.X + 1, rect.Y + 1, rect.W - 2, rect.H - 2) });
                }
                else
                {
                    var pad = PadOf(cell);
                    var dx = pad + (cell.Has("center") ? Math.Max(0, (rect.W - 2 * pad - placed.W) / 2) : 0);
                    var dy = cell.Has("top") ? CellPadY : Math.Max(CellPadY, (rect.H - placed.H) / 2);
                    Emit(placed, rect.X + dx, rect.Y + dy);
                }
            }

            Y = top + rowY[rowCount] + 6;
        }

        static int PadOf(TableCell cell) => cell.Has("chart") || cell.Has("timeGrid") ? 1 : CellPadX;

        static CellStyle StyleOf(TableCell cell)
        {
            var style = CellStyle.None;
            if (cell.Label is not null && !cell.Has("plain") && !cell.Has("head"))
            {
                style |= CellStyle.Label;
            }

            if (cell.Has("left")) style |= CellStyle.Left;
            if (cell.Has("bold")) style |= CellStyle.Bold;
            if (cell.Has("big")) style |= CellStyle.Big;
            if (cell.Has("noBorder")) style |= CellStyle.NoBorder;
            if (cell.Has("head")) style |= CellStyle.Head | CellStyle.Bold | CellStyle.Left;
            return style;
        }

        /// <summary>라벨 칸 높이. 줄바꿈 문자와 칸 폭을 넘는 글자에서 줄을 나눈다.</summary>
        int LabelHeight(string label, int width)
        {
            var lines = label.Split('\n').Sum(part => Math.Max(1, (measure(part) + width - 1) / Math.Max(1, width)));
            return Math.Max(ControlHeight - 2 * CellPadY, lines * LabelLineHeight + 2);
        }

        Placed Content(IReadOnlyList<FormControl> controls, int width, bool spread = false, bool grid5 = false)
        {
            var flow = new ContentFlow(Math.Max(width, 20));
            for (var i = 0; i < controls.Count; i++)
            {
                ContentControl(controls[i], flow, spread, grid5, TrailingWidth(controls, i + 1));
            }

            return flow.Result();
        }

        /// <summary>
        /// 남은 폭을 채우는 입력 뒤에 붙는 라벨·고정 폭 입력의 폭(단위 "㎖" 등). 그만큼 남겨 둬야 같은 줄에 붙는다.
        /// </summary>
        int TrailingWidth(IReadOnlyList<FormControl> controls, int from)
        {
            var width = 0;
            for (var i = from; i < controls.Count; i++)
            {
                switch (controls[i])
                {
                    case LabelControl or TextControl { Grow: false } or PatientControl:
                        width += Leaf(controls[i] is PatientControl p
                            ? new LabelControl("", PatientFieldWidth(p.Field)) : controls[i]).Bounds.W + Gap;
                        break;
                    default:
                        return width;
                }
            }

            return width;
        }

        void ContentControl(FormControl control, ContentFlow flow, bool spread, bool grid5, int trailing = 0)
        {
            switch (control)
            {
                case BreakControl:
                    flow.NewLine();
                    break;
                case LineControl line:
                    var placed = Content(line.Items, flow.Width);
                    flow.PutBlock(placed, line.Center ? Math.Max(0, (flow.Width - placed.W) / 2) : 0);
                    break;
                case VBoxControl box:
                    flow.Put(VBox(box, box.W ?? (flow.Remaining >= 60 ? flow.Remaining : flow.Width)));
                    break;
                case HBoxControl box:
                    flow.PutBlock(HBox(box, flow.Width), 0);
                    break;
                case OptionControl options when (spread || grid5) && !options.Stack
                                                && SpreadOptions(options, flow.Width, grid5) is { } spreadOut:
                    flow.PutBlock(spreadOut, 0);
                    if (options.Tail is { } spreadTail)
                    {
                        ContentControl(spreadTail, flow, false, false);
                    }

                    break;
                case OptionControl options:
                    for (var i = 0; i < options.AllOptions.Count; i++)
                    {
                        if (options.Stack)
                        {
                            flow.NewLine();
                        }

                        flow.Put(Inline([OptionItem(options, i), .. InlineFor(options, i).Select(Leaf)]));
                    }

                    if (options.Tail is { } tail)
                    {
                        ContentControl(tail, flow, false, false);
                    }

                    break;
                case TextControl { Grow: true } text:
                    var w = flow.Remaining - trailing;
                    if (w < 40 && !flow.AtLineStart)
                    {
                        flow.NewLine();
                        w = flow.Width - trailing;
                    }

                    flow.Put(Single(new LayoutItem(LayoutKind.Text, new LayoutRect(0, 0, Math.Max(24, w), ControlHeight),
                        text.Key, Control: text)));
                    break;
                case TextAreaControl { W: null } area:
                    flow.PutBlock(Single(new LayoutItem(LayoutKind.TextArea, new LayoutRect(0, 0, flow.Width, area.H),
                        area.Key, Control: area)), 0);
                    break;
                case PatientControl patient:
                    flow.Put(Single(new LayoutItem(LayoutKind.PatientField,
                        new LayoutRect(0, 0, PatientFieldWidth(patient.Field), ControlHeight), Text: patient.Field, Control: patient)));
                    break;
                case ImageControl image:
                    flow.Put(Single(new LayoutItem(LayoutKind.Image, new LayoutRect(0, 0, image.W, image.H),
                        Text: image.Name, Control: image)));
                    break;
                case LegendControl legend:
                    flow.PutBlock(Single(new LayoutItem(LayoutKind.Legend, new LayoutRect(0, 0, flow.Width, legend.H),
                        Control: legend)), 0);
                    break;
                case ChartControl chart:
                    flow.PutBlock(Single(new LayoutItem(LayoutKind.Chart, new LayoutRect(0, 0, flow.Width, chart.H),
                        Control: chart)), 0);
                    break;
                case LabelControl { W: { } fixedW } label:
                    flow.Put(Single(new LayoutItem(LayoutKind.Label, new LayoutRect(0, 0, fixedW, ControlHeight), Text: label.Text)));
                    break;
                default:
                    flow.Put(Single(Leaf(control)));
                    break;
            }
        }

        /// <summary>선택지와 인라인 입력처럼 떨어지면 안 되는 요소를 가로로 붙인 덩어리.</summary>
        static Placed Inline(IReadOnlyList<LayoutItem> items)
        {
            var list = new List<LayoutItem>();
            var x = 0;
            foreach (var item in items)
            {
                list.Add(item with { Bounds = item.Bounds with { X = x, Y = 0 } });
                x += item.Bounds.W + Gap;
            }

            return new Placed(list, x - Gap, items.Max(i => i.Bounds.H));
        }

        /// <summary>
        /// 선택지를 같은 폭의 칸에 나눠 놓는다(웹 .spread: 한 줄에 모두, .k5: 한 줄에 5개). 선택지가 칸보다
        /// 넓으면 null을 돌려 보통 흐름 배치로 놓는다.
        /// </summary>
        Placed? SpreadOptions(OptionControl options, int width, bool grid5)
        {
            var count = options.AllOptions.Count;
            var perRow = grid5 ? 5 : count;
            var slot = width / Math.Min(count, perRow);
            var units = Enumerable.Range(0, count)
                .Select(i => Inline([OptionItem(options, i), .. InlineFor(options, i).Select(Leaf)])).ToList();
            if (units.Any(u => u.W > slot - Gap))
            {
                return null;
            }

            var items = new List<LayoutItem>();
            for (var i = 0; i < count; i++)
            {
                var dx = i % perRow * slot;
                var dy = i / perRow * (ControlHeight + 2);
                items.AddRange(units[i].Items.Select(item => item with { Bounds = item.Bounds.Offset(dx, dy) }));
            }

            var rows = (count + perRow - 1) / perRow;
            return new Placed(items, width, rows * ControlHeight + (rows - 1) * 2);
        }

        Placed VBox(VBoxControl box, int width)
        {
            var items = new List<LayoutItem>();
            var y = 0;
            var maxW = 0;
            foreach (var child in box.Items)
            {
                var placed = child is LineControl line ? Content(line.Items, width) : Content([child], width);
                var center = box.Center || child is LineControl { Center: true };
                var x = center ? Math.Max(0, (width - placed.W) / 2) : 0;
                items.AddRange(placed.Items.Select(i => i with { Bounds = i.Bounds.Offset(x, y) }));
                y += placed.H + 1;
                maxW = Math.Max(maxW, x + placed.W);
            }

            return new Placed(items, box.W ?? (box.Center ? width : maxW), Math.Max(0, y - 1));
        }

        Placed HBox(HBoxControl box, int width)
        {
            const int gap = 10;
            var count = box.Items.Count;
            var fixedW = box.Spread ? 0 : box.Items.Sum(c => c is VBoxControl { W: { } w } ? w : 0);
            var flexible = box.Spread ? count : box.Items.Count(c => c is not VBoxControl { W: not null });
            var share = flexible == 0 ? 0 : Math.Max(40, (width - fixedW - gap * (count - 1)) / flexible);
            var items = new List<LayoutItem>();
            var x = 0;
            var height = 0;
            foreach (var child in box.Items)
            {
                var childW = !box.Spread && child is VBoxControl { W: { } w } ? w : share;
                var placed = child is VBoxControl vbox ? VBox(vbox, childW) : Content([child], childW);
                var dx = box.Spread ? Math.Max(0, (childW - placed.W) / 2) : 0;
                items.AddRange(placed.Items.Select(i => i with { Bounds = i.Bounds.Offset(x + dx, 0) }));
                x += childW + gap;
                height = Math.Max(height, placed.H);
            }

            return new Placed(items, Math.Max(0, x - gap), height);
        }

        /// <summary>칸 안의 흐름 배치. 넘치면 줄을 바꾸고, 블록(줄·가로 묶음·그래프)은 줄 하나를 통째로 쓴다.</summary>
        sealed class ContentFlow(int width)
        {
            readonly List<LayoutItem> _items = [];
            int _x;
            int _top;
            int _lineHeight;
            int _maxW;

            public int Width => width;
            public int Remaining => width - _x;
            public bool AtLineStart => _x == 0;

            public void NewLine()
            {
                if (_x > 0 || _lineHeight > 0)
                {
                    _top += _lineHeight + 2;
                }

                _x = 0;
                _lineHeight = 0;
            }

            public void Put(Placed unit)
            {
                if (_x > 0 && _x + unit.W > width)
                {
                    NewLine();
                }

                Append(unit, _x, _top);
                _x += unit.W + Gap;
                _lineHeight = Math.Max(_lineHeight, unit.H);
            }

            public void PutBlock(Placed unit, int x)
            {
                NewLine();
                Append(unit, x, _top);
                _top += unit.H + 2;
            }

            void Append(Placed unit, int x, int y)
            {
                _items.AddRange(unit.Items.Select(i => i with { Bounds = i.Bounds.Offset(x, y) }));
                _maxW = Math.Max(_maxW, x + unit.W);
            }

            public Placed Result() =>
                new(_items, _maxW, _items.Count == 0 ? 0 : _items.Max(i => i.Bounds.Bottom));
        }

        sealed class Flow
        {
            readonly Builder owner;
            readonly int left;
            readonly int right;
            int _x;
            int _lineTop;
            int _lineHeight = ControlHeight;

            public Flow(Builder owner, int left, int right, int top)
            {
                this.owner = owner;
                this.left = left;
                this.right = right;
                _x = left;
                _lineTop = top;
            }

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

            public void Place(LayoutItem item) => PlaceTogether([item]);

            /// <summary>선택지와 그 인라인 입력처럼 줄바꿈으로 떨어지면 안 되는 요소를 한 덩어리로 놓는다.</summary>
            public void PlaceTogether(IReadOnlyList<LayoutItem> items)
            {
                var width = items.Sum(i => i.Bounds.W) + Gap * (items.Count - 1);
                var (x, y) = Reserve(width, items.Max(i => i.Bounds.H));
                foreach (var item in items)
                {
                    owner.Add(item with { Bounds = item.Bounds with { X = x, Y = y } });
                    x += item.Bounds.W + Gap;
                }
            }
        }
    }
}
