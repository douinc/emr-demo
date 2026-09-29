namespace EmrDemo.App;

sealed record GridColumn(string Header, int Width, string Property);

static class GridMetrics
{
    public const int HeaderHeight = 20;
    public const int RowHeight = 20;
    const int MinColumnWidth = 24;

    /// <summary>
    /// 두 모드가 같은 열 위치를 갖도록 표준·custom 그리드가 함께 쓰는 열 폭 계산.
    /// 남는 폭은 마지막 열에 주고, 모자라면 비율대로 줄인다.
    /// </summary>
    public static int[] FitWidths(IReadOnlyList<GridColumn> columns, int available)
    {
        var widths = columns.Select(c => c.Width).ToArray();
        var total = widths.Sum();
        if (available < total)
        {
            for (var i = 0; i < widths.Length; i++)
            {
                widths[i] = Math.Max(MinColumnWidth, widths[i] * available / total);
            }
        }

        widths[^1] = Math.Max(MinColumnWidth, available - widths[..^1].Sum());
        return widths;
    }
}

interface ITextField
{
    Control Control { get; }

    event Action<string>? Committed;

    string Value { get; }

    void Load(string value);

    void CommitPending();
}

interface IGridField
{
    Control Control { get; }

    event Action<int, int, string>? CellCommitted;

    void Load(IReadOnlyList<string[]> rows);

    void CommitPending();
}

sealed class StandardTextField : ITextField
{
    readonly TextBox _box;
    string _committed = "";
    bool _loading;

    public StandardTextField(string name, bool multiline)
    {
        _box = new TextBox
        {
            Name = name,
            AccessibleName = name,
            Multiline = multiline,
            AcceptsReturn = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Font,
            Dock = DockStyle.Fill,
        };
        _box.Leave += (_, _) => CommitPending();
        _box.TextChanged += (_, _) =>
        {
            if (!_loading && !_box.Focused)
            {
                CommitPending();
            }
        };
    }

    public Control Control => _box;
    public event Action<string>? Committed;
    public string Value => Normalize(_box.Text);

    public void Load(string value)
    {
        _loading = true;
        _box.Text = value.Replace("\n", "\r\n");
        _loading = false;
        _committed = Value;
    }

    public void CommitPending()
    {
        if (Value == _committed)
        {
            return;
        }

        _committed = Value;
        Committed?.Invoke(_committed);
    }

    static string Normalize(string text) => text.Replace("\r\n", "\n");
}

sealed class CustomTextField : ITextField
{
    readonly CustomTextBox _box;
    string _committed = "";

    public CustomTextField()
    {
        _box = new CustomTextBox { Dock = DockStyle.Fill };
        _box.Leave += (_, _) => CommitPending();
    }

    public Control Control => _box;
    public event Action<string>? Committed;
    public string Value => _box.Buffer.Text;

    public void Load(string value)
    {
        _box.SetText(value);
        _committed = Value;
    }

    public void CommitPending()
    {
        if (Value == _committed)
        {
            return;
        }

        _committed = Value;
        Committed?.Invoke(_committed);
    }
}

sealed class StandardGridField : IGridField
{
    readonly DataGridView _grid;
    bool _loading;

    public StandardGridField(string name, IReadOnlyList<GridColumn> columns, bool readOnly)
    {
        _grid = new DataGridView
        {
            Name = name,
            AccessibleName = name,
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            ReadOnly = readOnly,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Theme.CellLine,
            Font = Theme.Font,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = GridMetrics.HeaderHeight,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.TableHead;
        _grid.RowTemplate.Height = GridMetrics.RowHeight;
        foreach (var column in columns)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = column.Property,
                HeaderText = column.Header,
                Width = column.Width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            });
        }

        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Resize += (_, _) => FitColumns(columns);
        _grid.VisibleChanged += (_, _) => ClearCurrentCell();
        _grid.CellValueChanged += (_, e) =>
        {
            if (!_loading && e.RowIndex >= 0)
            {
                var value = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string ?? "";
                CellCommitted?.Invoke(e.RowIndex, e.ColumnIndex, value);
            }
        };
    }

    public Control Control => _grid;
    public event Action<int, int, string>? CellCommitted;

    public void Load(IReadOnlyList<string[]> rows)
    {
        _loading = true;
        _grid.Rows.Clear();
        foreach (var row in rows)
        {
            _grid.Rows.Add(row.Cast<object>().ToArray());
        }

        _loading = false;
        ClearCurrentCell();
    }

    void FitColumns(IReadOnlyList<GridColumn> columns)
    {
        var widths = GridMetrics.FitWidths(columns, _grid.ClientSize.Width - 3);
        for (var i = 0; i < widths.Length; i++)
        {
            _grid.Columns[i].Width = widths[i];
        }
    }

    void ClearCurrentCell()
    {
        if (!_grid.IsCurrentCellInEditMode)
        {
            _grid.CurrentCell = null;
            _grid.ClearSelection();
        }
    }

    public void CommitPending()
    {
        if (_grid.IsCurrentCellInEditMode)
        {
            _grid.EndEdit();
        }
    }
}

sealed class CustomGridField : IGridField
{
    readonly CustomGrid _grid;

    public CustomGridField(IReadOnlyList<GridColumn> columns)
    {
        _grid = new CustomGrid(columns) { Dock = DockStyle.Fill };
        _grid.CellCommitted += (row, column, value) => CellCommitted?.Invoke(row, column, value);
    }

    public Control Control => _grid;
    public event Action<int, int, string>? CellCommitted;

    public void Load(IReadOnlyList<string[]> rows) => _grid.Load(rows);

    public void CommitPending() => _grid.CommitEdit();
}
