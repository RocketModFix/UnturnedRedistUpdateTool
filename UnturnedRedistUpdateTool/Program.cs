using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
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

        if (args.Length < 3)
        {
            Console.WriteLine("Wrong usage. Correct usage: UnturnedRedistUpdateTool.exe <unturned_path> <redist_path> <app_id> [args]");
            return 1;
        }
        var unturnedPath = args[0];
        var redistPath = args[1];
        AppId = args[2];
        Force = args.Any(x => x.Equals("--force", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(AppId))
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

        var steamappsDirectory = Path.Combine(unturnedPath, "steamapps");
        if (!Directory.Exists(steamappsDirectory))
        {
            Console.WriteLine($"steamapps Directory not found: \"{steamappsDirectory}\"");
            return 1;
        }
        var unturnedDataPath = GetUnturnedDataDirectoryName(unturnedPath);
        var managedDirectory = Path.Combine(unturnedDataPath, "Managed");
        if (!Directory.Exists(managedDirectory))
        {
            Console.WriteLine($"Unturned Managed Directory not found: \"{managedDirectory}\"");
            return 1;
        }
        const string statusFileName = "Status.json";
        var statusFilePath = Path.Combine(unturnedPath, statusFileName);
        if (!File.Exists(statusFilePath))
        {
            throw new FileNotFoundException("Status file is not found", statusFilePath);
        }
        var (version, buildId) = await GetInfo(unturnedPath, steamappsDirectory, AppId);

        Console.WriteLine($"Found Unturned v{version} ({buildId})");

        var doc = XDocument.Load(nuspecFilePath, LoadOptions.PreserveWhitespace);
        var ns = XNamespace.Get("http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");
        var versionElement = doc.Root.Element(ns + "metadata").Element(ns + "version");
        if (versionElement == null)
        {
            Console.WriteLine("Version element not found in nuspec file!");
            return 1;
        }

        Console.WriteLine($"nuspec version: {versionElement.Value}");
        if (version == versionElement.Value)
        {
            Console.WriteLine("Unturned Version is the same as in nuspec, it means new version is not detected, skipping...");
            return 1;
        }
        versionElement.Value = version;

        doc.Save(nuspecFilePath);

        var updatedFiles = await UpdateRedist(managedDirectory);
        if (updatedFiles.Count == 0)
        {
            Console.WriteLine($"No one file were updated, perhaps something went wrong.");
            return 1;
        }

        Console.WriteLine($"Updated {updatedFiles.Count} File(s)");
        foreach (var (fromPath, toPath) in updatedFiles)
        {
            Console.WriteLine($"Updated File. From: \"{fromPath}\", To: \"{toPath}\"");
        }

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

        async Task<Dictionary<string, string>> UpdateRedist(string unturnedManagedDirectory)
        {
            var managedFiles = new DirectoryInfo(unturnedManagedDirectory).GetFiles();
            if (managedFiles.Length == 0)
            {
                throw new InvalidOperationException($"{unturnedManagedDirectory} directory was empty");
            }

            Dictionary<string, string> updatedFiles = [];
            Dictionary<string, string> manifest = [];
            foreach (var fileInfo in managedFiles)
            {
                try
                {
                    var managedFilePath = fileInfo.FullName;
                    var redistFilePath = Path.Combine(redistPath, fileInfo.Name);
                    var managedHash = HashHelper.GetFileHash(managedFilePath);
                    manifest[fileInfo.Name] = managedHash;
                    if (!File.Exists(redistFilePath))
                    {
                        continue;
                    }
                    var redistHash = HashHelper.GetFileHash(redistFilePath);
                    if (managedHash == redistHash)
                    {
                        continue;
                    }

                    fileInfo.CopyTo(redistFilePath, true);
                    updatedFiles.Add(managedFilePath, redistFilePath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"An error occured while updating file: \"{fileInfo.FullName}\".", ex);
                }
            }

            var manifestPath = Path.Combine(redistPath, "manifest.sha256.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonSerializerOptions));

            return updatedFiles;
        }
    }

    private static readonly JsonSerializerOptions ManifestJsonSerializerOptions = new() { WriteIndented = true };

    private static string GetUnturnedDataDirectoryName(string unturnedPath)
    {
        const string linuxUnturnedDataDirectoryName = "Unturned_Headless_Data";
        var headless = Path.Combine(unturnedPath, linuxUnturnedDataDirectoryName);
        if (Directory.Exists(headless))
        {
            return headless;
        }
        const string windowsUnturnedDataDirectoryName = "Unturned_Data";
        var usual = Path.Combine(unturnedPath, windowsUnturnedDataDirectoryName);
        if (Directory.Exists(usual))
        {
            return usual;
        }
        throw new DirectoryNotFoundException($"Unturned Data directory cannot be found in {unturnedPath}");
    }
    private static async Task<(string version, string buildId)> GetInfo(string unturnedPath, string steamappsPath, string appId)
    {
        var node = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(unturnedPath, "Status.json")))!["Game"]!;
        var version = $"3.{node["Major_Version"]}.{node["Minor_Version"]}.{node["Patch_Version"]}";

        var appmanifestFileName = $"appmanifest_{appId}.acf";
        var appmanifestFilePath = Path.Combine(steamappsPath, appmanifestFileName);
        if (!File.Exists(appmanifestFilePath))
        {
            throw new FileNotFoundException("Required file is not found", appmanifestFilePath);
        }

        await using var file = File.OpenRead(appmanifestFilePath);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var obj = kv.Deserialize(file);

        var buildId1 = obj["buildid"].ToString();
        return (version, buildId1);
    }
}

internal static class HashHelper
{
    public static string GetFileHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string GetHashFromArray(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsSameHashes(byte[] managedFileData, byte[] redistFileData)
    {
        return GetHashFromArray(managedFileData) == GetHashFromArray(redistFileData);
    }
}