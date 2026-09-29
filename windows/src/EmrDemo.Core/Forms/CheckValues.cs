namespace EmrDemo.Core.Forms;

/// <summary>체크 그룹 값: 선택된 선택지를 스키마 순서로 "|"로 잇는다(선택지 문자열에는 "|"가 없다).</summary>
public static class CheckValues
{
    public static string Join(IReadOnlyList<string> options, IEnumerable<string> selected)
    {
        var set = selected.ToHashSet();
        return string.Join('|', options.Where(set.Contains));
    }

    public static IReadOnlyList<string> Split(string value) =>
        value.Length == 0 ? [] : value.Split('|');
}
