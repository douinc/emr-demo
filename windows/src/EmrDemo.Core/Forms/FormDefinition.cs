using System.Text.Json.Serialization;

namespace EmrDemo.Core.Forms;

public sealed record FormDefinition(
    string Id,
    string Title,
    string Department,
    string Summary,
    int LabelWidth,
    string WrittenOnKey,
    IReadOnlyList<FormBlock> Blocks)
{
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
                    yield return new FormInput(other.Key, new TextControl(other.Key, other.W));
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
public abstract record FormControl;

public sealed record LabelControl(string Text) : FormControl;

public sealed record BreakControl : FormControl;

public sealed record ButtonControl(string Text) : FormControl;

public abstract record InputControl(string Key) : FormControl;

public sealed record TextControl(string Key, int W, bool Fill = false, bool Highlight = false, bool ReadOnly = false)
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
