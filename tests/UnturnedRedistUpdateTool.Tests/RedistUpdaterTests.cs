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

        // Create one changed file and one unchanged file
        File.WriteAllText(Path.Combine(source, "Test.dll"), "hello");
        File.WriteAllText(Path.Combine(target, "Test.dll"), "stale");

        File.WriteAllText(Path.Combine(source, "Unchanged.dll"), "same");
        File.WriteAllText(Path.Combine(target, "Unchanged.dll"), "same");

        var updater = new RedistUpdater(source, target, []);
        var (updated, manifests) = await updater.UpdateAsync();

        updated.ShouldContainKey(Path.Combine(source, "Test.dll"));
        updated.ShouldNotContainKey(Path.Combine(source, "Unchanged.dll"));

        File.ReadAllText(Path.Combine(target, "Test.dll")).ShouldBe("hello");

        // Manifest checks
        manifests.ShouldContainKey("Test.dll");
        manifests.Count.ShouldBe(1);

        var manifestPath = Path.Combine(target, "manifest.sha256.json");
        File.Exists(manifestPath).ShouldBeTrue();

        var manifestContent = File.ReadAllText(manifestPath);
        manifestContent.ShouldContain("Test.dll");
    }
}