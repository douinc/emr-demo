using EmrDemo.Core.Forms;

namespace EmrDemo.Core.Tests;

public class FormLayoutTests
{
    static int Measure(string text) => text.Length * 7;

    static FormLayoutResult Build(string formId, int width = 860) =>
        FormLayout.Build(FormCatalog.Get(formId), Measure, width);

    public static TheoryData<string> FormIds => new(FormCatalog.All.Select(f => f.Id));

    static bool IsLeaf(LayoutItem item) =>
        item.Kind is not (LayoutKind.TitleBand or LayoutKind.SetBox or LayoutKind.SignBox or LayoutKind.GridTitle);

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
