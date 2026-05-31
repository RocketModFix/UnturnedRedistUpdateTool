using System.Security.Cryptography;
using System.Text;

namespace UnturnedRedistUpdateTool;

/// <summary>
/// Runs the full redist-update pipeline. Extracted from Program.Main so it can
/// be exercised by tests with temp directories. Returns a process exit code.
/// </summary>
public class UpdateRunner
{
    public async Task<int> RunAsync(
        string unturnedPath,
        string redistPath,
        string appId,
        bool force,
        bool preview,
        List<string> publicizeAssemblies,
        List<string> updateFiles)
    {
        if (!Directory.Exists(unturnedPath))
        {
            Console.WriteLine($"Path doesn't exist: \"{unturnedPath}\".");
            return 1;
        }
        if (!Directory.Exists(redistPath))
        {
            Console.WriteLine($"Redist path doesn't exist: \"{redistPath}\".");
            return 1;
        }
        var nuspecFilePath = Directory.GetFiles(redistPath, Constants.NuspecSearchPattern).FirstOrDefault();
        if (nuspecFilePath == null)
        {
            Console.WriteLine($".nuspec file cannot be found in redist folder: \"{redistPath}\".");
            return 1;
        }

        Console.WriteLine("Preparing to run tool...");

        var appManifestPath = GameInfoParser.FindAppManifestFile(unturnedPath, appId);

        var managedDirectory = Path.Combine(GetUnturnedDataDirectory(unturnedPath), Constants.ManagedDirName);
        if (!Directory.Exists(managedDirectory))
        {
            Console.WriteLine($"Unturned Managed Directory not found: \"{managedDirectory}\"");
            return 1;
        }

        var (newVersion, newBuildId) = await GameInfoParser.ParseAsync(unturnedPath, appManifestPath);
        if (string.IsNullOrWhiteSpace(newVersion))
        {
            Console.WriteLine("New Game Version is not found");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(newBuildId))
        {
            Console.WriteLine("New Game BuildId is not found");
            return 1;
        }

        Console.WriteLine($"Found Unturned v{newVersion} ({newBuildId})");

        var nuspecHandler = new NuspecHandler(nuspecFilePath);
        var currentNuspecVersion = nuspecHandler.GetVersion();
        if (string.IsNullOrWhiteSpace(currentNuspecVersion))
        {
            Console.WriteLine("Version element not found in nuspec file!");
            return 1;
        }
        Console.WriteLine($"Current nuspec version: {currentNuspecVersion}");

        var versionTracker = new VersionTracker(redistPath, preview);
        var versionInfo = await versionTracker.LoadAsync();

        Console.WriteLine($"Current Build Id: {versionInfo?.BuildId}");

        var redistUpdater = new RedistUpdater(managedDirectory, redistPath, publicizeAssemblies, updateFiles);
        var (updatedFiles, manifests) = await redistUpdater.UpdateAsync();
        if (updatedFiles.Count == 0)
        {
            Console.WriteLine("No files were updated - either no changes or something went wrong.");
            if (versionInfo?.BuildId == newBuildId)
            {
                Console.WriteLine("Build ID is the same, no update needed.");
                return 0;
            }
            Console.WriteLine("Build ID changed but no files updated - this might be an issue.");
            return 1;
        }
        Console.WriteLine($"{updatedFiles.Count} Unturned's file(s) were updated");
        var combinedHash = CreateCombinedHash(manifests);
        Console.WriteLine($"Combined hash of updated files: {combinedHash}");
        Console.WriteLine($"Old Combined hash of updated files: {versionInfo?.FilesHash}");

        if (versionInfo?.FilesHash == combinedHash)
        {
            Console.WriteLine("Files haven't changed, keeping current version");
            return 0;
        }
        Console.WriteLine("Files are different now!");
        var versionToUse = preview
            ? $"{newVersion}-preview{newBuildId}"
            : newVersion;
        Console.WriteLine($"New Version: {newVersion}");
        Console.WriteLine($"New Build Id: {newBuildId}");
        Console.WriteLine($"Version to use: {versionToUse}");
        if (versionToUse == currentNuspecVersion)
        {
            Console.WriteLine("Skip. nuspec and version to use are same.");
            return 0;
        }
        await versionTracker.SaveAsync(new VersionInfo
        {
            GameVersion = newVersion,
            BuildId = newBuildId,
            NuGetVersion = versionToUse,
            FilesHash = combinedHash,
            LastUpdated = DateTime.UtcNow
        });

        nuspecHandler.UpdateVersion(versionToUse);
        nuspecHandler.Save();

        Console.WriteLine($"Updated {updatedFiles.Count} File(s)");
        foreach (var (_, toPath) in updatedFiles)
        {
            var fileName = Path.GetFileName(toPath);
            if (!manifests.TryGetValue(fileName, out var sha256))
                continue;
            Console.WriteLine($"{fileName} (SHA256: {sha256[..8]}...)");
        }

        await new CommitFileWriter().WriteAsync(unturnedPath, versionToUse, newBuildId, force);

        return 0;
    }

    public static string CreateCombinedHash(IReadOnlyDictionary<string, string> manifests)
    {
        var combinedData = new StringBuilder();
        foreach (var (fileName, fileHash) in manifests.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            combinedData.Append($"{fileName}:{fileHash}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combinedData.ToString())));
    }

    private static string GetUnturnedDataDirectory(string unturnedPath)
    {
        return Constants.UnturnedDataDirNames
                   .Select(name => Path.Combine(unturnedPath, name))
                   .FirstOrDefault(Directory.Exists)
               ?? throw new DirectoryNotFoundException($"Unturned Data directory cannot be found in {unturnedPath}");
    }
}
