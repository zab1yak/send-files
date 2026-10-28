namespace SendFiles.Core.Files;

/// <summary>
/// Marks a received file as coming from the network (NTFS <c>Zone.Identifier</c> stream, ZoneId=3 "Internet"),
/// like a browser download, so Windows SmartScreen and Office Protected View treat it accordingly.
/// </summary>
public static class MarkOfTheWeb
{
    public const string StreamName = "Zone.Identifier";
    private const string Content = "[ZoneTransfer]\r\nZoneId=3\r\n";

    /// <returns>False if the volume does not support alternate data streams (FAT32, exFAT).</returns>
    public static bool TryWrite(string filePath)
    {
        try
        {
            File.WriteAllText($"{filePath}:{StreamName}", Content);
            return true;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string? Read(string filePath)
    {
        try
        {
            return File.ReadAllText($"{filePath}:{StreamName}");
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
