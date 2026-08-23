namespace KeyboardWatcher;

static class Program
{
    private const string SingleInstanceMutexName = "KeyboardWatcher_SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
            return;

        ApplicationConfiguration.Initialize();
        StartupRegistration.EnsureRegistered();
        Application.Run(new TrayApplicationContext());
    }
}
