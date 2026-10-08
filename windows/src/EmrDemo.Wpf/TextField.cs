using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace EmrDemo.Wpf;

/// <summary>
/// 차트·메모 칸. WinForms판 StandardTextField와 같은 기록 규칙: 키보드 포커스를 잃을 때 바뀌었으면 기록하고,
/// 포커스 없이 글자가 바뀌면(UIA SetValue) 바로 기록한다. 불러오기는 기록하지 않는다.
/// </summary>
sealed class TextField
{
    readonly TextBox _box;
    readonly TextCommit _commit = new();
    bool _loading;

    public TextField(string automationId)
    {
        _box = NewBox(automationId, multiline: true);
        _box.LostKeyboardFocus += (_, _) => CommitPending();
        _box.TextChanged += (_, _) =>
        {
            if (!_loading && !_box.IsKeyboardFocusWithin)
            {
                CommitPending();
            }
        };
    }

    public FrameworkElement Element => _box;

    public event Action<string>? Committed;

    public string Value => TextCommit.Normalize(_box.Text);

    public void Load(string value)
    {
        _loading = true;
        _box.Text = value.Replace("\n", "\r\n");
        _loading = false;
        _commit.Reset(Value);
    }

    public void CommitPending()
    {
        if (_commit.TryCommit(Value))
        {
            Committed?.Invoke(Value);
        }
    }

    public static TextBox NewBox(string automationId, bool multiline)
    {
        var box = new TextBox
        {
            BorderBrush = Theme.Line,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1, 0, 1, 0),
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden,
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(box, automationId);
        return box;
    }
}
