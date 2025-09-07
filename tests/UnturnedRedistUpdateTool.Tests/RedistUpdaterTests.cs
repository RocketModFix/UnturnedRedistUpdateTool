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

        File.WriteAllText(Path.Combine(source, "Unchanged.dll"), "same");
        File.WriteAllText(Path.Combine(target, "Unchanged.dll"), "same");
        
        // Add a static file that should not be in manifest
        File.WriteAllText(Path.Combine(target, "README.md"), "static content");

        var updater = new RedistUpdater(source, target, [], ["Test.dll"]);
        var (updated, manifests) = await updater.UpdateAsync();

        updated.ShouldContainKey(Path.Combine(source, "Test.dll"));
        updated.ShouldNotContainKey(Path.Combine(source, "Unchanged.dll"));

        File.ReadAllText(Path.Combine(target, "Test.dll")).ShouldBe("hello");

        manifests.ShouldContainKey("Test.dll");
        manifests.Count.ShouldBe(1);

        var manifestPath = Path.Combine(target, "manifest.sha256.json");
        File.Exists(manifestPath).ShouldBeTrue();

        var manifestContent = File.ReadAllText(manifestPath);
        manifestContent.ShouldContain("Test.dll");
        manifestContent.ShouldNotContain("Unchanged.dll"); // Not in updateFiles list
        manifestContent.ShouldNotContain("README.md"); // Static file
    }

    [Fact]
    public async Task ShouldOnlyUpdateSpecifiedFilesWhenUpdateFilesListProvided()
    {
        using var tempDir = new TempDir();
        var sourceDir = tempDir.Path;
        var source = Path.Combine(sourceDir, "source");
        var target = Path.Combine(sourceDir, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        File.WriteAllText(Path.Combine(source, "Test1.dll"), "hello1");
        File.WriteAllText(Path.Combine(target, "Test1.dll"), "stale1");

        File.WriteAllText(Path.Combine(source, "Test2.dll"), "hello2");
        File.WriteAllText(Path.Combine(target, "Test2.dll"), "stale2");

        File.WriteAllText(Path.Combine(source, "Test3.dll"), "hello3");
        File.WriteAllText(Path.Combine(target, "Test3.dll"), "stale3");

        List<string> updateFiles = ["Test1.dll", "Test3.dll"];
        var updater = new RedistUpdater(source, target, [], updateFiles);
        var (updated, manifests) = await updater.UpdateAsync();

        updated.ShouldContainKey(Path.Combine(source, "Test1.dll"));
        updated.ShouldContainKey(Path.Combine(source, "Test3.dll"));
        updated.ShouldNotContainKey(Path.Combine(source, "Test2.dll"));

        File.ReadAllText(Path.Combine(target, "Test1.dll")).ShouldBe("hello1");
        File.ReadAllText(Path.Combine(target, "Test3.dll")).ShouldBe("hello3");
        File.ReadAllText(Path.Combine(target, "Test2.dll")).ShouldBe("stale2");

        manifests.ShouldContainKey("Test1.dll");
        manifests.ShouldContainKey("Test3.dll");
        manifests.ShouldNotContainKey("Test2.dll");
        manifests.Count.ShouldBe(2);

        var manifestPath = Path.Combine(target, "manifest.sha256.json");
        File.Exists(manifestPath).ShouldBeTrue();

        var manifestContent = File.ReadAllText(manifestPath);
        manifestContent.ShouldContain("Test1.dll");
        manifestContent.ShouldContain("Test3.dll");
        manifestContent.ShouldNotContain("Test2.dll");
    }

    [Fact]
    public async Task ShouldThrowExceptionWhenSpecifiedFileDoesNotExist()
    {
        using var tempDir = new TempDir();
        var sourceDir = tempDir.Path;
        var source = Path.Combine(sourceDir, "source");
        var target = Path.Combine(sourceDir, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        File.WriteAllText(Path.Combine(source, "Test1.dll"), "hello1");
        File.WriteAllText(Path.Combine(target, "Test1.dll"), "stale1");

        List<string> updateFiles = ["Test1.dll", "NonExistent.dll"];
        var updater = new RedistUpdater(source, target, [], updateFiles);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => updater.UpdateAsync());
        exception.Message.ShouldContain("NonExistent.dll");
        exception.Message.ShouldContain("-update-files");
    }
}