using System.Net.Http.Headers;
using Trendo.Core;
using Trendo.Core.Rebuild;

namespace Trendo.App;

/// <summary>Wikipedia-API, nur GET, ohne Weiterleitungen, ohne Skripte. Der Text bleibt untrusted. Liegt in der App, damit der Kern keine Netzbibliothek braucht.</summary>
public sealed class WikipediaResearch : IPublicWeb
{
    private readonly HttpClient _http;
    public bool Allowed { get; }

    public WikipediaResearch(bool offline, HttpMessageHandler? handler = null)
    {
        Allowed = !offline;
        var sockets = handler ?? new SocketsHttpHandler { AllowAutoRedirect = false };
        _http = new HttpClient(sockets) { Timeout = TimeSpan.FromSeconds(12) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TrendoOS-Recherche/1.0 (nur lesen)");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public IReadOnlyList<PublicPage> Research(string query)
    {
        if (!Allowed) throw new OfflineException("Offline: es wird nichts aus dem Internet gelesen.");
        if (string.IsNullOrWhiteSpace(query)) return [];
        var pages = new List<PublicPage>();
        foreach (var host in new[] { "de.wikipedia.org", "en.wikipedia.org" })
        {
            var titles = WikiJson.Titles(Get(host, "action=opensearch&limit=1&namespace=0&format=json&search=" + Uri.EscapeDataString(query)));
            foreach (var title in titles.Take(1))
            {
                var (heading, extract) = WikiJson.Extract(Get(host, "action=query&prop=extracts&explaintext=1&exchars=3500&redirects=1&format=json&titles=" + Uri.EscapeDataString(title)));
                if (string.IsNullOrWhiteSpace(extract)) continue;
                var shown = string.IsNullOrWhiteSpace(heading) ? title : heading;
                pages.Add(new PublicPage("https://" + host + "/wiki/" + Uri.EscapeDataString(shown.Replace(' ', '_')), shown, extract));
            }
            if (pages.Count > 0) break;
        }
        return pages;
    }

    private string Get(string host, string query)
    {
        var url = "https://" + host + "/w/api.php?" + query;
        if (ResearchDesk.Classify(url).Kind != UrlKind.Lesemodus) return "";
        try
        {
            using var res = _http.Send(new HttpRequestMessage(HttpMethod.Get, url));
            if ((int)res.StatusCode >= 300) return "";
            var text = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return text.Length > 400_000 ? text[..400_000] : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new PublicReadException("Die Seite ließ sich nicht lesen.", ex);
        }
    }
}
