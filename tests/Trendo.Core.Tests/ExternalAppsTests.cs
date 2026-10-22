using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class T0203_ExternalApps
{
    static string Tmp(string name) { var d = Path.Combine(Path.GetTempPath(), "t0203-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); var f = Path.Combine(d, name); File.WriteAllText(f, "x"); return f; }

    [Fact(DisplayName = "T-0203: Programme bekommen die Datei bzw. das Projekt als Argument")]
    public void PlanPassesFile()
    {
        var blend = Tmp("m.blend"); Assert.Equal([blend], ExternalApps.Plan(ExternalApps.Get("blender"), blend));
        var stl = Tmp("teil.stl"); var b = ExternalApps.Plan(ExternalApps.Get("blender"), stl);
        Assert.Equal("--python-expr", b[0]); Assert.Contains("stl_import", b[1]); Assert.Contains("teil.stl", b[1]);
        var up = Tmp("Spiel.uproject"); Assert.Equal([up], ExternalApps.Plan(ExternalApps.Get("unreal"), up));
        var dir = Path.GetDirectoryName(up)!; Assert.Equal([dir], ExternalApps.Plan(ExternalApps.Get("vscode"), dir));
        var docx = Tmp("brief.docx"); Assert.Equal([docx], ExternalApps.Plan(ExternalApps.Get("libreoffice"), docx));
    }

    [Fact(DisplayName = "T-0203: OBS lädt die Szenensammlung per Name, die Datei liegt danach im OBS-Ordner")]
    public void Obs()
    {
        var f = Tmp("Trendo Show.json"); File.WriteAllText(f, ObsScenes.Collection("Trendo Show", ["Begrüßung", "Interview"]));
        var scenes = Path.Combine(Path.GetDirectoryName(f)!, "obs-scenes");
        Assert.Equal(["--collection", "Trendo Show"], ExternalApps.Plan(ExternalApps.Get("obs"), f, scenes));
        Assert.True(File.Exists(Path.Combine(scenes, "Trendo Show.json")));
        Assert.Contains("\"Interview\"", File.ReadAllText(f));
        Assert.Throws<InvalidOperationException>(() => ObsScenes.Collection("x", []));
    }

    [Fact(DisplayName = "T-0203: klare Meldung ohne Datei, bei fehlender Datei und falschem Typ")]
    public void Errors()
    {
        Assert.Contains("zuerst", Assert.Throws<ArgumentException>(() => ExternalApps.Plan(ExternalApps.Get("libreoffice"), "")).Message);
        Assert.Throws<IOException>(() => ExternalApps.Plan(ExternalApps.Get("libreoffice"), "/gibt/es/nicht.docx"));
        Assert.Throws<ArgumentException>(() => ExternalApps.Plan(ExternalApps.Get("unreal"), Tmp("a.txt")));
    }

    [Fact(DisplayName = "T-0203: jedes externe Programm ist in der Programmprüfung (Abhängigkeitskatalog) bekannt")]
    public void InCatalog()
    {
        foreach (var a in ExternalApps.All) Assert.Contains(DependencyCatalog.All, c => c.Id == a.ComponentId);
    }
}
