using System.Text.Json.Serialization;

namespace SendFiles.Core.Hosting;

/// <summary>Wire format of the HTTP API (camelCase, no indentation).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PingResponse))]
internal sealed partial class CoreApiJsonContext : JsonSerializerContext;
