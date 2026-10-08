using EmrDemo.Core;
using EmrDemo.Wpf;

namespace EmrDemo.Wpf.Tests;

public class GridRowTests
{
    [Fact]
    public void SettingNewValue_RaisesCellChangedOnce()
    {
        var row = new GridRow("1", 3);
        var changes = new List<(int, string)>();
        row.CellChanged += (column, value) => changes.Add((column, value));

        row[1] = "120";
        row[1] = "120";

        Assert.Equal([(1, "120")], changes);
        Assert.Equal("120", row[1]);
    }

    [Fact]
    public void Load_UpdatesCellsWithoutCellChanged()
    {
        var row = new GridRow("2", 2);
        var changed = false;
        var notified = new List<string?>();
        row.CellChanged += (_, _) => changed = true;
        row.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        row.Load(["a", "b"]);

        Assert.False(changed);
        Assert.Equal(["Item[]"], notified);
        Assert.Equal(("a", "b"), (row[0], row[1]));
        Assert.Equal("2", row.Number);
    }

    [Fact]
    public void Load_FillsMissingCellsWithEmpty()
    {
        var row = new GridRow("1", 3);
        row.Load(["a"]);

        Assert.Equal(("a", "", ""), (row[0], row[1], row[2]));
    }
}

public class PatientLabelTests
{
    [Fact]
    public void Label_IsIdNameDepartmentSeparatedByTwoSpaces()
    {
        var patient = new Patient { Id = "DEMO-10", Name = "예시 환자 10", Department = "산부인과" };

        Assert.Equal("DEMO-10  예시 환자 10  산부인과", PatientLabel.For(patient));
    }
}
