using System.Net.Http.Headers;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Online-Vollpaket (Wunsch 27.10.2026: „im Grunde alles soll direkt vom Setup aus funktionieren“).
/// Ohne Vollpaket am PC lädt Trendo genau das benötigte Paket samt Manifest vom GitHub-Release (nur GitHub-Adressen über HTTPS);
/// eingerichtet wird es wie vom USB-Stick – mit Prüfsumme, ohne Admin, nur in den Werkzeugordner.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Inhalt einer Adresse des Online-Vollpakets statt Netz.</summary>
    public Func<string, Task<byte[]>>? VollpaketOnlineOverride { get; set; }

    void InitVollpaketOnline() => _deps.FetchVollpaketPackage = VollpaketOnlineFetch;

    /// <summary>Wunsch 04.11.2026: nach dem Setup alle mitgelieferten Programme automatisch von GitHub laden und einrichten – ohne Ordnerwahl.
    /// Kleine Grundwerkzeuge zuerst (Trendo ist damit gleich nutzbar), große danach; jede Datei wird gegen ihre Prüfsumme geprüft.
    /// Ohne Internet bleibt ein Merker liegen, beim nächsten Start lädt Trendo nach.</summary>
    public async Task<Vollpaket.InstallReport?> InstallVollpaketOnline(CancellationToken ct = default)
    {
        var marker = Path.Combine(_deps.ToolsRoot, Vollpaket.PendingMarker);
        Directory.CreateDirectory(_deps.ToolsRoot);
        if (_core.Offline) { File.WriteAllText(marker, "offline"); VollpaketMissing = true; _depLog.Text = "Ohne Internet – Trendo lädt die Programme, sobald du online bist."; return null; }
        var dir = Path.Combine(_deps.ToolsRoot, ".vollpaket-online"); Directory.CreateDirectory(dir);
        string? found = null;
        foreach (var b in Vollpaket.OnlineBases)
        {
            try { await VollpaketGet(b + Vollpaket.ManifestName, Path.Combine(dir, Vollpaket.ManifestName), 4 << 20, null, ct); if (Vollpaket.Load(dir) is { Count: > 0 }) { found = b; break; } }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException) { }
        }
        if (found is null) { File.WriteAllText(marker, "nicht erreichbar"); VollpaketMissing = true; _depLog.Text = "GitHub ist gerade nicht erreichbar – Trendo versucht es beim nächsten Start erneut."; return null; }
        var list = Vollpaket.Load(dir)!.OrderBy(e => e.Bytes).ToList();
        var missing = list.Where(e => Vollpaket.InSetup(e.Id) && !File.Exists(Path.Combine(_deps.ToolsRoot, e.Id, e.Exe.Replace('/', Path.DirectorySeparatorChar)))).ToList(); // nur was gleich gebraucht wird
        _core.Audit.Append("app", "vollpaket.online.start", $"{found} {missing.Count} von {list.Count}");
        BeginSetupOverlay("Trendo OS lädt deine Programme von GitHub");
        var step = 0;
        try
        {
            foreach (var e in missing)
            {
                ct.ThrowIfCancellationRequested();
                var target = Path.Combine(dir, e.File);
                if (File.Exists(target) && new FileInfo(target).Length == e.Bytes) { step++; continue; }
                SetupOverlayProgress(step, missing.Count, $"{e.Name} wird geladen …");
                try { await VollpaketGet(found + e.File, target, e.Bytes, done => _ui.Post(() => SetupOverlayProgress(step, missing.Count, $"{e.Name}: {done >> 20:N0} von {e.Bytes >> 20:N0} MB")), ct); }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException) { _core.Audit.Append("app", "vollpaket.online.failed", $"{e.Id}: {ex.GetType().Name}"); }
                step++;
            }
        }
        catch (OperationCanceledException) { File.WriteAllText(marker, "abgebrochen"); EndSetupOverlay(); return null; }
        EndSetupOverlay();
        var r = await InstallVollpaket(dir, Vollpaket.InSetup); // prüft jede Datei gegen ihre Prüfsumme und richtet sie ein (nur was gleich gebraucht wird)
        if (r is { Failed.Count: 0, Canceled: false }) TryDelete(marker); else File.WriteAllText(marker, "unvollständig");
        return r;
    }

    async Task<string?> VollpaketOnlineFetch(string pkg, Action<string>? progress, CancellationToken ct)
    {
        if (_core.Offline) return null;
        var dir = Path.Combine(_deps.ToolsRoot, ".vollpaket-online");
        try
        {
            Directory.CreateDirectory(dir);
            await VollpaketGet(Vollpaket.OnlineBase + Vollpaket.ManifestName, Path.Combine(dir, Vollpaket.ManifestName), 4 << 20, null, ct);
            if (Vollpaket.Load(dir)?.FirstOrDefault(e => e.Id == pkg) is not { } e) return null;
            var target = Path.Combine(dir, e.File);
            if (!File.Exists(target) || new FileInfo(target).Length != e.Bytes)
                await VollpaketGet(Vollpaket.OnlineBase + e.File, target, e.Bytes, done => progress?.Invoke($"{e.Name}: {done >> 20:N0} von {e.Bytes >> 20:N0} MB geladen"), ct);
            return dir;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            _core.Audit.Append("app", "vollpaket.online.failed", $"{pkg}: {ex.GetType().Name}");
            progress?.Invoke("Das Online-Vollpaket ist gerade nicht erreichbar – Trendo versucht die offizielle Quelle.");
            return null;
        }
    }

    /// <summary>Datei laden: Weiterleitungen nur zu GitHub über HTTPS, höchstens <paramref name="max"/> Bytes, erst fertig dann umbenannt.</summary>
    async Task VollpaketGet(string url, string file, long max, Action<long>? progress, CancellationToken ct)
    {
        var tmp = file + ".laden";
        if (VollpaketOnlineOverride is { } o)
        {
            var bytes = await o(url);
            if (bytes.Length > max) throw new InvalidOperationException("Datei größer als angegeben.");
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            SafeFile.Move(tmp, file);
            return;
        }
        var u = new Uri(url);
        HttpResponseMessage? res = null;
        for (var i = 0; i < 6; i++)
        {
            CheckHost(u);
            var req = new HttpRequestMessage(HttpMethod.Get, u);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("TrendoOS", "1.0"));
            res = await GitHubHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc) { u = loc.IsAbsoluteUri ? loc : new Uri(u, loc); res.Dispose(); res = null; continue; }
            res.EnsureSuccessStatusCode();
            break;
        }
        if (res is null) throw new InvalidOperationException("Zu viele Weiterleitungen.");
        using (res)
        {
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using (var dst = File.Create(tmp))
            {
                var buf = new byte[1 << 20]; long done = 0; int n; var last = DateTime.UtcNow;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    done += n;
                    if (done > max) throw new InvalidOperationException("Datei größer als angegeben.");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    if ((DateTime.UtcNow - last).TotalMilliseconds > 500) { last = DateTime.UtcNow; progress?.Invoke(done); }
                }
            }
            SafeFile.Move(tmp, file);
        }
    }
}
