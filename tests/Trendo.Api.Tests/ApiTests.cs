using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using Trendo.Api;
using Trendo.Core;
using Xunit;

namespace Trendo.Api.Tests;

public sealed class FakeLauncher : IProcessLauncher { public List<string> Opened { get; } = []; public void Open(string t) => Opened.Add(t); }
sealed class Probe : IToolProbe { public bool IsAvailable(string t) => false; }

public sealed class ApiFixture : IAsyncLifetime
{
    public TrendoCore Core = null!; public ApiTokens Tokens = null!; public LocalApi Api = null!; public HttpClient Http = null!;
    public FakeLauncher Launcher = new();
    public async Task InitializeAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-api-" + Guid.NewGuid().ToString("N"));
        Core = new TrendoCore(root, new InMemorySecretStore(), Launcher);
        Tokens = new ApiTokens(Core.Secrets);
        Api = await LocalApi.StartAsync(Core, Tokens, Path.Combine(AppContext.BaseDirectory, "requirements.yaml"), new Probe());
        Http = new HttpClient { BaseAddress = Api.BaseAddress };
    }
    public async Task DisposeAsync() { Http.Dispose(); await Api.DisposeAsync(); }
    public HttpRequestMessage Req(HttpMethod m, string url, Role? r, object? body = null)
    {
        var q = new HttpRequestMessage(m, url);
        if (r is { } rr) q.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.GetOrCreate(rr));
        if (body != null) q.Content = JsonContent.Create(body);
        return q;
    }
}

public class Api_Auth_Binding(ApiFixture f) : IClassFixture<ApiFixture>
{
    [NeedsLanFact(DisplayName = "API: nur an 127.0.0.1 gebunden (Verbindung über Nicht-Loopback-Adresse scheitert)")]
    public async Task Bound_to_loopback_only()
    {
        Assert.Equal("127.0.0.1", f.Api.BaseAddress.Host);
        var other = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        if (other is null) return; // keine Nicht-Loopback-Adresse vorhanden → nicht prüfbar
        using var c = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => c.ConnectAsync(other, f.Api.Port).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(DisplayName = "API: ohne/mit falschem Token → 401, health offen")]
    public async Task Unauthenticated()
    {
        Assert.Equal(HttpStatusCode.OK, (await f.Http.GetAsync("api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.GetAsync("api/studios")).StatusCode);
        var q = new HttpRequestMessage(HttpMethod.Get, "api/studios"); q.Headers.Authorization = new("Bearer", "falsch");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.SendAsync(q)).StatusCode);
    }

    [Fact(DisplayName = "API: fremder Host-Header (DNS-Rebinding) → 421")]
    public async Task Host_header()
    {
        var q = f.Req(HttpMethod.Get, "api/studios", Role.Owner); q.Headers.Host = "evil.example";
        Assert.Equal(HttpStatusCode.MisdirectedRequest, (await f.Http.SendAsync(q)).StatusCode);
    }

    [Fact(DisplayName = "API: Tokens nur im Secret-Store, nie im Audit-Log; Rotation macht altes Token ungültig")]
    public async Task Token_storage_rotation()
    {
        var t = f.Tokens.GetOrCreate(Role.Guest);
        Assert.Equal(t, f.Core.Secrets.Get("api-token-guest"));
        Assert.True(t.Length >= 40);
        f.Tokens.Rotate(Role.Guest);
        var q = new HttpRequestMessage(HttpMethod.Get, "api/studios"); q.Headers.Authorization = new("Bearer", t);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Http.SendAsync(q)).StatusCode);
        Assert.DoesNotContain(t, File.ReadAllText(f.Core.Audit.FilePath));
    }
}

public class Api_Rbac(ApiFixture f) : IClassFixture<ApiFixture>
{
    [Theory(DisplayName = "RBAC über API: Rechte je Rolle")]
    [InlineData("GET", "api/studios", Role.Guest, HttpStatusCode.OK)]
    [InlineData("GET", "api/projects", Role.Guest, HttpStatusCode.Forbidden)]
    [InlineData("GET", "api/projects", Role.User, HttpStatusCode.OK)]
    [InlineData("GET", "api/approvals", Role.User, HttpStatusCode.Forbidden)]
    [InlineData("GET", "api/approvals", Role.Owner, HttpStatusCode.OK)]
    [InlineData("GET", "api/audit", Role.User, HttpStatusCode.Forbidden)]
    [InlineData("GET", "api/audit", Role.Owner, HttpStatusCode.OK)]
    public async Task Role_matrix(string method, string url, Role role, HttpStatusCode expect)
        => Assert.Equal(expect, (await f.Http.SendAsync(f.Req(new HttpMethod(method), url, role))).StatusCode);

    [Fact(DisplayName = "RBAC über API: Guest darf weder Projekt anlegen noch Aktion planen")]
    public async Task Guest_cannot_write()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Http.SendAsync(f.Req(HttpMethod.Post, "api/projects", Role.Guest, new { id = "g-proj", name = "G" }))).StatusCode);
        f.Core.Projects.Create("g-ziel", "Z");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Http.SendAsync(f.Req(HttpMethod.Post, "api/actions/open-folder", Role.Guest, new { projectId = "g-ziel" }))).StatusCode);
    }

    [Fact(DisplayName = "Approval-Gate über API: User plant, User darf nicht freigeben, Owner gibt frei → genau eine Ausführung")]
    public async Task Approval_flow()
    {
        Assert.Equal(HttpStatusCode.Created, (await f.Http.SendAsync(f.Req(HttpMethod.Post, "api/projects", Role.User, new { id = "flow-proj", name = "F" }))).StatusCode);
        var plan = await f.Http.SendAsync(f.Req(HttpMethod.Post, "api/actions/open-folder", Role.User, new { projectId = "flow-proj" }));
        Assert.Equal(HttpStatusCode.Accepted, plan.StatusCode);
        var id = (await plan.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["approvalId"].ToString();
        var before = f.Launcher.Opened.Count;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Http.SendAsync(f.Req(HttpMethod.Post, $"api/approvals/{id}/approve", Role.User))).StatusCode);
        Assert.Equal(before, f.Launcher.Opened.Count);
        Assert.Equal(HttpStatusCode.OK, (await f.Http.SendAsync(f.Req(HttpMethod.Post, $"api/approvals/{id}/approve", Role.Owner))).StatusCode);
        Assert.Equal(before + 1, f.Launcher.Opened.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Http.SendAsync(f.Req(HttpMethod.Post, $"api/approvals/{id}/approve", Role.Owner))).StatusCode);
        Assert.Equal(before + 1, f.Launcher.Opened.Count);
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "rbac.denied");
    }

    [Fact(DisplayName = "API: Pfadausbruch über Projekt-ID → 400")]
    public async Task Isolation_via_api()
        => Assert.Equal(HttpStatusCode.BadRequest, (await f.Http.SendAsync(f.Req(HttpMethod.Post, "api/actions/open-folder", Role.User, new { projectId = "../x" }))).StatusCode);

    [Fact(DisplayName = "API: Freigabe eines Livestreams startet nichts und öffnet nichts")]
    public async Task Live_approval_does_not_start()
    {
        var before = f.Launcher.Opened.Count;
        var r = f.Core.Approvals.Request(Principal.LocalOwner, LiveBroadcast.Action, "Livestream zu YouTube starten. Es startet nicht von allein.", "youtube");
        var res = await f.Http.SendAsync(f.Req(HttpMethod.Post, $"api/approvals/{r.Id}/approve", Role.Owner));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("false", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, f.Launcher.Opened.Count);
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Action == "execution.start" && e.Detail.Contains(r.Id, StringComparison.Ordinal));
    }
}
