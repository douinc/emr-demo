using System.Globalization;
using System.Text.Json;

namespace EmrDemo.Core;

public sealed class EventLog(string dataDir, UiMode uiMode, bool elevated, Func<DateTimeOffset> clock)
{
    public EventLog(string dataDir, UiMode uiMode, bool elevated)
        : this(dataDir, uiMode, elevated, () => DateTimeOffset.UtcNow)
    {
    }

    public string LogPath => Path.Combine(dataDir, "events.jsonl");

    public void Save(Patient patient) => Append(new Dictionary<string, object?>
    {
        ["type"] = "save",
        ["patientId"] = patient.Id,
        ["patient"] = patient,
    });

    public void FieldCommit(string patientId, string field, string value) => Append(new Dictionary<string, object?>
    {
        ["type"] = "field_commit",
        ["patientId"] = patientId,
        ["field"] = field,
        ["value"] = value,
    });

    void Append(Dictionary<string, object?> body)
    {
        var line = new Dictionary<string, object?>
        {
            ["ts"] = clock().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["uiMode"] = uiMode.ToString().ToLowerInvariant(),
            ["elevated"] = elevated,
        };
        foreach (var (key, value) in body)
        {
            line[key] = value;
        }

        Directory.CreateDirectory(dataDir);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(line, EmrJson.Options) + "\n");
    }
}
