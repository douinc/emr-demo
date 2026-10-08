using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EmrDemo.Core.Forms;

namespace EmrDemo.Wpf;

sealed record GridColumn(string Header, int Width, string Property);

/// <summary>
/// 바이탈·처방·상병 표(WinForms판 StandardGridField). 줄은 <see cref="GridRow"/>에 묶고, 셀 값이 실제로 바뀌면
/// CellCommitted(줄, 열, 값)를 낸다. 마지막 열이 남는 폭을 갖는다.
/// </summary>
sealed class GridField
{
    readonly DataGrid _grid;
    readonly int _columnCount;
    bool _loading;

    public GridField(string automationId, IReadOnlyList<GridColumn> columns, bool readOnly)
    {
        _columnCount = columns.Count;
        _grid = NewGrid(Theme.TableHead, Theme.CellLine);
        _grid.IsReadOnly = readOnly;
        _grid.BorderBrush = Theme.Line;
        _grid.BorderThickness = new Thickness(1);
        AutomationProperties.SetAutomationId(_grid, automationId);
        AutomationProperties.SetName(_grid, automationId);
        for (var i = 0; i < columns.Count; i++)
        {
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = columns[i].Header,
                Binding = new Binding($"[{i}]"),
                Width = i == columns.Count - 1
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(columns[i].Width),
                MinWidth = 24,
            });
        }
    }

    public FrameworkElement Element => _grid;

    /// <summary>(줄, 열, 값).</summary>
    public event Action<int, int, string>? CellCommitted;

    public void Load(IReadOnlyList<string[]> rows)
    {
        _loading = true;
        _grid.CancelEdit(DataGridEditingUnit.Row);
        var items = new List<GridRow>();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = new GridRow((r + 1).ToString(), _columnCount);
            row.Load(rows[r]);
            var index = r;
            row.CellChanged += (column, value) =>
            {
                if (!_loading)
                {
                    CellCommitted?.Invoke(index, column, value);
                }
            };
            items.Add(row);
        }

        _grid.ItemsSource = items;
        _grid.UnselectAllCells();
        _loading = false;
    }

    public void CommitPending() => _grid.CommitEdit(DataGridEditingUnit.Row, true);

    /// <summary>표 공통 모양: 열 머리글만, 정렬·크기 조절·줄 추가·삭제 없음, 셀 하나씩 선택.</summary>
    public static DataGrid NewGrid(Brush header, Brush lines) => new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        CanUserResizeRows = false,
        CanUserResizeColumns = false,
        CanUserReorderColumns = false,
        CanUserSortColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.All,
        HorizontalGridLinesBrush = lines,
        VerticalGridLinesBrush = lines,
        Background = Brushes.White,
        RowBackground = Brushes.White,
        SelectionUnit = DataGridSelectionUnit.Cell,
        SelectionMode = DataGridSelectionMode.Single,
        RowHeight = FormLayout.GridRowHeight,
        ColumnHeaderHeight = FormLayout.GridRowHeight,
        ColumnHeaderStyle = HeaderStyle(header, lines),
    };

    static Style HeaderStyle(Brush background, Brush lines)
    {
        var style = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.BackgroundProperty, background));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, lines));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2, 0, 2, 0)));
        return style;
    }
}
