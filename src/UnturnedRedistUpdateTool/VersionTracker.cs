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

    public VersionTracker(string redistPath, bool preview)
    {
        _versionFilePath = Path.Combine(redistPath, preview ? "version.preview.json" : "version.json");
    }

    public async Task<VersionInfo?> LoadAsync()
    {
        if (!File.Exists(_versionFilePath))
            return null;
        var json = await File.ReadAllTextAsync(_versionFilePath);
        return JsonSerializer.Deserialize<VersionInfo>(json);
    }

    public async Task SaveAsync(VersionInfo info)
    {
        var json = JsonSerializer.Serialize(info, JsonSerializerOptions);
        await File.WriteAllTextAsync(_versionFilePath, json);
    }
}