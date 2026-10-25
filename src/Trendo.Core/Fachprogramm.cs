namespace Trendo.Core;

/// <summary>
/// Paket 44: Fachprogramme nur bei Bedarf.
/// Die Masterzeile nennt zehn Funktionen, aber keine Einzelnamen. Diese Datei erfindet sie nicht.
/// Sie bietet eine gemeinsame Verbindung: erkennen, prüfen, anbinden, aufrufen.
/// Beim Anlegen startet nichts und es wird nichts installiert. Es gibt kein Process-Feld.
/// Eine Prüfung wartet auf das Ende. Ein Aufruf übergibt nur an den Launcher.
/// </summary>
public enum FachprogrammStand
{
    /// <summary>Noch nicht geprüft und nicht verbunden. Es läuft nichts.</summary>
    NichtGeladen,
    /// <summary>Lokal nicht vorhanden. Es wurde nichts heruntergeladen.</summary>
    NichtGefunden,
    /// <summary>Datei da, aber keine bestandene Prüfung. Nicht als bereit melden.</summary>
    GefundenNichtGeprueft,
    /// <summary>Die Prüfung hat geantwortet. Der Prozess der Prüfung ist beendet.</summary>
    Erreichbar,
    /// <summary>Prüfung gestartet, aber keine brauchbare Antwort.</summary>
    NichtErreichbar,
    /// <summary>Ohne gültige Freigabe. Es wurde nichts ausgeführt.</summary>
    FreigabeNoetig,
}

public sealed record Fachprogramm(string Id, string Name, string Zweck, bool BrauchtGeheimnis = false);

/// <summary>Schwere externe Programme aus dem vorhandenen Katalog. Keine Laufzeit, keine KI, nichts aus dem Basispaket.</summary>
public static class FachprogrammKatalog
{
    public static readonly IReadOnlyList<Fachprogramm> Alle =
    [
        new("blender", "Blender", "3D-Modelle"),
        new("unreal", "Unreal Engine", "Spiele"),
        new("libreoffice", "LibreOffice", "Texte, Tabellen, Präsentationen"),
        new("krita", "Krita", "Bilder zeichnen"),
        new("inkscape", "Inkscape", "Vektoren"),
        new("kdenlive", "Kdenlive", "Videoschnitt"),
        new("audacity", "Audacity", "Ton"),
        new("obs", "OBS Studio", "Aufnehmen"),
        new("colmap", "COLMAP", "3D aus Fotos"),
        new("meshroom", "Meshroom", "3D aus Fotos"),
        new("vscode", "VS Code", "Code und Webseiten"),
    ];

    public static Fachprogramm Get(string id) =>
        Alle.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Dieses Programm kennt Trendo nicht.");
}

public sealed record FachprogrammBericht(
    string Id,
    string Name,
    FachprogrammStand Stand,
    string Hinweis,
    string? Pfad,
    string? Ausgabe,
    bool Angebunden);

/// <summary>Eine Verbindung für alle Fachprogramme. Lokale Datei zuerst. Download nur nach Freigabe, und nur wenn noch nichts da ist.</summary>
public sealed class FachprogrammAdapter
{
    public const string AufrufAktion = "fachprogramm.aufruf";
    public const string InstallAktion = "fachprogramm.install";

    private readonly DependencyManager _deps;
    private readonly ApprovalGate _gate;
    private readonly ISecretStore _secrets;
    private readonly AuditLog _audit;
    private readonly IProcessLauncher _launcher;
    private readonly bool _offline;
    private readonly IReadOnlyList<Fachprogramm> _katalog;
    private readonly Dictionary<string, FachprogrammBericht> _probe = new();
    private readonly HashSet<string> _gebunden = new();

    /// <summary>Optionaler Pfad aus der Oberfläche. Null heißt: im Katalog weiter suchen. Ein gesetzter Pfad wird nicht noch einmal auf die Festplatte geprüft.</summary>
    public Func<Fachprogramm, string?>? PfadSuche { get; set; }

    /// <summary>Wie lange LibreOffice, OBS und VS Code auf die Versionsabfrage warten. Danach wird abgebrochen, ohne das Programm zu öffnen.</summary>
    public TimeSpan Versionszeit { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>Unter Windows liest OBS die Dateiversion und startet keinen Prozess. Sonst gilt obs --version.</summary>
    public bool ObsWieWindows { get; set; } = OperatingSystem.IsWindows();

    /// <summary>Liest die Dateiversion, ohne ein Programm zu starten. Tests setzen hier einen Ersatz. Sonst gilt die Dateiinformation.</summary>
    public Func<string, string?>? DateiversionLesen { get; set; }

    public FachprogrammAdapter(DependencyManager deps, ApprovalGate gate, ISecretStore secrets, AuditLog audit, IProcessLauncher launcher, bool offline = false, IReadOnlyList<Fachprogramm>? katalog = null)
    {
        _deps = deps;
        _gate = gate;
        _secrets = secrets;
        _audit = audit;
        _launcher = launcher;
        _offline = offline;
        _katalog = katalog ?? FachprogrammKatalog.Alle;
    }

    public bool IstAngebunden(string id) => _gebunden.Contains(id);

    /// <summary>Nur nach der Datei schauen. Startet nichts.</summary>
    public FachprogrammBericht Erkennen(string id)
    {
        var p = Programm(id);
        var pfad = Finde(p);
        if (pfad is null)
            return Neu(p, FachprogrammStand.NichtGefunden, "Nicht geladen. Auf diesem PC nicht gefunden. Trendo hat nichts installiert.", null, null, false);
        return Neu(p, FachprogrammStand.GefundenNichtGeprueft, "Gefunden, nicht geprüft. Die Datei liegt da. Es läuft nichts.", pfad, null, false);
    }

    /// <summary>Kurze Prüfung. LibreOffice, OBS und VS Code fragen die Version ab, ohne ein Fenster zu behalten. Ohne sicheres Prüfargument wird nichts gestartet.</summary>
    public async Task<FachprogrammBericht> ProbeAsync(string id, CancellationToken ct = default)
    {
        var p = Programm(id);
        if (p.BrauchtGeheimnis && string.IsNullOrEmpty(_secrets.Get(Slot(p.Id))))
        {
            var fehlt = Neu(p, FachprogrammStand.NichtErreichbar, "Fehlgeschlagen. Der Zugang fehlt. Es wurde nichts gesendet.", null, null, false);
            Merken(fehlt);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} NichtErreichbar zugang");
            return fehlt;
        }
        var pfad = Finde(p);
        if (pfad is null)
        {
            var weg = Neu(p, FachprogrammStand.NichtGefunden, "Fehlgeschlagen. Auf diesem PC nicht gefunden. Trendo hat nichts installiert.", null, null, false);
            Merken(weg);
            return weg;
        }
        if (p.Id == "obs" && ObsWieWindows)
            return DateiVersion(p, pfad);
        var versionsabfrage = p.Id is "libreoffice" or "vscode" or "obs";
        var args = versionsabfrage ? "--version" : PruefArgs(p.Id);
        if (args is null)
        {
            var offen = Neu(p, FachprogrammStand.GefundenNichtGeprueft, "Die Datei liegt da. Eine stille Prüfung würde ein Fenster öffnen oder hängen. Ob es funktioniert, ist offen.", pfad, null, false);
            Merken(offen);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} GefundenNichtGeprueft");
            return offen;
        }
        var zeitText = versionsabfrage
            ? "Fehlgeschlagen. Die Versionsabfrage hat zu lange gedauert. Das Programm wurde nicht gestartet."
            : "Fehlgeschlagen. Zeitüberschreitung. Nicht bereit.";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(versionsabfrage ? Versionszeit : TimeSpan.FromSeconds(25));
            var (exit, output) = await _deps.Runner.RunAsync(pfad, args, null, cts.Token);
            var zeile = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(zeile) && zeile.Length > 80) zeile = zeile[..80];
            if (exit != 0)
            {
                var stand = pfad.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
                    ? Neu(p, FachprogrammStand.NichtGefunden, "Nur ein Platzhalter, nicht das Programm. Es wurde nichts installiert.", null, null, false)
                    : Neu(p, FachprogrammStand.NichtErreichbar, $"Fehlgeschlagen. Die Prüfung hat nicht geklappt (Code {exit}). Es ist nicht bereit.", pfad, zeile, false);
                Merken(stand);
                _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} {stand.Stand} exit={exit}");
                return stand;
            }
            _deps.Integrate(p.Id, pfad);
            var ok = Neu(p, FachprogrammStand.Erreichbar, "Bestanden. Die Prüfung hat geklappt. Das Programm ist nicht weiter geladen.", pfad, zeile, false);
            Merken(ok);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} Erreichbar");
            return ok;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            var zeit = Neu(p, FachprogrammStand.NichtErreichbar, zeitText, pfad, null, false);
            Merken(zeit);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} NichtErreichbar zeit");
            return zeit;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var kaputt = Neu(p, FachprogrammStand.NichtErreichbar, $"Fehlgeschlagen. Die Prüfung hat nicht geklappt ({ex.GetType().Name}). Es ist nicht bereit.", pfad, null, false);
            Merken(kaputt);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} NichtErreichbar {ex.GetType().Name}");
            return kaputt;
        }
    }

    /// <summary>Nur nach bestandener Prüfung in diesem Programmlauf. Startet nichts.</summary>
    public FachprogrammBericht Anbinden(string id)
    {
        var p = Programm(id);
        if (_probe.TryGetValue(id, out var last) && last.Stand == FachprogrammStand.Erreichbar && last.Pfad is not null)
        {
            _gebunden.Add(id);
            var verbunden = last with { Angebunden = true, Hinweis = "Bestanden. Verbunden für diesen Start von Trendo. Es läuft kein Extra-Prozess." };
            _probe[id] = verbunden;
            _audit.Append("fachprogramm", "fachprogramm.anbinden", p.Id);
            return verbunden;
        }
        _audit.Append("fachprogramm", "fachprogramm.anbinden.blockiert", p.Id);
        var basis = _probe.TryGetValue(id, out var da) ? da : Erkennen(id);
        return basis with { Angebunden = false, Hinweis = "Zuerst „Verbindung prüfen“. Es wurde nichts verbunden." };
    }

    /// <summary>Löst nur die Merker. Es gibt keinen Prozess zu beenden.</summary>
    public FachprogrammBericht Trennen(string id)
    {
        var p = Programm(id);
        _gebunden.Remove(id);
        _audit.Append("fachprogramm", "fachprogramm.trennen", p.Id);
        if (_probe.TryGetValue(id, out var last))
        {
            var frei = last with { Angebunden = false, Hinweis = "Getrennt. Es läuft kein Prozess." };
            _probe[id] = frei;
            return frei;
        }
        return Neu(p, FachprogrammStand.NichtGeladen, "Getrennt. Es läuft kein Prozess.", null, null, false);
    }

    /// <summary>Startet nur verbunden und nur mit gültiger Freigabe für genau dieses Programm. Lädt nichts herunter.</summary>
    public FachprogrammBericht Aufrufen(string approvalId, string id, IReadOnlyList<string>? args = null)
    {
        var p = Programm(id);
        args ??= [];
        if (!_gebunden.Contains(id) || !_probe.TryGetValue(id, out var last) || last.Stand != FachprogrammStand.Erreichbar || last.Pfad is null)
        {
            _audit.Append("fachprogramm", "fachprogramm.aufruf.blockiert", p.Id + " nicht verbunden");
            var basis = _probe.TryGetValue(id, out var da) ? da : Erkennen(id);
            return basis with { Angebunden = false, Hinweis = "Zuerst „Verbindung prüfen“. Es wurde nichts gestartet." };
        }
        try
        {
            _gate.Execute(approvalId, AufrufAktion, id, () =>
            {
                _launcher.OpenWithArgs(last.Pfad, args);
                return true;
            });
        }
        catch (ApprovalException)
        {
            return last with { Angebunden = true, Stand = FachprogrammStand.FreigabeNoetig, Hinweis = "Ohne deine Freigabe passiert nichts." };
        }
        _audit.Append("fachprogramm", "fachprogramm.aufruf", $"{p.Id} args={args.Count}");
        return last with { Angebunden = true, Hinweis = "Geöffnet. Trendo hält es nicht fest." };
    }

    /// <summary>Nichts laden, wenn die Datei schon da ist oder die Freigabe fehlt. Geführte Programme werden nie automatisch geholt.</summary>
    public async Task<FachprogrammBericht> InstallierenAsync(Principal by, string approvalId, string id, CancellationToken ct = default)
    {
        var p = Programm(id);
        var vorhanden = Finde(p);
        if (vorhanden is not null)
            return Neu(p, _probe.TryGetValue(id, out var ok) && ok.Stand == FachprogrammStand.Erreichbar ? FachprogrammStand.Erreichbar : FachprogrammStand.GefundenNichtGeprueft,
                "Schon vorhanden. Es wird nichts heruntergeladen.", vorhanden, null, _gebunden.Contains(id));
        var c = DependencyCatalog.Get(p.Id);
        if (c.Mode is not (DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Included))
            return Neu(p, FachprogrammStand.NichtGefunden, "Das wird nicht automatisch installiert. Es wurde nichts geladen.", null, null, false);
        if (_offline && c.Mode != DeliveryMode.Included) // Vollpaket braucht kein Internet
            return Neu(p, FachprogrammStand.NichtGefunden, "Offline. Es wird nichts heruntergeladen.", null, null, false);
        if (!_deps.HasConsent)
            return Neu(p, FachprogrammStand.FreigabeNoetig, "Die Zustimmung fehlt. Es wurde nichts installiert.", null, null, false);
        Rbac.Demand(by, Permission.ApproveAction, _audit);
        try
        {
            var geschafft = await _gate.Execute(approvalId, InstallAktion, id, () => _deps.InstallAsync(by, c, null, ct));
            if (!geschafft || Finde(p) is null)
                return Neu(p, FachprogrammStand.NichtGefunden, "Die Installation hat nicht geklappt. Das Programm ist nicht da.", null, null, false);
            return Erkennen(id);
        }
        catch (ApprovalException)
        {
            return Neu(p, FachprogrammStand.FreigabeNoetig, "Ohne deine Freigabe passiert nichts.", null, null, false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            _audit.Append("fachprogramm", "fachprogramm.install.fail", $"{p.Id} {ex.GetType().Name}");
            return Neu(p, FachprogrammStand.NichtGefunden, "Die Installation hat nicht geklappt. Es wurde nichts weiter geladen.", null, null, false);
        }
    }

    /// <summary>Nur wenn das Programm einen Zugang braucht. Der Wert steht nie im Protokoll.</summary>
    public void ZugangMerken(string id, string wert)
    {
        var p = Programm(id);
        if (!p.BrauchtGeheimnis) throw new InvalidOperationException($"{p.Name} braucht keinen gespeicherten Schlüssel.");
        if (string.IsNullOrWhiteSpace(wert)) throw new ArgumentException("Leer ablegen geht nicht.");
        _secrets.Set(Slot(p.Id), wert);
        _audit.Append("fachprogramm", "fachprogramm.zugang", $"{p.Id} gesetzt");
    }

    private Fachprogramm Programm(string id) =>
        _katalog.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("Dieses Programm kennt Trendo nicht.");

    private void Merken(FachprogrammBericht bericht) => _probe[bericht.Id] = bericht;

    private string? Finde(Fachprogramm programm)
    {
        if (PfadSuche is { } suche && suche(programm) is { } extra && extra.Length > 0) return extra;
        var c = DependencyCatalog.Get(programm.Id);
        if (_deps.Integrated(c.Id) is { } saved && _deps.Exists(saved)) return saved;
        var resolved = _deps.ResolvePath(c);
        if (resolved is not null) return resolved;
        var system = _deps.FindOnSystem(c);
        if (system is not null) return system;
        var env = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var name in c.Tools ?? [])
            foreach (var dir in env.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir, name);
                if (_deps.Exists(candidate)) return candidate;
            }
        return null;
    }

    private FachprogrammBericht DateiVersion(Fachprogramm p, string pfad)
    {
        string? version = null;
        try { version = (DateiversionLesen ?? _deps.FileVersion)(pfad); }
        catch { version = null; }
        if (string.IsNullOrWhiteSpace(version))
        {
            var leer = Neu(p, FachprogrammStand.NichtErreichbar, "Fehlgeschlagen. Die Datei liegt da, aber eine Version ist nicht lesbar. Das Programm wurde nicht gestartet.", pfad, null, false);
            Merken(leer);
            _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} NichtErreichbar version");
            return leer;
        }
        if (version.Length > 80) version = version[..80];
        _deps.Integrate(p.Id, pfad);
        var ok = Neu(p, FachprogrammStand.Erreichbar, "Bestanden. Die Prüfung hat geklappt. Das Programm ist nicht weiter geladen.", pfad, version, false);
        Merken(ok);
        _audit.Append("fachprogramm", "fachprogramm.probe", $"{p.Id} Erreichbar");
        return ok;
    }

    private static string? PruefArgs(string id) =>
        StudioTools.Verify.TryGetValue(id, out var v) ? v.VerifyArgs : null;

    private static string Slot(string id) => "fachprogramm/" + id;

    private static FachprogrammBericht Neu(Fachprogramm p, FachprogrammStand stand, string hinweis, string? pfad, string? ausgabe, bool angebunden) =>
        new(p.Id, p.Name, stand, hinweis, pfad, ausgabe, angebunden);
}
