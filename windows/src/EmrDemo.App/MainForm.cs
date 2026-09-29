using System.Reflection;
using EmrDemo.Core;
using EmrDemo.Core.Forms;

namespace EmrDemo.App;

sealed class MainForm : Form
{
    static readonly GridColumn[] VitalColumns =
    [
        new("측정일", 74, nameof(VitalSign.Date)),
        new("시간", 44, nameof(VitalSign.Time)),
        new("SBP", 38, nameof(VitalSign.Sbp)),
        new("DBP", 38, nameof(VitalSign.Dbp)),
        new("맥박", 38, nameof(VitalSign.Pulse)),
        new("체온", 38, nameof(VitalSign.Temp)),
    ];

    static readonly GridColumn[] DiagnosisColumns =
    [
        new("발행일", 74, nameof(Diagnosis.Date)),
        new("코드", 46, nameof(Diagnosis.Code)),
        new("상병명", 100, nameof(Diagnosis.Name)),
    ];

    static readonly GridColumn[] OrderColumns =
    [
        new("처방명", 210, nameof(Order.Name)),
        new("용량", 50, nameof(Order.Dose)),
        new("횟수", 44, nameof(Order.Frequency)),
        new("일수", 44, nameof(Order.Days)),
        new("용법", 80, nameof(Order.Route)),
    ];

    readonly AppOptions _options;
    readonly bool _elevated;
    readonly EmrStore _store;
    readonly EventLog _log;
    readonly List<Patient> _roster;

    readonly ITextField _chart;
    readonly IFormView _formView;
    readonly Panel _chartHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    TableLayoutPanel _main = null!;
    Control _midColumn = null!;
    readonly ITextField _medicalMemo;
    readonly ITextField _patientMemo;
    readonly IGridField _vitals;
    readonly IGridField _orders;
    readonly StandardGridField _diagnoses;

    readonly ListBox _patientList = new();
    readonly Label _patientNo = HeaderValue(Theme.Cream);
    readonly Label _patientName = HeaderValue(Theme.Cream);
    readonly Label _department = HeaderValue(Color.White);
    readonly Label _visitDate = HeaderValue(Theme.Cream);
    readonly Label _chartTitle = SectionLabel("");
    readonly ToolStripStatusLabel _savedStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };

    Patient _current = new();

    public MainForm(AppOptions options, bool elevated)
    {
        _options = options;
        _elevated = elevated;
        _store = new EmrStore(options.DataDir);
        _log = new EventLog(options.DataDir, options.UiMode, elevated);
        _roster = _store.LoadAll();

        var custom = options.UiMode == UiMode.Custom;
        _chart = custom ? new CustomTextField() : new StandardTextField("chartText", multiline: true);
        _formView = custom ? new CustomFormCanvas() : new StandardFormView();
        _vitals = custom ? new CustomGridField(VitalColumns) : new StandardGridField("vitalGrid", VitalColumns, readOnly: false);
        _orders = custom ? new CustomGridField(OrderColumns) : new StandardGridField("orderGrid", OrderColumns, readOnly: false);
        _medicalMemo = new StandardTextField("medicalMemo", multiline: true);
        _patientMemo = new StandardTextField("patientMemo", multiline: true);
        _diagnoses = new StandardGridField("diagnosisGrid", DiagnosisColumns, readOnly: true);

        Font = Theme.Font;
        BackColor = Theme.Face;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(1540, 875);
        MinimumSize = new Size(1100, 640);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);

        BuildLayout();
        WireFields();

        _patientList.SelectedIndex = 0;
    }

    void BuildLayout()
    {
        var main = _main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(2, 3, 2, 3),
            BackColor = Theme.Face,
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        main.Controls.Add(BuildPatientColumn(), 0, 0);
        main.Controls.Add(BuildChartColumn(), 1, 0);
        _midColumn = BuildMidColumn();
        main.Controls.Add(_midColumn, 2, 0);
        main.Controls.Add(BuildOrderColumn(), 3, 0);

        Controls.Add(main);
        Controls.Add(BuildTabStrip());
        Controls.Add(BuildHeader());
        Controls.Add(BuildMenu());
        Controls.Add(BuildStatus());
    }

    MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { BackColor = Theme.Face, Font = Theme.Font, Padding = new Padding(2, 0, 0, 0) };
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("종료", null, (_, _) => Close());
        menu.Items.Add(file);
        foreach (var name in new[] { "진료환자정보", "기본기능", "추가기능", "보조기능", "Clinical Pathway", "간호업무",
                     "상담업무", "진료지원자료조회", "통계", "설정" })
        {
            menu.Items.Add(new ToolStripMenuItem(name));
        }

        MainMenuStrip = menu;
        return menu;
    }

    Control BuildHeader()
    {
        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4, 3, 0, 0),
            BackColor = Theme.Face,
        };
        header.Controls.Add(HeaderRow(("환자 번호", _patientNo), ("진 료 과", _department),
            ("내원구분", Fixed("외래", Theme.Gray)), ("초 / 재진", Fixed("초진", Theme.Gray)),
            ("환자유형", Fixed("일반", Theme.Mint))));
        header.Controls.Add(HeaderRow(("환자 성명", _patientName), ("진 료 일자", _visitDate),
            ("HD/POD", Fixed("외래 / 0", Theme.Cream)), ("선 / 후불", Fixed("후불", Theme.Gray)),
            ("원외조제", Fixed("원외조제", Theme.Mint))));
        header.Controls.Add(new Label
        {
            Text = "[합성 데모 데이터]",
            AutoSize = true,
            ForeColor = Color.FromArgb(0x33, 0x33, 0x33),
            Margin = new Padding(0, 2, 0, 0),
        });
        return header;
    }

    static FlowLayoutPanel HeaderRow(params (string Label, Label Value)[] fields)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 2) };
        foreach (var (label, value) in fields)
        {
            row.Controls.Add(new Label
            {
                Text = label,
                Size = new Size(66, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.Label,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 2, 0),
            });
            row.Controls.Add(value);
        }

        return row;
    }

    static Label HeaderValue(Color back) => new()
    {
        Size = new Size(84, 18),
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = back,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 0, 8, 0),
        AutoEllipsis = true,
    };

    static Label Fixed(string text, Color back)
    {
        var label = HeaderValue(back);
        label.Text = text;
        return label;
    }

    static Control BuildTabStrip()
    {
        var strip = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 22, Padding = new Padding(16, 2, 0, 0), BackColor = Theme.Face };
        strip.Controls.Add(new Label { Text = "새록 인터페이스", AutoSize = true, BackColor = Theme.Face2, Padding = new Padding(6, 2, 6, 2), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 2, 0) });
        strip.Controls.Add(new Label { Text = "차트 / 슬립", AutoSize = true, BackColor = Color.White, Padding = new Padding(6, 2, 6, 2), BorderStyle = BorderStyle.FixedSingle });
        return strip;
    }

    Control BuildPatientColumn()
    {
        _patientList.Name = "patientList";
        _patientList.AccessibleName = "patientList";
        _patientList.Dock = DockStyle.Fill;
        _patientList.BorderStyle = BorderStyle.FixedSingle;
        _patientList.IntegralHeight = false;
        _patientList.ItemHeight = 34;
        _patientList.DrawMode = DrawMode.OwnerDrawFixed;
        foreach (var patient in _roster)
        {
            _patientList.Items.Add($"{patient.Id}  {patient.Name}  {patient.Department}");
        }

        _patientList.DrawItem += DrawPatientItem;
        _patientList.SelectedIndexChanged += (_, _) =>
        {
            if (_patientList.SelectedIndex >= 0)
            {
                SwitchPatient(_roster[_patientList.SelectedIndex].Id);
            }
        };

        return Stack((SectionLabel("진료기록 목록"), 20), (_patientList, 0));
    }

    void DrawPatientItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? Theme.Selection : Color.White);
        e.Graphics.FillRectangle(back, e.Bounds);
        var patient = _roster[e.Index];
        var fore = selected ? Color.White : Color.Black;
        var top = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 12, 15);
        TextRenderer.DrawText(e.Graphics, $"{e.Index + 1}. {patient.Name}", Theme.Bold, top, fore, TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics, patient.Department, Theme.Font, top, fore, TextFormatFlags.Right);
        var bottom = top with { Y = top.Y + 15 };
        TextRenderer.DrawText(e.Graphics, patient.Id, Theme.Font, bottom, fore, TextFormatFlags.Left);
        using var line = new Pen(Theme.Line2);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    Control BuildChartColumn()
    {
        _chart.Control.Dock = DockStyle.Fill;
        _formView.Control.Dock = DockStyle.Fill;
        _formView.Control.Visible = false;
        _chartHost.Controls.Add(_chart.Control);
        _chartHost.Controls.Add(_formView.Control);
        return Stack((_chartTitle, 20), (_chartHost, 0));
    }

    /// <summary>웹 데모의 form-mode처럼 서식을 띄울 때는 중간 열을 숨겨 가운데 칸을 넓힌다.</summary>
    void SetFormMode(bool formMode)
    {
        _main.SuspendLayout();
        _main.ColumnStyles[2].Width = formMode ? 0 : 280;
        _midColumn.Visible = !formMode;
        _main.ColumnStyles[1].Width = formMode ? 70 : 55;
        _main.ColumnStyles[3].Width = formMode ? 30 : 45;
        _chart.Control.Visible = !formMode;
        _formView.Control.Visible = formMode;
        _main.ResumeLayout();
    }

    Control BuildMidColumn() => Stack(
        (SectionLabel("VitalSign"), 20),
        (_vitals.Control, GridHeight(Patient.VitalRowCount)),
        (SectionLabel("상병정보"), 20),
        (_diagnoses.Control, 80),
        (SectionLabel("메디칼메모"), 20),
        (_medicalMemo.Control, 70),
        (SectionLabel("환자메모"), 20),
        (_patientMemo.Control, 70),
        (new Panel(), 0));

    Control BuildOrderColumn()
    {
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0), BackColor = Theme.Face };
        buttons.Controls.Add(ActionButton("resetButton", "초기화", ResetPatient));
        buttons.Controls.Add(ActionButton("refreshButton", "새로고침", () => SwitchPatient(_current.Id)));
        buttons.Controls.Add(ActionButton("saveButton", "저장", Save));
        return Stack(
            (SectionLabel("처방  [ Routine Order ]"), 20),
            (_orders.Control, GridHeight(Patient.OrderRowCount)),
            (buttons, 44),
            (new Panel(), 0));
    }

    static int GridHeight(int rows) => GridMetrics.HeaderHeight + GridMetrics.RowHeight * rows + 8;

    static Button ActionButton(string name, string text, Action action)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            Size = new Size(84, 32),
            FlatStyle = FlatStyle.System,
            Margin = new Padding(0, 0, 4, 0),
        };
        button.Click += (_, _) => action();
        return button;
    }

    static Label SectionLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Theme.Face2,
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(4, 0, 0, 0),
        Margin = new Padding(0),
    };

    static TableLayoutPanel Stack(params (Control Control, int Height)[] items)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = items.Length,
            Margin = new Padding(2, 0, 2, 0),
            BackColor = Theme.Face,
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < items.Length; i++)
        {
            var (control, height) = items[i];
            stack.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.Percent, 100));
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 0, 0, height > 0 && height != 20 ? 3 : 0);
            stack.Controls.Add(control, 0, i);
        }

        return stack;
    }

    StatusStrip BuildStatus()
    {
        var status = new StatusStrip { BackColor = Theme.Face2, SizingGrip = false };
        status.Items.Add(new ToolStripStatusLabel($"UI 모드: {_options.UiMode.ToString().ToLowerInvariant()}"));
        status.Items.Add(new ToolStripStatusLabel(_elevated ? "권한: 관리자" : "권한: 일반"));
        status.Items.Add(new ToolStripStatusLabel($"데이터: {_options.DataDir}"));
        status.Items.Add(_savedStatus);
        return status;
    }

    void WireFields()
    {
        _chart.Committed += value => CommitText(EmrFields.ChartText, value, p => p.ChartText = value);
        _formView.Committed += (key, value) =>
        {
            if (value.Length == 0)
            {
                _current.FormValues.Remove(key);
            }
            else
            {
                _current.FormValues[key] = value;
            }

            _log.FieldCommit(_current.Id, EmrFields.Form(key), value);
        };
        _medicalMemo.Committed += value => CommitText(EmrFields.MedicalMemo, value, p => p.MedicalMemo = value);
        _patientMemo.Committed += value => CommitText(EmrFields.PatientMemo, value, p => p.PatientMemo = value);
        _vitals.CellCommitted += (row, column, value) =>
            CommitCell(_current.Vitals[row], VitalColumns[column], EmrFields.Vital(row, VitalColumns[column].Property), value);
        _orders.CellCommitted += (row, column, value) =>
            CommitCell(_current.Orders[row], OrderColumns[column], EmrFields.Order(row, OrderColumns[column].Property), value);
    }

    void CommitText(string field, string value, Action<Patient> apply)
    {
        apply(_current);
        _log.FieldCommit(_current.Id, field, value);
    }

    void CommitCell(object row, GridColumn column, string field, string value)
    {
        var property = Property(row.GetType(), column.Property);
        if ((string?)property.GetValue(row) == value)
        {
            return;
        }

        property.SetValue(row, value);
        _log.FieldCommit(_current.Id, field, value);
    }

    IEnumerable<ITextField> TextFields => [_chart, _medicalMemo, _patientMemo];
    IEnumerable<IGridField> EditableGrids => [_vitals, _orders];

    void CommitAllPending()
    {
        foreach (var field in TextFields)
        {
            field.CommitPending();
        }

        foreach (var grid in EditableGrids)
        {
            grid.CommitPending();
        }

        if (_current.FormId is not null)
        {
            _formView.CommitPending();
        }
    }

    void SwitchPatient(string id)
    {
        if (_current.Id.Length > 0)
        {
            CommitAllPending();
        }

        ShowPatient(_store.Get(id));
    }

    void ResetPatient()
    {
        CommitAllPending();
        ShowPatient(EmrStore.SeedFor(_current.Id));
        _savedStatus.Text = "초기화됨 (저장 전)";
    }

    void ShowPatient(Patient patient)
    {
        _current = patient;
        _savedStatus.Text = "";
        var index = _roster.FindIndex(p => p.Id == patient.Id);
        Text = $"[접속정보 : Tester1] 처방관리 Ver. 2.0.0.122 : [ 예시 환자 {index + 1:00} ]" + (_elevated ? " (관리자)" : "");
        _patientNo.Text = patient.Id;
        _patientName.Text = patient.Name;
        _department.Text = patient.Department;
        _visitDate.Text = patient.VisitDate;
        _chartTitle.Text = patient.FormId is { } formId
            ? $"서식 : {FormCatalog.Get(formId).Title} - {patient.Name} ({patient.Id})"
            : $"차트 조회 : {patient.Name} ({patient.Id})";

        SetFormMode(patient.FormId is not null);
        if (patient.FormId is { } id)
        {
            var layout = _formView.Show(FormCatalog.Get(id), patient.FormValues);
            WriteLayout(id, layout);
        }

        _chart.Load(patient.ChartText);
        _medicalMemo.Load(patient.MedicalMemo);
        _patientMemo.Load(patient.PatientMemo);
        _vitals.Load(Rows(patient.Vitals, VitalColumns));
        _orders.Load(Rows(patient.Orders, OrderColumns));
        _diagnoses.Load(Rows(patient.Diagnoses, DiagnosisColumns));
    }

    void WriteLayout(string formId, FormLayoutResult layout)
    {
        try
        {
            FormLayoutFile.Write(_options.DataDir, formId, layout);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _savedStatus.Text = $"좌표 파일 기록 실패: {e.Message}";
        }
    }

    void Save()
    {
        CommitAllPending();
        try
        {
            _store.Save(_current);
            _log.Save(_current);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _savedStatus.Text = $"저장 실패: {e.Message}";
            return;
        }

        _savedStatus.Text = $"저장됨 {DateTime.Now:HH:mm:ss}";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            Save();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    static List<string[]> Rows<T>(IEnumerable<T> items, IReadOnlyList<GridColumn> columns) =>
        items.Select(item => columns.Select(c => (string)Property(typeof(T), c.Property).GetValue(item)!).ToArray()).ToList();

    static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name) ?? throw new InvalidOperationException($"{type.Name}.{name}");
}
