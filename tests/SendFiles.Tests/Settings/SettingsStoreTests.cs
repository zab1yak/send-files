using SendFiles.Core.Networking;
using SendFiles.Core.Settings;

namespace SendFiles.Tests.Settings;

public class SettingsStoreTests
{
    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var temp = new TempDir();

        var store = new SettingsStore(temp.File("settings.json"));

        Assert.Equal(PortSelector.DefaultPort, store.Current.Port);
        Assert.False(store.Current.Autostart);
        Assert.Equal("auto", store.Current.Language);
        Assert.Null(store.Current.ReceiveFolder);
    }

    [Fact]
    public void Corrupt_file_gives_defaults_and_is_set_aside()
    {
        using var temp = new TempDir();
        var path = temp.File("settings.json");
        File.WriteAllText(path, "{ not json");

        var store = new SettingsStore(path);

        Assert.Equal(PortSelector.DefaultPort, store.Current.Port);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(temp.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Update_persists_and_raises_changed()
    {
        using var temp = new TempDir();
        var path = temp.File("settings.json");
        var store = new SettingsStore(path);
        AppSettings? raised = null;
        store.Changed += (_, s) => raised = s;

        store.Update(s => s with { Port = 50000, Language = "ru" });

        Assert.Equal(50000, raised?.Port);
        var reloaded = new SettingsStore(path);
        Assert.Equal(50000, reloaded.Current.Port);
        Assert.Equal("ru", reloaded.Current.Language);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Unchanged_update_does_not_raise()
    {
        using var temp = new TempDir();
        var store = new SettingsStore(temp.File("settings.json"));
        var count = 0;
        store.Changed += (_, _) => count++;

        store.Update(s => s);

        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(70000)]
    [InlineData(-1)]
    public void Invalid_port_is_normalized(int port)
    {
        Assert.Equal(PortSelector.DefaultPort, new AppSettings { Port = port }.Normalize().Port);
    }

    [Fact]
    public void Unknown_language_is_normalized()
    {
        Assert.Equal("auto", new AppSettings { Language = "de" }.Normalize().Language);
    }
}
