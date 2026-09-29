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

    [Theory]
    [MemberData(nameof(FormIds))]
    public void LeafItems_DoNotOverlap(string formId)
    {
        var leaves = Build(formId).Items.Where(IsLeaf).ToList();

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
    [MemberData(nameof(FormIds))]
    public void EveryInput_IsPlacedOnce_AndEveryOptionOnce(string formId)
    {
        var form = FormCatalog.Get(formId);
        var items = Build(formId).Items;

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
    public void Result_ContainsAllItems(string formId)
    {
        var result = Build(formId);

        Assert.All(result.Items, i => Assert.True(i.Bounds.Right <= result.Width && i.Bounds.Bottom <= result.Height,
            $"{i.Kind} {i.Key} at {i.Bounds} outside {result.Width}x{result.Height}"));
    }

    [Fact]
    public void NarrowWidth_WrapsRowsInsteadOfOverflowing()
    {
        var wide = Build("sono1st", 860);
        var narrow = Build("sono1st", 420);

        Assert.True(narrow.Height > wide.Height);
        var flowItems = narrow.Items.Where(i => i.Kind is LayoutKind.Option or LayoutKind.Text or LayoutKind.Label);
        Assert.All(flowItems, i => Assert.True(i.Bounds.Right <= 420 || i.Bounds.X == narrow.Items
            .Where(o => o.Bounds.Y == i.Bounds.Y && o.Kind != LayoutKind.RowLabel).Min(o => o.Bounds.X),
            $"{i.Kind} {i.Key} ends at {i.Bounds.Right}"));
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
    public void Build_IsDeterministic()
    {
        Assert.Equal(Build("csec").Items, Build("csec").Items);
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
}
