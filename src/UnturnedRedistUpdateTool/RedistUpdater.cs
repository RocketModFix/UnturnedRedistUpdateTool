using System.Text.Json;
using BepInEx.AssemblyPublicizer;

namespace UnturnedRedistUpdateTool;

public class RedistUpdater
{
    private static readonly JsonSerializerOptions ManifestJsonSerializerOptions = new() { WriteIndented = true };
    private readonly string _managedDir;
    private readonly string _redistPath;
    private readonly List<string> _publicizeAssemblies;
    private readonly List<string> _updateFiles;

    public RedistUpdater(string managedDir, string redistPath, List<string> publicizeAssemblies, List<string> updateFiles)
    {
        _managedDir = managedDir;
        _redistPath = redistPath;
        _publicizeAssemblies = publicizeAssemblies;
        _updateFiles = updateFiles;
    }

    public async Task<(Dictionary<string, string> UpdatedFiles, Dictionary<string, string> Manifests)> UpdateAsync()
    {
        Dictionary<string, string> updatedFiles = [];
        Dictionary<string, string> manifests = [];
        var managedFiles = new DirectoryInfo(_managedDir).GetFiles();
        if (managedFiles.Length == 0)
            throw new InvalidOperationException($"{_managedDir} is empty");

        var filesToProcess = _updateFiles.Count > 0
            ? managedFiles.Where(f => _updateFiles.Contains(f.Name)).ToArray()
            : managedFiles;

        if (_updateFiles.Count > 0)
        {
            var existingFileNames = managedFiles.Select(f => f.Name).ToHashSet();
            var missingFiles = _updateFiles.Where(fileName => !existingFileNames.Contains(fileName)).ToList();
            if (missingFiles.Count > 0)
            {
                throw new FileNotFoundException($"The following files specified in -update-files were not found in the managed directory: {string.Join(", ", missingFiles)}");
            }
        }
        foreach (var file in filesToProcess)
        {
            var managedFilePath = file.FullName;
            var redistFilePath = Path.Combine(_redistPath, file.Name);
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
                if (File.Exists(redistFilePath))
                {
                    var redistHash = HashHelper.GetFileHash(redistFilePath);
                    if (managedHash == redistHash)
                        continue;
                }
                file.CopyTo(redistFilePath, true);
                var copiedHash = HashHelper.GetFileHash(redistFilePath);
                manifests[file.Name] = copiedHash;
            }
            updatedFiles[managedFilePath] = redistFilePath;
        }
        
        var completeManifest = CreateCompleteManifest();
        var manifestPath = Path.Combine(_redistPath, Constants.ManifestFileName);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(completeManifest, ManifestJsonSerializerOptions));
        return (updatedFiles, manifests);
    }

    private Dictionary<string, string> CreateCompleteManifest()
    {
        var redistDirectory = new DirectoryInfo(_redistPath);

        // Only the files that can actually be updated from Unturned, sorted by
        // name so manifest.sha256.json is deterministic (avoids diff churn).
        var relevantFiles = redistDirectory.GetFiles()
            .Where(f => f.Name != Constants.ManifestFileName && _updateFiles.Contains(f.Name))
            .OrderBy(f => f.Name, StringComparer.Ordinal);

        var completeManifest = new Dictionary<string, string>();
        foreach (var file in relevantFiles)
        {
            completeManifest[file.Name] = HashHelper.GetFileHash(file.FullName);
        }
        return completeManifest;
    }
}