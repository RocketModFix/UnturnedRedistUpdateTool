using Shouldly;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class RedistUpdaterTests
{
    [Fact]
    public async Task ShouldCopyOnlyChangedFilesAndGenerateManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var source = Path.Combine(tempDir, "source");
        var target = Path.Combine(tempDir, "target");
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

        Directory.Delete(tempDir, true);
    }
}