using System.IO;
using System.Linq;
using System.Reflection;

namespace AionMeter.App.Services;

/// <summary>
/// Unpacks bundled lookup tables to the user's app-data cache. This keeps the portable download free of a visible
/// data folder while preserving the file-based lookup API used by the parser and timer services. This is packaging,
/// not encryption or access control.
/// </summary>
internal static class EmbeddedData
{
    private const string ResourcePrefix = "AionMeter.Data/";
    private const string MarkerName = ".embedded-data-version";

    public static string Install()
    {
        var targetRoot = Path.Combine(AppSettings.AppDataDir, "data");
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        var marker = Path.Combine(targetRoot, MarkerName);
        // Also seed the new user-owned file when an existing cache has the same app version.
        Directory.CreateDirectory(targetRoot);
        var idsPath = Path.Combine(targetRoot, "ids.json");
        if (!File.Exists(idsPath))
        {
            try { using var ids = new FileStream(idsPath, FileMode.CreateNew, FileAccess.Write); ids.Write("{}"u8); }
            catch (IOException) when (File.Exists(idsPath)) { } // Another instance created it; never truncate it.
        }
        var eventsPath = Path.Combine(targetRoot, "events.json");
        if (!File.Exists(eventsPath))
        {
            try
            {
                using var source = assembly.GetManifestResourceStream(ResourcePrefix + "events.json")
                    ?? throw new InvalidDataException("Die Ereignisvorlagen fehlen.");
                using var output = new FileStream(eventsPath, FileMode.CreateNew, FileAccess.Write);
                source.CopyTo(output);
            }
            catch (IOException) when (File.Exists(eventsPath)) { }
        }
        if (File.Exists(marker) && File.ReadAllText(marker).Trim() == version) return targetRoot;

        Directory.CreateDirectory(targetRoot);
        var rootFull = Path.GetFullPath(targetRoot) + Path.DirectorySeparatorChar;
        foreach (var resourceName in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var relative = resourceName[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(targetRoot, relative));
            if (!destination.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded data path escaped its target directory.");

            // This mapping belongs to the player; updates must never erase their own ID research.
            if ((relative.Equals("ids.json", StringComparison.OrdinalIgnoreCase) || relative.Equals("events.json", StringComparison.OrdinalIgnoreCase)) && File.Exists(destination)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidDataException($"Missing embedded resource: {resourceName}");
            using var output = File.Create(destination);
            source.CopyTo(output);
        }

        File.WriteAllText(marker, version);
        return targetRoot;
    }
}
