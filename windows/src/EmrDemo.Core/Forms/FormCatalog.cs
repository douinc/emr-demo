using System.Reflection;
using System.Text.Json;

namespace EmrDemo.Core.Forms;

public static class FormCatalog
{
    static readonly Lazy<IReadOnlyList<FormDefinition>> Forms = new(Load);

    public static IReadOnlyList<FormDefinition> All => Forms.Value;

    public static FormDefinition Get(string id) =>
        All.FirstOrDefault(f => f.Id == id) ?? throw new KeyNotFoundException(id);

    static IReadOnlyList<FormDefinition> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EmrDemo.Core.Seed.forms.json")
            ?? throw new InvalidOperationException("서식 리소스가 없습니다");
        return JsonSerializer.Deserialize<List<FormDefinition>>(stream, EmrJson.Options)
            ?? throw new InvalidDataException("서식 리소스가 비어 있습니다");
    }
}
