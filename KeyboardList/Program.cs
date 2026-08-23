using System.Management;

const string query =
    "SELECT Name, DeviceID, Description FROM Win32_PnPEntity " +
    "WHERE PNPClass='Keyboard' OR Name LIKE '%Keyboard%'";

Console.WriteLine("Keyboard devices (same WMI query as KeyboardWatcher):");
Console.WriteLine(query);
Console.WriteLine(new string('-', 72));

using var searcher = new ManagementObjectSearcher(query);
using var collection = searcher.Get();

var index = 0;
var filterCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (ManagementObject device in collection)
{
    index++;
    var name = device["Name"]?.ToString() ?? "";
    var deviceId = device["DeviceID"]?.ToString() ?? "";
    var description = device["Description"]?.ToString() ?? "";

    Console.WriteLine($"[{index}]");
    Console.WriteLine($"  Name        : {(string.IsNullOrWhiteSpace(name) ? "(empty)" : name)}");
    Console.WriteLine($"  Description : {(string.IsNullOrWhiteSpace(description) ? "(empty)" : description)}");
    Console.WriteLine($"  DeviceID    : {(string.IsNullOrWhiteSpace(deviceId) ? "(empty)" : deviceId)}");

    var matchedBy = !string.IsNullOrWhiteSpace(name) ? "Name"
        : !string.IsNullOrWhiteSpace(description) ? "Description"
        : !string.IsNullOrWhiteSpace(deviceId) ? "DeviceID"
        : "(none)";

    var matchedValue = matchedBy switch
    {
        "Name" => name,
        "Description" => description,
        "DeviceID" => deviceId,
        _ => ""
    };

    Console.WriteLine($"  -> Used by KeyboardWatcher filter: {matchedBy} = \"{matchedValue}\"");
    Console.WriteLine();

    if (!string.IsNullOrWhiteSpace(name))
        filterCandidates.Add(name);
    if (!string.IsNullOrWhiteSpace(description))
        filterCandidates.Add(description);
    if (!string.IsNullOrWhiteSpace(deviceId))
        filterCandidates.Add(deviceId);
}

Console.WriteLine(new string('=', 72));
Console.WriteLine($"Found {index} device(s).");
Console.WriteLine();
Console.WriteLine("All strings you can use for TargetKeyboardFilter (substring match, case-insensitive):");
Console.WriteLine();

if (filterCandidates.Count == 0)
{
    Console.WriteLine("  (none)");
}
else
{
    foreach (var candidate in filterCandidates.OrderBy(c => c))
        Console.WriteLine($"  \"{candidate}\"");
}
