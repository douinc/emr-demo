using System.Reflection;
using System.Text.Json;

namespace EmrDemo.Core;

public sealed class EmrStore(string dataDir)
{
    static readonly Lazy<IReadOnlyList<Patient>> Seed = new(BuildSeed);

    public string DataDir { get; } = dataDir;
    string StorePath => Path.Combine(DataDir, "store.json");

    public List<Patient> LoadAll()
    {
        if (!File.Exists(StorePath))
        {
            var seeded = Seed.Value.Select(EmrJson.Clone).ToList();
            Write(seeded);
            return seeded;
        }

        var patients = JsonSerializer.Deserialize<List<Patient>>(File.ReadAllText(StorePath), EmrJson.Options)
            ?? throw new InvalidDataException($"{StorePath}가 비어 있습니다");
        patients.ForEach(p => p.Normalize());
        return patients;
    }

    public Patient Get(string id) =>
        LoadAll().FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException(id);

    public void Save(Patient patient)
    {
        var patients = LoadAll();
        var index = patients.FindIndex(p => p.Id == patient.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException(patient.Id);
        }

        patients[index] = EmrJson.Clone(patient);
        Write(patients);
    }

    public static Patient SeedFor(string id) =>
        EmrJson.Clone(Seed.Value.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException(id));

    void Write(List<Patient> patients)
    {
        Directory.CreateDirectory(DataDir);
        var temp = StorePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(patients, EmrJson.Indented));
        File.Move(temp, StorePath, overwrite: true);
    }

    sealed record SeedRecord(string Department, string Summary, string Content);

    static IReadOnlyList<Patient> BuildSeed()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EmrDemo.Core.Seed.records.json")
            ?? throw new InvalidOperationException("시드 리소스가 없습니다");
        var records = JsonSerializer.Deserialize<List<SeedRecord>>(stream, EmrJson.Options)!;

        var charts = records.Select((record, i) =>
        {
            var patient = new Patient
            {
                Id = $"DEMO-{i + 1:00}",
                Name = $"예시 환자 {i + 1:00}",
                Department = record.Department,
                VisitDate = "2026-07-28",
                ChartText = record.Content,
                Diagnoses = [new Diagnosis { Date = "2026/07/28", Code = "00", Name = "상병미정" }],
            };
            patient.Normalize();
            return patient;
        });
        var forms = Forms.FormCatalog.All.Select((form, i) =>
        {
            var number = records.Count + i + 1;
            var patient = new Patient
            {
                Id = $"DEMO-{number:00}",
                Name = $"예시 환자 {number:00}",
                Department = form.Department,
                VisitDate = "2026-07-28",
                FormId = form.Id,
            };
            patient.Normalize();
            return patient;
        });
        return charts.Concat(forms).ToList();
    }
}
