using System.Text.Json.Nodes;
using ValveKeyValue;

namespace UnturnedRedistUpdateTool;

public static class GameInfoParser
{
    private const string GameSection = "Game";
    private const string MajorVersionKey = "Major_Version";
    private const string MinorVersionKey = "Minor_Version";
    private const string PatchVersionKey = "Patch_Version";
    private const string BuildIdKey = "buildid";

    public static string FindAppManifestFile(string unturnedPath, string appId)
    {
        var appManifest = Constants.AppManifestFileName(appId);
        string[] possiblePaths =
        [
            Path.Combine(unturnedPath, Constants.SteamAppsDirName, appManifest), // steamcmd +force_install_dir <unturnedPath>
            Path.GetFullPath(Path.Combine(unturnedPath, "..", "..", appManifest)) // standard Steam library layout
        ];
        var found = possiblePaths.FirstOrDefault(File.Exists);
        if (found == null)
        {
            // List the real paths searched — a missing appmanifest usually means
            // steamcmd didn't finish the download.
            var searched = string.Join(Environment.NewLine + "  ", possiblePaths);
            throw new FileNotFoundException(
                $"{appManifest} not found (did steamcmd finish downloading?). Searched:{Environment.NewLine}  {searched}",
                appManifest);
        }
        return found;
    }

    public static async Task<(string Version, string BuildId)> ParseAsync(string unturnedPath, string appManifestPath)
    {
        var statusFilePath = Path.Combine(unturnedPath, Constants.StatusFileName);
        if (!File.Exists(statusFilePath))
        {
            throw new FileNotFoundException($"{Constants.StatusFileName} not found.", statusFilePath);
        }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(statusFilePath));
        var game = root?[GameSection]
                   ?? throw new InvalidOperationException($"{Constants.StatusFileName} is missing the '{GameSection}' section: {statusFilePath}");
        var major = game[MajorVersionKey];
        var minor = game[MinorVersionKey];
        var patch = game[PatchVersionKey];
        if (major is null || minor is null || patch is null)
        {
            throw new InvalidOperationException(
                $"{Constants.StatusFileName} '{GameSection}' section is missing {MajorVersionKey}/{MinorVersionKey}/{PatchVersionKey}: {statusFilePath}");
        }
        var version = $"3.{major}.{minor}.{patch}";

        await using var file = File.OpenRead(appManifestPath);
        var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        var obj = kv.Deserialize(file);
        var buildId = obj[BuildIdKey]?.ToString();
        if (string.IsNullOrWhiteSpace(buildId))
        {
            throw new InvalidOperationException($"'{BuildIdKey}' not found in app manifest: {appManifestPath}");
        }
        return (version, buildId);
    }
}
