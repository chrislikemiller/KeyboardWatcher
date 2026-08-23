namespace KeyboardWatcher;

sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly KeyboardMonitor _keyboardMonitor;

    public TrayApplicationContext()
    {
        _keyboardMonitor = new KeyboardMonitor();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "KeyboardWatcher",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        _trayIcon.ContextMenuStrip!.Items.Add("Exit", null, (_, _) => ExitThread());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _keyboardMonitor.Dispose();
        }

        base.Dispose(disposing);
    }
}
