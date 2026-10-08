using System.Windows;
using System.Windows.Media;

namespace EmrDemo.Wpf;

/// <summary>WinForms판 Theme·FormMetrics와 같은 색과 글꼴(맑은 고딕 8.25pt = 11px).</summary>
static class Theme
{
    public static readonly Brush Face = Hex("#f0efe8");
    public static readonly Brush Face2 = Hex("#e6e4dc");
    public static readonly Brush Line = Hex("#8e9aab");
    public static readonly Brush Line2 = Hex("#b8bcc4");
    public static readonly Brush CellLine = Hex("#e3e3e3");
    public static readonly Brush Label = Hex("#e9edf3");
    public static readonly Brush Selection = Hex("#316ac5");
    public static readonly Brush Cream = Hex("#fdf7dd");
    public static readonly Brush Mint = Hex("#dff0e4");
    public static readonly Brush Gray = Hex("#eceef2");
    public static readonly Brush TableHead = Hex("#e4e8ef");

    public static readonly Brush Band = Hex("#ecebe6");
    public static readonly Brush SectionText = Hex("#1a3c66");
    public static readonly Brush GridTitle = Hex("#eef0f3");
    public static readonly Brush GridHeader = Hex("#a8c6ea");
    public static readonly Brush GridLine = Hex("#9aa4b1");
    public static readonly Brush Highlight = Hex("#fff7d6");
    public static readonly Brush ReadOnly = Hex("#f1f2f4");

    public static readonly FontFamily Font = new("Malgun Gothic");
    public const double FontSize = 11;
    public static readonly Typeface Regular = new(Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    static Brush Hex(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
