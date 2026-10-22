using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core.Rebuild;

public sealed record PublicPage(string Url, string Title, string PlainText);

/// <summary>Öffentliche Seiten nur lesen. Nichts davon wird ausgeführt oder als Auftrag gelesen.</summary>
public interface IPublicWeb
{
    bool Allowed { get; }
    IReadOnlyList<PublicPage> Research(string query);
}

/// <summary>Die öffentliche Seite ließ sich nicht lesen. Der Kern kennt keine Netzbibliothek.</summary>
public sealed class PublicReadException : Exception
{
    public PublicReadException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class OfflineWeb : IPublicWeb
{
    public bool Allowed => false;
    public IReadOnlyList<PublicPage> Research(string query) => throw new OfflineException("Offline: es wird nichts aus dem Internet gelesen.");
}

public static class WikiJson
{
    public static IReadOnlyList<string> Titles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2) return [];
            return doc.RootElement[1].EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).Take(2).ToList();
        }
        catch (JsonException) { return []; }
    }

    public static (string? Title, string Extract) Extract(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, "");
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return (null, "");
            foreach (var page in pages.EnumerateObject())
            {
                var title = page.Value.TryGetProperty("title", out var t) ? t.GetString() : null;
                var extract = page.Value.TryGetProperty("extract", out var e) ? e.GetString() ?? "" : "";
                if (extract.Length > 0) return (title, extract);
            }
            return (null, "");
        }
        catch (JsonException) { return (null, ""); }
    }
}

public static class SourceHash
{
    public static string Of(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""))).ToLowerInvariant();
}
