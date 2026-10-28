using SendFiles.Core.Persistence;

namespace SendFiles.Tests.Persistence;

public class DataDirectoryTests
{
    [Fact]
    public void Environment_override_wins()
    {
        var dir = DataDirectory.Resolve(isDev: false, environmentOverride: @"D:\custom", localAppData: @"C:\Users\x\AppData\Local");

        Assert.Equal(@"D:\custom", dir.Root);
    }

    [Fact]
    public void Release_uses_separate_folder_from_velopack()
    {
        var dir = DataDirectory.Resolve(isDev: false, environmentOverride: "", localAppData: @"C:\L");

        Assert.Equal(@"C:\L\SendFiles.Data", dir.Root);
        Assert.Equal("", dir.InstanceSuffix);
    }

    [Fact]
    public void Dev_build_is_isolated()
    {
        var dir = DataDirectory.Resolve(isDev: true, environmentOverride: "", localAppData: @"C:\L");

        Assert.Equal(@"C:\L\SendFiles.Data-dev", dir.Root);
        Assert.Equal("-dev", dir.InstanceSuffix);
    }
}
