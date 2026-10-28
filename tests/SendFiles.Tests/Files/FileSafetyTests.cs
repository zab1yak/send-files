using System.Text;
using SendFiles.Core.Files;

namespace SendFiles.Tests.Files;

public class FileSafetyTests
{
    [Theory]
    [InlineData("..\\x.txt")]
    [InlineData("sub\\x.txt")]
    [InlineData("C:\\x.txt")]
    [InlineData("\\\\server\\share\\x.txt")]
    public void ChildPath_rejects_anything_but_a_direct_child(string name)
    {
        using var temp = new TempDir();

        Assert.Throws<UnsafePathException>(() => SafePathGuard.ChildPath(temp.Path, name));
    }

    [Fact]
    public void ChildPath_accepts_plain_name()
    {
        using var temp = new TempDir();

        Assert.Equal(Path.Combine(temp.Path, "a.txt"), SafePathGuard.ChildPath(temp.Path, "a.txt"));
    }

    [Fact]
    public async Task Junction_folder_is_rejected()
    {
        using var temp = new TempDir();
        var target = Directory.CreateDirectory(temp.File("target")).FullName;
        var link = temp.File("link");

        // Junctions (unlike symlinks) need no admin rights or Developer Mode.
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", link, target },
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!)
        {
            await mklink.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, mklink.ExitCode);
        }

        Assert.True(SafePathGuard.IsLink(link));
        Assert.Throws<UnsafePathException>(() => SafePathGuard.EnsureRealDirectory(link));
    }

    [Fact]
    public void Mark_of_the_web_is_written_as_internet_zone()
    {
        using var temp = new TempDir();
        var file = temp.File("received.exe");
        File.WriteAllBytes(file, [1, 2, 3]);

        Assert.True(MarkOfTheWeb.TryWrite(file));

        Assert.Contains("ZoneId=3", MarkOfTheWeb.Read(file), StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(file));
    }

    [Theory]
    [InlineData("", 0x00000000u)]
    [InlineData("a", 0xE8B7BE43u)]
    [InlineData("123456789", 0xCBF43926u)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Crc32_matches_reference_values(string input, uint expected)
    {
        Assert.Equal(expected, Crc32.Compute(Encoding.ASCII.GetBytes(input)));
    }

    [Fact]
    public void Crc32_incremental_equals_one_shot()
    {
        var data = Enumerable.Range(0, 10_000).Select(i => (byte)(i * 31)).ToArray();
        var crc = new Crc32();
        crc.Append(data.AsSpan(0, 4000));
        crc.Append(data.AsSpan(4000));

        Assert.Equal(Crc32.Compute(data), crc.Value);
    }
}
