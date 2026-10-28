using System.Text.Json.Serialization;
using SendFiles.Core.Settings;

namespace SendFiles.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SendFiles.Core.Devices.DeviceFile))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
