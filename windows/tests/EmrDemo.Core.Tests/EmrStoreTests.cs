using EmrDemo.Core;

namespace EmrDemo.Core.Tests;

public sealed class EmrStoreTests : IDisposable
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
    public void Load_WithoutStore_CreatesSeededStoreFile()
    {
        var store = new EmrStore(_dir);

        var patients = store.LoadAll();

        Assert.True(File.Exists(Path.Combine(_dir, "store.json")));
        Assert.Equal(14, patients.Count);
        Assert.Equal("DEMO-01", patients[0].Id);
        Assert.Equal("예시 환자 01", patients[0].Name);
        Assert.Equal("안과", patients[0].Department);
        Assert.StartsWith("C.C: 가려움", patients[0].ChartText);
        Assert.Equal("DEMO-08", patients[7].Id);
    }

    [Fact]
    public void Seed_HasFixedEditableRowCounts()
    {
        var patient = new EmrStore(_dir).LoadAll()[0];

        Assert.Equal(Patient.VitalRowCount, patient.Vitals.Count);
        Assert.Equal(Patient.OrderRowCount, patient.Orders.Count);
        Assert.NotEmpty(patient.Diagnoses);
    }

    [Fact]
    public void Save_PersistsAcrossStoreInstances()
    {
        var store = new EmrStore(_dir);
        var patient = store.Get("DEMO-02");
        patient.ChartText = "새록 요약: 식후 복통";
        patient.Orders[0].Name = "알레지온 점안액";
        patient.Vitals[1].Sbp = "120";

        store.Save(patient);
        var reloaded = new EmrStore(_dir).Get("DEMO-02");

        Assert.Equal("새록 요약: 식후 복통", reloaded.ChartText);
        Assert.Equal("알레지온 점안액", reloaded.Orders[0].Name);
        Assert.Equal("120", reloaded.Vitals[1].Sbp);
        Assert.StartsWith("C.C: 가려움", new EmrStore(_dir).Get("DEMO-01").ChartText);
    }

    [Fact]
    public void Get_ReturnsIndependentCopy()
    {
        var store = new EmrStore(_dir);

        store.Get("DEMO-01").ChartText = "저장하지 않은 편집";

        Assert.StartsWith("C.C: 가려움", store.Get("DEMO-01").ChartText);
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => new EmrStore(_dir).Get("DEMO-99"));
    }

    [Fact]
    public void Load_PadsMissingRowsFromHandEditedStore()
    {
        var store = new EmrStore(_dir);
        var patient = store.Get("DEMO-01");
        patient.Orders.RemoveRange(1, patient.Orders.Count - 1);
        patient.Vitals.Clear();
        store.Save(patient);

        var reloaded = new EmrStore(_dir).Get("DEMO-01");

        Assert.Equal(Patient.OrderRowCount, reloaded.Orders.Count);
        Assert.Equal(Patient.VitalRowCount, reloaded.Vitals.Count);
    }

    [Fact]
    public void SeedFor_ReturnsOriginalEvenAfterSave()
    {
        var store = new EmrStore(_dir);
        var patient = store.Get("DEMO-03");
        patient.ChartText = "변경";
        store.Save(patient);

        Assert.Equal("S", EmrStore.SeedFor("DEMO-03").ChartText.Split('\n')[0]);
    }

    void WriteStore(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "store.json"), json);
    }

    [Fact]
    public void Load_CorruptStore_ThrowsWithoutReseeding()
    {
        WriteStore("{");

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => new EmrStore(_dir).LoadAll());
        Assert.Equal("{", File.ReadAllText(Path.Combine(_dir, "store.json")));
    }

    [Fact]
    public void Load_NullStore_Throws()
    {
        WriteStore("null");

        Assert.Throws<InvalidDataException>(() => new EmrStore(_dir).LoadAll());
    }

    [Fact]
    public void Load_NullListsAndEntries_AreTreatedAsEmpty()
    {
        WriteStore("""[{"id":"DEMO-01","vitals":null,"orders":[null],"diagnoses":null}]""");

        var patient = Assert.Single(new EmrStore(_dir).LoadAll());

        Assert.Equal(Patient.VitalRowCount, patient.Vitals.Count);
        Assert.Equal(Patient.OrderRowCount, patient.Orders.Count);
        Assert.All(patient.Orders, order => Assert.Equal("", order.Name));
        Assert.Empty(patient.Diagnoses);
    }

    [Fact]
    public void Save_UnknownId_ThrowsAndKeepsStore()
    {
        var store = new EmrStore(_dir);
        store.LoadAll();
        var before = File.ReadAllText(Path.Combine(_dir, "store.json"));

        Assert.Throws<KeyNotFoundException>(() => store.Save(new Patient { Id = "DEMO-99" }));
        Assert.Equal(before, File.ReadAllText(Path.Combine(_dir, "store.json")));
    }
}
