using System.Security.Principal;
using EmrDemo.Core;

namespace EmrDemo.App;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        AppOptions options;
        try
        {
            var defaultDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmrDemo");
            options = AppOptions.Parse(args, defaultDir);
        }
        catch (ArgumentException e)
        {
            MessageBox.Show(e.Message + "\n\n사용법: EmrDemo.exe [--ui=standard|custom] [--data-dir=<경로>]",
                "EmrDemo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        MainForm form;
        try
        {
            form = new MainForm(options, IsElevated());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException
                                      or System.Text.Json.JsonException)
        {
            MessageBox.Show($"저장소를 열 수 없습니다 ({options.DataDir}).\n\n{e.Message}",
                "EmrDemo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 3;
        }

        Application.Run(form);
        return 0;
    }

    static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
