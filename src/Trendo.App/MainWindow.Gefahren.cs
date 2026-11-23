using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>T-0755 SPEC-12 „System auf Gefahren prüfen“: Autostart-Ordner, Run-Schlüssel, geplante Aufgaben und installierte Programme –
/// nur lesen, je Eintrag Datei, Herausgeber, Signatur und Ampel mit Erklärung. Trendo ändert nichts; die Tipps sagen, was man selbst tun kann.</summary>
public partial class MainWindow
{
    SystemScan.Result? _gef; string _gefMsg = ""; bool _gefBusy; string _gefFilter = "auffaellig"; CancellationTokenSource? _gefCts;
    TextBlock? _gefMsgView;
    /// <summary>Tests: eigene Quellen (z. B. ein eigener Autostart-Ordner ohne Registrierung).</summary>
    public Func<SystemScan.Sources>? ScanSourcesOverride { get; set; }
    public SystemScan.Result? LastSystemScan => _gef;
    public Task? SystemScanTask { get; private set; }

    public Task SystemScanStart() => SystemScanTask = SystemScanAsync();

    async Task SystemScanAsync()
    {
        if (_gefBusy) return;
        _gefBusy = true; _gefCts = new(); _gefMsg = "Prüft … (nur lesen)";
        if (CurrentView == "forensik") Show("forensik");
        var src = ScanSourcesOverride?.Invoke() ?? SystemScan.Sources.System;
        var prog = new Progress<string>(s => { _gefMsg = s; if (_gefMsgView is { } v) v.Text = s; });
        var ct = _gefCts.Token;
        try
        {
            _gef = await Task.Run(() => SystemScan.Run(src, null, prog, ct), ct);
            _gefMsg = $"Fertig: {_gef.Findings.Count} Einträge – {_gef.Count(Ampel.Rot)} rot, {_gef.Count(Ampel.Gelb)} gelb, {_gef.Count(Ampel.Gruen)} grün. Trendo hat nichts verändert.";
            _core.Audit.Append(CurrentPrincipal.Name, "gefahren.pruefung", $"{_gef.Findings.Count} Einträge, rot {_gef.Count(Ampel.Rot)}, gelb {_gef.Count(Ampel.Gelb)}");
        }
        catch (OperationCanceledException) { _gefMsg = "Abgebrochen – nichts verändert."; }
        finally { _gefBusy = false; _gefCts?.Dispose(); _gefCts = null; }
        if (CurrentView == "forensik") Show("forensik");
    }

    /// <summary>Bericht als HTML und CSV in den Projektordner (Unterordner „sicherheit“), atomar geschrieben.</summary>
    public (string Html, string Csv) SystemScanExport()
    {
        if (_gef is not { } r) throw new InvalidOperationException("Erst „System prüfen“ – dann gibt es einen Bericht.");
        var dir = WorkFolder("sicherheit"); var stamp = r.At.ToString("yyyyMMdd-HHmmss");
        var html = Path.Combine(dir, $"systempruefung-{stamp}.html"); var csv = Path.Combine(dir, $"systempruefung-{stamp}.csv");
        AtomicFile.WriteAllText(html, SystemScan.ReportHtml(r)); AtomicFile.WriteAllText(csv, "﻿" + SystemScan.ReportCsv(r));
        _core.Audit.Append(CurrentPrincipal.Name, "gefahren.bericht", Path.GetFileName(html));
        return (html, csv);
    }

    static string LightColor(Ampel a) => a switch { Ampel.Rot => "#F87171", Ampel.Gelb => "#FACC15", _ => "#4ADE80" };

    Control SystemScanCard()
    {
        var go = new Button { Name = "for-scan", Content = _gefBusy ? "Prüft …" : _gef is null ? "System prüfen" : "Noch einmal prüfen", Background = Accent, IsEnabled = !_gefBusy };
        go.Click += (_, _) => SystemScanStart();
        var row = new WrapPanel { Children = { MSp(go) } };
        if (_gefBusy) row.Children.Add(MSp(Act("for-scan-cancel", "Abbrechen", () => _gefCts?.Cancel())));
        if (_gef is not null && !_gefBusy)
            row.Children.Add(MSp(Act("for-scan-export", "Bericht speichern", () =>
            {
                try { var (h, c) = SystemScanExport(); _gefMsg = $"Bericht gespeichert: {h} (Tabelle: {Path.GetFileName(c)})"; Launch(h); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _gefMsg = "Bericht ging nicht: " + Trendo.Core.FriendlyError.Say(ex); }
                Show("forensik");
            })));
        var msg = new TextBlock { Name = "for-scan-msg", Text = _gefMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap }; _gefMsgView = msg;
        var body = new StackPanel { Spacing = 6, Children = { row, msg } };
        if (_gef is { } r)
        {
            var filters = new WrapPanel();
            foreach (var (id, label) in new[] { ("auffaellig", $"Rot und Gelb ({r.Count(Ampel.Rot) + r.Count(Ampel.Gelb)})"), ("rot", $"Rot ({r.Count(Ampel.Rot)})"), ("gelb", $"Gelb ({r.Count(Ampel.Gelb)})"), ("gruen", $"Grün ({r.Count(Ampel.Gruen)})"), ("alle", $"Alle ({r.Findings.Count})") })
            { var b = Act("for-scan-f-" + id, (_gefFilter == id ? "● " : "") + label, () => { _gefFilter = id; Show("forensik"); }); b.Margin = new Thickness(0, 0, 6, 6); filters.Children.Add(b); }
            body.Children.Add(filters);
            var list = r.Findings.Where(f => _gefFilter switch { "rot" => f.Light == Ampel.Rot, "gelb" => f.Light == Ampel.Gelb, "gruen" => f.Light == Ampel.Gruen, "alle" => true, _ => f.Light != Ampel.Gruen }).ToList();
            if (list.Count == 0) body.Children.Add(P(_gefFilter == "auffaellig" ? "Nichts Auffälliges gefunden – alle Einträge sind grün." : "Keine Einträge in dieser Auswahl.", Text));
            var i = 0;
            foreach (var f in list.Take(150))
            {
                var k = i++; var file = f.Entry.File;
                var light = new Border { Name = $"for-scan-light-{k}", Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Brush.Parse(LightColor(f.Light)), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
                ToolTip.SetTip(light, SystemScan.LightText(f.Light));
                var btn = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
                if (file is not null && Path.GetDirectoryName(file) is { } dir && Directory.Exists(dir)) { var o = Act($"for-scan-folder-{k}", "Ordner zeigen", () => Launch(dir)); o.FontSize = 11; btn.Children.Add(o); }
                var who = f.Signature.Publisher ?? f.Entry.DeclaredPublisher;
                body.Children.Add(WCard(new Grid { Name = $"for-scan-row-{k}", ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Children = { light, Pos(new StackPanel { Spacing = 2, Children = {
                    Row(new TextBlock { Text = $"{f.Entry.Name} · {f.Reason}", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, btn),
                    P($"{f.Entry.Kind} · {f.Entry.Where}", Muted, 10.5),
                    new SelectableTextBlock { Text = "Datei: " + (file ?? "nicht bestimmbar") + (f.Entry.Command.Length > 0 && f.Entry.Command != file ? "\nBefehl: " + f.Entry.Command : ""), FontFamily = Mono, FontSize = 10.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                    P($"Herausgeber: {who ?? "unbekannt"} · Signatur: {f.Signature.Detail}", Text, 11),
                    new TextBlock { Name = $"for-scan-text-{k}", Text = f.Explanation, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    P("Was du tun kannst: " + f.Tip, Muted, 11) } }, 1) } }));
            }
            if (list.Count > 150) body.Children.Add(P($"… und {list.Count - 150} weitere – alle stehen im Bericht.", Muted, 11));
            if (r.TasksWithoutProgram > 0) body.Children.Add(P($"{r.TasksWithoutProgram} geplante Aufgaben starten kein Programm (z. B. Windows-interne Dienste) und wurden nicht bewertet.", Muted, 10.5));
        }
        return ToolCard("for-card-scan", Mdi.ShieldSearch, "#F5D27A", "#9C7419", "System auf Gefahren prüfen",
            "Trendo liest, was beim Start und als Programm auf diesem PC steht – Autostart-Ordner, Run-Schlüssel, geplante Aufgaben, installierte Programme – und prüft die Signatur jeder Datei. Grün: gültig signiert von bekannter Firma. Gelb: nicht signiert oder unbekannt. Rot: Datei fehlt, Signatur ungültig oder verdächtiger Ort. Trendo ändert nichts.",
            body);
    }
}
