using System.IO.Compression;
using System.Net;
using System.Text;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Paket D: Netlify-Veröffentlichung (in der App, der Kern bleibt ohne Netzwerk).
public class NetlifyLinkTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-conn-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    sealed class FakeNetlify : HttpMessageHandler
    {
        public readonly List<(string Url, string Auth, string Type, byte[] Body)> Calls = [];
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Calls.Add((r.RequestUri!.AbsoluteUri, r.Headers.Authorization?.ToString() ?? "", r.Content?.Headers.ContentType?.MediaType ?? "", r.Content is null ? [] : await r.Content.ReadAsByteArrayAsync(ct)));
            if (Status != HttpStatusCode.OK) return new(Status);
            var json = r.RequestUri.AbsolutePath.EndsWith("/sites") ? """{"id":"site-1"}""" : """{"id":"d1","ssl_url":"https://meine-seite.netlify.app"}""";
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    [Fact(DisplayName = "Netlify: Schlüssel nur im Tresor, Seite anlegen + ZIP hochladen (ohne .trendo/.git/.env), Seite gemerkt, zweites Mal nur hochladen; abgelehnter Schlüssel verständlich")]
    public async Task Netlify_deploy()
    {
        var dir = Temp();
        try
        {
            var secrets = new InMemorySecretStore(); var fake = new FakeNetlify(); var nl = new NetlifyLink(secrets, fake);
            Assert.False(nl.Connected);
            Assert.Throws<ArgumentException>(() => nl.Connect("zu kurz"));
            nl.Connect("  nfp_0123456789abcdefghijklmnop  "); Assert.True(nl.Connected); Assert.Equal("nfp_0123456789abcdefghijklmnop", secrets.Get(NetlifyLink.SecretKey));
            await Assert.ThrowsAsync<InvalidOperationException>(() => nl.DeployAsync(dir)); // ohne index.html
            File.WriteAllText(Path.Combine(dir, "index.html"), "<h1>Hallo</h1>");
            Directory.CreateDirectory(Path.Combine(dir, ".trendo")); File.WriteAllText(Path.Combine(dir, ".trendo", "geheim.txt"), "x");
            File.WriteAllText(Path.Combine(dir, ".env"), "KEY=1");
            var url = await nl.DeployAsync(dir, "Meine Seite");
            Assert.Equal("https://meine-seite.netlify.app", url);
            Assert.Equal(2, fake.Calls.Count);
            Assert.EndsWith("/api/v1/sites", fake.Calls[0].Url); Assert.Equal("Bearer nfp_0123456789abcdefghijklmnop", fake.Calls[0].Auth);
            Assert.Contains("meine-seite", Encoding.UTF8.GetString(fake.Calls[0].Body));
            Assert.EndsWith("/sites/site-1/deploys", fake.Calls[1].Url); Assert.Equal("application/zip", fake.Calls[1].Type);
            using (var z = new ZipArchive(new MemoryStream(fake.Calls[1].Body))) Assert.Equal(["index.html"], z.Entries.Select(e => e.FullName));
            Assert.Equal(("site-1", "https://meine-seite.netlify.app"), NetlifyLink.SiteOf(dir));
            await nl.DeployAsync(dir); Assert.Equal(3, fake.Calls.Count); Assert.EndsWith("/sites/site-1/deploys", fake.Calls[2].Url); // gemerkte Seite
            fake.Status = HttpStatusCode.Unauthorized;
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => nl.DeployAsync(dir)); Assert.Contains("neu verbinden", e.Message);
            nl.Disconnect(); Assert.False(nl.Connected);
        }
        finally { Directory.Delete(dir, true); }
    }
}
