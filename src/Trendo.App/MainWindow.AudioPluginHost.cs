using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin-Host im Audioeditor – Plugin prüfen (nur lesen), Herkunft angeben und freigeben,
/// Parameter lesen, auf die Auswahl/ganze Spur anwenden. Geladen wird nur abgeschottet (Sandbox) und nur genau die freigegebene Datei;
/// Absturz → Bericht, nichts übernommen. Ergebnis ist ein Schritt der Bearbeitung (rückgängig machbar).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: abgeschotteten Host-Lauf ersetzen (Argumente, Arbeitsordner → Ergebnis; schreibt selbst ergebnis.json/aus.wav).</summary>
    public Func<IReadOnlyList<string>, string, Sandbox.Result>? AuPluginRunOverride { get; set; }
    public PluginInspect.Result? AuPluginChecked { get; private set; }
    public PluginHost.Outcome? AuPluginLast { get; private set; }
    public string? AuPluginCrashReport { get; private set; }
    string _auPlugPath = "", _auPlugNote = "", _auPlugParams = "", _auPlugPreset = "";
    bool _auPlugBusy;

    PluginHost.Approvals PluginApprovals => new(_core.DataRoot);

    public PluginInspect.Result AuPluginInspect(string path)
    {
        path = path.Trim().Trim('"');
        if (path.Length == 0) throw new InvalidOperationException("Erst den Pfad zum Plugin angeben (Ordner „….vst3“ oder Datei „….clap“).");
        var r = PluginInspect.Inspect(path); AuPluginChecked = r; AuPluginLast = null; AuPluginCrashReport = null;
        var f = Path.Combine(WorkFolder(Programs[AUK].Folder), "Plugins", DevStudio.SafeName(Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/'))) + " – Prüfung.md");
        Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, PluginInspect.Report(r));
        var refuse = PluginHost.Refuse(r, PluginApprovals);
        _progMsg[AUK] = (r.Problem is not null ? "Nicht ladbar: " + r.Problem : $"Geprüft: {r.Format}, {r.Arch}, {r.Risks.Count} Hinweise") + (refuse is null ? " – freigegeben." : r.Problem is null ? " – " + refuse : "") + $" Bericht: {Path.GetFileName(f)}";
        return r;
    }

    public void AuPluginApprove(string note)
    {
        var r = AuPluginChecked ?? throw new InvalidOperationException("Erst „Prüfen“.");
        var a = PluginApprovals.Approve(r, note);
        _core.Audit.Append(Principal.LocalOwner.Name, "audio.plugin.freigabe", $"{a.Name} sha256={a.Sha256} herkunft={a.Note}");
        _progMsg[AUK] = $"„{a.Name}“ freigegeben (nur genau diese Datei, SHA-256 {a.Sha256[..12]}…). Ändert sich die Datei, muss neu freigegeben werden.";
    }

    public void AuPluginRevoke()
    {
        var r = AuPluginChecked ?? throw new InvalidOperationException("Erst „Prüfen“.");
        if (PluginApprovals.Revoke(r.Sha256)) _core.Audit.Append(Principal.LocalOwner.Name, "audio.plugin.zurueck", r.Sha256);
        _progMsg[AUK] = "Freigabe zurückgenommen.";
    }

    (string Py, string Script, string Site)? PluginRuntime()
    {
        if (AuPluginRunOverride is not null) return ("python.exe", "trendo_plugin.py", "site-packages");
        if (!KiStore.RuntimeReady(KiRuntime.PluginHost)) return null;
        var p = KiStore.PyPaths(KiRuntime.PluginHost);
        return PluginHost.BasePython(p.Env) is { } py ? (py, Path.Combine(p.Dir, KiPyApps.For(KiRuntime.PluginHost)!.Script), PluginHost.SitePackages(p.Env)) : null;
    }

    Sandbox.Result PluginRun(IReadOnlyList<string> args, string box, string py) =>
        AuPluginRunOverride is { } o ? o(args, box) : Sandbox.Run(py, args, box, new Sandbox.Options(TimeoutMs: 600_000, MemoryBytes: 4L << 30));

    /// <summary>„info“ oder „render“ abgeschottet. Bei „render“ wird die Auswahl (sonst die ganze Spur) bearbeitet und als Schritt übernommen.</summary>
    public async Task<bool> AuPluginRun(ProgramDocument d, string cmd, string paramsText = "", string preset = "")
    {
        var r = AuPluginChecked ?? throw new InvalidOperationException("Erst „Prüfen“.");
        if (PluginHost.Refuse(r, PluginApprovals) is { } why) { _progMsg[AUK] = why; Show(AUK); return false; }
        if (PluginRuntime() is not { } rt) { _progMsg[AUK] = "Der Plugin-Host ist nicht eingerichtet – in der KI-Bibliothek unter „Musik“ „Plugin-Host für VST3“ einrichten."; Show(AUK); return false; }
        if (_opt.Isolated && AuPluginRunOverride is null) { _progMsg[AUK] = "Im abgeschotteten Testmodus lädt Trendo keine Plugins."; Show(AUK); return false; }
        var ps = PluginHost.ParseParams(paramsText);
        if (preset.Trim().Length > 0 && !File.Exists(preset.Trim().Trim('"'))) throw new InvalidOperationException("Preset-Datei fehlt: " + Path.GetFileName(preset));
        var box = Path.Combine(_core.DataRoot, "plugins", "lauf", Guid.NewGuid().ToString("N")[..10]); Directory.CreateDirectory(box);
        var result = Path.Combine(box, "ergebnis.json"); string? inp = null, outp = null; double s = 0, e = 0;
        if (cmd == "render")
        {
            var a = AuRendered(d) ?? throw new InvalidOperationException("Erst eine Tondatei öffnen.");
            (s, e) = AuSel; if (e <= s) { s = 0; e = 0; }
            var part = e > s ? a.Slice((int)(s * a.Rate), (int)(e * a.Rate)) : a;
            inp = Path.Combine(box, "ein.wav"); outp = Path.Combine(box, "aus.wav"); AudioFile.WriteWav(part, inp, 24);
        }
        var args = PluginHost.Args(rt.Script, rt.Site, cmd, r.Binary, result, // innere Programmdatei: Pedalboard lädt unter Windows keine Bündel-Ordner (gemessen 07.11.2026)
            inp, outp, preset.Trim().Length > 0 ? preset.Trim().Trim('"') : null, ps);
        _auPlugBusy = true; _progMsg[AUK] = cmd == "render" ? "Plugin bearbeitet den Ton (abgeschottet) …" : "Plugin wird abgeschottet gelesen …"; Show(AUK);
        try
        {
            var res = await Task.Run(() => PluginRun(args, box, rt.Py));
            _core.Audit.Append(Principal.LocalOwner.Name, "audio.plugin.lauf", $"{cmd} {Path.GetFileName(r.Path.TrimEnd('\\', '/'))} → {res.Text}");
            if (res.Crashed || res.TimedOut)
            {
                var rep = Path.Combine(WorkFolder(Programs[AUK].Folder), "Plugins", "Absturzbericht " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".md");
                Directory.CreateDirectory(Path.GetDirectoryName(rep)!); File.WriteAllText(rep, PluginHost.CrashReport(r, res, cmd == "render" ? "Ton bearbeiten" : "Parameter lesen"));
                AuPluginCrashReport = rep; AuPluginLast = null;
                _progMsg[AUK] = $"Plugin {res.Text}. Dein Projekt ist unverändert. Bericht: {Path.GetFileName(rep)}";
                return false;
            }
            var o = File.Exists(result) ? PluginHost.Parse(await File.ReadAllTextAsync(result)) : null; AuPluginLast = o;
            if (o is null || !o.Ok) { _progMsg[AUK] = "Das Plugin hat nicht gearbeitet: " + (o?.Error ?? res.Text) + " – nichts übernommen."; return false; }
            if (cmd != "render") { _progMsg[AUK] = $"„{o.Name}“ ({o.Kind}) – {o.Params.Count} Parameter gelesen."; return true; }
            if (outp is null || !File.Exists(outp)) { _progMsg[AUK] = "Das Plugin hat keinen Ton geliefert – nichts übernommen."; return false; }
            var keep = Path.Combine(WorkFolder(Programs[AUK].Folder), "Plugins", $"ausgabe-{Guid.NewGuid():N}.wav"); File.Copy(outp, keep, true);
            d.Edit("Plugin: " + o.Name, x => AudioEdit.Add(x, AudioEdit.Op("plugin", s, e, 0, keep)));
            _progMsg[AUK] = $"„{o.Name}“ angewendet{(e > s ? $" ({AudioEdit.Time(s)}–{AudioEdit.Time(e)})" : " (ganze Spur)")} · Spitze {o.InPeakDb:0.0} → {o.OutPeakDb:0.0} dB, Veränderung {o.Changed:P0}, abgeschottet in {res.Seconds:0.0} s. Strg+Z macht es rückgängig.";
            return true;
        }
        finally { _auPlugBusy = false; try { Directory.Delete(box, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } Show(AUK); }
    }

    Control AuPluginPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "au-plugin", Spacing = 5, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Plugins (VST3, abgeschottet)", 13));
        if (_auPlugPath.Length == 0 && AuPluginsLast?.FirstOrDefault(p => p.Status.StartsWith("ok") && p.Format == "VST3") is { } first) _auPlugPath = first.Path;
        TextBox Box(string n, string v, string hint, Action<string> set) { var t = new TextBox { Name = n, Text = v, FontSize = 12, PlaceholderText = hint }; t.TextChanged += (_, _) => set(t.Text ?? ""); return t; }
        sp.Children.Add(Box("au-plug-path", _auPlugPath, "Pfad zum Plugin, z. B. C:\\Programme\\Gemeinsame Dateien\\VST3\\….vst3", v => _auPlugPath = v));
        sp.Children.Add(Act("au-plug-check", "Prüfen (nur lesen)", () => ProgramRun(AUK, () => AuPluginInspect(_auPlugPath))));
        if (AuPluginChecked is { } r)
        {
            sp.Children.Add(P($"{r.Format} · {r.Arch} · Signatur {(r.Signed ? "vorhanden" : "keine")} · SHA-256 {(r.Sha256.Length > 12 ? r.Sha256[..12] + "…" : "–")}", Text, 11));
            if (r.Problem is not null) sp.Children.Add(P("" + r.Problem, Brush.Parse("#F87171"), 11));
            foreach (var x in r.Risks.Take(8)) sp.Children.Add(P((x.Serious ? "" : "• ") + x.Text, x.Serious ? Brush.Parse("#FACC15") : Muted, 10.5));
            var approved = PluginApprovals.IsApproved(r);
            if (r.Loadable && !approved)
            {
                sp.Children.Add(Box("au-plug-note", _auPlugNote, "Herkunft, z. B. „offizielle Seite des Herstellers“", v => _auPlugNote = v));
                sp.Children.Add(Act("au-plug-approve", "Diesem Plugin vertrauen und freigeben", () => ProgramRun(AUK, () => AuPluginApprove(_auPlugNote))));
            }
            if (approved)
            {
                sp.Children.Add(P("freigegeben – läuft nur abgeschottet (eigener Prozess, keine Schreibrechte auf deine Dateien, Zeit- und Speichergrenze).", Text, 11));
                sp.Children.Add(Box("au-plug-params", _auPlugParams, "Parameter: name=wert, name2=wert2 (leer = Voreinstellung)", v => _auPlugParams = v));
                sp.Children.Add(Box("au-plug-preset", _auPlugPreset, "Optional: Preset-Datei (.vstpreset)", v => _auPlugPreset = v));
                var info = Act("au-plug-info", "Parameter lesen", () => { if (!_auPlugBusy) _ = AuPluginRun(d, "info"); });
                var run = Act("au-plug-run", _auPlugBusy ? "Plugin arbeitet …" : "Auf Auswahl / ganze Spur anwenden", () => { if (!_auPlugBusy) _ = AuPluginRun(d, "render", _auPlugParams, _auPlugPreset); });
                run.IsEnabled = info.IsEnabled = !_auPlugBusy;
                sp.Children.Add(new WrapPanel { Children = { info, new Border { Width = 4 }, run, new Border { Width = 4 }, Act("au-plug-revoke", "Freigabe zurücknehmen", () => ProgramRun(AUK, AuPluginRevoke)) } });
                if (AuPluginLast is { Ok: true } o && o.Params.Count > 0)
                    sp.Children.Add(P("Parameter: " + string.Join(", ", o.Params.Take(12).Select(p => $"{p.Name}={p.Value}")) + (o.Params.Count > 12 ? $" … (+{o.Params.Count - 12})" : ""), Muted, 10.5));
            }
            if (AuPluginCrashReport is { } cr) sp.Children.Add(P("Absturzbericht: " + Path.GetFileName(cr), Brush.Parse("#F87171"), 11));
        }
        if (PluginRuntime() is null) sp.Children.Add(Nav2("Plugin-Host einrichten (KI-Bibliothek)", "ki-bibliothek"));
        return sp;
    }
}
