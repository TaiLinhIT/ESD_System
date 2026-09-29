using ESD.Service;

namespace ESD.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "appsettings.json");
        Application.Run(new MainForm(configPath));
    }
}
