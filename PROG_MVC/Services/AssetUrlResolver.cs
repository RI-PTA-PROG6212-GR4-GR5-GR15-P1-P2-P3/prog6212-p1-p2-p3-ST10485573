namespace PROG_MVC.Services;

/// <summary>Resolves API-owned image paths against the configured API origin.</summary>
public sealed class AssetUrlResolver(IConfiguration configuration)
{
    public string? Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        if (!url.StartsWith("/blob/", StringComparison.Ordinal) &&
            !url.StartsWith("/api/blob/", StringComparison.Ordinal))
            return url;

        var apiBaseUrl = configuration["Api:PublicBaseUrl"] ?? configuration["Api:BaseUrl"];
        return Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBase)
            ? new Uri(apiBase, url.TrimStart('/')).ToString()
            : url;
    }
}
