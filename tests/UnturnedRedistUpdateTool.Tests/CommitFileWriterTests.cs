using Shouldly;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class CommitFileWriterTests
{
    [Theory]
    [InlineData(2025, 7, 5, "3.25.7.4", "4202", true)]
    [InlineData(2024, 1, 30, "3.25.6.0", "1", true)]
    [InlineData(2030, 2, 5, "3.25.6.12", "0", true)]
    [InlineData(2027, 7, 5, "3.25.7.4", "2351", false)]
    [InlineData(2028, 6, 20, "3.28.2.0", "3805", false)]
    [InlineData(2028, 6, 6, "3.28.2.0", "6960", false)]
    [InlineData(2028, 6, 6, "3.0.2.8", "6960", false)]
    [InlineData(2030, 1, 1, "3.30.0.0", "0011", false)]
    public async Task ShouldContainVersionAndDate(int year, int month, int day, string version, string buildId, bool force)
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);

        var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        var writer = new CommitFileWriter(() => date);
        await writer.WriteAsync(temp, version, buildId, force);

        var commit = await File.ReadAllTextAsync(Path.Combine(temp, ".commit"));
        var forcedText = force ? " [Forced]" : "";
        commit.ShouldBe($"{date:dd MMMM yyyy} - Version {version} ({buildId})" + forcedText);

        Directory.Delete(temp, true);
    }
}