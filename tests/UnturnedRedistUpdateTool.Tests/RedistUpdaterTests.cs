using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class RedistUpdaterTests
{
    [Fact]
    public async Task ShouldCopyOnlyChangedFilesAndGenerateManifest()
    {
        using var tempDir = new TempDir();
        var sourceDir = tempDir.Path;
        var source = Path.Combine(sourceDir, "source");
        var target = Path.Combine(sourceDir, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        File.WriteAllText(Path.Combine(source, "Test.dll"), "hello");
        File.WriteAllText(Path.Combine(target, "Test.dll"), "stale");

        var updater = new RedistUpdater(source, target);
        var updated = await updater.UpdateAsync();

        updated.ShouldContainKey(Path.Combine(source, "Test.dll"));
        File.ReadAllText(Path.Combine(target, "Test.dll")).ShouldBe("hello");

        var manifest = File.ReadAllText(Path.Combine(target, "manifest.sha256.json"));
        manifest.ShouldContain("Test.dll");
    }
}