using System.Xml.Linq;

namespace UnturnedRedistUpdateTool;

public class NuspecHandler
{
    private readonly string _nuspecFilePath;
    private readonly XNamespace _ns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";
    private readonly XDocument _doc;

    public NuspecHandler(string nuspecFilePath)
    {
        _nuspecFilePath = nuspecFilePath;
        _doc = XDocument.Load(_nuspecFilePath, LoadOptions.PreserveWhitespace);
    }

    public string? GetVersion() => _doc.Root.Element(_ns + "metadata").Element(_ns + "version")?.Value;

    public void UpdateVersion(string newVersion)
    {
        var versionElement = _doc.Root.Element(_ns + "metadata").Element(_ns + "version");
        if (versionElement == null)
            throw new InvalidOperationException("Version element missing in nuspec");
        versionElement.Value = newVersion;
    }

    public void Save() => _doc.Save(_nuspecFilePath);
}