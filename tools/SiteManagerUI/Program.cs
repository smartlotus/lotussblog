namespace SiteManagerUI;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.ThreadException += (_, e) => ShowFatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatalError(e.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            var root = Environment.GetEnvironmentVariable("SITE_MANAGER_ROOT");
            var logDir = !string.IsNullOrWhiteSpace(root)
                ? Path.Combine(root, "logs")
                : Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "site-manager-ui-crash.log");
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n";
            File.AppendAllText(logPath, content);

            MessageBox.Show(
                "SiteManagerUI failed to start.\n\n" +
                $"Error: {ex.Message}\n\n" +
                $"Log: {logPath}\n\n" +
                "Please send this message to support.",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            MessageBox.Show(
                $"SiteManagerUI failed to start: {ex.Message}",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
