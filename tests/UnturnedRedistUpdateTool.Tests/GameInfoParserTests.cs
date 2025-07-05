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
        var steamappsPath = Path.Combine(testDataDir, "Steamapps");

        var parser = new GameInfoParser();

        var (version, buildId) = await parser.ParseAsync(unturnedPath, steamappsPath, appId);

        version.ShouldBe(expectedVersion);
        buildId.ShouldBe(expectedBuildId);
    }

    [Fact]
    public async Task ThrowsFileNotFoundException_WhenAppManifestMissing()
    {
        var testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        var unturnedPath = Path.Combine(testDataDir, "Unturned");
        var steamappsPath = Path.Combine(testDataDir, "Steamapps");
        var appId = "missing_appid";

        var parser = new GameInfoParser();

        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await parser.ParseAsync(unturnedPath, steamappsPath, appId));
    }
}