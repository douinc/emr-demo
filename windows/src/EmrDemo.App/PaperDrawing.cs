using System.Drawing.Drawing2D;
using EmrDemo.Core.Forms;

namespace EmrDemo.App;

/// <summary>
/// 종이 서식(표·그림·그래프)의 장식. 웹 paper-forms.js·paper-forms.css의 모양을 따른다. 두 모드가 함께 쓴다.
/// </summary>
static class PaperDrawing
{
    /// <summary>
    /// 장식은 스크롤 이동 변환 아래에서 그리므로 TextRenderer에 변환·클립을 지키게 한다
    /// (<see cref="CustomFormCanvas.KeepGraphics"/>).
    /// </summary>
    const TextFormatFlags Keep = CustomFormCanvas.KeepGraphics | TextFormatFlags.NoPrefix;

    const TextFormatFlags CellText = Keep | TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter
        | TextFormatFlags.TextBoxControl;

    public static void Cell(Graphics g, LayoutItem item, Rectangle r)
    {
        if (item.Style.HasFlag(CellStyle.Label))
        {
            using var back = new SolidBrush(FormMetrics.PaperLabel);
            g.FillRectangle(back, r);
        }

        if (!item.Style.HasFlag(CellStyle.NoBorder))
        {
            // 이웃 칸과 테두리 선을 공유한다(웹 border-collapse)
            using var pen = new Pen(FormMetrics.PaperLine);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }
    }

    /// <summary>custom 모드의 라벨 칸 글자. standard 모드는 같은 자리에 Label을 놓는다.</summary>
    public static void CellLabel(Graphics g, LayoutItem item, Rectangle r)
    {
        var font = item.Style.HasFlag(CellStyle.Big) ? Theme.Heading : item.Style.HasFlag(CellStyle.Bold) ? Theme.Bold : Theme.Font;
        var flags = CellText | (item.Style.HasFlag(CellStyle.Left) ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, item.Text, font, Rectangle.Inflate(r, -3, -1), Color.Black, flags);
    }

    public static void Image(Graphics g, string name, Rectangle r)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (name == "logo")
        {
            Logo(g, r);
        }
        else if (name.StartsWith("mallampati", StringComparison.Ordinal) && int.TryParse(name["mallampati".Length..], out var grade))
        {
            g.TranslateTransform(r.X, r.Y);
            g.ScaleTransform(r.Width / 80f, r.Height / 100f);
            Mouth(g, grade);
        }

        g.Restore(state);
    }

    /// <summary>병원 로고는 비식별화한 자리표시 로고(회색 십자 + "예시병원")로 그린다.</summary>
    static void Logo(Graphics g, Rectangle r)
    {
        var size = Math.Min(r.Height, 24);
        var box = new Rectangle(r.X, r.Y + (r.Height - size) / 2, size, size);
        using (var path = RoundRect(box, 5))
        using (var fill = new SolidBrush(ColorTranslator.FromHtml("#5b6b80")))
        {
            g.FillPath(fill, path);
        }

        var u = size / 24f;
        var cross = new[]
        {
            new PointF(10, 5), new PointF(14, 5), new PointF(14, 10), new PointF(19, 10), new PointF(19, 14), new PointF(14, 14),
            new PointF(14, 19), new PointF(10, 19), new PointF(10, 14), new PointF(5, 14), new PointF(5, 10), new PointF(10, 10),
        }.Select(p => new PointF(box.X + p.X * u, box.Y + p.Y * u)).ToArray();
        g.FillPolygon(Brushes.White, cross);
        using var font = new Font("Malgun Gothic", 11f, FontStyle.Bold);
        TextRenderer.DrawText(g, "예시병원", font, new Rectangle(box.Right + 4, r.Y, r.Width - size - 4, r.Height),
            ColorTranslator.FromHtml("#3e4a5a"), Keep | TextFormatFlags.VerticalCenter);
    }

    static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Mallampati 입 모양(80×100 좌표). 등급이 높을수록 혀가 올라와 목젖·편도궁이 가려진다.</summary>
    static void Mouth(Graphics g, int grade)
    {
        grade = Math.Clamp(grade, 1, 4);
        float tongueTop = new[] { 0, 60, 52, 44, 34 }[grade];
        Ellipse(g, "#c62f3a", 40, 50, 37, 47);
        Ellipse(g, "#6e1220", 40, 50, 29, 39);
        Ellipse(g, "#d9707f", 40, 34, 21, 14);
        if (grade <= 2)
        {
            using var pillar = new Pen(ColorTranslator.FromHtml("#a83448"), 3);
            g.DrawBeziers(pillar, Quad(new(22, 40), new(24, 62), new(28, 74)));
            g.DrawBeziers(pillar, Quad(new(58, 40), new(56, 62), new(52, 74)));
        }

        if (grade <= 3)
        {
            Ellipse(g, "#b8475a", 40, grade == 3 ? 42 : 46, 4, grade == 3 ? 5 : 8);
        }

        var cy = (tongueTop + 88) / 2;
        var ry = (88 - tongueTop) / 2;
        Ellipse(g, "#f2a9b6", 40, cy, 25, ry, "#d27b8d");
        using (var mid = new Pen(ColorTranslator.FromHtml("#d98a9b")))
        {
            g.DrawLine(mid, 40, tongueTop + 6, 40, 82);
        }

        Teeth(g, new(13, 30), new(40, 2), new(67, 30), new(62, 35), new(40, 12), new(18, 35));
        Teeth(g, new(15, 72), new(40, 99), new(65, 72), new(60, 68), new(40, 90), new(20, 68));
    }

    static void Ellipse(Graphics g, string fill, float cx, float cy, float rx, float ry, string? stroke = null)
    {
        using var brush = new SolidBrush(ColorTranslator.FromHtml(fill));
        g.FillEllipse(brush, cx - rx, cy - ry, rx * 2, ry * 2);
        if (stroke is not null)
        {
            using var pen = new Pen(ColorTranslator.FromHtml(stroke));
            g.DrawEllipse(pen, cx - rx, cy - ry, rx * 2, ry * 2);
        }
    }

    /// <summary>바깥 호(a→c, 조절점 b)와 안쪽 호(d→f, 조절점 e)로 닫힌 치열.</summary>
    static void Teeth(Graphics g, PointF a, PointF b, PointF c, PointF d, PointF e, PointF f)
    {
        using var path = new GraphicsPath();
        path.AddBeziers(Quad(a, b, c));
        path.AddLine(c, d);
        path.AddBeziers(Quad(d, e, f));
        path.CloseFigure();
        using var fill = new SolidBrush(ColorTranslator.FromHtml("#f6f6f2"));
        using var pen = new Pen(ColorTranslator.FromHtml("#bdbdb5"));
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }

    /// <summary>2차 베지어(SVG Q)를 GDI의 3차 베지어 점 4개로 바꾼다.</summary>
    static PointF[] Quad(PointF p0, PointF c, PointF p2) =>
    [
        p0,
        new(p0.X + 2f / 3 * (c.X - p0.X), p0.Y + 2f / 3 * (c.Y - p0.Y)),
        new(p2.X + 2f / 3 * (c.X - p2.X), p2.Y + 2f / 3 * (c.Y - p2.Y)),
        p2,
    ];

    /// <summary>활력징후 그래프 격자: 10mmHg 가로선(20마다 진하게), 5분 세로선(15분마다 굵게).</summary>
    public static void Chart(Graphics g, ChartControl chart, Rectangle r)
    {
        using var light = new Pen(ColorTranslator.FromHtml("#aaaaaa"));
        using var dark = new Pen(ColorTranslator.FromHtml("#555555"));
        using var minor = new Pen(ColorTranslator.FromHtml("#999999"));
        using var major = new Pen(ColorTranslator.FromHtml("#222222"), 1.5f);
        for (var v = chart.Min; v <= chart.Max; v += 10)
        {
            var y = r.Y + ChartY(chart.Min, chart.Max, r.Height, v);
            g.DrawLine(v % 20 == 0 ? dark : light, r.X, y, r.Right, y);
        }

        var cw = r.Width / (float)chart.Columns;
        for (var c = 1; c < chart.Columns; c++)
        {
            var x = r.X + c * cw;
            g.DrawLine(c % 3 == 0 ? major : minor, x, r.Y, x, r.Bottom);
        }
    }

    public static void Legend(Graphics g, LegendControl legend, Rectangle r)
    {
        string[] keys = ["○ SPONT", "⊘ ASSIST", "⊗ CONTROL", "∧∨ BP", "• PULSE", "× ANESTHESIA", "⊙ OPERATION"];
        var step = (r.Height - 12) / (float)keys.Length;
        for (var i = 0; i < keys.Length; i++)
        {
            var top = r.Y + 6 + (int)(i * step);
            TextRenderer.DrawText(g, keys[i], Theme.Font, new Rectangle(r.X + 3, top, r.Width - 34, (int)step),
                Color.Black, Keep | TextFormatFlags.VerticalCenter);
        }

        for (var v = 20; v <= 200; v += 20)
        {
            if (v < legend.Min || v > legend.Max)
            {
                continue;
            }

            var y = r.Y + ChartY(legend.Min, legend.Max, r.Height, v);
            TextRenderer.DrawText(g, v.ToString(), Theme.Font, new Rectangle(r.Right - 32, y - 7, 29, 14), Color.Black,
                Keep | TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    static int ChartY(int min, int max, int height, int value) => (int)Math.Round((max - value) / (double)(max - min) * height);
}
