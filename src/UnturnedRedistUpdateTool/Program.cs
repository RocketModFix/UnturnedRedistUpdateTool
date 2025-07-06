using System.Runtime.InteropServices;

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
        var (newVersion, buildId) = await GameInfoParser.ParseAsync(unturnedPath, appManifestPath);
        if (string.IsNullOrWhiteSpace(newVersion))
        {
            Console.WriteLine("New Game Version is not found");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(buildId))
        {
            Console.WriteLine("New Game BuildId is not found");
            return 1;
        }

        Console.WriteLine($"Found Unturned v{newVersion} ({buildId})");

        var nuspecHandler = new NuspecHandler(nuspecFilePath);
        var currentNuspecVersion = nuspecHandler.GetVersion();
        if (string.IsNullOrWhiteSpace(currentNuspecVersion))
        {
            Console.WriteLine("Version element not found in nuspec file!");
            return 1;
        }
        var newVersionWithBuildId = nuspecHandler.CreateVersion(newVersion, buildId);
        Console.WriteLine($"Current nuspec version: {currentNuspecVersion}");
        Console.WriteLine($"New Version & Build Id: {newVersionWithBuildId}");
        if (newVersionWithBuildId == currentNuspecVersion)
        {
            Console.WriteLine("Unturned Version is the same as in nuspec, it means new version is not detected, skipping...");
            return 1;
        }
        nuspecHandler.UpdateVersion(newVersionWithBuildId);
        nuspecHandler.Save();

        var redistUpdater = new RedistUpdater(managedDirectory, redistPath);
        var updatedFiles = await redistUpdater.UpdateAsync();
        if (updatedFiles.Count == 0)
        {
            Console.WriteLine("No one file were updated, perhaps something went wrong.");
            return 1;
        }

        Console.WriteLine($"Updated {updatedFiles.Count} File(s)");
        foreach (var (fromPath, toPath) in updatedFiles)
        {
            Console.WriteLine($"Updated File \"{fromPath}\" -> \"{toPath}\"");
        }

        await new CommitFileWriter().WriteAsync(unturnedPath, newVersion, buildId, force);

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
}