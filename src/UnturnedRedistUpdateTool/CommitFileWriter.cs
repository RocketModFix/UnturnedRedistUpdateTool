namespace UnturnedRedistUpdateTool;

public delegate DateTimeOffset GetTime();

public class CommitFileWriter
{
    private readonly GetTime _getTime;

    public CommitFileWriter(GetTime? getTime = null)
    {
        _getTime = getTime ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task WriteAsync(string path, string version, string buildId, bool force)
    {
        var forcedNote = force ? " [Forced]" : "";
        var line = $"{_getTime():dd MMMM yyyy} - Version {version} ({buildId}){forcedNote}";
        await File.WriteAllTextAsync(Path.Combine(path, ".commit"), line);
    }
}