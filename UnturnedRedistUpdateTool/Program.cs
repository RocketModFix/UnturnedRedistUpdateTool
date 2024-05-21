using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Tar;
using ValveKeyValue;

internal class Program
{
    private static string AppId { get; set; }
    private static bool Force { get; set; }

    public static async Task<int> Main(string[] args)
    {
        var linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        AssertPlatformSupported();

        string unturnedPath;
        if (args.Length < 3)
        {
            Console.WriteLine("Wrong usage. Correct usage: UnturnedRedistAutoUpdate.exe <path> <app_id> [args]");
            return 1;
        }
        unturnedPath = args[0];
        AppId = args[1];
        Force = !args.Any(x => x.Equals("--force", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(AppId))
        {
            Console.WriteLine("AppId is not specified.");
            return 1;
        }
        if (Path.Exists(unturnedPath) == false)
        {
            Console.WriteLine($"Path doesn't exists: \"{unturnedPath}\".");
            return 1;
        }
        var redistPath = Path.Combine(unturnedPath, "redist");
        if (Path.Exists(redistPath) == false)
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
        if (File.Exists(nuspecFilePath) == false)
        {
            Console.WriteLine($".nuspec file doesn't exists in redist folder: \"{redistPath}\".");
            return 1;
        }

        Console.WriteLine("Preparing to run tool...");

        var steamappsDirectory = Path.Combine(unturnedPath, "steamapps");
        if (Directory.Exists(steamappsDirectory) == false)
        {
            Console.WriteLine($"steamapps Directory not found: \"{steamappsDirectory}\"");
            return 1;
        }
        Console.WriteLine("steamappsDirectory: " + string.Join(", ", Directory.GetDirectories(steamappsDirectory)));

        var unturnedDataPath = GetUnturnedDataDirectoryName(unturnedPath);
        var managedDirectory = Path.Combine(unturnedDataPath, "Managed");
        if (Directory.Exists(managedDirectory) == false)
        {
            Console.WriteLine($"Unturned Managed Directory not found: \"{managedDirectory}\"");
            return 1;
        }
        const string statusFileName = "Status.json";
        var statusFilePath = Path.Combine(unturnedPath, statusFileName);
        if (File.Exists(statusFilePath) == false)
        {
            throw new FileNotFoundException("Required file is not found", statusFilePath);
        }
        var (version, buildId) = await GetInfo(unturnedPath, steamappsDirectory, AppId);

        Console.WriteLine($"Found Unturned v{version} ({buildId})");

        var doc = XDocument.Load(nuspecFilePath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";
        var versionElement = doc.Root.Element(ns + "metadata").Element(ns + "version");
        if (versionElement != null)
        {
            if (version == versionElement.Value)
            {
                Console.WriteLine("Unturned Version is the same as in nuspec, it means new version is not detected, skipping...");
                return 1;
            }
            versionElement.Value = version;
        }
        else
        {
            Console.WriteLine("Version element not found in nuspec file!");
            return 1;
        }

        doc.Save(nuspecFilePath);

        UpdateRedist(managedDirectory);

        var forcedNote = Force ? " [Forced]" : "";

        await File.WriteAllTextAsync(Path.Combine(unturnedPath, ".commit"),
            $"{DateTime.UtcNow:dd MMMM yyyy} - Version {version} ({buildId})" + forcedNote);

        return 0;

        void AssertPlatformSupported()
        {
            if (!(linux || windows))
            {
                throw new PlatformNotSupportedException();
            }
        }
        void UpdateRedist(string unturnedManagedDirectory)
        {
            var managedFiles = new DirectoryInfo(unturnedManagedDirectory).GetFiles();
            if (managedFiles.Length == 0)
            {
                throw new InvalidOperationException($"{nameof(managedFiles)} was empty");
            }

            foreach (var fileInfo in managedFiles)
            {
                var redistFilePath = Path.Combine(unturnedPath, fileInfo.Name);
                if (File.Exists(redistFilePath))
                {
                    fileInfo.CopyTo(redistFilePath, true);
                }
            }
        }
    }

    private static string GetUnturnedDataDirectoryName(string unturnedPath)
    {
        var headless = Path.Combine(unturnedPath, "Unturned_Headless_Data");
        if (Directory.Exists(headless))
        {
            return headless;
        }
        var usual = Path.Combine(unturnedPath, "Unturned_Data");
        if (Directory.Exists(usual))
        {
            return usual;
        }
        throw new DirectoryNotFoundException("Unturned Data directory cannot be found!");
    }
    private static async Task<(string version, string buildId)> GetInfo(string unturnedPath, string steamappsPath, string appId)
    {
        var node = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(unturnedPath, "Status.json")))!["Game"]!;
        var version = $"3.{node["Major_Version"]}.{node["Minor_Version"]}.{node["Patch_Version"]}";

        var appmanifestFileName = $"appmanifest_{appId}.acf";
        var appdataPath = Path.Combine(steamappsPath, "steamapps", appmanifestFileName);
        if (!File.Exists(appdataPath))
        {
            throw new FileNotFoundException("Required file is not found", appmanifestFileName);
        }

        await using var file = File.OpenRead(appdataPath);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var obj = kv.Deserialize(file);

        var buildId1 = obj["buildid"].ToString();
        return (version, buildId1);
    }
}