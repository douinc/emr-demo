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

        Assert.Equal(14, patients.Count);
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

        Assert.Empty(Assert.Single(new EmrStore(_dir).LoadAll()).FormValues);
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
}
