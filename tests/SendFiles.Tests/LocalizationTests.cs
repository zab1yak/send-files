using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SendFiles.Tests;

public partial class LocalizationTests
{
    private static readonly string AppDir = Path.Combine(RepoRoot(), "src", "SendFiles.App");

    [Fact]
    public void Russian_and_english_have_the_same_keys()
    {
        var en = Keys(Path.Combine(AppDir, "Resources", "Strings.resx"));
        var ru = Keys(Path.Combine(AppDir, "Resources", "Strings.ru.resx"));

        Assert.Equal(en.Order(StringComparer.Ordinal), ru.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_key_has_a_typed_property_and_vice_versa()
    {
        var keys = Keys(Path.Combine(AppDir, "Resources", "Strings.resx"));
        var source = File.ReadAllText(Path.Combine(AppDir, "Resources", "Strings.cs"));
        var properties = PropertyPattern().Matches(source).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(keys.Order(StringComparer.Ordinal), properties.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void No_value_is_empty()
    {
        foreach (var file in new[] { "Strings.resx", "Strings.ru.resx" })
        {
            var doc = XDocument.Load(Path.Combine(AppDir, "Resources", file));
            Assert.All(doc.Root!.Elements("data"), d => Assert.False(string.IsNullOrWhiteSpace(d.Element("value")?.Value), $"{file}: {d.Attribute("name")?.Value}"));
        }
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SendFiles.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private static HashSet<string> Keys(string path) =>
        XDocument.Load(path).Root!.Elements("data").Select(d => d.Attribute("name")!.Value).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"public static string (\w+) => Get\(nameof\(\1\)\);")]
    private static partial Regex PropertyPattern();
}
