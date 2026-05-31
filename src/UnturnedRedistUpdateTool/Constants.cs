namespace UnturnedRedistUpdateTool;

/// <summary>
/// Centralized file/directory names and patterns the tool reads or writes, so
/// they aren't scattered as magic strings across the codebase.
/// </summary>
public static class Constants
{
    // Files the tool writes into the redist directory.
    public const string ManifestFileName = "manifest.sha256.json";
    public const string VersionFileName = "version.json";
    public const string PreviewVersionFileName = "version.preview.json";
    public const string CommitFileName = ".commit";

    // Files / patterns the tool reads.
    public const string StatusFileName = "Status.json";
    public const string NuspecSearchPattern = "*.nuspec";

    // Unturned / Steam layout.
    public const string ManagedDirName = "Managed";
    public const string SteamAppsDirName = "steamapps";

    // Checked in order; the headless (server) data dir takes precedence.
    public static readonly string[] UnturnedDataDirNames = ["Unturned_Headless_Data", "Unturned_Data"];

    public static string AppManifestFileName(string appId) => $"appmanifest_{appId}.acf";
}
