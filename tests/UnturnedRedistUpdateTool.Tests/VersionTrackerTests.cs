using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class VersionTrackerTests
{
    [Theory]
    [InlineData(false, "version.json")]
    [InlineData(true, "version.preview.json")]
    public async Task ShouldRoundTripVersionInfoToExpectedFile(bool preview, string expectedFileName)
    {
        using var tempDir = new TempDir();
        var tracker = new VersionTracker(tempDir.Path, preview);

        (await tracker.LoadAsync()).ShouldBeNull(); // nothing saved yet

        var info = new VersionInfo
        {
            GameVersion = "3.25.6.1",
            BuildId = "19099921",
            NuGetVersion = preview ? "3.25.6.1-preview19099921" : "3.25.6.1",
            FilesHash = "ABCDEF0123456789",
            LastUpdated = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        await tracker.SaveAsync(info);

        File.Exists(Path.Combine(tempDir.Path, expectedFileName)).ShouldBeTrue();

        var loaded = await new VersionTracker(tempDir.Path, preview).LoadAsync();
        loaded.ShouldNotBeNull();
        loaded.GameVersion.ShouldBe(info.GameVersion);
        loaded.BuildId.ShouldBe(info.BuildId);
        loaded.NuGetVersion.ShouldBe(info.NuGetVersion);
        loaded.FilesHash.ShouldBe(info.FilesHash);
        loaded.LastUpdated.ShouldBe(info.LastUpdated);
    }
}
