namespace EmrDemo.Core;

public enum UiMode
{
    Standard,
    Custom,
}

public sealed record AppOptions(UiMode UiMode, string DataDir)
{
    public static AppOptions Parse(IReadOnlyList<string> args, string defaultDataDir)
    {
        var uiMode = UiMode.Standard;
        var dataDir = defaultDataDir;

        foreach (var arg in args)
        {
            if (TryValue(arg, "--ui=", out var ui))
            {
                uiMode = ui.ToLowerInvariant() switch
                {
                    "standard" => UiMode.Standard,
                    "custom" => UiMode.Custom,
                    _ => throw new ArgumentException($"--ui 값은 standard 또는 custom이어야 합니다: {arg}"),
                };
            }
            else if (TryValue(arg, "--data-dir=", out var dir))
            {
                dataDir = dir;
            }
            else
            {
                throw new ArgumentException($"알 수 없는 인자: {arg}");
            }
        }

        return new AppOptions(uiMode, dataDir);
    }

    static bool TryValue(string arg, string prefix, out string value)
    {
        value = "";
        if (!arg.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        value = arg[prefix.Length..];
        if (value.Length == 0)
        {
            throw new ArgumentException($"{prefix} 뒤에 값이 필요합니다");
        }

        return true;
    }
}
