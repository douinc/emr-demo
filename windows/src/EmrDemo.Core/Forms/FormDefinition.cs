using System.Text.Json.Serialization;

namespace EmrDemo.Core.Forms;

public sealed record FormDefinition(
    string Id,
    string Title,
    string Department,
    string Summary,
    int LabelWidth,
    string WrittenOnKey,
    IReadOnlyList<FormBlock> Blocks,
    IReadOnlyDictionary<string, string>? Patient = null)
{
    /// <summary>
    /// 머리글 환자 칸(<see cref="PatientControl"/>)에 들어갈 값. 등록번호·이름·진료과는 환자 기록에서, 성별·나이는
    /// 서식 정의의 예시 값(<see cref="Patient"/>)에서 온다.
    /// </summary>
    public IReadOnlyDictionary<string, string> PatientFields(string id, string name, string department)
    {
        var sex = Patient?.GetValueOrDefault("sex") ?? "";
        var age = Patient?.GetValueOrDefault("age") ?? "";
        return new Dictionary<string, string>
        {
            ["id"] = id,
            ["name"] = name,
            ["dept"] = department,
            ["sex"] = sex,
            ["age"] = age,
            ["sexAge"] = sex.Length + age.Length > 0 ? $"{sex} / {age}" : "",
        };
    }

    /// <summary>값을 받는 모든 입력(작성일, 행 안의 컨트롤과 파생 입력, 표 셀)을 배치 순서대로 낸다.</summary>
    public IEnumerable<FormInput> Inputs()
    {
        yield return new FormInput(WrittenOnKey, new DateControl(WrittenOnKey));
        foreach (var input in Inputs(Blocks))
        {
            yield return input;
        }
    }

    static IEnumerable<FormInput> Inputs(IEnumerable<FormBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case RowBlock row:
                    foreach (var input in row.Items.SelectMany(Inputs))
                    {
                        yield return input;
                    }

                    break;
                case GridBlock grid:
                    for (var r = 0; r < grid.Rows; r++)
                    {
                        for (var c = 0; c < grid.Columns.Count; c++)
                        {
                            var key = GridBlock.CellKey(grid.Key, r, c);
                            yield return new FormInput(key, new TextControl(key, grid.Columns[c].W ?? 80));
                        }
                    }

                    break;
                case SetBlock set:
                    foreach (var input in Inputs(set.Blocks))
                    {
                        yield return input;
                    }

                    break;
                case SignBlock sign:
                    foreach (var input in Inputs(sign.Blocks))
                    {
                        yield return input;
                    }

                    break;
                case LineBlock line:
                    foreach (var input in line.Items.SelectMany(Inputs))
                    {
                        yield return input;
                    }

                    break;
                case TableBlock table:
                    foreach (var input in table.Rows.SelectMany(r => r).SelectMany(c => c.Items ?? []).SelectMany(Inputs))
                    {
                        yield return input;
                    }

                    break;
            }
        }
    }

    static IEnumerable<FormInput> Inputs(FormControl control)
    {
        switch (control)
        {
            case OptionControl options:
                yield return new FormInput(options.Key, options);
                var inlineItems = options.Options
                    .SelectMany(o => options.Inline is not null && options.Inline.TryGetValue(o, out var items) ? items : []);
                foreach (var item in inlineItems)
                {
                    foreach (var input in Inputs(item))
                    {
                        yield return input;
                    }
                }

                if (options.Other is { } other)
                {
                    yield return new FormInput(other.Key, new TextControl(other.Key, other.W, Fill: true));
                }

                if (options.Tail is { } tail)
                {
                    foreach (var input in Inputs(tail))
                    {
                        yield return input;
                    }
                }

                break;
            case InputControl input:
                yield return new FormInput(input.Key, input);
                break;
            case ContainerControl container:
                foreach (var input in container.Items.SelectMany(Inputs))
                {
                    yield return input;
                }

                break;
        }
    }
}

public sealed record FormInput(string Key, FormControl Control);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SectionBlock), "sec")]
[JsonDerivedType(typeof(SubBlock), "sub")]
[JsonDerivedType(typeof(NoteBlock), "note")]
[JsonDerivedType(typeof(StaticBlock), "static")]
[JsonDerivedType(typeof(DrawBlock), "draw")]
[JsonDerivedType(typeof(RowBlock), "row")]
[JsonDerivedType(typeof(GridBlock), "grid")]
[JsonDerivedType(typeof(SetBlock), "set")]
[JsonDerivedType(typeof(SignBlock), "sign")]
[JsonDerivedType(typeof(HeadingBlock), "heading")]
[JsonDerivedType(typeof(LineBlock), "line")]
[JsonDerivedType(typeof(GapBlock), "gap")]
[JsonDerivedType(typeof(TableBlock), "table")]
public abstract record FormBlock;

public sealed record SectionBlock(string Text) : FormBlock;

public sealed record SubBlock(string Text) : FormBlock;

public sealed record NoteBlock(string Text) : FormBlock;

public sealed record StaticBlock(string Text) : FormBlock;

public sealed record DrawBlock(string Text) : FormBlock;

public sealed record RowBlock(string Label, int Indent, IReadOnlyList<FormControl> Items) : FormBlock;

public sealed record GridColumnDef(string Header, int? W);

public sealed record GridBlock(
    string Key, string Title, IReadOnlyList<string> Buttons, IReadOnlyList<GridColumnDef> Columns, int Rows, bool Peach)
    : FormBlock
{
    public static string CellKey(string gridKey, int row, int column) => $"{gridKey}[{row}][{column}]";
}

public sealed record SetBlock(string Text, IReadOnlyList<FormBlock> Blocks) : FormBlock;

public sealed record SignBlock(IReadOnlyList<FormBlock> Blocks) : FormBlock;

/// <summary>종이 서식의 큰 제목.</summary>
public sealed record HeadingBlock(string Text) : FormBlock;

/// <summary>라벨 열 없이 서식 폭 전체에 흘려 놓는 한 줄.</summary>
public sealed record LineBlock(IReadOnlyList<FormControl> Items) : FormBlock;

public sealed record GapBlock(int H) : FormBlock;

/// <summary>종이 서식의 표. 열 너비는 고정(px)이고 칸은 가로·세로로 병합할 수 있다.</summary>
public sealed record TableBlock(IReadOnlyList<int> Columns, IReadOnlyList<IReadOnlyList<TableCell>> Rows) : FormBlock;

/// <summary>표의 칸. Label이 있으면 라벨 칸, 없으면 Items를 흘려 놓는 입력 칸이다.</summary>
public sealed record TableCell(
    int ColSpan = 1,
    int RowSpan = 1,
    string? Label = null,
    IReadOnlyList<FormControl>? Items = null,
    int? H = null,
    IReadOnlyList<string>? Style = null)
{
    public bool Has(string style) => Style?.Contains(style) == true;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "c")]
[JsonDerivedType(typeof(LabelControl), "label")]
[JsonDerivedType(typeof(BreakControl), "break")]
[JsonDerivedType(typeof(ButtonControl), "button")]
[JsonDerivedType(typeof(TextControl), "text")]
[JsonDerivedType(typeof(DateControl), "date")]
[JsonDerivedType(typeof(TextAreaControl), "textarea")]
[JsonDerivedType(typeof(SelectControl), "select")]
[JsonDerivedType(typeof(RadioControl), "radio")]
[JsonDerivedType(typeof(CheckControl), "check")]
[JsonDerivedType(typeof(PatientControl), "patient")]
[JsonDerivedType(typeof(ImageControl), "image")]
[JsonDerivedType(typeof(LegendControl), "legend")]
[JsonDerivedType(typeof(ChartControl), "chart")]
[JsonDerivedType(typeof(LineControl), "line")]
[JsonDerivedType(typeof(VBoxControl), "vbox")]
[JsonDerivedType(typeof(HBoxControl), "hbox")]
public abstract record FormControl;

/// <summary>W가 있으면 그 폭을 차지한다(빈 글자로 간격을 둘 때도 쓴다).</summary>
public sealed record LabelControl(string Text, int? W = null) : FormControl;

/// <summary>머리글 환자 칸(읽기 전용 표시). Field는 <see cref="FormDefinition.PatientFields"/>의 키다.</summary>
public sealed record PatientControl(string Field) : FormControl;

/// <summary>그림(Mallampati 입 모양 "mallampati1"~"mallampati4", 병원 로고 "logo").</summary>
public sealed record ImageControl(string Name, int W, int H) : FormControl;

/// <summary>마취기록지 활력징후 그래프의 범례와 눈금. 값은 표시만 한다.</summary>
public sealed record LegendControl(int H, int Min, int Max) : FormControl;

/// <summary>마취기록지 활력징후 그래프 칸(5분 칸 Columns개). 데모에서는 빈 격자만 그린다.</summary>
public sealed record ChartControl(int Columns, int H, int Min, int Max) : FormControl;

public abstract record ContainerControl(IReadOnlyList<FormControl> Items) : FormControl;

/// <summary>가로 한 줄(넘치면 줄바꿈).</summary>
public sealed record LineControl(IReadOnlyList<FormControl> Items, bool Center = false) : ContainerControl(Items);

/// <summary>세로 묶음. W가 없으면 남은 폭을 쓴다.</summary>
public sealed record VBoxControl(IReadOnlyList<FormControl> Items, int? W = null, bool Center = false) : ContainerControl(Items);

/// <summary>가로 묶음. 폭이 없는 자식은 남은 폭을 나눠 갖고, Spread면 모든 자식이 같은 폭에서 가운데 놓인다.</summary>
public sealed record HBoxControl(IReadOnlyList<FormControl> Items, bool Spread = false) : ContainerControl(Items);

public sealed record BreakControl : FormControl;

public sealed record ButtonControl(string Text) : FormControl;

public abstract record InputControl(string Key) : FormControl;

/// <summary>Grow면 놓인 줄의 남은 폭을 채운다(종이 서식).</summary>
public sealed record TextControl(
    string Key, int W, bool Fill = false, bool Highlight = false, bool ReadOnly = false, bool Grow = false)
    : InputControl(Key);

public sealed record DateControl(string Key) : InputControl(Key);

public sealed record TextAreaControl(string Key, int H, int? W = null) : InputControl(Key);

public sealed record SelectControl(string Key, IReadOnlyList<string> Options, int? W = null) : InputControl(Key);

public sealed record OtherOption(string Key, int W, string Text);

public abstract record OptionControl(
    string Key,
    IReadOnlyList<string> Options,
    bool Stack,
    IReadOnlyDictionary<string, IReadOnlyList<FormControl>>? Inline,
    OtherOption? Other,
    FormControl? Tail) : InputControl(Key)
{
    /// <summary>other 선택지("Other" 등)까지 포함한 표시 순서의 선택지.</summary>
    public IReadOnlyList<string> AllOptions => Other is null ? Options : [.. Options, Other.Text];
}

public sealed record RadioControl(
    string Key,
    IReadOnlyList<string> Options,
    bool Stack = false,
    IReadOnlyDictionary<string, IReadOnlyList<FormControl>>? Inline = null,
    OtherOption? Other = null,
    FormControl? Tail = null) : OptionControl(Key, Options, Stack, Inline, Other, Tail);

public sealed record CheckControl(
    string Key,
    IReadOnlyList<string> Options,
    bool Stack = false,
    IReadOnlyDictionary<string, IReadOnlyList<FormControl>>? Inline = null,
    OtherOption? Other = null,
    FormControl? Tail = null) : OptionControl(Key, Options, Stack, Inline, Other, Tail);
