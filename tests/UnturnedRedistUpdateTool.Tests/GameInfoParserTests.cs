using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class GameInfoParserTests
{
    [Theory]
    [InlineData("304930", "3.25.6.1", "18694317")]
    [InlineData("1110390", "3.25.6.1", "19099921")]
    public async Task ReturnsVersionAndBuildId_WhenFilesExist(string appId, string expectedVersion, string expectedBuildId)
    {
        var testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        var unturnedPath = Path.Combine(testDataDir, "Unturned");

        var appManifest = GameInfoParser.FindAppManifestFile(testDataDir, appId);
        var (version, buildId) = await GameInfoParser.ParseAsync(unturnedPath, appManifest);

        version.ShouldBe(expectedVersion);
        buildId.ShouldBe(expectedBuildId);
    }

    [Theory]
    [InlineData("missing_appid")]
    [InlineData("123456789")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData(" ")]
    public void ThrowsFileNotFoundException_WhenAppManifestMissing(string appId)
    {
        var testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        var unturnedPath = Path.Combine(testDataDir, "Unturned");

        Should.Throw<FileNotFoundException>(() =>
            GameInfoParser.FindAppManifestFile(unturnedPath, appId));
    }

    private static string AppManifest =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "steamapps", "appmanifest_1110390.acf");

    [Fact]
    public async Task ShouldThrowWhenGameSectionMissing()
    {
        using var tempDir = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tempDir.Path, "Status.json"), "{ \"NotGame\": {} }");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            GameInfoParser.ParseAsync(tempDir.Path, AppManifest));
    }

    [Fact]
    public async Task ShouldThrowWhenVersionFieldsMissing()
    {
        using var tempDir = new TempDir();
        await File.WriteAllTextAsync(Path.Combine(tempDir.Path, "Status.json"), "{ \"Game\": { \"Major_Version\": 25 } }");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            GameInfoParser.ParseAsync(tempDir.Path, AppManifest));
    }

    [Fact]
    public async Task ShouldThrowWhenStatusFileMissing()
    {
        using var tempDir = new TempDir();

        await Should.ThrowAsync<FileNotFoundException>(() =>
            GameInfoParser.ParseAsync(tempDir.Path, AppManifest));
    }
}