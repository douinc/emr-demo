using EmrDemo.Core.Forms;

namespace EmrDemo.Core.Tests;

public class FormLayoutTests
{
    static int Measure(string text) => text.Length * 7;

    static FormLayoutResult Build(string formId, int width = 860) =>
        FormLayout.Build(FormCatalog.Get(formId), Measure, width);

    public static TheoryData<string> FormIds => new(FormCatalog.All.Select(f => f.Id));

    static bool IsLeaf(LayoutItem item) =>
        item.Kind is not (LayoutKind.TitleBand or LayoutKind.SetBox or LayoutKind.SignBox or LayoutKind.GridTitle
            or LayoutKind.TableCell);

    public static TheoryData<string, int> FormIdsAndWidths => new(
        FormCatalog.All.SelectMany(f => new[] { 860, 420, 200 }.Select(w => (f.Id, w))));

    [Theory]
    [MemberData(nameof(FormIdsAndWidths))]
    public void LeafItems_DoNotOverlap(string formId, int width)
    {
        var leaves = Build(formId, width).Items.Where(IsLeaf).ToList();

        for (var i = 0; i < leaves.Count; i++)
        {
            for (var j = i + 1; j < leaves.Count; j++)
            {
                Assert.False(leaves[i].Bounds.Intersects(leaves[j].Bounds),
                    $"{leaves[i].Kind} {leaves[i].Key}{leaves[i].Text} overlaps {leaves[j].Kind} {leaves[j].Key}{leaves[j].Text}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(FormIdsAndWidths))]
    public void EveryInput_IsPlacedOnce_AndEveryOptionOnce(string formId, int width)
    {
        var form = FormCatalog.Get(formId);
        var items = Build(formId, width).Items;

        foreach (var input in form.Inputs())
        {
            if (input.Control is OptionControl options)
            {
                var placed = items.Where(i => i.Kind == LayoutKind.Option && i.Key == input.Key)
                    .Select(i => i.OptionIndex).ToList();
                Assert.Equal(Enumerable.Range(0, options.AllOptions.Count), placed.Order());
            }
            else
            {
                Assert.Single(items, i => i.Key == input.Key && i.Kind != LayoutKind.Option);
            }
        }
    }

    [Theory]
    [MemberData(nameof(FormIds))]
    public void AtDefaultWidth_NothingOverflowsTheRequestedWidth(string formId)
    {
        var result = Build(formId);

        Assert.All(result.Items.Where(i => i.Kind != LayoutKind.TitleBand), i => Assert.True(i.Bounds.Right <= 860 - FormLayout.Pad && i.Bounds.Bottom <= result.Height,
            $"{i.Kind} {i.Key}{i.Text} at {i.Bounds} past the right padding"));
        Assert.Equal(860, result.Width);
    }

    [Theory]
    [MemberData(nameof(FormIdsAndWidths))]
    public void TitleBand_SpansContentAndContainsTitleItems(string formId, int width)
    {
        var result = Build(formId, width);
        var band = result.Items.Single(i => i.Kind == LayoutKind.TitleBand);

        Assert.Equal(result.Width, band.Bounds.W);
        Assert.All(result.Items.Where(i => i.Kind is LayoutKind.Title or LayoutKind.Department || i.Key == FormCatalog.Get(formId).WrittenOnKey),
            i => Assert.True(i.Bounds.Right <= band.Bounds.Right && i.Bounds.Bottom <= band.Bounds.Bottom, $"{i.Kind} outside band"));
    }

    [Theory]
    [MemberData(nameof(FormIdsAndWidths))]
    public void Containers_EncloseTheirChildren(string formId, int width)
    {
        var items = Build(formId, width).Items.ToList();
        var boxes = items.Where(i => i.Kind is LayoutKind.SetBox or LayoutKind.SignBox).ToList();

        foreach (var box in boxes)
        {
            var inside = items.Skip(items.IndexOf(box) + 1)
                .TakeWhile(i => i.Bounds.Y < box.Bounds.Bottom && i.Kind is not (LayoutKind.SetBox or LayoutKind.SignBox));
            Assert.All(inside, i => Assert.True(i.Bounds.X >= box.Bounds.X && i.Bounds.Right <= box.Bounds.Right
                && i.Bounds.Y >= box.Bounds.Y && i.Bounds.Bottom <= box.Bounds.Bottom, $"{i.Kind} {i.Key}{i.Text} outside {box.Kind}"));
        }
    }

    [Fact]
    public void NarrowWidth_WrapsRowsAndShrinksFillInputs()
    {
        var wide = Build("sono1st", 860);
        var narrow = Build("sono1st", 420);

        Assert.True(narrow.Height > wide.Height);
        static int OptionRows(FormLayoutResult r) => r.Items.Where(i => i.Kind == LayoutKind.Option).Select(i => i.Bounds.Y).Distinct().Count();
        Assert.True(OptionRows(narrow) > OptionRows(wide));
        var fills = narrow.Items.Where(i => i.Control is TextControl { Fill: true }).ToList();
        Assert.Contains(fills, i => i.Bounds.W < ((TextControl)i.Control!).W);
        Assert.All(fills, i => Assert.True(i.Bounds.W >= Math.Min(40, ((TextControl)i.Control!).W)));
    }

    [Fact]
    public void OtherOption_StaysOnTheSameLineAsItsText()
    {
        foreach (var width in new[] { 860, 420 })
        {
            var items = Build("sono1st", width).Items;
            foreach (var other in items.Where(i => i.Key?.EndsWith(".other") == true))
            {
                var key = other.Key![..^".other".Length];
                var option = items.Single(i => i.Kind == LayoutKind.Option && i.Key == key
                    && i.OptionIndex == ((OptionControl)i.Control!).Options.Count);
                Assert.Equal(option.Bounds.Y, other.Bounds.Y);
                Assert.True(other.Bounds.X > option.Bounds.X);
            }
        }
    }

    [Fact]
    public void LongRowLabels_WrapInsteadOfClipping()
    {
        foreach (var form in FormCatalog.All)
        {
            var labels = Build(form.Id).Items.Where(i => i.Kind == LayoutKind.RowLabel).ToList();
            Assert.All(labels.Where(l => Measure(l.Text!) > l.Bounds.W), l => Assert.True(l.Bounds.H > FormLayout.ControlHeight,
                $"{form.Id} '{l.Text}' is {Measure(l.Text!)}px in a {l.Bounds.W}px single line"));
        }
    }

    [Fact]
    public void GridColumnsWithoutWidth_ShareTheRemainingWidth()
    {
        var form = FormCatalog.Get("csec");
        var grid = form.Blocks.OfType<GridBlock>().First(g => g.Columns.Any(c => c.W is null));
        var items = Build("csec").Items;

        var title = items.First(i => i.Kind == LayoutKind.GridTitle && i.Bounds.Y < items.Single(c => c.Key == GridBlock.CellKey(grid.Key, 0, 0)).Bounds.Y
            && items.Single(c => c.Key == GridBlock.CellKey(grid.Key, 0, 0)).Bounds.Y - i.Bounds.Y <= 22 + FormLayout.GridRowHeight);
        var lastCell = items.Single(i => i.Key == GridBlock.CellKey(grid.Key, 0, grid.Columns.Count - 1));
        Assert.Equal(title.Bounds.Right - 1, lastCell.Bounds.Right);
    }

    [Fact]
    public void WideGrid_GrowsTheBoxAndKeepsButtonsInside()
    {
        var result = Build("labor2", 420);
        var title = result.Items.First(i => i.Kind == LayoutKind.GridTitle);
        var buttons = result.Items.Where(i => i.Kind == LayoutKind.GridButton && i.Bounds.Y == title.Bounds.Y + 1).ToList();

        Assert.True(result.Width > 420);
        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.Bounds.X >= title.Bounds.X && b.Bounds.Right <= title.Bounds.Right));
        Assert.Equal(title.Bounds.Right - 2, buttons.Max(b => b.Bounds.Right));
    }

    [Fact]
    public void Grid_CellsFollowColumnWidthsAndRows()
    {
        var form = FormCatalog.Get("labor2");
        var grid = form.Blocks.OfType<GridBlock>().First();
        var items = Build("labor2").Items;

        var first = items.Single(i => i.Key == GridBlock.CellKey(grid.Key, 0, 0));
        var below = items.Single(i => i.Key == GridBlock.CellKey(grid.Key, 1, 0));
        var right = items.Single(i => i.Key == GridBlock.CellKey(grid.Key, 0, 1));
        Assert.Equal(grid.Columns[0].W, first.Bounds.W);
        Assert.Equal(first.Bounds.Bottom, below.Bounds.Y);
        Assert.Equal(first.Bounds.Right, right.Bounds.X);
        Assert.Equal(grid.Columns.Count, items.Count(i => i.Kind == LayoutKind.GridHeader && i.Bounds.Y == first.Bounds.Y - first.Bounds.H));
    }

    [Fact]
    public void StackedOptions_AreVertical()
    {
        var form = FormCatalog.Get("sono1st");
        var stacked = form.Inputs().Select(i => i.Control).OfType<RadioControl>().First(r => r.Stack);
        var options = Build("sono1st").Items.Where(i => i.Kind == LayoutKind.Option && i.Key == stacked.Key)
            .OrderBy(i => i.OptionIndex).ToList();

        Assert.All(options.Skip(1).Zip(options), pair => Assert.True(pair.First.Bounds.Y > pair.Second.Bounds.Y));
        Assert.Single(options.Select(o => o.Bounds.X).Distinct());
    }

    public static TheoryData<string> PaperFormIds => new(FormCatalog.All.Where(f => f.Blocks.OfType<TableBlock>().Any()).Select(f => f.Id));

    [Theory]
    [MemberData(nameof(PaperFormIds))]
    public void TableCellContents_StayInsideTheirCell(string formId)
    {
        var items = Build(formId).Items;
        var cells = items.Where(i => i.Kind == LayoutKind.TableCell).ToList();

        foreach (var item in items.Where(i => i.Kind is not (LayoutKind.TableCell or LayoutKind.TitleBand)))
        {
            var cell = cells.FirstOrDefault(c => c.Bounds.Contains(item.Bounds.X, item.Bounds.Y));
            if (cell is null)
            {
                continue;
            }

            Assert.True(item.Bounds.Right <= cell.Bounds.Right && item.Bounds.Bottom <= cell.Bounds.Bottom,
                $"{item.Kind} {item.Key}{item.Text} at {item.Bounds} spills out of cell {cell.Bounds}");
        }
    }

    [Theory]
    [MemberData(nameof(PaperFormIds))]
    public void TableCells_TileTheTableWithoutOverlap(string formId)
    {
        var cells = Build(formId).Items.Where(i => i.Kind == LayoutKind.TableCell).ToList();

        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                Assert.False(cells[i].Bounds.Intersects(cells[j].Bounds), $"cell {cells[i].Bounds} overlaps {cells[j].Bounds}");
            }
        }
    }

    [Fact]
    public void Table_RowSpanCoversTheSpannedRows()
    {
        var items = Build("preanes").Items;
        var label = items.Single(i => i.Kind == LayoutKind.TableCell && i.Text == "마취관련 과거력");
        var rows = items.Where(i => i.Kind == LayoutKind.TableCell && i.Text is "Difficult airway" or "Complication" or "FHx").ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal(label.Bounds.Y, rows.Min(r => r.Bounds.Y));
        Assert.Equal(label.Bounds.Bottom, rows.Max(r => r.Bounds.Bottom));
        Assert.All(rows, r => Assert.Equal(label.Bounds.Right, r.Bounds.X));
        Assert.Equal(CellStyle.Label, label.Style);
        Assert.True(rows.All(r => r.Style.HasFlag(CellStyle.Left) && !r.Style.HasFlag(CellStyle.Label)));
    }

    [Fact]
    public void AnesthesiaRecord_TimeInputsFillTheirCellsAndRemarkFillsItsSpan()
    {
        var items = Build("anesrec").Items;
        var cells = items.Where(i => i.Kind == LayoutKind.TableCell).ToList();
        var chart = items.Single(i => i.Kind == LayoutKind.Chart);
        var legend = items.Single(i => i.Kind == LayoutKind.Legend);
        var remark = items.Single(i => i.Kind == LayoutKind.TextArea);
        var remarkCell = cells.Single(c => c.Bounds.Contains(remark.Bounds.X, remark.Bounds.Y));

        Assert.Equal(remarkCell.Bounds with { X = remarkCell.Bounds.X + 2, Y = remarkCell.Bounds.Y + 2,
            W = remarkCell.Bounds.W - 4, H = remarkCell.Bounds.H - 4 }, remark.Bounds);
        Assert.True(remark.Bounds.Bottom > chart.Bounds.Bottom, "REMARK spans past the chart");
        Assert.Equal(chart.Bounds.Y, legend.Bounds.Y);
        Assert.Equal(36 * 13 - 2, chart.Bounds.W);
        // TIME 줄 : 15분 칸 12개
        var timeRow = items.Where(i => i.Kind == LayoutKind.Text && i.Bounds.W == 3 * 13 - 2)
            .GroupBy(i => i.Bounds.Y).OrderBy(g => g.Key).First();
        Assert.Equal(12, timeRow.Count());
    }

    [Fact]
    public void GrowInput_LeavesRoomForTheUnitLabelAfterIt()
    {
        var items = Build("anesrec").Items;
        var units = items.Where(i => i.Kind == LayoutKind.Label && i.Text == "㎖").ToList();

        Assert.Equal(2, units.Count);
        Assert.All(units, unit => Assert.Contains(items, i => i.Kind == LayoutKind.Text && i.Bounds.Y == unit.Bounds.Y
            && i.Bounds.Right + FormLayout.Gap == unit.Bounds.X));
    }

    [Fact]
    public void PaperForms_PlaceImagesPatientFieldsAndSpreadOptions()
    {
        var items = Build("preanes").Items;
        var images = items.Where(i => i.Kind == LayoutKind.Image).ToList();
        var classes = items.Where(i => i.Kind == LayoutKind.Option && i.Text!.StartsWith("Class ")).ToList();

        Assert.Equal(["mallampati1", "mallampati2", "mallampati3", "mallampati4", "logo"], images.Select(i => i.Text));
        Assert.All(classes.Zip(images), pair => Assert.True(pair.First.Bounds.Y >= pair.Second.Bounds.Bottom));
        Assert.Equal(["id", "name", "sexAge"], items.Where(i => i.Kind == LayoutKind.PatientField).Select(i => i.Text));
        var plan = items.Where(i => i.Kind == LayoutKind.Option && i.Control is CheckControl c && c.Options.Contains("Spinal"))
            .OrderBy(i => i.OptionIndex).ToList();
        var steps = plan.Zip(plan.Skip(1), (a, b) => b.Bounds.X - a.Bounds.X).Distinct().ToList();
        Assert.Single(steps);
        Assert.Single(plan.Select(o => o.Bounds.Y).Distinct());
    }

    [Fact]
    public void HitTest_FindsInputUnderPoint()
    {
        var result = Build("labor2");
        var target = result.Items.First(i => i.Kind == LayoutKind.Text);

        var hit = result.HitTest(target.Bounds.X + 2, target.Bounds.Y + 2);

        Assert.Equal(target, hit);
        Assert.Null(result.HitTest(-5, -5));
    }

    [Fact]
    public void HitTest_SkipsDecorationsAndFindsOptionsByText()
    {
        var result = Build("sono1st");
        var option = result.Items.First(i => i.Kind == LayoutKind.Option && i.OptionIndex == 1);
        var section = result.Items.First(i => i.Kind == LayoutKind.Section);

        Assert.Equal(option, result.HitTest(option.Bounds.Right - 3, option.Bounds.Y + 5));
        Assert.Null(result.HitTest(section.Bounds.X + 2, section.Bounds.Y + 2));
    }
}
