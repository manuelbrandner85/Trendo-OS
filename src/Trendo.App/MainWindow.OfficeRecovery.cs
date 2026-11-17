using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0254 (100): Text, Tabelle und Präsentation sichern ungespeicherte Änderungen automatisch; T-0691: „3D-Modelle erstellen“ (3d) und Trendo CAD (cad) ebenso.
/// Nach einem Absturz oder Schließen ohne Speichern bietet das Programm „Wiederherstellen“ an. Gespeichert wird im eigenen Datenordner, nie über die Originaldatei;
/// nach dem Speichern oder Verwerfen wird die Sicherung gelöscht.
/// T-0714 (B6 „Programm-Hülle für alle“, Etappe E2): Es gibt nur noch EINE Notsicherung – die der Hülle. Die fünf Programme hängen über <see cref="ProgramLink"/> an
/// (<c>programme/sicherung/LINK-TEXT|LINK-TABELLE|LINK-PRAESENTATION|LINK-CAD|LINK-3D-&lt;Kennung&gt;.tdoc</c>, atomar, im 20-s-Takt der Hülle, für dieses Fenster gesperrt).
/// Der Stand ist die Datei genau so, wie das Programm sie selbst schreibt (DOCX, XLSX, PPTX, .tcad, Szene-JSON) – es geht nichts verloren, was die Datei kann.
/// Der frühere eigene Weg (feste Dateien in <c>wiederherstellen/</c>) ist entfernt; dort liegende Sicherungen älterer Trendo-Fassungen werden beim ersten Anzeigen
/// in Sicherungen der Hülle umgewandelt und angeboten.
/// Rückgängig bleibt das eigene der Programme (bis 100 Schritte; Begründung im Bericht T-0714): Es arbeitet mit Kopien der Dokument-Objekte bzw. bei 3D mit einer
/// 150-MB-Grenze und fasst Tippen zusammen; ein Umweg über JSON-Schnappschüsse der ganzen Datei wäre langsamer und brächte nichts dazu.</summary>
public partial class MainWindow
{
    static readonly string[] OfficeKinds = ["text", "tabelle", "praesentation", "cad", "3d"];
    readonly Dictionary<string, ProgramLink> _officeLinks = [];

    /// <summary>Studio-Kennung der Notsicherung je Programm (beginnt mit „LINK-“, nicht mit einem Studio der Hülle verwechselbar).</summary>
    static string OfficeStudio(string kind) => "LINK-" + kind.ToUpperInvariant();
    /// <summary>Dateiname des Stands – derselbe wie früher in <c>wiederherstellen/</c>.</summary>
    static string OfficeFileName(string kind) => kind switch { "text" => "text.docx", "tabelle" => "tabelle.xlsx", "3d" => "3d-szene.json", "cad" => "cad.tcad", _ => "praesentation.pptx" };
    static (string One, string Many) OfficeWords(string kind) => kind switch
    {
        "text" => ("Ein Text-Dokument", "Text-Dokumente"), "tabelle" => ("Eine Tabelle", "Tabellen"), "cad" => ("Ein Bauteil", "Bauteile"),
        "3d" => ("Eine 3D-Szene", "3D-Szenen"), _ => ("Eine Präsentation", "Präsentationen"),
    };

    /// <summary>Anschluss eines der fünf Programme an die Notsicherung der Hülle (für Tests öffentlich).</summary>
    public ProgramLink OfficeLink(string kind)
    {
        if (!OfficeKinds.Contains(kind)) throw new ArgumentException("Unbekanntes Programm: " + kind, nameof(kind));
        return _officeLinks.TryGetValue(kind, out var l) ? l : _officeLinks[kind] = new ProgramLink(OfficeStudio(kind), kind, null, OfficeTitle(kind)) { Locks = ProgLocks };
    }

    bool OfficeDirty(string kind) => kind switch
    {
        "text" => TextDirty, "tabelle" => SheetDirty, "praesentation" => DeckDirty, "cad" => CadDirty, "3d" => Scene3DDirty && _scene.Parts.Count > 0, _ => false,
    };

    string OfficeTitle(string kind)
    {
        static string N(string? p) => p is null ? "" : Path.GetFileName(p);
        var t = kind switch
        {
            "text" => N(TextPath) is { Length: > 0 } a ? a : (TextDocument.Blocks.OfType<Para>().Select(p => p.Text.Trim()).FirstOrDefault(x => x.Length > 0) ?? ""),
            "tabelle" => N(SheetPath), "praesentation" => N(DeckPath), "cad" => N(CadPath), "3d" => _scene.Name, _ => "",
        };
        if (t.Length == 0) t = kind switch { "text" => "Neues Text-Dokument", "tabelle" => "Neue Tabelle", "cad" => "Neues Bauteil", "3d" => "3D-Szene", _ => "Neue Präsentation" };
        return t.Length > 60 ? t[..60] + "…" : t;
    }

    string OfficeScratch => Path.Combine(ProgBackup, ".arbeit");

    /// <summary>Den Stand genau so, wie das Programm ihn speichert (Office-Formate über eine Zwischendatei im Datenordner).</summary>
    System.Text.Json.Nodes.JsonObject OfficeState(string kind)
    {
        var name = OfficeFileName(kind);
        return kind switch
        {
            "3d" => ProgramLink.FileState(name, System.Text.Encoding.UTF8.GetBytes(_scene.ToJson())),
            "cad" => ProgramLink.FileState(name, System.Text.Encoding.UTF8.GetBytes(Cad.ToJson())),
            "text" => ProgramLink.FileStateVia(name, f => TextFiles.Save(TextDocument, f), OfficeScratch),
            "tabelle" => ProgramLink.FileStateVia(name, f => SheetFiles.Save(SheetDocument, f), OfficeScratch),
            _ => ProgramLink.FileStateVia(name, f => SlideFiles.Save(Deck, f), OfficeScratch),
        };
    }

    /// <summary>Takt der Hülle (aus <see cref="LinksAutosave"/>): ungespeicherte Dokumente der fünf Programme sichern.</summary>
    void OfficeLinksAutosave()
    {
        foreach (var kind in OfficeKinds)
        {
            if (!OfficeDirty(kind)) continue;
            try { OfficeAutosaveKind(kind); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { }
        }
    }

    string? OfficeAutosaveKind(string kind)
    {
        var l = OfficeLink(kind);
        l.Track(OfficeState(kind), OfficeTitle(kind));
        var f = l.Autosave(ProgBackup);
        l.Track(null); // den Inhalt nicht bis zum nächsten Takt im Speicher halten (bei 3D bis zu einige hundert MB)
        return f;
    }

    /// <summary>Nach dem Speichern im eigenen Format: Notsicherung überflüssig.</summary>
    void OfficeSaved(string kind) { try { OfficeLink(kind).Saved(ProgBackup); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

    /// <summary>Für Tests: Liegt die Notsicherung des offenen Dokuments (dieses Fensters) gerade vor?</summary>
    public bool OfficeBackupPending(string kind) => File.Exists(OfficeLink(kind).Doc.BackupFile(ProgBackup));

    /// <summary>Nicht gespeicherte Stände aus einer früheren Sitzung oder einem Absturz (neueste zuerst), ohne die dieses Fensters und ohne die eines anderen laufenden Fensters.</summary>
    public List<(string File, string Title, DateTime When)> OfficeRecoveries(string kind)
    {
        OfficeMigrateOld();
        return OfficeLink(kind).Pending(ProgBackup);
    }

    /// <summary>T-0714: Sicherungen der früheren Notsicherung (<c>wiederherstellen/</c>, Trendo vor T-0714) einmal in Sicherungen der Hülle umwandeln.
    /// Die Datei wird dabei erst umbenannt (ein zweites Fenster nimmt sie dann nicht noch einmal), Inhalt unverändert übernommen, Zeitpunkt beibehalten.
    /// Danach ist der Ordner weg – jeder weitere Aufruf prüft nur noch, ob es ihn gibt.</summary>
    public int OfficeMigrateOld()
    {
        var dir = Path.Combine(_core.DataRoot, "wiederherstellen"); var n = 0;
        if (!Directory.Exists(dir)) return 0;
        foreach (var kind in OfficeKinds)
        {
            var old = Path.Combine(dir, OfficeFileName(kind));
            OfficeReclaimMigration(old);
            if (!File.Exists(old)) continue;
            var claim = old + ".uebernahme-" + Environment.ProcessId;
            try
            {
                var when = File.GetLastWriteTime(old);
                File.Move(old, claim);
                var bytes = File.ReadAllBytes(claim);
                var link = new ProgramLink(OfficeStudio(kind), kind, null, OfficeWords(kind).One + " (ältere Sicherung)"); // ohne Sperre: wird in jedem Fenster angeboten
                link.Track(ProgramLink.FileState(OfficeFileName(kind), bytes));
                if (link.Autosave(ProgBackup) is { } f) { File.SetLastWriteTime(f, when); File.Delete(claim); n++; }
                _core.Audit.Append(Principal.LocalOwner.Name, "office.recover.migrate", kind);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        try { if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return n;
    }

    /// <summary>T-0730: Eine abgebrochene Umwandlung (Absturz oder Fehler zwischen Umbenennen und Sichern) hinterließ „&lt;Datei&gt;.uebernahme-&lt;PID&gt;“,
    /// die nie wieder angeboten wurde. Gehört die Übernahme diesem Fenster oder einem nicht mehr laufenden Prozess, bekommt die Datei ihren alten Namen
    /// zurück und wird gleich wieder umgewandelt. Liegt schon wieder eine Datei unter dem alten Namen, wartet die Übernahme bis zum nächsten Mal.</summary>
    static void OfficeReclaimMigration(string old)
    {
        string[] claims;
        try { claims = Directory.GetFiles(Path.GetDirectoryName(old)!, Path.GetFileName(old) + ".uebernahme-*"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
        foreach (var claim in claims.Order(StringComparer.Ordinal))
        {
            if (!int.TryParse(claim[(claim.LastIndexOf('-') + 1)..], out var pid)) continue;
            if (pid != Environment.ProcessId && ProcessAlive(pid)) continue; // ein anderes Fenster wandelt gerade um
            if (File.Exists(old)) return;
            try { File.Move(claim, old); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    static bool ProcessAlive(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; } // kein Prozess mit dieser Nummer
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; } // nicht prüfbar → vorsichtshalber als laufend werten
    }

    /// <summary>Gesicherten Stand übernehmen (nicht als gespeichert markiert – bitte speichern) bzw. verwerfen. Ist das offene Dokument ungespeichert,
    /// wird es vorher gesichert und bleibt zum Zurückholen liegen.</summary>
    public void OfficeRecover(string kind, int index, bool restore)
    {
        var all = OfficeRecoveries(kind); if (index < 0 || index >= all.Count) return;
        var file = all[index].File;
        if (!restore) { ProgramLink.Discard(file, ProgLocks); return; }
        // T-0730: ERST den Inhalt lesen und mit dem Programm öffnen, DANN die Sicherung übernehmen. Vorher übernahm der Anschluss die Sicherung
        // (Kennung, „geändert“, Sperre) schon vor dem Öffnen – scheiterte das Öffnen (kaputtes Base64, neuere Fassung, beschädigte DOCX …),
        // war die Sicherung aus dem Angebot verschwunden, der nächste 20-s-Takt überschrieb sie und das nächste Speichern löschte sie.
        // Jetzt bleiben bei einem Fehler Anschluss und Sicherung unberührt, die Karte bietet sie weiter an.
        var (name, bytes) = ProgramLink.FileOf(OfficeBackupState(kind, file));
        Directory.CreateDirectory(OfficeScratch);
        var tmp = Path.Combine(OfficeScratch, Guid.NewGuid().ToString("N")[..10] + "-" + (Path.GetExtension(name).Length > 0 ? name : OfficeFileName(kind)));
        try
        {
            File.WriteAllBytes(tmp, bytes);
            object loaded;
            try
            {
                loaded = kind switch
                {
                    "text" => (object)TextFiles.Open(tmp), "tabelle" => SheetFiles.Open(tmp), "3d" => Scene3D.Load(tmp),
                    "cad" => Trendo.Core.Cad.CadDoc.Load(tmp), _ => SlideFiles.Open(tmp),
                };
            }
            catch (Exception e) when (e is not (InvalidDataException or IOException or UnauthorizedAccessException or OutOfMemoryException or OperationCanceledException))
            { throw new InvalidDataException("Die Sicherung lässt sich nicht öffnen – sie bleibt erhalten.", e); } // z. B. Fehler im Dateiaufbau einer DOCX/XLSX
            // geöffnet – jetzt erst übernehmen
            if (OfficeDirty(kind)) OfficeAutosaveKind(kind); // bleibt laut ProgramLink.Restore liegen und wird angeboten
            OfficeLink(kind).Restore(file, ProgBackup);
            OfficeLink(kind).Track(null);
            switch (kind)
            {
                case "text": TextDocument = (TextDoc)loaded; TextPath = null; TextDirty = true; TextFocusBlock = 0; _txSel = _txSelLen = 0; _txUndo.Clear(); _txRedo.Clear(); _txMsg = "Wiederhergestellt – bitte speichern."; ImportGuardClear("text"); break;
                case "tabelle": SheetDocument = (SheetDoc)loaded; SheetIndex = 0; ResetSheetState(); SheetPath = null; SheetDirty = true; _shMsg = "Wiederhergestellt – bitte speichern."; ImportGuardClear("tabelle"); break;
                case "3d": Restore3D((Scene3D)loaded); break;
                case "cad": CadRestore(tmp); break; // derselbe, eben geprüfte Inhalt
                default: Deck = (SlideDoc)loaded; ResetDeckState(); DeckPath = null; DeckDirty = true; _dkMsg = "Wiederhergestellt – bitte speichern."; ImportGuardClear("praesentation"); break;
            }
        }
        finally { try { File.Delete(tmp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        _core.Audit.Append(Principal.LocalOwner.Name, "office.recover", kind);
    }

    /// <summary>T-0730: Stand einer Notsicherung lesen und prüfen, ohne den Anschluss anzufassen (gleiche Prüfung wie <see cref="ProgramLink.Restore"/>).</summary>
    static System.Text.Json.Nodes.JsonNode? OfficeBackupState(string kind, string file)
    {
        var d = ProgramDocument.Load(file);
        if (!d.Studio.Equals(OfficeStudio(kind), StringComparison.OrdinalIgnoreCase) || d.Data["format"].AsText() != kind) throw new InvalidDataException("Diese Sicherung gehört zu einem anderen Programm.");
        return d.Data["inhalt"];
    }

    void OfficeSay(string kind, string msg)
    {
        switch (kind)
        {
            case "text": _txMsg = msg; break;
            case "tabelle": _shMsg = msg; break;
            case "cad": _cadMsg = msg; break;
            case "3d": Say(msg, "3d"); break;
            default: _dkMsg = msg; break;
        }
    }

    /// <summary>Wiederherstellen-Karte, solange nichts Neues ungespeichert ist (Knöpfe „office-restore-&lt;Programm&gt;-&lt;n&gt;“ / „office-discard-…“).</summary>
    Control? RecoveryBanner(string kind, bool dirty)
    {
        if (dirty) return null;
        List<(string File, string Title, DateTime When)> all;
        try { all = OfficeRecoveries(kind); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        var (one, many) = OfficeWords(kind);
        return RecoveryCardView($"office-recovery-{kind}", (k, yes) => $"office-{(yes ? "restore" : "discard")}-{kind}-{k}", one, many, all, (k, yes) =>
        {
            try { OfficeRecover(kind, k, yes); if (!yes) OfficeSay(kind, "Sicherung verworfen."); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { OfficeSay(kind, "Zurückholen ging nicht: " + FriendlyError.Say(ex)); }
            Show(kind);
        });
    }

    /// <summary>Hinweise über eine Programmseite legen, ohne deren Aufbau zu ändern: Wiederherstellung (T-0254) und fremde Inhalte (T-0682).</summary>
    Control WithRecovery(string kind, bool dirty, Control page)
    {
        var banners = new[] { RecoveryBanner(kind, dirty), ImportBanner(kind) }.OfType<Control>().ToList();
        if (banners.Count == 0) return page;
        var dock = new DockPanel();
        foreach (var b in banners) dock.Children.Add(new ContentControl { Content = b, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top });
        dock.Children.Add(page);
        return dock;
    }
}
