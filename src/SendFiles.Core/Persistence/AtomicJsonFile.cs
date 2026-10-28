using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SendFiles.Core.Persistence;

/// <summary>JSON files written atomically (temp file → flush → move). A corrupt file is set aside, never fatal.</summary>
public static class AtomicJsonFile
{
    public static T Read<T>(string path, JsonTypeInfo<T> typeInfo, Func<T> fallback, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(fallback);
        if (!File.Exists(path))
        {
            return fallback();
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize(stream, typeInfo) ?? fallback();
        }
        catch (JsonException)
        {
            Quarantine(path, time ?? TimeProvider.System);
            return fallback();
        }
        catch (IOException)
        {
            // Locked by another process (antivirus, sync client): use defaults, keep the file.
            return fallback();
        }
    }

    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);

        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, typeInfo);
            stream.Flush(flushToDisk: true);
        }

        try
        {
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    private static void Quarantine(string path, TimeProvider time)
    {
        var stamp = time.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        try
        {
            File.Move(path, $"{path}.corrupt-{stamp}", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort: if it cannot be moved, the next successful write replaces it.
        }
    }
}
