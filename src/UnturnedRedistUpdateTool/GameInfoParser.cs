using System.Text.Json.Nodes;
using ValveKeyValue;

namespace UnturnedRedistUpdateTool;

public class GameInfoParser
{
    public async Task<(string Version, string BuildId)> ParseAsync(string unturnedPath, string steamappsPath, string appId)
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
        var buildId = obj["buildid"].ToString();
        return (version, buildId);
    }
}