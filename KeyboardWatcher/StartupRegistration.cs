using Microsoft.Win32;

namespace KeyboardWatcher;

static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyboardWatcher";

    public static void EnsureRegistered()
    {
        var exePath = Application.ExecutablePath;
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open startup registry key.");

        key.SetValue(ValueName, $"\"{exePath}\"");
    }
}
