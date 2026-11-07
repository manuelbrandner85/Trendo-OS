using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Spiele bauen → „Veröffentlichen“ (Wunsch 02.11.2026): Ziele wählen, Store-Angaben, Checkliste je Plattform, fertige Pakete und Hochladen mit dem eigenen Konto –
/// Hochladen erst nach Ja, in einem sichtbaren Fenster, Anmeldung macht der Nutzer selbst.</summary>
public partial class MainWindow
{
    string? _gameUploadAsk; List<string> _gamePackages = [];
    public IReadOnlyList<string> GamePackages => _gamePackages;
    /// <summary>Für Tests: statt ein Fenster zu öffnen (Befehl zum Hochladen).</summary>
    public static Action<string>? UploadOverride { get; set; }

    private Control GameReleaseView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-release", Spacing = 8 };
        sp.Children.Add(H("Veröffentlichen", 15));
        sp.Children.Add(P("Wähle, wo dein Spiel erscheinen soll. Trendo zeigt, was dafür fertig sein muss, baut die Pakete und lädt erst nach deinem Ja mit deinem eigenen Konto hoch.", null, 12));
        var targets = GameRelease.TargetsOf(d.Data).ToList();
        var row = new WrapPanel();
        foreach (var t in GameRelease.Targets)
        {
            var on = targets.Contains(t.Id); var id = t.Id;
            var b = new Button { Name = "game-rel-target-" + t.Id, Content = (on ? "✓ " : "") + t.Label, FontSize = 12.5, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(10),
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            ToolTip.SetTip(b, t.Hint);
            b.Click += (_, _) => ProgramRun(GK, () => { var l = GameRelease.TargetsOf(d.Data).ToList(); if (!l.Remove(id)) l.Add(id); d.Edit("Ziele", data => GameRelease.SetTargets(data, l)); _progMsg[GK] = l.Count == 0 ? "Noch kein Ziel gewählt." : "Ziele: " + string.Join(", ", l.Select(GameRelease.Label)); });
            row.Children.Add(b);
        }
        sp.Children.Add(row);

        // Store-Angaben – Claude füllt sie auf Wunsch live aus
        TextBox F(string name, string key, string hint, bool multi = false)
        {
            var tb = new TextBox { Name = "game-rel-" + name, Text = GameRelease.Get(d.Data, key), PlaceholderText = hint, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 70 : 0 };
            tb.LostFocus += (_, _) => { if ((tb.Text ?? "") != GameRelease.Get(d.Data, key)) d.Edit(hint, data => GameRelease.Set(data, key, tb.Text ?? "")); };
            return tb;
        }
        sp.Children.Add(P("Store-Angaben", Text, 12.5));
        sp.Children.Add(F("short", "short", "Kurzbeschreibung – ein Satz, der neugierig macht"));
        sp.Children.Add(F("long", "long", "Beschreibung – die besten Seiten deines Spiels", true));
        sp.Children.Add(new WrapPanel { Children = { F("price", "price", "Preis (z. B. kostenlos, 4,99 €)"), new Border { Width = 6 }, F("age", "age", "Altersfreigabe (z. B. USK 12)") } });
        if (targets.Contains("itchio")) sp.Children.Add(F("itch", "itch", "itch.io: dein-name/dein-spiel"));
        if (targets.Contains("steam")) sp.Children.Add(new WrapPanel { Children = { F("steamApp", "steamApp", "Steam App-ID"), new Border { Width = 6 }, F("steamUser", "steamUser", "Steam-Anmeldename (für SteamCMD)") } });

        // G3: Lizenzen der Assets prüfen, bevor es nach draußen geht
        foreach (var w in GameAssets.Check(d.Data, commercial: true)) sp.Children.Add(P("⚠ " + w, Brush.Parse("#F5A524"), 12));
        // Checkliste wie in Spielestudios
        var items = GameRelease.ChecklistFor(targets);
        var done = items.Count(i => GameRelease.Done(d.Data, i.Id));
        sp.Children.Add(P(items.Count == 0 ? "Wähle oben ein Ziel – dann erscheint die Checkliste." : $"Checkliste: {done} von {items.Count} erledigt", Text, 12.5));
        foreach (var it in items)
        {
            var id = it.Id;
            var cb = new CheckBox { Name = "game-rel-check-" + it.Id, IsChecked = GameRelease.Done(d.Data, it.Id), Content = new TextBlock { Text = it.Label + " – " + it.Hint, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
            cb.IsCheckedChanged += (_, _) => { d.Edit(it.Label, data => GameRelease.SetDone(data, id, cb.IsChecked == true)); };
            sp.Children.Add(cb);
        }

        // Pakete und Hochladen
        var built = d.Text("built");
        var pack = Act("game-rel-pack", "📦 Pakete bauen", () => ProgramRun(GK, () => GameReleasePack(d)), true);
        pack.IsEnabled = targets.Count > 0;
        sp.Children.Add(new WrapPanel { Children = { pack } });
        if (built.Length == 0) sp.Children.Add(P("Tipp: Zuerst im Reiter „Bauen“ das Spiel bauen – daraus entstehen die Pakete.", null, 11.5));
        foreach (var f in _gamePackages) sp.Children.Add(P("✓ " + Path.GetFileName(f), Brush.Parse("#2FBF71"), 12));
        foreach (var t in targets.Where(t => t is "itchio" or "steam"))
        {
            var tt = t;
            if (_gameUploadAsk == t)
            {
                var yes = Act("game-rel-upload-yes", $"Ja, zu {GameRelease.Label(t)} hochladen", () => GameReleaseUpload(d, tt), true);
                var no = Act("game-rel-upload-no", "Abbrechen", () => { _gameUploadAsk = null; Show(GK); });
                sp.Children.Add(new Border { Background = Brush.Parse("#22F5A524"), BorderBrush = Brush.Parse("#F5A524"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
                    Child = new StackPanel { Spacing = 6, Children = {
                        P($"Wirklich zu {GameRelease.Label(t)} hochladen? Trendo öffnet dazu ein Fenster ({(t == "steam" ? "SteamCMD" : "butler")}) – dort meldest du dich selbst mit deinem Konto an. Trendo sieht dein Passwort nie.", Text, 12),
                        new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
            }
            else sp.Children.Add(Act("game-rel-upload-" + t, $"⬆ Zu {GameRelease.Label(t)} hochladen …", () => { _gameUploadAsk = tt; Show(GK); }));
        }
        return sp;
    }

    /// <summary>Pakete je Ziel bauen (aus dem gebauten Spiel) in „Spiele/Veröffentlicht/&lt;Titel&gt;“.</summary>
    void GameReleasePack(ProgramDocument d)
    {
        var built = d.Text("built");
        if (built.Length == 0 || !Directory.Exists(built)) { _progMsg[GK] = "Zuerst „Bauen“ – dann entstehen aus dem fertigen Spiel die Pakete."; return; }
        var outDir = Path.Combine(WorkFolder("Spiele"), "Veröffentlicht", GameRelease.Slug(d.Text("title", "Spiel")));
        var (files, notes) = GameRelease.Pack(d.Data, built, outDir, GameRelease.TargetsOf(d.Data));
        _gamePackages = files;
        // Unreal-Spiel für Windows: Trendo packt selbst mit Unreals Paketierer (ein Klick statt „von Hand in Unreal packen“)
        var unreal = Directory.GetFiles(built, "*.uproject").Length > 0 && GameRelease.TargetsOf(d.Data).Contains("windows");
        if (unreal) notes.RemoveAll(n => n.StartsWith("Windows:", StringComparison.Ordinal));
        _core.Audit.Append(Principal.LocalOwner.Name, "game.release.pack", $"{files.Count} Pakete");
        _progMsg[GK] = (files.Count > 0 ? $"{files.Count} Pakete fertig in „Veröffentlicht“." : unreal ? "" : "Keine Pakete gebaut.") + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : "");
        if (files.Count > 0) Toast("Pakete fertig.", ("Ordner öffnen", () => Launch(outDir)));
        if (unreal) _ = GameUnrealPackage(d, outDir);
    }

    /// <summary>Vorschlag 12: fertiges Windows-Spiel aus dem Unreal-Projekt – Unreals Paketierer (BuildCookRun, Shipping) und ein ZIP zum Weitergeben.</summary>
    public async Task GameUnrealPackage(ProgramDocument d, string outDir)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) { _progMsg[GK] = "Zuerst „Bauen“ – dann packt Trendo das Unreal-Spiel."; Show(GK); return; }
        if (UnrealEngineDir() is not { } engine) { _progMsg[GK] = "Für das Windows-Paket braucht es Unreal Engine 5 (kostenlos über den Epic Games Launcher)."; Show(GK); return; }
        if (UnrealBusy) { _progMsg[GK] = "Unreal arbeitet gerade – bitte warten, bis die Prüfung fertig ist."; Show(GK); return; }
        UnrealBusy = true;
        void Step(string t) { _progMsg[GK] = t; _ui.Post(() => { if (CurrentView == GK) Show(GK); }); }
        var slug = GameRelease.Slug(d.Text("title", "Spiel")); var archive = Path.Combine(outDir, "unreal-paket");
        var logs = Path.Combine(built, "Trendo-Pruefung"); Directory.CreateDirectory(logs);
        string msg;
        try
        {
            Step("📦 Unreal baut das fertige Windows-Spiel: Code (Shipping), Inhalte kochen und packen … beim ersten Mal kann das lange dauern.");
            var (code, log) = await Task.Run(() => UnrealRun(UnrealPackage.RunUat(engine), UnrealPackage.Args(uproject, archive), Path.Combine(logs, "paket.txt"), 14_400_000));
            if (!UnrealPackage.Ok(log)) msg = $"✗ Windows-Spiel nicht fertig (Exitcode {code}): {UnrealPackage.Reason(log)}";
            else
            {
                Step("📦 Windows-Spiel fertig – Trendo packt es als ZIP …");
                var zip = await Task.Run(() => UnrealPackage.Zip(archive, Path.Combine(outDir, slug + "-windows.zip")));
                _gamePackages.Add(zip);
                msg = $"✓ Windows-Spiel fertig: {Path.GetFileName(zip)} ({new FileInfo(zip).Length / 1048576.0:0} MB) – entpacken und {Path.GetFileName(UnrealPackage.Exe(archive) ?? "das Spiel")} starten.";
                _ui.Post(() => Toast("Windows-Spiel fertig.", ("Ordner öffnen", () => Launch(outDir))));
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { msg = "✗ Windows-Spiel nicht gepackt: " + e.Message; }
        finally { UnrealBusy = false; }
        _core.Audit.Append(Principal.LocalOwner.Name, "game.release.unreal", msg.StartsWith('✓') ? "ok" : "fehler");
        Step(msg);
    }

    /// <summary>Hochladen nach Ja: sichtbares Fenster mit butler bzw. SteamCMD – die Anmeldung macht der Nutzer dort selbst.</summary>
    void GameReleaseUpload(ProgramDocument d, string target)
    {
        _gameUploadAsk = null;
        var package = target == "steam" ? _gamePackages.FirstOrDefault(Directory.Exists) : _gamePackages.FirstOrDefault(f => f.EndsWith("-web.zip", StringComparison.OrdinalIgnoreCase));
        if (package is null) { _progMsg[GK] = "Zuerst „📦 Pakete bauen“."; Show(GK); return; }
        if (GameRelease.Upload(d.Data, target, package) is not { } cmd) { _progMsg[GK] = target == "steam" ? "Bitte zuerst deine Steam App-ID und deinen Anmeldenamen eintragen." : "Bitte zuerst deine itch.io-Seite eintragen (dein-name/dein-spiel)."; Show(GK); return; }
        var tool = target == "steam" ? "steamcmd" : "butler";
        _core.Audit.Append(Principal.LocalOwner.Name, "game.release.upload", target);
        if (UploadOverride is { } o) { o(cmd); _progMsg[GK] = $"Hochladen zu {GameRelease.Label(target)} gestartet."; Show(GK); return; }
        if (_opt.Isolated) { _progMsg[GK] = "Testmodus: nichts hochgeladen."; Show(GK); return; }
        if (Cli.Find(tool) is null)
        {
            _progMsg[GK] = $"Dafür braucht es {(target == "steam" ? "SteamCMD" : "butler")} von der offiziellen Seite – danach noch einmal „Hochladen“.";
            Launch(target == "steam" ? "https://developer.valvesoftware.com/wiki/SteamCMD" : "https://itch.io/docs/butler/installing.html");
            Show(GK); return;
        }
        try { Process.Start(new ProcessStartInfo("cmd.exe", "/k " + cmd) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(package)! }); _progMsg[GK] = $"Fenster zum Hochladen ist offen – melde dich dort mit deinem {GameRelease.Label(target)}-Konto an."; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _progMsg[GK] = "Das Fenster ließ sich nicht öffnen: " + e.Message; }
        Show(GK);
    }
}
