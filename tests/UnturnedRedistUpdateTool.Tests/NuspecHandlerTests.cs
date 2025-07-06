using Shouldly;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

public class NuspecHandlerTests
{
    private readonly string _testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData", "Redist");
    private readonly string _realNuspecPath;

    public NuspecHandlerTests()
    {
        _realNuspecPath = Path.Combine(_testDataDir, "Test.Unturned.Redist.Client.nuspec");
        if (!File.Exists(_realNuspecPath))
            throw new FileNotFoundException($"Test nuspec file not found at {_realNuspecPath}");
    }

    [Fact]
    public void GetVersion_ShouldReturnCurrentVersion()
    {
        var handler = new NuspecHandler(_realNuspecPath);
        var version = handler.GetVersion();
        version.ShouldNotBeNullOrEmpty();
        version.ShouldStartWith("3.");
    }

    [Fact]
    public void UpdateVersion_ShouldModifyVersionElement()
    {
        using var tempDir = new TempDir();
        var tempNuspecPath = Path.Combine(tempDir.Path, Path.GetFileName(_realNuspecPath));
        File.Copy(_realNuspecPath, tempNuspecPath);

        var handler = new NuspecHandler(tempNuspecPath);

        var newVersion = "1.0.0-test";
        handler.UpdateVersion(newVersion);
        handler.Save();

        var reloadedHandler = new NuspecHandler(tempNuspecPath);
        reloadedHandler.GetVersion().ShouldBe(newVersion);
    }

    [Fact]
    public void CreateVersion_ShouldReturnCorrectFormat()
    {
        var handler = new NuspecHandler(_realNuspecPath);
        var result = handler.CreateVersion("3.25.7.2", "20398");
        result.ShouldBe("3.25.7.2-build20398");
    }
}