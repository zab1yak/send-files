using SendFiles.Core.Persistence;

namespace SendFiles.Core.Devices;

public sealed record TrustedDevice(string Id, string Name, string KeyHash, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

public sealed record DeviceFile(int Version, IReadOnlyList<TrustedDevice> Devices)
{
    public static DeviceFile Empty => new(1, []);
}

/// <summary>Trusted devices (devices.json). Only key hashes are stored.</summary>
public sealed class DeviceStore
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private Dictionary<string, TrustedDevice> _devices;

    public DeviceStore(string path, TimeProvider time)
    {
        _path = path;
        _time = time;
        var file = AtomicJsonFile.Read(path, CoreJsonContext.Default.DeviceFile, () => DeviceFile.Empty);
        _devices = file.Devices.ToDictionary(d => d.Id, StringComparer.Ordinal);
    }

    /// <summary>Raised after add/remove (not on last-seen updates).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a device is removed; the server closes its connections and tickets.</summary>
    public event EventHandler<string>? Revoked;

    public IReadOnlyList<TrustedDevice> List()
    {
        lock (_gate)
        {
            return [.. _devices.Values.OrderByDescending(d => d.LastSeenAt)];
        }
    }

    /// <returns>The new device's key — shown to the phone exactly once.</returns>
    public (TrustedDevice Device, string Key) Add(string name)
    {
        var (id, secret, key) = DeviceKey.Generate();
        var now = _time.GetUtcNow();
        var device = new TrustedDevice(id, name, DeviceKey.Hash(secret), now, now);
        lock (_gate)
        {
            _devices[id] = device;
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return (device, key);
    }

    /// <summary>Checks a key from the Authorization header; updates last-seen at most once a minute.</summary>
    public TrustedDevice? Authenticate(string? key)
    {
        if (!DeviceKey.TryParse(key, out var id, out var secret))
        {
            return null;
        }

        lock (_gate)
        {
            if (!_devices.TryGetValue(id, out var device) || !DeviceKey.Verify(secret, device.KeyHash))
            {
                return null;
            }

            var now = _time.GetUtcNow();
            if (now - device.LastSeenAt >= TouchInterval)
            {
                device = device with { LastSeenAt = now };
                _devices[id] = device;
                TrySave();
            }

            return device;
        }
    }

    public TrustedDevice? Find(string id)
    {
        lock (_gate)
        {
            return _devices.GetValueOrDefault(id);
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (!_devices.Remove(id))
            {
                return false;
            }

            Save();
        }

        Revoked?.Invoke(this, id);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RemoveAll()
    {
        string[] ids;
        lock (_gate)
        {
            ids = [.. _devices.Keys];
            _devices = new Dictionary<string, TrustedDevice>(StringComparer.Ordinal);
            Save();
        }

        foreach (var id in ids)
        {
            Revoked?.Invoke(this, id);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save() =>
        AtomicJsonFile.Write(_path, new DeviceFile(1, [.. _devices.Values]), CoreJsonContext.Default.DeviceFile);

    private void TrySave()
    {
        try
        {
            Save();
        }
        catch (IOException)
        {
            // Last-seen is cosmetic; the next touch retries.
        }
    }
}
