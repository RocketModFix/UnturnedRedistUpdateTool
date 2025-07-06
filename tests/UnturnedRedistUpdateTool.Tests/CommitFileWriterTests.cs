using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class CommitFileWriterTests
{
    [Theory]
    [InlineData(2025, 7, 5, "3.25.7.4", "4202", true, "05 July 2025 - Version 3.25.7.4 (4202) [Forced]")]
    [InlineData(2024, 1, 30, "3.25.6.0", "1", true, "30 January 2024 - Version 3.25.6.0 (1) [Forced]")]
    [InlineData(2030, 2, 5, "3.25.6.12", "0", true, "05 February 2030 - Version 3.25.6.12 (0) [Forced]")]
    [InlineData(2027, 7, 5, "3.25.7.4", "2351", false, "05 July 2027 - Version 3.25.7.4 (2351)")]
    [InlineData(2028, 6, 20, "3.28.2.0", "3805", false, "20 June 2028 - Version 3.28.2.0 (3805)")]
    [InlineData(2028, 6, 6, "3.28.2.0", "6960", false, "06 June 2028 - Version 3.28.2.0 (6960)")]
    [InlineData(2028, 6, 6, "3.0.2.8", "6960", false, "06 June 2028 - Version 3.0.2.8 (6960)")]
    [InlineData(2030, 1, 1, "3.30.0.0", "0011", false, "01 January 2030 - Version 3.30.0.0 (0011)")]
    public async Task ShouldContainVersionAndDate(int year, int month, int day, string version, string buildId, bool force, string expected)
    {
        using var tempDir = new TempDir();
        var sourceDir = tempDir.Path;
        var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        var writer = new CommitFileWriter(() => date);
        await writer.WriteAsync(sourceDir, version, buildId, force);
        var commit = await File.ReadAllTextAsync(Path.Combine(sourceDir, ".commit"));
        commit.ShouldBe(expected);
    }
}