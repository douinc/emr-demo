using EmrDemo.Wpf;

namespace EmrDemo.Wpf.Tests;

public class KoreanCultureTests
{
    static readonly DateTime Date = new(2026, 10, 8);

    [Fact]
    public void ShortDate_IsYearMonthDayWithHyphens()
    {
        var culture = KoreanCulture.Create();

        Assert.Equal("ko-KR", culture.Name);
        Assert.Equal("yyyy-MM-dd", culture.DateTimeFormat.ShortDatePattern);
        Assert.Equal("2026-10-08", Date.ToString("d", culture));
    }

    [Fact]
    public void ShortDate_ParsesTypedDate()
    {
        Assert.Equal(Date, DateTime.Parse("2026-10-08", KoreanCulture.Create()));
    }

    [Fact]
    public void StoredDate_IsInvariantYearMonthDayOrEmpty()
    {
        Assert.Equal("2026-10-08", KoreanCulture.StoredDate(Date));
        Assert.Equal("", KoreanCulture.StoredDate(null));
    }

    [Theory]
    [InlineData("2026-10-08", true)]
    [InlineData("", false)]
    [InlineData("2026. 10. 8.", false)]
    [InlineData("10/8/2026", false)]
    public void ParseStored_AcceptsOnlyStoredFormat(string value, bool parsed)
    {
        Assert.Equal(parsed ? Date : null, KoreanCulture.ParseStored(value));
    }
}
