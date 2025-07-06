using System.Text.Json.Nodes;
using ValveKeyValue;

namespace UnturnedRedistUpdateTool;

public static class GameInfoParser
{
    public static string FindAppManifestFile(string unturnedPath, string appId)
    {
        string[] possiblePath =
        [
            Path.Combine(unturnedPath, "steamapps", $"appmanifest_{appId}.acf"), // inside of Unturned folder
            Path.GetFullPath(Path.Combine(unturnedPath, "..", "..", $"appmanifest_{appId}.acf")) // outside of unturned folder
        ];
        var appdataPath = possiblePath.FirstOrDefault(File.Exists);
        if (appdataPath == null)
        {
            throw new FileNotFoundException($"Required file is not found. Searched: {unturnedPath}", $"appmanifest_{appId}.acf");
        }
        return appdataPath;
    }
    public static async Task<(string Version, string BuildId)> ParseAsync(string unturnedPath, string appManifestPath)
    {
        var statusFilePath = Path.Combine(unturnedPath, "Status.json");
        if (!File.Exists(statusFilePath))
        {
            throw new FileNotFoundException("Status file is not found", statusFilePath);
        }
        var node = JsonNode.Parse(await File.ReadAllTextAsync(statusFilePath))!["Game"]!;
        var version = $"3.{node["Major_Version"]}.{node["Minor_Version"]}.{node["Patch_Version"]}";
        await using var file = File.OpenRead(appManifestPath);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var obj = kv.Deserialize(file);
        var buildId = obj["buildid"].ToString();
        return (version, buildId);
    }
}