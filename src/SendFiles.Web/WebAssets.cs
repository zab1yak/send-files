using System.Reflection;

namespace SendFiles.Web;

/// <summary>Access to the embedded phone UI files.</summary>
public static class WebAssets
{
    public static Assembly Assembly => typeof(WebAssets).Assembly;

    public const string Root = "wwwroot";
}
