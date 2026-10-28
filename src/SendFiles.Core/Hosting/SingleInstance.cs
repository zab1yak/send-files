using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace SendFiles.Core.Hosting;

/// <summary>
/// One running instance per user (and per build flavour via the suffix). A second launch forwards its
/// command-line arguments to the first one over a current-user-only named pipe and exits.
/// Explorer starts one process per selected file, so arguments arriving close together are batched.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(300);
    private const int MaxMessageBytes = 64 * 1024;

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _batchGate = new();
    private readonly List<string> _batch = [];
    private Timer? _batchTimer;

    private SingleInstance(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    /// <summary>Batched arguments from later launches. Raised on a background thread.</summary>
    public event EventHandler<IReadOnlyList<string>>? ArgumentsReceived;

    public static string BaseName(string suffix)
    {
        var sid = OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().User?.Value : Environment.UserName;
        return $"SendFiles-{sid}{suffix}";
    }

    /// <returns>The instance if this process is the first one; otherwise <c>null</c>.</returns>
    public static SingleInstance? TryAcquire(string suffix)
    {
        var name = BaseName(suffix);
        // Existence of the named mutex is the lock; ownership is not used (it is thread-affine).
        var mutex = new Mutex(initiallyOwned: false, $@"Local\{name}", out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, name);
    }

    /// <summary>Sends arguments to the running instance. Returns false if it could not be reached.</summary>
    public static bool SendToPrimary(IReadOnlyList<string> args, string suffix, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            using var client = new NamedPipeClientStream(".", BaseName(suffix), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeout ?? TimeSpan.FromSeconds(3));
            var payload = Encoding.UTF8.GetBytes(string.Join('\0', args));
            if (payload.Length > MaxMessageBytes)
            {
                return false;
            }

            client.Write(payload);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Starts accepting messages from later launches.</summary>
    public void Listen() => _ = Task.Run(ListenLoopAsync);

    public void Dispose()
    {
        _stop.Cancel();
        _batchTimer?.Dispose();
        _mutex.Dispose();
        _stop.Dispose();
    }

    private async Task ListenLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);

                using var buffer = new MemoryStream();
                var chunk = new byte[4096];
                int read;
                while ((read = await server.ReadAsync(chunk, _stop.Token).ConfigureAwait(false)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > MaxMessageBytes)
                    {
                        break;
                    }
                }

                if (buffer.Length is > 0 and <= MaxMessageBytes)
                {
                    Enqueue(Encoding.UTF8.GetString(buffer.ToArray()).Split('\0'));
                }
                else if (buffer.Length == 0)
                {
                    Enqueue([]); // plain re-launch: just show the window
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Client vanished mid-message; keep listening.
            }
        }
    }

    private void Enqueue(IEnumerable<string> args)
    {
        lock (_batchGate)
        {
            _batch.AddRange(args.Where(a => a.Length > 0));
            _batchTimer ??= new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
            _batchTimer.Change(BatchWindow, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        string[] items;
        lock (_batchGate)
        {
            items = [.. _batch];
            _batch.Clear();
        }

        ArgumentsReceived?.Invoke(this, items);
    }
}
