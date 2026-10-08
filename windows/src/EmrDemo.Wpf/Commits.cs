namespace EmrDemo.Wpf;

/// <summary>
/// 텍스트 칸 하나의 마지막 기록 값. WinForms판 StandardTextField와 같이, 값이 바뀌었을 때만 기록한다.
/// </summary>
sealed class TextCommit
{
    string _committed = "";

    public static string Normalize(string text) => text.Replace("\r\n", "\n");

    public void Reset(string value) => _committed = Normalize(value);

    /// <summary>마지막 기록 값과 다르면 기록 값으로 삼고 true.</summary>
    public bool TryCommit(string value)
    {
        value = Normalize(value);
        if (value == _committed)
        {
            return false;
        }

        _committed = value;
        return true;
    }
}

/// <summary>
/// 서식 한 장의 기록 여부 판단(WinForms판 FormPage.Commit과 같은 규칙). 불러오는 중에는 기록하지 않고,
/// 마지막 기록 값(없으면 불러온 값)과 같으면 기록하지 않는다.
/// </summary>
sealed class FormCommits
{
    readonly Dictionary<string, string> _committed = [];
    readonly Dictionary<string, string> _initial = [];

    public bool Loading { get; private set; }

    public void BeginLoad()
    {
        Loading = true;
        _committed.Clear();
    }

    public void Remember(string key, IReadOnlyDictionary<string, string> values) =>
        _initial[key] = values.GetValueOrDefault(key, "");

    public void EndLoad() => Loading = false;

    public bool ShouldCommit(string key, string value)
    {
        if (Loading || _committed.GetValueOrDefault(key, _initial.GetValueOrDefault(key, "")) == value)
        {
            return false;
        }

        _committed[key] = value;
        return true;
    }
}
