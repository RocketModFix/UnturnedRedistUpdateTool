using System.Text.Json;

namespace UnturnedRedistUpdateTool;

public class RedistUpdater
{
    private static readonly JsonSerializerOptions ManifestJsonSerializerOptions = new() { WriteIndented = true };
    private readonly string _managedDir;
    private readonly string _redistPath;

    public RedistUpdater(string managedDir, string redistPath)
    {
        _managedDir = managedDir;
        _redistPath = redistPath;
    }

    public async Task<Dictionary<string, string>> UpdateAsync()
    {
        Dictionary<string, string> updatedFiles = [];
        Dictionary<string, string> manifest = [];
        var managedFiles = new DirectoryInfo(_managedDir).GetFiles();
        if (managedFiles.Length == 0)
            throw new InvalidOperationException($"{_managedDir} is empty");
        foreach (var file in managedFiles)
        {
            var managedFilePath = file.FullName;
            var redistFilePath = Path.Combine(_redistPath, file.Name);
            var managedHash = HashHelper.GetFileHash(managedFilePath);
            manifest[file.Name] = managedHash;
            if (!File.Exists(redistFilePath))
                continue;
            var redistHash = HashHelper.GetFileHash(redistFilePath);
            if (managedHash == redistHash)
                continue;
            file.CopyTo(redistFilePath, true);
            updatedFiles[managedFilePath] = redistFilePath;
        }
        var manifestPath = Path.Combine(_redistPath, "manifest.sha256.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonSerializerOptions));
        return updatedFiles;
    }
}