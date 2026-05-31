using System.Text.Json;
using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class UpdateRunnerTests
{
    // TestData contains appmanifest_1110390.acf (buildid 19099921) and a
    // Status.json reporting 3.25.6.1.
    private const string AppId = "1110390";
    private const string ExpectedVersion = "3.25.6.1";
    private const string ExpectedBuildId = "19099921";

    private static string TestDataDir => Path.Combine(AppContext.BaseDirectory, "TestData");

    private static (string unturnedPath, string redistPath, string nuspecPath) Setup(TempDir tempDir)
    {
        var unturnedPath = Path.Combine(tempDir.Path, "Unturned");
        var redistPath = Path.Combine(tempDir.Path, "Redist");

        var steamApps = Path.Combine(unturnedPath, Constants.SteamAppsDirName);
        Directory.CreateDirectory(steamApps);
        File.Copy(
            Path.Combine(TestDataDir, Constants.SteamAppsDirName, Constants.AppManifestFileName(AppId)),
            Path.Combine(steamApps, Constants.AppManifestFileName(AppId)));

        File.Copy(
            Path.Combine(TestDataDir, "Unturned", Constants.StatusFileName),
            Path.Combine(unturnedPath, Constants.StatusFileName));

        var managed = Path.Combine(unturnedPath, "Unturned_Data", Constants.ManagedDirName);
        Directory.CreateDirectory(managed);
        File.WriteAllText(Path.Combine(managed, "Assembly-CSharp.dll"), "new-managed-content");

        Directory.CreateDirectory(redistPath);
        var nuspecPath = Path.Combine(redistPath, "Test.nuspec");
        File.WriteAllText(nuspecPath, MinimalNuspec("0.0.0")); // differs so the update proceeds
        return (unturnedPath, redistPath, nuspecPath);
    }

    private static string MinimalNuspec(string version) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">\n" +
        "  <metadata>\n" +
        "    <id>Test.Redist</id>\n" +
        $"    <version>{version}</version>\n" +
        "    <description>test</description>\n" +
        "    <authors>test</authors>\n" +
        "  </metadata>\n" +
        "</package>\n";

    [Fact]
    public async Task ShouldWriteVersionNuspecManifestAndCommitForStableVariant()
    {
        using var tempDir = new TempDir();
        var (unturnedPath, redistPath, nuspecPath) = Setup(tempDir);

        var exit = await new UpdateRunner().RunAsync(
            unturnedPath, redistPath, AppId, force: false, preview: false,
            publicizeAssemblies: [], updateFiles: ["Assembly-CSharp.dll"]);

        exit.ShouldBe(0);

        var versionPath = Path.Combine(redistPath, Constants.VersionFileName);
        File.Exists(versionPath).ShouldBeTrue();
        var info = JsonSerializer.Deserialize<VersionInfo>(await File.ReadAllTextAsync(versionPath))!;
        info.GameVersion.ShouldBe(ExpectedVersion);
        info.BuildId.ShouldBe(ExpectedBuildId);
        info.NuGetVersion.ShouldBe(ExpectedVersion);

        new NuspecHandler(nuspecPath).GetVersion().ShouldBe(ExpectedVersion);
        File.Exists(Path.Combine(redistPath, "Assembly-CSharp.dll")).ShouldBeTrue();
        File.Exists(Path.Combine(redistPath, Constants.ManifestFileName)).ShouldBeTrue();
        File.Exists(Path.Combine(unturnedPath, Constants.CommitFileName)).ShouldBeTrue();
    }

    [Fact]
    public async Task ShouldUsePreviewVersionAndFileForPreviewVariant()
    {
        using var tempDir = new TempDir();
        var (unturnedPath, redistPath, nuspecPath) = Setup(tempDir);

        var exit = await new UpdateRunner().RunAsync(
            unturnedPath, redistPath, AppId, force: false, preview: true,
            publicizeAssemblies: [], updateFiles: ["Assembly-CSharp.dll"]);

        exit.ShouldBe(0);
        var previewPath = Path.Combine(redistPath, Constants.PreviewVersionFileName);
        File.Exists(previewPath).ShouldBeTrue();
        var info = JsonSerializer.Deserialize<VersionInfo>(await File.ReadAllTextAsync(previewPath))!;
        info.NuGetVersion.ShouldBe($"{ExpectedVersion}-preview{ExpectedBuildId}");
        new NuspecHandler(nuspecPath).GetVersion().ShouldBe($"{ExpectedVersion}-preview{ExpectedBuildId}");
    }

    [Fact]
    public async Task ShouldReturnErrorWhenManagedDirMissing()
    {
        using var tempDir = new TempDir();
        var (unturnedPath, redistPath, _) = Setup(tempDir);
        // Keep Unturned_Data but remove its Managed subdir -> validation returns 1.
        Directory.Delete(Path.Combine(unturnedPath, "Unturned_Data", Constants.ManagedDirName), recursive: true);

        var exit = await new UpdateRunner().RunAsync(
            unturnedPath, redistPath, AppId, force: false, preview: false,
            publicizeAssemblies: [], updateFiles: ["Assembly-CSharp.dll"]);

        exit.ShouldBe(1);
    }
}
