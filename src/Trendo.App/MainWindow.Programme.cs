using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

public partial class MainWindow
{
    /// <summary>Läuft, während „Ja, einrichten“ das Programm prüft oder installiert. Tests können darauf warten.</summary>
    public Task? LaufendeEinrichtung { get; private set; }

    /// <summary>Pfad eines erkannten Programms: gespeicherte Einrichtung, bekannte Orte, PATH oder (bei Unreal) der Editor.</summary>
    public string? ProgrammPfad(string id)
    {
        if (ToolPath(id) is { } gespeichert && File.Exists(gespeichert)) return gespeichert;
        foreach (var name in Suchnamen(id))
            if (Cli.Find(name) is { } gefunden) return gefunden;
        if (id == "unreal")
            foreach (var e in EngineFinder())
            {
                var exe = GameStudio.EditorExe(e);
                if (File.Exists(exe)) return exe;
            }
        return null;
    }

    private static string[] Suchnamen(string id) => id switch
    {
        "vscode" => ["code", "code.cmd", "Code"],
        "libreoffice" => ["soffice", "libreoffice"],
        "obs" => ["obs", "obs64"],
        "unreal" => ["UnrealEditor"],
        "blender" => ["blender"],
        "mpv" => ["mpv"],
        _ => [id],
    };

    private string? AktuelleModellDatei() => NeuesteDatei(Folder3D, Studio3D.Imports, false);
    private string? AktuellesUnrealProjekt() => NeuesteDatei(GameFolder, [".uproject"], true);
    private string? AktuellesDevProjekt()
    {
        if (_devSelected is not null && Directory.Exists(_devSelected)) return _devSelected;
        if (!Directory.Exists(DevFolder)) return null;
        return Directory.GetDirectories(DevFolder).OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault();
    }
    private string? AktuellesDokument()
    {
        if (_finalFile is not null && File.Exists(_finalFile)) return _finalFile;
        return FileShelf.Recent([ProjectsRoot], FileShelf.Office, 1).FirstOrDefault()?.FullName;
    }
    private string? AktuelleSendung()
    {
        var dir = Fach.StoreDir(_core.DataRoot, "sendung");
        var csv = Path.Combine(dir, "ablaufplan.csv");
        return File.Exists(csv) ? csv : dir;
    }
    private string? DateiFuerProgramm(string id) => id switch
    {
        "blender" => AktuelleModellDatei(),
        "unreal" => AktuellesUnrealProjekt(),
        "vscode" => AktuellesDevProjekt(),
        "libreoffice" => AktuellesDokument(),
        "obs" => AktuelleSendung(),
        "mpv" => _mediaFile,
        _ => null,
    };

    private static string? NeuesteDatei(string wurzel, IEnumerable<string> endungen, bool auchUnterordner)
    {
        if (!Directory.Exists(wurzel)) return null;
        var end = new HashSet<string>(endungen, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> dateien = auchUnterordner
            ? Directory.EnumerateFiles(wurzel, "*.*", SearchOption.AllDirectories)
            : Directory.EnumerateFiles(wurzel);
        return dateien.Where(f => end.Contains(Path.GetExtension(f))).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>Zeile mit „In … öffnen“ oder „Einrichten“. Die Rückfrage installiert nichts, bevor „Ja, einrichten“ gewählt ist.</summary>
    private Control ProgrammLeiste(string id, string anzeige, Func<string?> datei, string? einrichtenName = null)
    {
        var zeile = new StackPanel { Spacing = 6 };
        if (_programmFrage == id)
        {
            zeile.Children.Add(P($"{anzeige} fehlt. Jetzt einrichten? Es wird nichts installiert, bevor du zustimmst.", Text));
            zeile.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    Act("prog-yes-" + id, "Ja, einrichten", () => EinrichtenBestaetigt(id), true),
                    Act("prog-no-" + id, "Später", () =>
                    {
                        _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", id);
                        _programmFrage = null;
                        Show(CurrentView);
                    }),
                },
            });
            return zeile;
        }
        if (ProgrammPfad(id) is null)
        {
            zeile.Children.Add(P($"{anzeige} ist nicht eingerichtet.", Brush.Parse("#FACC15")));
            zeile.Children.Add(Act(einrichtenName ?? "prog-setup-" + id, "Einrichten", () => { _programmFrage = id; Show(CurrentView); }));
        }
        else
        {
            zeile.Children.Add(P($"{anzeige} ist bereit.", Brush.Parse("#4ADE80")));
            zeile.Children.Add(Act("prog-open-" + id, "In " + anzeige + " öffnen", () => OeffneProgramm(id, datei())));
        }
        return zeile;
    }

    private void OeffneProgramm(string id, string? datei)
    {
        var exe = ProgrammPfad(id);
        if (exe is null) { _programmFrage = id; Show(CurrentView); return; }
        if (string.IsNullOrWhiteSpace(datei) || (!File.Exists(datei) && !Directory.Exists(datei)))
        {
            Say(ProgrammName(id) + " braucht zuerst eine Datei oder ein Projekt.", CurrentView);
            return;
        }
        try { _core.Integrations.OpenWithFile(Principal.LocalOwner, exe, datei, _core.Audit); }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException) { Say(ex.Message, CurrentView); }
    }

    private static string ProgrammName(string id) => id switch
    {
        "blender" => "Blender",
        "unreal" => "Unreal",
        "vscode" => "VS Code",
        "libreoffice" => "LibreOffice",
        "obs" => "OBS",
        "mpv" => "mpv",
        _ => id,
    };

    private Task EinrichtenBestaetigt(string id)
    {
        var lauf = EinrichtenKern(id);
        LaufendeEinrichtung = lauf;
        return lauf;
    }

    private async Task EinrichtenKern(string id)
    {
        var c = DependencyCatalog.Get(id);
        var check = await _deps.CheckAsync(c);
        if (!check.Ok)
        {
            if (c.Mode == DeliveryMode.Guided)
            {
                if (!_core.Offline && c.OfficialUrl is { } url)
                    _core.Integrations.LaunchDirect(Principal.LocalOwner, url, _core.Audit);
                check = await _deps.CheckAsync(c);
            }
            else if (c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated)
            {
                if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
                var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{c.Name} einrichten", c.Id);
                _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                await InstallOne(c, refresh: false);
                check = await _deps.CheckAsync(c);
            }
        }
        if (check.Ok && check.Path is { } pfad) _deps.Integrate(c.Id, pfad);
        _programmFrage = null;
        Show(CurrentView);
    }

    private Button ProgrammKnopf(string name, string id, string datei)
    {
        var da = ProgrammPfad(id) is not null;
        return Act(name, da ? "In " + ProgrammName(id) + " öffnen" : "Einrichten", () =>
        {
            if (ProgrammPfad(id) is null) { _programmFrage = id; Show(CurrentView); }
            else OeffneProgramm(id, datei);
        });
    }
}
