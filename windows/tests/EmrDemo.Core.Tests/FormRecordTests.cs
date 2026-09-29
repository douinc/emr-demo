using System.Text.Json;
using EmrDemo.Core.Forms;

namespace EmrDemo.Core.Tests;

public sealed class FormRecordTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "emrdemo-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Seed_AddsOneBlankFormRecordPerForm()
    {
        var patients = new EmrStore(_dir).LoadAll();

        Assert.Equal(8 + FormCatalog.All.Count, patients.Count);
        var formPatients = patients.Where(p => p.FormId is not null).ToList();
        Assert.Equal(FormCatalog.All.Select(f => f.Id), formPatients.Select(p => p.FormId));
        Assert.Equal("DEMO-09", formPatients[0].Id);
        Assert.Equal("예시 환자 09", formPatients[0].Name);
        Assert.All(formPatients, p =>
        {
            Assert.Equal("산부인과", p.Department);
            Assert.Equal("", p.ChartText);
            Assert.Empty(p.FormValues);
        });
        Assert.All(patients.Take(8), p => Assert.Null(p.FormId));
    }

    [Fact]
    public void FormValues_PersistAcrossStoreInstances()
    {
        var store = new EmrStore(_dir);
        var patient = store.Get("DEMO-11");
        patient.FormValues["sono1st.writtenOn"] = "2026-09-29";
        patient.FormValues["sono1st.b3.0"] = "Good Quality";

        store.Save(patient);

        Assert.Equal("Good Quality", new EmrStore(_dir).Get("DEMO-11").FormValues["sono1st.b3.0"]);
    }

    [Fact]
    public void Load_NullFormValues_AreTreatedAsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "store.json"), """[{"id":"DEMO-09","formId":"labor2","formValues":null}]""");

        Assert.Empty(new EmrStore(_dir).LoadAll().Single(p => p.Id == "DEMO-09").FormValues);
    }

    [Fact]
    public void FormField_PrefixesKey()
    {
        Assert.Equal("form.labor2.b2[0][1]", EmrFields.Form("labor2.b2[0][1]"));
    }

    [Fact]
    public void CheckValues_JoinInOptionOrderAndSplitBack()
    {
        IReadOnlyList<string> options = ["Anterior", "Posterior", "Fundal"];

        var joined = CheckValues.Join(options, ["Fundal", "Anterior"]);

        Assert.Equal("Anterior|Fundal", joined);
        Assert.Equal(["Anterior", "Fundal"], CheckValues.Split(joined));
        Assert.Empty(CheckValues.Split(""));
        Assert.Equal("", CheckValues.Join(options, []));
    }

    [Fact]
    public void CheckValues_IncludeOtherOptionAndDropUnknownSelections()
    {
        var control = FormCatalog.All.SelectMany(f => f.Inputs()).Select(i => i.Control).OfType<OptionControl>()
            .First(c => c.Other is not null);

        var joined = CheckValues.Join(control.AllOptions, [control.Other!.Text, "not an option", control.Options[0]]);

        Assert.Equal($"{control.Options[0]}|{control.Other.Text}", joined);
    }

    [Fact]
    public void Load_UnknownFormId_Throws()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "store.json"), """[{"id":"DEMO-09","formId":"../x"}]""");

        Assert.Throws<InvalidDataException>(() => new EmrStore(_dir).LoadAll());
    }

    [Fact]
    public void Load_StoreFromBeforeForms_GetsMissingSeedRecordsAppended()
    {
        var store = new EmrStore(_dir);
        var charts = store.LoadAll().Where(p => p.FormId is null).ToList();
        charts[0].ChartText = "보존";
        File.WriteAllText(Path.Combine(_dir, "store.json"), JsonSerializer.Serialize(charts, EmrJson.Options));

        var patients = new EmrStore(_dir).LoadAll();

        Assert.Equal(8 + FormCatalog.All.Count, patients.Count);
        Assert.Equal("보존", patients[0].ChartText);
        Assert.Equal("DEMO-14", patients[^1].Id);
    }

    [Fact]
    public void LayoutFile_ItemFieldsMatchTheLayout()
    {
        var form = FormCatalog.Get("csec");
        var layout = FormLayout.Build(form, t => t.Length * 7, 860);
        var option = layout.Items.First(i => i.Kind == LayoutKind.Option && i.OptionIndex == 1);
        var cell = layout.Items.First(i => i.Kind == LayoutKind.GridCell);

        using var doc = JsonDocument.Parse(File.ReadAllText(FormLayoutFile.Write(_dir, form.Id, layout)));
        var root = doc.RootElement;
        Assert.Equal(layout.Height, root.GetProperty("height").GetInt32());
        Assert.Equal(layout.Width, root.GetProperty("width").GetInt32());
        var items = root.GetProperty("items").EnumerateArray().ToList();
        var o = items.Single(i => i.GetProperty("key").GetString() == option.Key && i.TryGetProperty("option", out var n) && n.GetInt32() == 1);
        Assert.Equal(option.Text, o.GetProperty("text").GetString());
        Assert.Equal([option.Bounds.X, option.Bounds.Y, option.Bounds.W, option.Bounds.H],
            new[] { "x", "y", "w", "h" }.Select(p => o.GetProperty(p).GetInt32()));
        var c = items.Single(i => i.GetProperty("key").GetString() == cell.Key);
        Assert.Equal("gridCell", c.GetProperty("kind").GetString());
        Assert.False(c.TryGetProperty("option", out _));
        Assert.False(c.TryGetProperty("text", out _));
        Assert.Equal(cell.Bounds.Y, c.GetProperty("y").GetInt32());
    }

    [Fact]
    public void LayoutFile_WritesItemsWithKeysAndBounds()
    {
        var form = FormCatalog.Get("labor2");
        var layout = FormLayout.Build(form, t => t.Length * 7, 860);

        var path = FormLayoutFile.Write(_dir, form.Id, layout);

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(Path.Combine(_dir, "layout-labor2.json"), path);
        Assert.Equal("labor2", doc.RootElement.GetProperty("formId").GetString());
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(layout.Items.Count(i => i.IsInteractive), items.Count);
        var first = items.First(i => i.GetProperty("key").GetString() == form.WrittenOnKey);
        Assert.Equal("date", first.GetProperty("kind").GetString());
        Assert.True(first.GetProperty("w").GetInt32() > 0);
    }

    [Fact]
    public void LayoutFile_MarksOptionGroupKindAndReadOnlyText()
    {
        var sono = FormCatalog.Get("sono23");
        var path = FormLayoutFile.Write(_dir, sono.Id, FormLayout.Build(sono, t => t.Length * 7, 860));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("kind").GetString() == "option" && i.GetProperty("group").GetString() == "radio");
        Assert.Contains(items, i => i.GetProperty("kind").GetString() == "option" && i.GetProperty("group").GetString() == "check");
        Assert.Contains(items, i => i.GetProperty("kind").GetString() == "text" && i.TryGetProperty("readOnly", out var ro) && ro.GetBoolean());
        Assert.DoesNotContain(items, i => i.GetProperty("kind").GetString() == "text" && i.TryGetProperty("group", out _));
    }

    [Fact]
    public void LayoutFile_ListsSelectOptions()
    {
        var csec = FormCatalog.Get("csec");
        var select = csec.Inputs().Select(i => i.Control).OfType<SelectControl>().First();
        var path = FormLayoutFile.Write(_dir, csec.Id, FormLayout.Build(csec, t => t.Length * 7, 860));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var item = doc.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == select.Key);
        Assert.Equal(select.Options, item.GetProperty("options").EnumerateArray().Select(o => o.GetString()));
    }
}
