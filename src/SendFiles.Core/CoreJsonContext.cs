using System.Text.Json.Serialization;
using SendFiles.Core.Settings;

namespace SendFiles.Core;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
