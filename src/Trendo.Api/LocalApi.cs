using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trendo.Core;

namespace Trendo.Api;

public sealed record CreateProjectDto(string Id, string Name);
public sealed record OpenFolderDto(string ProjectId);

/// <summary>Lokale Trendo-API: nur 127.0.0.1, Bearer-Token je Rolle, RBAC serverseitig. Keine Firewall-Änderung (Loopback).</summary>
public sealed class LocalApi : IAsyncDisposable
{
    private readonly WebApplication _app;
    public int Port { get; private set; }
    public Uri BaseAddress => new($"http://127.0.0.1:{Port}/");

    private LocalApi(WebApplication app) => _app = app;

    public static async Task<LocalApi> StartAsync(TrendoCore core, ApiTokens tokens, string requirementsPath, IToolProbe probe, int port = 0)
    {
        var b = WebApplication.CreateSlimBuilder();
        b.Logging.ClearProviders();
        b.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
        var app = b.Build();

        // 1) Nur Loopback-Clients, 2) Host-Header muss lokal sein (DNS-Rebinding-Schutz), 3) Token → Rolle
        app.Use(async (ctx, next) =>
        {
            var ip = ctx.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip)) { ctx.Response.StatusCode = 403; return; }
            var host = ctx.Request.Host.Host;
            if (host is not ("127.0.0.1" or "localhost")) { ctx.Response.StatusCode = 421; return; }
            var auth = ctx.Request.Headers.Authorization.ToString();
            var role = auth.StartsWith("Bearer ", StringComparison.Ordinal) ? tokens.Resolve(auth[7..].Trim()) : null;
            if (role is null && ctx.Request.Path != "/api/health")
            {
                core.Audit.Append("api", "api.unauthenticated", ctx.Request.Path);
                ctx.Response.StatusCode = 401; return;
            }
            if (role is not null) ctx.Items["principal"] = new Principal($"api-{role.Value.ToString().ToLowerInvariant()}", role.Value);
            try { await next(); }
            catch (UnauthorizedAccessException) { ctx.Response.StatusCode = 403; }
            catch (ApprovalException e) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { error = e.Message }); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or ProjectIsolationException or OfflineException)
            { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = e.Message }); }
        });

        static Principal P(HttpContext c) => (Principal)c.Items["principal"]!;

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = core.Version, offline = core.Offline }));
        app.MapGet("/api/studios", (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.ViewStudios, core.Audit);
            return Results.Ok(core.Studios.Load(requirementsPath, probe).Select(s => new { s.Id, s.Name, s.ApprovalStatus, capability = s.Capability.ToString(), s.Reason }));
        });
        app.MapGet("/api/projects", (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.ViewProjects, core.Audit);
            return Results.Ok(core.Projects.List());
        });
        app.MapPost("/api/projects", (HttpContext c, CreateProjectDto d) =>
        {
            Rbac.Demand(P(c), Permission.ManageProjects, core.Audit);
            return Results.Created($"/api/projects/{d.Id}", core.Projects.Create(d.Id, d.Name));
        });
        app.MapPost("/api/actions/open-folder", (HttpContext c, OpenFolderDto d) =>
        {
            var r = core.Integrations.PlanOpenProjectFolder(P(c), d.ProjectId);
            return Results.Accepted($"/api/approvals/{r.Id}", new { approvalId = r.Id, state = r.State.ToString() });
        });
        app.MapGet("/api/approvals", (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.ApproveAction, core.Audit);
            return Results.Ok(core.Approvals.Pending().Select(r => new { r.Id, r.Action, r.Description, r.ExpiresAt }));
        });
        app.MapPost("/api/approvals/{id}/approve", (HttpContext c, string id) =>
        {
            var r = core.Approvals.Pending().FirstOrDefault(x => x.Id == id) ?? throw new ApprovalException("Keine offene Freigabe.");
            core.Approvals.Approve(id, P(c));
            core.Integrations.Execute(r);
            return Results.Ok(new { executed = true });
        });
        app.MapPost("/api/approvals/{id}/deny", (HttpContext c, string id) => { core.Approvals.Deny(id, P(c)); return Results.Ok(new { denied = true }); });
        app.MapGet("/api/audit", (HttpContext c) =>
        {
            Rbac.Demand(P(c), Permission.ViewAudit, core.Audit);
            return Results.Ok(new { intact = core.Audit.Verify(), entries = core.Audit.ReadAll().TakeLast(100) });
        });

        await app.StartAsync();
        var api = new LocalApi(app);
        var addr = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        api.Port = new Uri(addr).Port;
        core.Audit.Append("api", "api.start", $"127.0.0.1:{api.Port}");
        return api;
    }

    public async ValueTask DisposeAsync() { await _app.StopAsync(); await _app.DisposeAsync(); }
}
