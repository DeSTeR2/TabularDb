using Microsoft.Extensions.Configuration;
using TabularDb.Desktop.Forms;
using TabularDb.Desktop.Services;

namespace TabularDb.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .Build();
        var settings = configuration.GetSection(ClientSettings.Section).Get<ClientSettings>() ?? new ClientSettings();

        while (true)
        {
            using var connect = new ConnectForm(settings);
            if (connect.ShowDialog() != DialogResult.OK || connect.Client is null || connect.DatabaseName is null)
                return;

            using var main = new MainWindow(connect.Client, connect.DatabaseName);
            Application.Run(main);
            connect.Client.Dispose();
            if (!main.SwitchRequested)
                return;
        }
    }
}
