using SendFiles.Core.Files;

namespace SendFiles.Tests.Files;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("photo.jpg", "photo.jpg")]
    [InlineData("Фото отпуска.heic", "Фото отпуска.heic")]
    [InlineData("../../Windows/system.ini", "system.ini")]
    [InlineData("..\\..\\boot.ini", "boot.ini")]
    [InlineData("C:\\Users\\x\\evil.exe", "evil.exe")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("..", "file")]
    [InlineData(".", "file")]
    [InlineData("...", "file")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    [InlineData("   ", "file")]
    [InlineData("name.txt:hidden", "name.txthidden")]          // alternate data stream
    [InlineData("a<b>c:d\"e|f?g*h.txt", "abcdefgh.txt")]
    [InlineData("report.pdf.", "report.pdf")]                  // trailing dot
    [InlineData("report.pdf   ", "report.pdf")]                // trailing spaces
    [InlineData("report.pdf . . ", "report.pdf")]
    [InlineData("tab\there.txt", "tabhere.txt")]
    [InlineData("new\nline.txt", "newline.txt")]
    [InlineData("CON", "_CON")]
    [InlineData("con.txt", "_con.txt")]
    [InlineData("Nul.tar.gz", "_Nul.tar.gz")]
    [InlineData("AUX", "_AUX")]
    [InlineData("PRN.log", "_PRN.log")]
    [InlineData("COM1", "_COM1")]
    [InlineData("lpt9.doc", "_lpt9.doc")]
    [InlineData("COM¹", "_COM¹")]
    [InlineData("COM10", "COM10")]
    [InlineData("CONSOLE.txt", "CONSOLE.txt")]
    [InlineData("invoice\u202Egpj.exe", "invoicegpj.exe")]       // right-to-left override spoofing ".jpg"
    [InlineData("zero\u200Bwidth.txt", "zerowidth.txt")]
    [InlineData(".gitignore", ".gitignore")]
    public void Sanitizes(string? input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Normalizes_to_nfc()
    {
        var decomposed = "e\u0301.txt"; // e + combining acute
        Assert.Equal("\u00e9.txt", FileNameSanitizer.Sanitize(decomposed));
    }

    [Fact]
    public void Long_name_is_cut_keeping_extension()
    {
        var result = FileNameSanitizer.Sanitize(new string('a', 500) + ".jpeg");

        Assert.Equal(FileNameSanitizer.MaxLength, result.Length);
        Assert.EndsWith(".jpeg", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_name_does_not_split_emoji()
    {
        var result = FileNameSanitizer.Sanitize(new string('a', FileNameSanitizer.MaxLength - 5) + "😀😀😀😀😀😀.png");

        Assert.True(result.Length <= FileNameSanitizer.MaxLength);
        Assert.False(result.Any(char.IsSurrogate) && result.Count(char.IsHighSurrogate) != result.Count(char.IsLowSurrogate));
        Assert.EndsWith(".png", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Result_never_contains_separators_or_invalid_characters()
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var input in new[] { "a/b\\c", "x:y", "\0null", "..\\..\\..\\", "a|b" })
        {
            var result = FileNameSanitizer.Sanitize(input);
            Assert.DoesNotContain(result, c => invalid.Contains(c));
        }
    }

    [Fact]
    public void Conflict_gets_numbered_suffix()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "photo.jpg", "photo (2).jpg" };

        Assert.Equal("photo (3).jpg", FileNameSanitizer.MakeUnique("photo.jpg", taken.Contains));
        Assert.Equal("other.jpg", FileNameSanitizer.MakeUnique("other.jpg", taken.Contains));
        Assert.Equal("README (2)", FileNameSanitizer.MakeUnique("README", n => n == "README"));
    }
}
