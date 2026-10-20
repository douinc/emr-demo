using EmrDemo.Core.Forms;

namespace EmrDemo.Core.Tests;

public class FormCatalogTests
{
    [Fact]
    public void Load_ReturnsObFormsThenSurgeryFormsInOrder()
    {
        var forms = FormCatalog.All;

        Assert.Equal(["labor2", "csec", "sono1st", "sono23", "fetalecho", "neurosono",
                      "osfirst", "preanes", "anesrec", "pacu"], forms.Select(f => f.Id));
        Assert.All(forms.Take(6), f => Assert.Equal("산부인과", f.Department));
        Assert.Equal(["정형외과", "마취통증의학과", "마취통증의학과", "마취통증의학과"], forms.Skip(6).Select(f => f.Department));
        Assert.Equal("Labor record Ⅱ-OB", FormCatalog.Get("labor2").Title);
        Assert.Equal("마취기록지", FormCatalog.Get("anesrec").Title);
    }

    [Fact]
    public void SurgeryForms_AreTablesWithInputsInsideCellsAndContainers()
    {
        var preanes = FormCatalog.Get("preanes");
        var table = preanes.Blocks.OfType<TableBlock>().Single();
        var keys = preanes.Inputs().Select(i => i.Key).ToList();

        Assert.Contains(table.Rows.SelectMany(r => r), c => c.RowSpan == 3 && c.Label == "마취관련 과거력");
        Assert.All(table.Rows, row => Assert.All(row, c => Assert.True(c.Label is null != (c.Items is null))));
        // Mallampati 1~4 는 그림 아래 체크박스(세로 묶음 안)
        var classes = preanes.Inputs().Select(i => i.Control).OfType<CheckControl>()
            .Where(c => c.Options is [var o] && o.StartsWith("Class ")).ToList();
        Assert.Equal(["Class 1", "Class 2", "Class 3", "Class 4"], classes.Select(c => c.Options[0]));
        Assert.Contains(keys, k => k.StartsWith("preanes.b2.r"));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void SurgeryForms_KeepNoExampleValues()
    {
        using var stream = typeof(FormCatalog).Assembly.GetManifestResourceStream("EmrDemo.Core.Seed.forms.json")!;
        var json = new StreamReader(stream).ReadToEnd();

        Assert.DoesNotContain("Medial meniscus tear", json);
        Assert.DoesNotContain("\"val\"", json);
    }

    [Fact]
    public void PatientFields_ComeFromTheRecordAndTheFormDefaults()
    {
        var fields = FormCatalog.Get("osfirst").PatientFields("DEMO-15", "예시 환자 15", "정형외과");

        Assert.Equal("DEMO-15", fields["id"]);
        Assert.Equal("예시 환자 15", fields["name"]);
        Assert.Equal("정형외과", fields["dept"]);
        Assert.Equal("M / 54", fields["sexAge"]);
        Assert.Equal("", FormCatalog.Get("labor2").PatientFields("DEMO-09", "x", "y")["sexAge"]);
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
