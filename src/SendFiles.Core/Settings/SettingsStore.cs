using SendFiles.Core.Persistence;

namespace SendFiles.Core.Settings;

public sealed class SettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private AppSettings _current;

    public SettingsStore(string path)
    {
        _path = path;
        _current = AtomicJsonFile.Read(path, CoreJsonContext.Default.AppSettings, () => new AppSettings()).Normalize();
    }

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        AppSettings updated;
        lock (_gate)
        {
            updated = change(_current).Normalize();
            if (updated == _current)
            {
                return;
            }

            AtomicJsonFile.Write(_path, updated, CoreJsonContext.Default.AppSettings);
            _current = updated;
        }

        Changed?.Invoke(this, updated);
    }
}
