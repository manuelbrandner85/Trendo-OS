using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>T-0764 (SYS-11): eine Seite je Programm, das Trendo selbst eingerichtet hat – Starten, Aktualisieren und Entfernen an einem Ort.
/// Entfernen verschiebt nur Trendos eigenen Ordner in die Sicherung (mit Rückfrage, wiederherstellbar) und braucht keine Windows-Einstellungen.
/// Die Rolle prüft der Kern (<see cref="TrendoPrograms"/>): ein Kinderprofil entfernt nichts.</summary>
public partial class MainWindow
{
    const string ProgDetail = "prog-programm";
    string? _progSel;              // Id des gewählten Programms
    TrendoPrograms.Herkunft _progSelArt;
    bool _progRemoveAsk;           // „Entfernen …“ geklickt, Rückfrage sichtbar
    string? _progPurgeAsk;         // Sicherung, die endgültig gelöscht werden soll (Rückfrage sichtbar)

    /// <summary>Ordner der Portable-Programme (Seite „Pflegen“ → „Portable Programme“).</summary>
    internal string PortableRoot => Path.Combine(_core.DataRoot, "werkzeuge");

    /// <summary>Von Trendo eingerichtete Programme (für Seite und Tests).</summary>
    public IReadOnlyList<TrendoPrograms.Eintrag> TrendoProgramme() => TrendoPrograms.List(PortableRoot, _deps.ToolsRoot);

    /// <summary>Programm-Seite öffnen.</summary>
    public void OpenProgramPage(TrendoPrograms.Eintrag e) { _progSel = e.Id; _progSelArt = e.Art; _progRemoveAsk = false; _w45Msg = ""; Show(ProgDetail); }

    static string Groesse(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : $"{b / (double)(1L << 20):0.0} MB";
    static string ArtText(TrendoPrograms.Herkunft a) => a switch
    {
        TrendoPrograms.Herkunft.Portable => "Portable-Programm",
        TrendoPrograms.Herkunft.Vollpaket => "mitgeliefertes Programm",
        _ => "Programm von GitHub",
    };

    /// <summary>Karte auf „Pflegen“: alle von Trendo eingerichteten Programme, je eins mit „Öffnen“ zur Programm-Seite, dazu die Sicherung.</summary>
    Control TrendoProgrammeCard()
    {
        var list = new StackPanel { Name = "trendo-prog-list", Spacing = 4 };
        var all = TrendoProgramme();
        foreach (var e in all)
        {
            var entry = e;
            list.Children.Add(Row(P($"{e.Name} · {ArtText(e.Art)} · {Groesse(e.Bytes)}", Text, 12),
                Act($"trendo-prog-open-{e.Art}-{e.Id}", "Öffnen", () => OpenProgramPage(entry))));
        }
        if (all.Count == 0) list.Children.Add(P("Noch keine. Portable Programme, mitgelieferte Programme und Programme von GitHub erscheinen hier, sobald Trendo sie eingerichtet hat."));
        var body = new StackPanel { Spacing = 6, Children = { list } };
        var backups = TrendoPrograms.Backups(PortableRoot, _deps.ToolsRoot);
        if (backups.Count > 0)
        {
            body.Children.Add(H("Sicherung entfernter Programme", 13));
            foreach (var (s, i) in backups.Select((s, i) => (s, i)))
            {
                var sich = s;
                var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
                buttons.Children.Add(Act($"trendo-prog-restore-{i}", "Wiederherstellen", () => Try("prog-pflege", () =>
                {
                    var e = TrendoPrograms.Restore(CurrentPrincipal, sich, PortableRoot, _deps.ToolsRoot, _core.Audit);
                    ReloadStudios(); Say($"{e.Name} ist wiederhergestellt und wieder eingerichtet.", "prog-pflege");
                })));
                if (_progPurgeAsk == s.Ordner)
                {
                    buttons.Children.Add(Act($"trendo-prog-purge-yes-{i}", "Ja, endgültig löschen", () => Try("prog-pflege", () =>
                    {
                        _progPurgeAsk = null;
                        TrendoPrograms.Purge(CurrentPrincipal, sich, PortableRoot, _deps.ToolsRoot, _core.Audit);
                        Say($"{sich.Name} ist endgültig gelöscht ({Groesse(sich.Bytes)} frei).", "prog-pflege");
                    })));
                    buttons.Children.Add(Act($"trendo-prog-purge-no-{i}", "Abbrechen", () => { _progPurgeAsk = null; Say("Nichts gelöscht.", "prog-pflege"); }));
                }
                else buttons.Children.Add(Act($"trendo-prog-purge-{i}", "Endgültig löschen …", () => { _progPurgeAsk = sich.Ordner; Show("prog-pflege"); }));
                body.Children.Add(Row(P($"{s.Name} · entfernt am {s.Am:dd.MM.yyyy HH:mm} · {Groesse(s.Bytes)}", null, 11.5), buttons));
            }
        }
        return Card(Section("Von Trendo eingerichtete Programme", "Starten, aktualisieren und entfernen – je Programm auf einer eigenen Seite. Entfernen verschiebt das Programm in eine Sicherung; erst „Endgültig löschen“ gibt den Platz frei.", body));
    }

    /// <summary>Die Programm-Seite: was es ist, woher, wo es liegt – und Starten, Aktualisieren, Entfernen.</summary>
    Control ProgDetailView()
    {
        const string V = ProgDetail;
        var e = TrendoProgramme().FirstOrDefault(x => x.Id == _progSel && x.Art == _progSelArt);
        var sp = new StackPanel { Spacing = 10 };
        var back = Act("prog-detail-back", "← Alle Programme", () => Show("prog-pflege"));
        if (e is null)
        {
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { P("Dieses Programm ist nicht (mehr) eingerichtet. Entfernte Programme findest du unter „Pflegen“ in der Sicherung.", Text, 13), back } }));
            return ProgPage("Programm", sp);
        }
        var info = new StackPanel { Spacing = 3, Children = {
            new TextBlock { Name = "prog-detail-name", Text = e.Name, Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            P($"{ArtText(e.Art)}{(e.Version.Length > 0 ? " · Version " + e.Version : "")} · {Groesse(e.Bytes)}", Text, 12.5),
            P("Herkunft: " + e.Quelle, null, 11.5),
            P("Ordner: " + e.Ordner, null, 11.5),
            P(e.Exe is null ? "Kein startbares Programm gefunden (Werkzeug für andere Programme)." : "Programmdatei: " + Path.GetFileName(e.Exe), null, 11.5) } };
        var actions = new WrapPanel();
        void Add(Control c) { c.Margin = new Avalonia.Thickness(0, 0, 6, 6); actions.Children.Add(c); }
        if (e.Exe is { } exe) Add(Act("prog-detail-start", "Starten", () => { Launch(exe); Say(e.Name + " gestartet.", V); }, true));
        Add(Act("prog-detail-folder", "Ordner zeigen", () => Launch(e.Ordner)));
        if (e.Art == TrendoPrograms.Herkunft.GitHub && GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(x => x.Id == e.Id) is { } gh)
            Add(Act("prog-detail-update", _ghUpdates.TryGetValue(e.Id, out var tag) ? $"Auf {tag} aktualisieren" : "Nach Aktualisierung suchen", () =>
            {
                if (_ghUpdates.ContainsKey(e.Id)) _ = GitHubUpdate(gh); else _ = GitHubCheckUpdates();
                Say(_ghUpdates.ContainsKey(e.Id) ? "Aktualisierung läuft …" : "Trendo schaut auf GitHub nach einer neueren Version …", V);
            }));
        var updateHint = e.Art switch
        {
            TrendoPrograms.Herkunft.Portable => "Aktualisieren: neue ZIP-Datei unter „Pflegen → Portable Programme“ mit demselben Namen entpacken – die alte Fassung wird ersetzt.",
            TrendoPrograms.Herkunft.Vollpaket => "Aktualisieren: kommt mit dem nächsten Trendo-Vollpaket (Prüfsumme wird kontrolliert).",
            _ => "Aktualisieren: Trendo holt die neueste Version von GitHub, die alte bleibt bis zum Erfolg erhalten.",
        };
        if (_progRemoveAsk)
        {
            Add(new Button { Name = "prog-detail-remove-yes", Content = "Ja, entfernen", Background = Brush.Parse("#B91C1C"), Foreground = Brushes.White, Padding = new Avalonia.Thickness(12, 6), CornerRadius = new Avalonia.CornerRadius(8) }
                .Also(b => b.Click += (_, _) => Try(V, () =>
                {
                    _progRemoveAsk = false;
                    var s = TrendoPrograms.Remove(CurrentPrincipal, e, PortableRoot, _deps.ToolsRoot, _core.Audit);
                    ReloadStudios();
                    _progSel = null;
                    Say($"{e.Name} ist entfernt und liegt in der Sicherung ({Path.GetFileName(s.Ordner)}). Wiederherstellen kannst du es unter „Pflegen“.", "prog-pflege");
                })));
            Add(Act("prog-detail-remove-no", "Abbrechen", () => { _progRemoveAsk = false; Say("Nichts entfernt.", V); }));
        }
        else Add(Act("prog-detail-remove", "Entfernen …", () => { _progRemoveAsk = true; Show(V); }));
        if (!CanManageSoftware) Add(P(Rbac.NurErwachseneSoftware, Brush.Parse("#FACC15"), 11.5));
        sp.Children.Add(Card(new StackPanel { Spacing = 8, Children = { back, info, actions,
            P(updateHint, null, 11),
            _progRemoveAsk ? P($"„{e.Name}“ wirklich entfernen? Der Ordner wird in die Sicherung verschoben (nichts wird gelöscht). Läuft das Programm noch, bitte zuerst schließen.", Text, 12) : P("Entfernen betrifft nur Trendos eigenen Ordner – Windows-Einstellungen braucht es dafür nicht.", null, 11) } }));
        return ProgPage(e.Name, sp);
    }
}

