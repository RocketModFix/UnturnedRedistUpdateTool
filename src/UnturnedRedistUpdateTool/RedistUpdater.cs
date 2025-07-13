using System.Text.Json;
using BepInEx.AssemblyPublicizer;

namespace UnturnedRedistUpdateTool;

public class RedistUpdater
{
    private static readonly JsonSerializerOptions ManifestJsonSerializerOptions = new() { WriteIndented = true };
    private readonly string _managedDir;
    private readonly string _redistPath;
    private readonly List<string> _publicizeAssemblies;

    public RedistUpdater(string managedDir, string redistPath, List<string> publicizeAssemblies)
    {
        _managedDir = managedDir;
        _redistPath = redistPath;
        _publicizeAssemblies = publicizeAssemblies;
    }

    public async Task<(Dictionary<string, string> UpdatedFiles, Dictionary<string, string> Manifests)> UpdateAsync()
    {
        Dictionary<string, string> updatedFiles = [];
        Dictionary<string, string> manifests = [];
        var managedFiles = new DirectoryInfo(_managedDir).GetFiles();
        if (managedFiles.Length == 0)
            throw new InvalidOperationException($"{_managedDir} is empty");
        foreach (var file in managedFiles)
        {
            var managedFilePath = file.FullName;
            var redistFilePath = Path.Combine(_redistPath, file.Name);
            if (!File.Exists(redistFilePath))
                continue;
            if (_publicizeAssemblies.Any(x => x == file.Name))
            {
                AssemblyPublicizer.Publicize(managedFilePath, redistFilePath);
                Console.WriteLine($"Publicized {redistFilePath}");
                var publicizedHash = HashHelper.GetFileHash(redistFilePath);
                manifests[file.Name] = publicizedHash;
            }
            else
            {
                var managedHash = HashHelper.GetFileHash(managedFilePath);
                var redistHash = HashHelper.GetFileHash(redistFilePath);
                if (managedHash == redistHash)
                    continue;
                file.CopyTo(redistFilePath, true);
                var copiedHash = HashHelper.GetFileHash(redistFilePath);
                manifests[file.Name] = copiedHash;
            }
            updatedFiles[managedFilePath] = redistFilePath;
        }
        var manifestPath = Path.Combine(_redistPath, "manifest.sha256.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifests, ManifestJsonSerializerOptions));
        return (updatedFiles, manifests);
    }
}