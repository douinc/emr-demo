using EmrDemo.Core.Forms;

namespace EmrDemo.Core.Tests;

public class FormCatalogTests
{
    [Fact]
    public void Load_ReturnsSixObFormsInOrder()
    {
        var forms = FormCatalog.All;

        Assert.Equal(["labor2", "csec", "sono1st", "sono23", "fetalecho", "neurosono"], forms.Select(f => f.Id));
        Assert.All(forms, f => Assert.Equal("산부인과", f.Department));
        Assert.Equal("Labor record Ⅱ-OB", FormCatalog.Get("labor2").Title);
    }

    [Fact]
    public void InputKeys_AreUniqueAcrossAllForms()
    {
        var keys = FormCatalog.All.SelectMany(f => f.Inputs()).Select(i => i.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains("labor2.writtenOn", keys);
    }

    [Fact]
    public void Parses_EveryControlKind()
    {
        var controls = FormCatalog.All.SelectMany(f => f.Inputs()).Select(i => i.Control).ToList();

        Assert.Contains(controls, c => c is TextControl { ReadOnly: true });
        Assert.Contains(controls, c => c is TextAreaControl);
        Assert.Contains(controls, c => c is DateControl);
        Assert.Contains(controls, c => c is SelectControl { Options.Count: > 1 });
        Assert.Contains(controls, c => c is RadioControl { Stack: true, Other: not null });
        Assert.Contains(controls, c => c is CheckControl);
        Assert.Contains(controls, c => c is RadioControl { Inline.Count: > 0 });
        Assert.Contains(controls, c => c is RadioControl { Tail: TextControl });
    }

    [Fact]
    public void Grid_ExposesOneInputPerCell()
    {
        var form = FormCatalog.Get("labor2");
        var grid = form.Blocks.OfType<GridBlock>().First();

        Assert.Equal(9, grid.Columns.Count);
        Assert.Equal(7, grid.Rows);
        var cells = form.Inputs().Where(i => i.Key.StartsWith(grid.Key + "[")).ToList();
        Assert.Equal(63, cells.Count);
        Assert.Contains(cells, c => c.Key == grid.Key + "[6][8]");
    }

    [Fact]
    public void Inputs_IncludeNestedSetAndSignRowsAndDerivedFields()
    {
        var keys = FormCatalog.All.SelectMany(f => f.Inputs()).Select(i => i.Key).ToHashSet();
        var rowsInContainers = FormCatalog.All
            .SelectMany(f => f.Blocks)
            .SelectMany(b => b switch { SetBlock s => s.Blocks, SignBlock s => s.Blocks, _ => [] })
            .OfType<RowBlock>()
            .SelectMany(r => r.Items.OfType<TextControl>())
            .ToList();

        Assert.NotEmpty(rowsInContainers);
        Assert.All(rowsInContainers, t => Assert.Contains(t.Key, keys));
        Assert.Contains(keys, k => k.EndsWith(".other"));
        Assert.Contains(keys, k => k.EndsWith(".tail"));
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => FormCatalog.Get("nope"));
    }
}
