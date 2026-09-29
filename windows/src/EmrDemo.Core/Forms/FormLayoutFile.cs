using System.Text.Json;

namespace EmrDemo.Core.Forms;

/// <summary>
/// 서식 입력 요소의 정답 좌표(서식 콘텐츠 좌표, 스크롤 0 기준)를 데이터 디렉터리에 쓴다.
/// 채점·프로브용이며 에이전트 입력으로 쓰면 안 된다.
/// </summary>
public static class FormLayoutFile
{
    public static string Write(string dataDir, string formId, FormLayoutResult layout)
    {
        var items = layout.Items.Where(i => i.IsInteractive).Select(i => new
        {
            key = i.Key,
            kind = JsonNamingPolicy.CamelCase.ConvertName(i.Kind.ToString()),
            option = i.OptionIndex >= 0 ? i.OptionIndex : (int?)null,
            text = i.Kind == LayoutKind.Option ? i.Text : null,
            group = i.Control switch { RadioControl => "radio", CheckControl => "check", _ => null },
            readOnly = i.Control is TextControl { ReadOnly: true } ? true : (bool?)null,
            x = i.Bounds.X,
            y = i.Bounds.Y,
            w = i.Bounds.W,
            h = i.Bounds.H,
        });
        var path = Path.Combine(dataDir, $"layout-{formId}.json");
        Directory.CreateDirectory(dataDir);
        var options = new JsonSerializerOptions(EmrJson.Indented)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(new { formId, width = layout.Width, height = layout.Height, items }, options));
        return path;
    }
}
