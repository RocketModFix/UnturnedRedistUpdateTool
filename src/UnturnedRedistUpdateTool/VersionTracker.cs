using System.Text.Json;

namespace UnturnedRedistUpdateTool;

public class VersionInfo
{
    public string GameVersion { get; set; } = "";
    public string BuildId { get; set; } = "";
    public string NugetVersion { get; set; } = "";
    public string FilesHash { get; set; } = "";
    public DateTime LastUpdated { get; set; }
}

public class VersionTracker
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };
    private readonly string _versionFilePath;

    public VersionTracker(string redistPath)
    {
        _versionFilePath = Path.Combine(redistPath, "version-info.json");
    }

    public async Task<VersionInfo?> LoadAsync()
    {
        if (!File.Exists(_versionFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_versionFilePath);
            return JsonSerializer.Deserialize<VersionInfo>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not load version info: {ex}");
            return null;
        }
    }

    public async Task SaveAsync(VersionInfo versionInfo)
    {
        try
        {
            var json = JsonSerializer.Serialize(versionInfo, JsonSerializerOptions);
            await File.WriteAllTextAsync(_versionFilePath, json);
            Console.WriteLine($"Version info saved to: {_versionFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not save version info: {ex}");
        }
    }
}