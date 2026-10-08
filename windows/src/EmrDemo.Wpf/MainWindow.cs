using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EmrDemo.Core;
using EmrDemo.Core.Forms;

namespace EmrDemo.Wpf;

/// <summary>WinForms판 MainForm과 같은 화면·동작을 표준 WPF 컨트롤로 만든다.</summary>
sealed class MainWindow : Window
{
    const double ClientWidth = 1540;
    const double ClientHeight = 875;

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

    readonly TextField _chart = new("chartText");
    readonly FormView _formView = new();
    readonly TextField _medicalMemo = new("medicalMemo");
    readonly TextField _patientMemo = new("patientMemo");
    readonly GridField _vitals = new("vitalGrid", VitalColumns, readOnly: false);
    readonly GridField _orders = new("orderGrid", OrderColumns, readOnly: false);
    readonly GridField _diagnoses = new("diagnosisGrid", DiagnosisColumns, readOnly: true);

    readonly ListBox _patientList = new();
    readonly TextBlock _patientNo = new();
    readonly TextBlock _patientName = new();
    readonly TextBlock _department = new();
    readonly TextBlock _visitDate = new();
    readonly TextBlock _chartTitle = new();
    readonly TextBlock _savedStatus = new();
    readonly Grid _root = new();
    Grid _main = null!;
    FrameworkElement _midColumn = null!;

    Patient _current = new();

    public MainWindow(AppOptions options, bool elevated)
    {
        _options = options;
        _elevated = elevated;
        _store = new EmrStore(options.DataDir);
        _log = new EventLog(options.DataDir, options.UiMode, elevated);
        _roster = _store.LoadAll();

        FontFamily = Theme.Font;
        FontSize = Theme.FontSize;
        Background = Theme.Face;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        // 처음에는 내용 크기(= WinForms판 ClientSize)로 창을 맞추고, 뜬 뒤에는 창 크기를 따르게 푼다.
        SizeToContent = SizeToContent.WidthAndHeight;
        _root.Width = ClientWidth;
        _root.Height = ClientHeight;
        Loaded += (_, _) => ReleaseInitialSize();

        BuildLayout();
        WireFields();
        InputBindings.Add(new KeyBinding(new ActionCommand(Save), Key.S, ModifierKeys.Control));

        _patientList.SelectedIndex = 0;
    }

    void ReleaseInitialSize()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        SizeToContent = SizeToContent.Manual;
        Width = width;
        Height = height;
        MinWidth = width - (ClientWidth - 1100);
        MinHeight = height - (ClientHeight - 640);
        _root.Width = double.NaN;
        _root.Height = double.NaN;
    }

    void BuildLayout()
    {
        var dock = new DockPanel { LastChildFill = true };
        Docked(dock, BuildMenu(), Dock.Top);
        Docked(dock, BuildHeader(), Dock.Top);
        Docked(dock, BuildTabStrip(), Dock.Top);
        Docked(dock, BuildStatus(), Dock.Bottom);
        dock.Children.Add(BuildMain());
        _root.Children.Add(dock);
        Content = _root;
    }

    static void Docked(DockPanel dock, UIElement element, Dock side)
    {
        DockPanel.SetDock(element, side);
        dock.Children.Add(element);
    }

    Grid BuildMain()
    {
        var main = _main = new Grid { Margin = new Thickness(2, 3, 2, 3), Background = Theme.Face };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45, GridUnitType.Star) });
        InColumn(main, BuildPatientColumn(), 0);
        InColumn(main, BuildChartColumn(), 1);
        _midColumn = BuildMidColumn();
        InColumn(main, _midColumn, 2);
        InColumn(main, BuildOrderColumn(), 3);
        return main;
    }

    static void InColumn(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    Menu BuildMenu()
    {
        var menu = new Menu { Background = Theme.Face, Padding = new Thickness(2, 0, 0, 0) };
        var file = new MenuItem { Header = "File" };
        var exit = new MenuItem { Header = "종료" };
        exit.Click += (_, _) => Close();
        file.Items.Add(exit);
        menu.Items.Add(file);
        foreach (var name in new[] { "진료환자정보", "기본기능", "추가기능", "보조기능", "Clinical Pathway", "간호업무",
                     "상담업무", "진료지원자료조회", "통계", "설정" })
        {
            menu.Items.Add(new MenuItem { Header = name });
        }

        return menu;
    }

    FrameworkElement BuildHeader()
    {
        var header = new StackPanel { Height = 64, Margin = new Thickness(4, 3, 0, 0), Background = Theme.Face };
        header.Children.Add(HeaderRow(("환자 번호", Value(_patientNo, Theme.Cream)), ("진 료 과", Value(_department, Brushes.White)),
            ("내원구분", Fixed("외래", Theme.Gray)), ("초 / 재진", Fixed("초진", Theme.Gray)),
            ("환자유형", Fixed("일반", Theme.Mint))));
        header.Children.Add(HeaderRow(("환자 성명", Value(_patientName, Theme.Cream)), ("진 료 일자", Value(_visitDate, Theme.Cream)),
            ("HD/POD", Fixed("외래 / 0", Theme.Cream)), ("선 / 후불", Fixed("후불", Theme.Gray)),
            ("원외조제", Fixed("원외조제", Theme.Mint))));
        header.Children.Add(new TextBlock
        {
            Text = "[합성 데모 데이터]",
            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Margin = new Thickness(0, 2, 0, 0),
        });
        return header;
    }

    static StackPanel HeaderRow(params (string Label, Border Value)[] fields)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        foreach (var (label, value) in fields)
        {
            row.Children.Add(Boxed(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center }, 66,
                Theme.Label, new Thickness(0, 0, 2, 0)));
            row.Children.Add(value);
        }

        return row;
    }

    static Border Value(TextBlock text, Brush back)
    {
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Margin = new Thickness(2, 0, 2, 0);
        return Boxed(text, 84, back, new Thickness(0, 0, 8, 0));
    }

    static Border Fixed(string text, Brush back) => Value(new TextBlock { Text = text }, back);

    static Border Boxed(TextBlock text, double width, Brush back, Thickness margin)
    {
        text.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Child = text,
            Width = width,
            Height = 18,
            Background = back,
            BorderBrush = Theme.Line,
            BorderThickness = new Thickness(1),
            Margin = margin,
        };
    }

    static FrameworkElement BuildTabStrip()
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Height = 22, Margin = new Thickness(16, 2, 0, 0), Background = Theme.Face };
        strip.Children.Add(Tab("새록 인터페이스", Theme.Face2, new Thickness(0, 0, 2, 0)));
        strip.Children.Add(Tab("차트 / 슬립", Brushes.White, new Thickness(0)));
        return strip;
    }

    static Border Tab(string text, Brush back, Thickness margin) => new()
    {
        Child = new TextBlock { Text = text, Margin = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center },
        Background = back,
        BorderBrush = Theme.Line,
        BorderThickness = new Thickness(1),
        Margin = margin,
    };

    FrameworkElement BuildPatientColumn()
    {
        AutomationProperties.SetAutomationId(_patientList, "patientList");
        AutomationProperties.SetName(_patientList, "patientList");
        _patientList.BorderBrush = Theme.Line;
        _patientList.BorderThickness = new Thickness(1);
        ScrollViewer.SetHorizontalScrollBarVisibility(_patientList, ScrollBarVisibility.Disabled);
        for (var i = 0; i < _roster.Count; i++)
        {
            var item = new ListBoxItem
            {
                Content = PatientVisual(_roster[i], i),
                Height = 34,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(item, PatientLabel.For(_roster[i]));
            _patientList.Items.Add(item);
        }

        _patientList.SelectionChanged += (_, _) =>
        {
            if (_patientList.SelectedIndex >= 0)
            {
                SwitchPatient(_roster[_patientList.SelectedIndex].Id);
            }
        };

        return Stack((SectionLabel("진료기록 목록"), 20), (_patientList, 0));
    }

    /// <summary>두 줄 항목(WinForms판 DrawPatientItem): 번호·이름과 진료과, 아래 줄에 환자 번호.</summary>
    static FrameworkElement PatientVisual(Patient patient, int index)
    {
        var grid = new Grid { Margin = new Thickness(6, 2, 6, 0) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        grid.Children.Add(new TextBlock { Text = $"{index + 1}. {patient.Name}", FontWeight = FontWeights.Bold });
        grid.Children.Add(new TextBlock { Text = patient.Department, HorizontalAlignment = HorizontalAlignment.Right });
        var id = new TextBlock { Text = patient.Id };
        Grid.SetRow(id, 1);
        grid.Children.Add(id);
        return new Border
        {
            Child = grid,
            BorderBrush = Theme.Line2,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    FrameworkElement BuildChartColumn()
    {
        // 차트와 서식은 같은 칸에 겹쳐 두고 보이는 쪽만 바꾼다(접힌 쪽은 UIA 트리에 없다).
        var host = new Grid();
        host.Children.Add(_chart.Element);
        host.Children.Add(_formView.Element);
        _formView.Element.Visibility = Visibility.Collapsed;
        return Stack((SectionLabel(_chartTitle), 20), (host, 0));
    }

    void SetFormMode(bool formMode)
    {
        _chart.Element.Visibility = formMode ? Visibility.Collapsed : Visibility.Visible;
        _formView.Element.Visibility = formMode ? Visibility.Visible : Visibility.Collapsed;
        _main.ColumnDefinitions[2].Width = new GridLength(formMode ? 0 : 280);
        _midColumn.Visibility = formMode ? Visibility.Collapsed : Visibility.Visible;
        _main.ColumnDefinitions[1].Width = new GridLength(formMode ? 70 : 55, GridUnitType.Star);
        _main.ColumnDefinitions[3].Width = new GridLength(formMode ? 30 : 45, GridUnitType.Star);
    }

    FrameworkElement BuildMidColumn() => Stack(
        (SectionLabel("VitalSign"), 20),
        (_vitals.Element, GridHeight(Patient.VitalRowCount)),
        (SectionLabel("상병정보"), 20),
        (_diagnoses.Element, 80),
        (SectionLabel("메디칼메모"), 20),
        (_medicalMemo.Element, 70),
        (SectionLabel("환자메모"), 20),
        (_patientMemo.Element, 70),
        (new Border(), 0));

    FrameworkElement BuildOrderColumn()
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        buttons.Children.Add(ActionButton("resetButton", "초기화", ResetPatient));
        buttons.Children.Add(ActionButton("refreshButton", "새로고침", () => SwitchPatient(_current.Id)));
        buttons.Children.Add(ActionButton("saveButton", "저장", Save));
        return Stack(
            (SectionLabel("처방  [ Routine Order ]"), 20),
            (_orders.Element, GridHeight(Patient.OrderRowCount)),
            (buttons, 44),
            (new Border(), 0));
    }

    static int GridHeight(int rows) => FormLayout.GridRowHeight * (rows + 1) + 8;

    static Button ActionButton(string automationId, string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Width = 84,
            Height = 32,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetAutomationId(button, automationId);
        button.Click += (_, _) => action();
        return button;
    }

    static Border SectionLabel(string text) => SectionLabel(new TextBlock { Text = text });

    static Border SectionLabel(TextBlock text)
    {
        text.Margin = new Thickness(4, 0, 0, 0);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        return new Border
        {
            Child = text,
            Background = Theme.Face2,
            BorderBrush = Theme.Line,
            BorderThickness = new Thickness(1),
        };
    }

    static Grid Stack(params (FrameworkElement Element, int Height)[] items)
    {
        var stack = new Grid { Margin = new Thickness(2, 0, 2, 0), Background = Theme.Face };
        for (var i = 0; i < items.Length; i++)
        {
            var (element, height) = items[i];
            stack.RowDefinitions.Add(new RowDefinition
            {
                Height = height > 0 ? new GridLength(height) : new GridLength(1, GridUnitType.Star),
            });
            element.Margin = new Thickness(0, 0, 0, height > 0 && height != 20 ? 3 : 0);
            Grid.SetRow(element, i);
            stack.Children.Add(element);
        }

        return stack;
    }

    StatusBar BuildStatus()
    {
        var status = new StatusBar
        {
            Background = Theme.Face2,
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(DockPanel))),
        };
        status.Items.Add(StatusItem(new TextBlock { Text = $"UI 모드: {_options.UiMode.ToString().ToLowerInvariant()}" }));
        status.Items.Add(StatusItem(new TextBlock { Text = _elevated ? "권한: 관리자" : "권한: 일반" }));
        status.Items.Add(StatusItem(new TextBlock { Text = $"데이터: {_options.DataDir}" }));
        status.Items.Add(new StatusBarItem { Content = _savedStatus, HorizontalContentAlignment = HorizontalAlignment.Right });
        return status;
    }

    static StatusBarItem StatusItem(TextBlock text)
    {
        var item = new StatusBarItem { Content = text };
        DockPanel.SetDock(item, Dock.Left);
        return item;
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

    IEnumerable<TextField> TextFields => [_chart, _medicalMemo, _patientMemo];
    IEnumerable<GridField> EditableGrids => [_vitals, _orders];

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
        if (string.IsNullOrEmpty(_savedStatus.Text))
        {
            _savedStatus.Text = "초기화됨 (저장 전)";
        }
    }

    void ShowPatient(Patient patient)
    {
        _current = patient;
        _savedStatus.Text = "";
        var index = _roster.FindIndex(p => p.Id == patient.Id);
        Title = $"[접속정보 : Tester1] 처방관리 Ver. 2.0.0.122 : [ 예시 환자 {index + 1:00} ]" + (_elevated ? " (관리자)" : "");
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

    static List<string[]> Rows<T>(IEnumerable<T> items, IReadOnlyList<GridColumn> columns) =>
        items.Select(item => columns.Select(c => (string)Property(typeof(T), c.Property).GetValue(item)!).ToArray()).ToList();

    static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name) ?? throw new InvalidOperationException($"{type.Name}.{name}");
}
