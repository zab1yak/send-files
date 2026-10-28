using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SendFiles.Web;

namespace SendFiles.Core.Hosting;

/// <summary>Request pipeline and endpoints of the phone-facing server. Shared by the real host and tests.</summary>
public static class SendFilesEndpoints
{
    public static void Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/ping", () => Results.Json(new PingResponse(true), CoreApiJsonContext.Default.PingResponse));
        app.MapGet("/", ServeIndex);
        app.MapGet("/pair", ServeIndex);
    }

    private static IResult ServeIndex()
    {
        var stream = WebAssets.Assembly.GetManifestResourceStream($"{WebAssets.Root}/index.html");
        return stream is null ? Results.NotFound() : Results.Stream(stream, "text/html; charset=utf-8");
    }
}

public sealed record PingResponse(bool Ok);
