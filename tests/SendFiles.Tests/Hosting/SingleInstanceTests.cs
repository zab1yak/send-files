using SendFiles.Core.Hosting;

namespace SendFiles.Tests.Hosting;

public class SingleInstanceTests
{
    private static string UniqueSuffix() => "-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Second_acquire_fails_while_first_is_alive()
    {
        var suffix = UniqueSuffix();
        using var first = SingleInstance.TryAcquire(suffix);

        Assert.NotNull(first);
        var second = await Task.Run(() => SingleInstance.TryAcquire(suffix), TestContext.Current.CancellationToken);
        Assert.Null(second);
    }

    [Fact]
    public async Task Arguments_from_several_launches_arrive_in_one_batch()
    {
        var suffix = UniqueSuffix();
        using var primary = SingleInstance.TryAcquire(suffix)!;
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ArgumentsReceived += (_, args) => received.TrySetResult(args);
        primary.Listen();

        Assert.True(SingleInstance.SendToPrimary(["--send", @"C:\a.txt"], suffix));
        Assert.True(SingleInstance.SendToPrimary(["--send", @"C:\Папка\б.jpg"], suffix));

        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(["--send", @"C:\a.txt", "--send", @"C:\Папка\б.jpg"], args);
    }

    [Fact]
    public void Send_without_primary_returns_false()
    {
        Assert.False(SingleInstance.SendToPrimary(["x"], UniqueSuffix(), TimeSpan.FromMilliseconds(200)));
    }
}
