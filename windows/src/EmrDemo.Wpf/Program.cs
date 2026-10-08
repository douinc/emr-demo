using System.IO;
using System.Security.Principal;
using System.Windows;
using EmrDemo.Core;

namespace EmrDemo.Wpf;

static class Program
{
    const string Caption = "EmrDemoWpf";

    [STAThread]
    static int Main(string[] args)
    {
        KoreanCulture.Apply();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };

        AppOptions options;
        try
        {
            var defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmrDemoWpf");
            options = AppOptions.Parse(args, defaultDir);
        }
        catch (ArgumentException e)
        {
            MessageBox.Show(e.Message + "\n\n사용법: EmrDemoWpf.exe [--ui=standard] [--data-dir=<경로>]",
                Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }

        if (options.UiMode != UiMode.Standard)
        {
            MessageBox.Show("WPF판은 --ui=standard만 지원합니다.", Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }

        MainWindow window;
        try
        {
            window = new MainWindow(options, IsElevated());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException
                                      or System.Text.Json.JsonException)
        {
            MessageBox.Show($"저장소를 열 수 없습니다 ({options.DataDir}).\n\n{e.Message}",
                Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            return 3;
        }

        return app.Run(window);
    }

    static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
