using System.ComponentModel;

namespace EmrDemo.Wpf;

/// <summary>
/// 표 한 줄. 셀은 인덱서로 묶는다(Binding Path "[n]"). 값이 실제로 바뀌면 CellChanged를 낸다 —
/// 키보드 편집 확정과 UIA 셀 SetValue가 모두 이 setter를 지난다.
/// </summary>
sealed class GridRow(string number, int cellCount) : INotifyPropertyChanged
{
    readonly string[] _cells = Enumerable.Repeat("", cellCount).ToArray();

    public string Number { get; } = number;

    public int Count => _cells.Length;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>(열, 새 값).</summary>
    public event Action<int, string>? CellChanged;

    public string this[int column]
    {
        get => _cells[column];
        set
        {
            value ??= "";
            if (_cells[column] == value)
            {
                return;
            }

            _cells[column] = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            CellChanged?.Invoke(column, value);
        }
    }

    /// <summary>불러오기: 화면은 바꾸되 CellChanged는 내지 않는다.</summary>
    public void Load(IReadOnlyList<string> values)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i] = i < values.Count ? values[i] ?? "" : "";
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
