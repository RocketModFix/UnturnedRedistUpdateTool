using Shouldly;
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
}