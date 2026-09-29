using System.Text.Json;
using EmrDemo.Core;

namespace EmrDemo.Core.Tests;

public sealed class EventLogTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "emrdemo-tests-" + Guid.NewGuid().ToString("N"));
    DateTimeOffset _now = new(2026, 9, 29, 3, 12, 45, 123, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    EventLog NewLog(UiMode mode = UiMode.Custom, bool elevated = false) => new(_dir, mode, elevated, () => _now);

    List<JsonElement> ReadLines() =>
        File.ReadAllLines(Path.Combine(_dir, "events.jsonl"))
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();

    [Fact]
    public void Save_WritesSnapshotLine()
    {
        var patient = EmrStore.SeedFor("DEMO-01");
        patient.Orders[0].Name = "알레지온 점안액";

        NewLog(elevated: true).Save(patient);

        var line = Assert.Single(ReadLines());
        Assert.Equal("2026-09-29T03:12:45.123Z", line.GetProperty("ts").GetString());
        Assert.Equal("save", line.GetProperty("type").GetString());
        Assert.Equal("DEMO-01", line.GetProperty("patientId").GetString());
        Assert.Equal("custom", line.GetProperty("uiMode").GetString());
        Assert.True(line.GetProperty("elevated").GetBoolean());
        Assert.Equal("알레지온 점안액",
            line.GetProperty("patient").GetProperty("orders")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void FieldCommit_WritesFieldAndValue()
    {
        NewLog(UiMode.Standard).FieldCommit("DEMO-02", EmrFields.Order(3, nameof(Order.Days)), "7");

        var line = Assert.Single(ReadLines());
        Assert.Equal("field_commit", line.GetProperty("type").GetString());
        Assert.Equal("standard", line.GetProperty("uiMode").GetString());
        Assert.Equal("orders[3].days", line.GetProperty("field").GetString());
        Assert.Equal("7", line.GetProperty("value").GetString());
        Assert.False(line.TryGetProperty("patient", out _));
    }

    [Fact]
    public void Lines_AppendInOrderAcrossInstances()
    {
        NewLog().FieldCommit("DEMO-01", EmrFields.ChartText, "가");
        _now = _now.AddMilliseconds(5);
        NewLog().FieldCommit("DEMO-01", EmrFields.Vital(0, nameof(VitalSign.Sbp)), "120\n");

        var lines = ReadLines();
        Assert.Equal(2, lines.Count);
        Assert.Equal("chartText", lines[0].GetProperty("field").GetString());
        Assert.Equal("vitals[0].sbp", lines[1].GetProperty("field").GetString());
        Assert.Equal("2026-09-29T03:12:45.128Z", lines[1].GetProperty("ts").GetString());
        Assert.Equal("120\n", lines[1].GetProperty("value").GetString());
    }

    [Fact]
    public void Line_KeepsKoreanUnescapedAndStartsWithTimestamp()
    {
        NewLog().FieldCommit("DEMO-01", EmrFields.ChartText, "알레지온 점안액");

        var raw = File.ReadAllText(Path.Combine(_dir, "events.jsonl"));
        Assert.StartsWith("{\"ts\":", raw);
        Assert.Contains("알레지온 점안액", raw);
        Assert.EndsWith("}\n", raw);
    }
}
