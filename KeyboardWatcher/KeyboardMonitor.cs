using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace KeyboardWatcher;

sealed class KeyboardMonitor : NativeWindow, IDisposable
{
    private const string TargetKeyboardFilter = "VID_05AC&PID_0250";
    private const string PowerToysPath = @"C:\Users\Krisztian\AppData\Local\PowerToys\PowerToys.exe";
    private const string PowerToysRunnerProcessName = "PowerToys.Runner";

    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
    private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static readonly Guid GuidDevInterfaceHid = new("4D1E55B2-F16F-11CF-88CB-001111000030");

    private IntPtr _deviceNotificationHandle;
    private bool _targetWasConnected;
    private readonly System.Windows.Forms.Timer _debounceTimer;

    public KeyboardMonitor()
    {
        _debounceTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            EvaluateKeyboards();
        };

        CreateHandle(new CreateParams());
        RegisterForDeviceNotifications();
        EvaluateKeyboards();
    }

    private void ScheduleEvaluation()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void EvaluateKeyboards()
    {
        List<KeyboardDevice> keyboards;
        try
        {
            keyboards = QueryKeyboards();
        }
        catch (Exception)
        {
            ScheduleEvaluation();
            return;
        }

        var targetConnected = keyboards.Any(kb => MatchesFilter(kb));

        if (targetConnected && !_targetWasConnected)
            StartPowerToys();
        else if (!targetConnected && _targetWasConnected)
            KillPowerToysRunner();

        _targetWasConnected = targetConnected;
    }

    private static bool MatchesFilter(KeyboardDevice kb) =>
        kb.Name.Contains(TargetKeyboardFilter, StringComparison.OrdinalIgnoreCase)
        || kb.Description.Contains(TargetKeyboardFilter, StringComparison.OrdinalIgnoreCase)
        || kb.DeviceId.Contains(TargetKeyboardFilter, StringComparison.OrdinalIgnoreCase);

    private static List<KeyboardDevice> QueryKeyboards()
    {
        List<KeyboardDevice>? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = QueryKeyboardsCore();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10));

        if (error != null)
            throw error;

        return result ?? [];
    }

    private static List<KeyboardDevice> QueryKeyboardsCore()
    {
        const string query =
            "SELECT Name, DeviceID, Description FROM Win32_PnPEntity " +
            "WHERE PNPClass='Keyboard' OR Name LIKE '%Keyboard%'";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var results = new List<KeyboardDevice>();
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
                using var collection = searcher.Get();

                foreach (ManagementObject device in collection)
                {
                    results.Add(new KeyboardDevice(
                        device["Name"]?.ToString() ?? "",
                        device["Description"]?.ToString() ?? "",
                        device["DeviceID"]?.ToString() ?? ""));
                }

                return results.OrderBy(k => k.DeviceId).ToList();
            }
            catch (Exception) when (attempt < 3)
            {
                Thread.Sleep(500);
            }
        }

        throw new InvalidOperationException("WMI keyboard query failed after retries.");
    }

    private sealed record KeyboardDevice(string Name, string Description, string DeviceId);

    private static void StartPowerToys()
    {
        if (!File.Exists(PowerToysPath))
            return;

        if (Process.GetProcessesByName("PowerToys").Length > 0)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = PowerToysPath,
            UseShellExecute = true
        });
    }

    private static void KillPowerToysRunner()
    {
        foreach (var process in Process.GetProcessesByName(PowerToysRunnerProcessName))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }

    private void RegisterForDeviceNotifications()
    {
        var filter = new DevBroadcastDeviceInterface
        {
            dbcc_size = Marshal.SizeOf<DevBroadcastDeviceInterface>(),
            dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE,
            dbcc_classguid = GuidDevInterfaceHid
        };

        var filterPtr = Marshal.AllocHGlobal(filter.dbcc_size);
        try
        {
            Marshal.StructureToPtr(filter, filterPtr, false);
            _deviceNotificationHandle = RegisterDeviceNotification(Handle, filterPtr, DEVICE_NOTIFY_WINDOW_HANDLE);
        }
        finally
        {
            Marshal.FreeHGlobal(filterPtr);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DEVICECHANGE)
        {
            var eventType = m.WParam.ToInt32();
            if (eventType is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE)
                ScheduleEvaluation();
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _debounceTimer.Dispose();

        if (_deviceNotificationHandle != IntPtr.Zero)
        {
            UnregisterDeviceNotification(_deviceNotificationHandle);
            _deviceNotificationHandle = IntPtr.Zero;
        }

        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevBroadcastDeviceInterface
    {
        public int dbcc_size;
        public int dbcc_devicetype;
        public int dbcc_reserved;
        public Guid dbcc_classguid;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterDeviceNotification(IntPtr handle);
}
