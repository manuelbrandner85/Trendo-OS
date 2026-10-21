using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Trendo.Core;

namespace Trendo.Api;

/// <summary>Live-Selbsttest der lokalen API über echten Loopback mit echten Tokens aus dem Secret-Store. Führt keine externe Aktion aus (Launcher wird vom Aufrufer gestellt).</summary>
public static class ApiSelfTest
{
    public static async Task<int> RunAsync(TrendoCore core, string requirementsPath, TextWriter o)
    {
        var tokens = new ApiTokens(core.Secrets);
        await using var api = await LocalApi.StartAsync(core, tokens, requirementsPath, new DefaultToolProbe());
        o.WriteLine($"API gebunden an {api.BaseAddress}");
        using var http = new HttpClient { BaseAddress = api.BaseAddress };
        var fails = 0;
        async Task Check(string name, HttpStatusCode expect, Func<Task<HttpResponseMessage>> call)
        {
            var r = await call();
            var ok = r.StatusCode == expect;
            if (!ok) fails++;
            o.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}: erwartet {(int)expect}, erhalten {(int)r.StatusCode}");
        }
        HttpRequestMessage Req(HttpMethod m, string url, Role? role, object? body = null)
        {
            var q = new HttpRequestMessage(m, url);
            if (role is { } rr) q.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetOrCreate(rr));
            if (body is not null) q.Content = JsonContent.Create(body);
            return q;
        }
        var pid = "api-selftest-" + DateTime.Now.ToString("HHmmss");
        await Check("health ohne Token", HttpStatusCode.OK, () => http.GetAsync("api/health"));
        await Check("studios ohne Token", HttpStatusCode.Unauthorized, () => http.GetAsync("api/studios"));
        await Check("studios mit falschem Token", HttpStatusCode.Unauthorized, () => { var q = new HttpRequestMessage(HttpMethod.Get, "api/studios"); q.Headers.Authorization = new("Bearer", "falsch"); return http.SendAsync(q); });
        await Check("Guest: studios", HttpStatusCode.OK, () => http.SendAsync(Req(HttpMethod.Get, "api/studios", Role.Guest)));
        await Check("Guest: projects", HttpStatusCode.Forbidden, () => http.SendAsync(Req(HttpMethod.Get, "api/projects", Role.Guest)));
        await Check("User: Projekt anlegen", HttpStatusCode.Created, () => http.SendAsync(Req(HttpMethod.Post, "api/projects", Role.User, new { id = pid, name = "API-Selbsttest" })));
        var plan = await http.SendAsync(Req(HttpMethod.Post, "api/actions/open-folder", Role.User, new { projectId = pid }));
        o.WriteLine($"{(plan.StatusCode == HttpStatusCode.Accepted ? "PASS" : "FAIL")}  User: Aktion planen: {(int)plan.StatusCode}"); if (plan.StatusCode != HttpStatusCode.Accepted) fails++;
        var id = (await plan.Content.ReadFromJsonAsync<Dictionary<string, object>>())?["approvalId"]?.ToString();
        await Check("User: freigeben verboten", HttpStatusCode.Forbidden, () => http.SendAsync(Req(HttpMethod.Post, $"api/approvals/{id}/approve", Role.User)));
        await Check("Guest: audit verboten", HttpStatusCode.Forbidden, () => http.SendAsync(Req(HttpMethod.Get, "api/audit", Role.Guest)));
        await Check("Owner: ablehnen", HttpStatusCode.OK, () => http.SendAsync(Req(HttpMethod.Post, $"api/approvals/{id}/deny", Role.Owner)));
        await Check("Owner: audit", HttpStatusCode.OK, () => http.SendAsync(Req(HttpMethod.Get, "api/audit", Role.Owner)));
        await Check("Fremder Host-Header", HttpStatusCode.MisdirectedRequest, () => { var q = Req(HttpMethod.Get, "api/studios", Role.Owner); q.Headers.Host = "evil.example"; return http.SendAsync(q); });
        var tokenLeak = File.ReadAllText(core.Audit.FilePath).Contains(tokens.GetOrCreate(Role.Owner));
        o.WriteLine($"{(tokenLeak ? "FAIL" : "PASS")}  Token nicht im Audit-Log"); if (tokenLeak) fails++;
        o.WriteLine($"ERGEBNIS: {(fails == 0 ? "ALLE BESTANDEN" : fails + " FEHLER")}");
        return fails == 0 ? 0 : 1;
    }
}
