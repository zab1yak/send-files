using System.Globalization;
using System.Text;

namespace SendFiles.Core.Files;

/// <summary>
/// Turns an untrusted file name from the phone into a safe single Windows file name.
/// Never returns a path: separators, traversal, ADS (<c>:</c>), reserved device names, bidi tricks
/// and trailing dots/spaces are removed or neutralised.
/// </summary>
public static class FileNameSanitizer
{
    public const int MaxLength = 200;
    public const string Fallback = "file";
    private const int MaxExtensionLength = 20;

    private static readonly HashSet<string> ReservedNames = BuildReserved();

    public static string Sanitize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Fallback;
        }

        // Only the last segment counts: "../../x.txt" and "C:\a\b.txt" both become their file name.
        var lastSeparator = raw.LastIndexOfAny(['/', '\\']);
        var name = lastSeparator >= 0 ? raw[(lastSeparator + 1)..] : raw;

        name = name.Normalize(NormalizationForm.FormC);

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (!IsDropped(ch))
            {
                builder.Append(ch);
            }
        }

        name = builder.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length == 0)
        {
            return Fallback;
        }

        if (IsReserved(name))
        {
            name = "_" + name;
        }

        return Truncate(name);
    }

    /// <summary>"photo.jpg" → "photo (2).jpg"; <paramref name="exists"/> decides which names are taken.</summary>
    public static string MakeUnique(string name, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(exists);
        if (!exists(name))
        {
            return name;
        }

        var (stem, extension) = Split(name);
        for (var i = 2; i < 10_000; i++)
        {
            var suffix = string.Create(CultureInfo.InvariantCulture, $" ({i})");
            var candidate = Cut(stem, MaxLength - suffix.Length - extension.Length) + suffix + extension;
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Too many files with the same name.");
    }

    private static bool IsDropped(char ch) =>
        ch < 0x20 || ch == 0x7F || (ch >= 0x80 && ch <= 0x9F)          // control characters
        || ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
        || ch is '\u200B' or '\u200C' or '\u200D' or '\u200E' or '\u200F' or '\uFEFF' or '\u061C'
        || (ch >= '\u202A' && ch <= '\u202E')                           // bidi embeddings/overrides
        || (ch >= '\u2066' && ch <= '\u2069');                          // bidi isolates

    private static bool IsReserved(string name)
    {
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        var stem = (dot >= 0 ? name[..dot] : name).TrimEnd(' ');
        return ReservedNames.Contains(stem);
    }

    private static string Truncate(string name)
    {
        if (name.Length <= MaxLength)
        {
            return name;
        }

        var (stem, extension) = Split(name);
        if (extension.Length > MaxExtensionLength)
        {
            return Cut(name, MaxLength).TrimEnd('.', ' ');
        }

        return Cut(stem, MaxLength - extension.Length).TrimEnd('.', ' ') + extension;
    }

    private static (string Stem, string Extension) Split(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? (name[..dot], name[dot..]) : (name, string.Empty);
    }

    private static string Cut(string value, int length)
    {
        if (length <= 0)
        {
            return Fallback;
        }

        if (value.Length <= length)
        {
            return value;
        }

        // Don't split a surrogate pair (emoji).
        if (char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }

    private static HashSet<string> BuildReserved()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" };
        foreach (var prefix in new[] { "COM", "LPT" })
        {
            for (var i = 0; i <= 9; i++)
            {
                set.Add(prefix + i.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var superscript in new[] { '¹', '²', '³' })
            {
                set.Add(prefix + superscript);
            }
        }

        return set;
    }
}
