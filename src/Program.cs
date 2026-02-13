namespace BlackScreenSaver;

static class Program
{
    private const string MutexName = "Global\\BlackScreenSaverSingleInstance";

    [STAThread]
    static void Main()
    {
        // Enforce single instance
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Black Screen Saver is already running.",
                "Black Screen Saver", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
