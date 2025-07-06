using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace UnturnedRedistUpdateTool;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        AssertPlatformSupported();

#if DEBUG
        if (args.Length == 0)
        {
            args = [@"C:\Me\Apps\Steam\steamapps\common\Unturned", Path.Combine(AppContext.BaseDirectory, "TempRedist", "Client"), "304930", "--force"];
        }
#endif

        if (args.Length < 3)
        {
            Console.WriteLine("Wrong usage. Correct usage: UnturnedRedistUpdateTool.exe <unturned_path> <redist_path> <app_id> [args]");
            return 1;
        }
        var unturnedPath = args[0];
        var redistPath = args[1];
        var appId = args[2];
        var force = args.Any(x => x.Equals("--force", StringComparison.OrdinalIgnoreCase));
        var preview = args.Any(x => x.Equals("--preview", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(appId))
        {
            Console.WriteLine("AppId is not specified.");
            return 1;
        }
        if (!Directory.Exists(unturnedPath))
        {
            Console.WriteLine($"Path doesn't exists: \"{unturnedPath}\".");
            return 1;
        }
        if (!Directory.Exists(redistPath))
        {
            Console.WriteLine($"Redist path doesn't exists: \"{redistPath}\".");
            return 1;
        }
        var nuspecFilePath = Directory.GetFiles(redistPath, "*.nuspec").FirstOrDefault();
        if (nuspecFilePath == null)
        {
            Console.WriteLine($".nuspec file cannot be found in redist folder: \"{redistPath}\".");
            return 1;
        }
        if (!File.Exists(nuspecFilePath))
        {
            Console.WriteLine($".nuspec file doesn't exists in redist folder: \"{redistPath}\".");
            return 1;
        }

        Console.WriteLine("Preparing to run tool...");

        var appManifestPath = GameInfoParser.FindAppManifestFile(unturnedPath, appId);

        var unturnedDataPath = GetUnturnedDataDirectoryName();
        var managedDirectory = Path.Combine(unturnedDataPath, "Managed");
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

        var versionTracker = new VersionTracker(redistPath);
        var versionInfo = await versionTracker.LoadAsync();

        var redistUpdater = new RedistUpdater(managedDirectory, redistPath);
        var updatedFiles = await redistUpdater.UpdateAsync();
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
        var combinedHash = CreateCombinedHash(updatedFiles);
        Console.WriteLine($"Combined hash of updated files: {combinedHash}");
        var versionToUse = DetermineVersionToUse(newVersion, newBuildId, currentNuspecVersion, versionInfo, combinedHash, preview);
        Console.WriteLine($"New Version: {newVersion}");
        Console.WriteLine($"New Build Id: {newBuildId}");
        Console.WriteLine($"Version to use: {versionToUse}");
        if (versionToUse == currentNuspecVersion)
        {
            Console.WriteLine("Files haven't changed since last publish, skipping...");
            return 0;
        }
        await versionTracker.SaveAsync(new VersionInfo
        {
            GameVersion = newVersion,
            BuildId = newBuildId,
            NugetVersion = versionToUse,
            FilesHash = combinedHash,
            LastUpdated = DateTime.UtcNow
        });

        nuspecHandler.UpdateVersion(versionToUse);
        nuspecHandler.Save();

        Console.WriteLine($"Updated {updatedFiles.Count} File(s)");
        foreach (var (filePath, sha256) in updatedFiles)
        {
            Console.WriteLine($"Updated File \"{filePath}\" (SHA256: {sha256[..8]}...)");
        }

        await new CommitFileWriter().WriteAsync(unturnedPath, versionToUse, newBuildId, force);

        return 0;

        void AssertPlatformSupported()
        {
            if (!(linux || windows))
            {
                throw new PlatformNotSupportedException();
            }
        }

        string GetUnturnedDataDirectoryName()
        {
            if (linux)
            {
                const string linuxUnturnedDataDirectoryName = "Unturned_Headless_Data";
                var headless = Path.Combine(unturnedPath, linuxUnturnedDataDirectoryName);
                if (Directory.Exists(headless))
                {
                    return headless;
                }
            }
            else if (windows)
            {
                const string windowsUnturnedDataDirectoryName = "Unturned_Data";
                var usual = Path.Combine(unturnedPath, windowsUnturnedDataDirectoryName);
                if (Directory.Exists(usual))
                {
                    return usual;
                }
            }
            throw new DirectoryNotFoundException($"Unturned Data directory cannot be found in {unturnedPath}");
        }
    }

    private static string CreateCombinedHash(Dictionary<string, string> updatedFiles)
    {
        var sortedFiles = updatedFiles.OrderBy(kvp => kvp.Key).ToList();
        var combinedData = new StringBuilder();
        foreach (var (filePath, fileHash) in sortedFiles)
        {
            combinedData.Append($"{Path.GetFileName(filePath)}:{fileHash}");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combinedData.ToString())));
    }

    private static string DetermineVersionToUse(string gameVersion, string buildId, string currentNuspecVersion,
        VersionInfo? versionInfo, string newFilesHash, bool preview)
    {
        if (versionInfo?.FilesHash == newFilesHash)
        {
            Console.WriteLine("Files haven't changed, keeping current version");
            return currentNuspecVersion;
        }
        if (versionInfo?.GameVersion != gameVersion)
        {
            Console.WriteLine("Game version changed, using new game version");
            return gameVersion;
        }
        Console.WriteLine("Same game version but files changed");
        return preview
            ? $"{gameVersion}-preview{buildId}"
            : gameVersion;
    }
}