using System.Globalization;

namespace EmrDemo.Wpf;

/// <summary>
/// 한국어 Windows(.NET Framework·NLS)에서 돌던 EMR처럼 날짜를 yyyy-MM-dd로 보이고 읽게 한다.
/// .NET 10의 ICU ko-KR 짧은 날짜는 "yyyy. M. d."이고, 시험 VM은 en-US라 OS 설정에 기대지 않는다.
/// DatePicker는 Language를 직접 주지 않으면 CurrentCulture를 쓴다.
/// </summary>
static class KoreanCulture
{
    public const string DateFormat = "yyyy-MM-dd";

    public static CultureInfo Create()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ko-KR").Clone();
        culture.DateTimeFormat.ShortDatePattern = DateFormat;
        culture.DateTimeFormat.DateSeparator = "-";
        return culture;
    }

    public static void Apply()
    {
        var culture = Create();
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>날짜 칸의 저장 값: 고른 날짜는 yyyy-MM-dd, 빈 칸은 "".</summary>
    public static string StoredDate(DateTime? date) =>
        date is { } value ? value.ToString(DateFormat, CultureInfo.InvariantCulture) : "";

    /// <summary>저장 값을 날짜로 읽는다. 형식이 맞지 않으면 빈 칸이다.</summary>
    public static DateTime? ParseStored(string value) =>
        DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
