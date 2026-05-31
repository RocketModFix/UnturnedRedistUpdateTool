using System.Xml.Linq;

namespace UnturnedRedistUpdateTool;

public class NuspecHandler
{
    private const string MetadataElement = "metadata";
    private const string VersionElement = "version";

    private readonly string _nuspecFilePath;
    private readonly XNamespace _ns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";
    private readonly XDocument _doc;

    public NuspecHandler(string nuspecFilePath)
    {
        _nuspecFilePath = nuspecFilePath;
        _doc = XDocument.Load(_nuspecFilePath, LoadOptions.PreserveWhitespace);
    }

    public string? GetVersion() =>
        _doc.Root?.Element(_ns + MetadataElement)?.Element(_ns + VersionElement)?.Value;

    public void UpdateVersion(string newVersion)
    {
        var versionElement = _doc.Root?.Element(_ns + MetadataElement)?.Element(_ns + VersionElement);
        if (versionElement == null)
            throw new InvalidOperationException(
                "Version element not found in nuspec (expected <package><metadata><version>).");
        versionElement.Value = newVersion;
    }

    public void Save() => _doc.Save(_nuspecFilePath);
}
