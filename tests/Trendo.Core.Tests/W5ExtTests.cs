using System.IO.Compression;
using System.Text.Json;
using Trendo.Core;
using Xunit;

public class W5_X04_Extensions
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-ext-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "X-04: Beispiel-Erweiterung installieren (Ordner und ZIP), ein/aus, Prüfsumme stabil, entfernen")]
    public void Install_cycle()
    {
        var src = ExtensionSamples.WriteSample(Tmp());
        var s = new ExtensionStore(Tmp());
        var (e, err) = s.Install(src); Assert.Empty(err); Assert.NotNull(e);
        Assert.True(e!.Enabled); var h = e.Sha256;
        s.SetEnabled(e.Manifest.Id, false); Assert.False(s.Get(e.Manifest.Id)!.Enabled); Assert.Equal(h, s.Get(e.Manifest.Id)!.Sha256);
        s.Remove(e.Manifest.Id); Assert.Empty(s.All());
        var zip = Path.Combine(Tmp(), "v.zip"); ZipFile.CreateFromDirectory(src, zip, CompressionLevel.Fastest, includeBaseDirectory: true);
        Assert.Empty(s.Install(zip).Errors); Assert.Single(s.All());
        Assert.Equal(4, SitePage.Load(Path.Combine(s.All()[0].Dir, "seite.json")).Blocks.Count);
    }

    [Fact(DisplayName = "X-04: Programmcode, falsche Rechte, kaputte Beschreibung und ZIP-Ausbruch werden abgewiesen")]
    public void Rejects()
    {
        var s = new ExtensionStore(Tmp());
        var a = ExtensionSamples.WriteSample(Tmp()); File.WriteAllText(Path.Combine(a, "boese.exe"), "MZ");
        Assert.Contains(s.Install(a).Errors, x => x.Contains("kein Programmcode"));
        var b = ExtensionSamples.WriteSample(Tmp()); var mf = Path.Combine(b, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("\"Permissions\": []", "\"Permissions\": [\"alles\"]").Replace("1.0.0", "eins"));
        var err = s.Install(b).Errors; Assert.Contains(err, x => x.Contains("Recht")); Assert.Contains(err, x => x.Contains("Version"));
        var c = ExtensionSamples.WriteSample(Tmp()); File.WriteAllText(Path.Combine(c, Extensions.ManifestFile), "{kaputt");
        Assert.Contains(s.Install(c).Errors, x => x.Contains("JSON"));
        var zip = Path.Combine(Tmp(), "evil.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { using var w = new StreamWriter(z.CreateEntry("../../evil.json").Open()); w.Write("{}"); }
        Assert.Contains(s.Install(zip).Errors, x => x.Contains("Unsicherer Pfad"));
        Assert.Empty(s.All());
    }

    [Fact(DisplayName = "T-0241: Mit ApprovalGate nur mit Freigabe installieren/entfernen; Not-Stopp sperrt; geänderte Datei nach Freigabe wird abgewiesen")]
    public void Approval_required()
    {
        var root = Tmp();
        var audit = new AuditLog(Path.Combine(root, "a.jsonl"));
        var gate = new ApprovalGate(audit);
        var s = new ExtensionStore(root, audit, gate);
        var src = ExtensionSamples.WriteSample(Tmp());
        var zip = Path.Combine(Tmp(), "v.zip"); ZipFile.CreateFromDirectory(src, zip, CompressionLevel.Fastest, includeBaseDirectory: true);
        string Approved(string action, string target) { var r = gate.Request(Principal.LocalOwner, action, "Test", target); gate.Approve(r.Id, Principal.LocalOwner); return r.Id; }

        Assert.Contains(s.Install(zip).Errors, x => x.Contains("Freigabe"));
        Assert.Empty(s.All());
        // Freigabe gilt für genau diese Datei: danach verändert → blockiert
        var id = Approved(ExtensionStore.InstallAction, ExtensionStore.SourceHash(zip));
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Update)) { using var w = new StreamWriter(z.CreateEntry("visitenkarte/extra.txt").Open()); w.Write("neu"); }
        Assert.Contains(s.Install(zip, id).Errors, x => x.Contains("blockiert"));
        Assert.Empty(s.All());
        // passende Freigabe → installiert, genau einmal verwendbar
        var ok = Approved(ExtensionStore.InstallAction, ExtensionStore.SourceHash(zip));
        Assert.Empty(s.Install(zip, ok).Errors);
        Assert.Single(s.All());
        Assert.Contains(s.Install(zip, ok).Errors, x => x.Contains("blockiert"));
        // Not-Stopp: weder Freigabe noch Ausführung
        var rm = Approved(ExtensionStore.RemoveAction, "trendo.visitenkarte");
        gate.Halt(Principal.LocalOwner, "Test");
        Assert.NotNull(s.Remove("trendo.visitenkarte", rm));
        Assert.Single(s.All());
        gate.Resume(Principal.LocalOwner);
        Assert.Null(s.Remove("trendo.visitenkarte", Approved(ExtensionStore.RemoveAction, "trendo.visitenkarte")));
        Assert.Empty(s.All());
    }

    [Fact(DisplayName = "T-0241: Doppelte Dateien, zu viele Einträge, stark gepackte Dateien und fremde Kennung werden abgewiesen; alte Fassung bleibt heil")]
    public void Hardening()
    {
        var s = new ExtensionStore(Tmp());
        var src = ExtensionSamples.WriteSample(Tmp());
        Assert.Empty(s.Install(src).Errors);
        var before = s.All()[0].Sha256;
        // doppelte Datei
        var dup = Path.Combine(Tmp(), "dup.zip");
        using (var z = ZipFile.Open(dup, ZipArchiveMode.Create)) { z.CreateEntryFromFile(Path.Combine(src, Extensions.ManifestFile), Extensions.ManifestFile); z.CreateEntryFromFile(Path.Combine(src, "seite.json"), "seite.json"); z.CreateEntryFromFile(Path.Combine(src, "seite.json"), "seite.json"); }
        Assert.Contains(s.Install(dup).Errors, x => x.Contains("Doppelte"));
        // zu viele Einträge
        var many = Path.Combine(Tmp(), "many.zip");
        using (var z = ZipFile.Open(many, ZipArchiveMode.Create)) for (var i = 0; i <= ExtensionStore.MaxEntries; i++) z.CreateEntry($"x/{i}.txt");
        Assert.Contains(s.Install(many).Errors, x => x.Contains("zu viele"));
        // stark gepackt (2 MB Nullen)
        var bomb = Path.Combine(Tmp(), "bomb.zip");
        using (var z = ZipFile.Open(bomb, ZipArchiveMode.Create)) { using var st = z.CreateEntry("nullen.txt", CompressionLevel.Optimal).Open(); st.Write(new byte[2 << 20]); }
        Assert.Contains(s.Install(bomb).Errors, x => x.Contains("stark gepackt"));
        // gleiche Kennung, anderer Herausgeber
        var other = ExtensionSamples.WriteSample(Tmp()); var mf = Path.Combine(other, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("\"Author\": \"Trendo\"", "\"Author\": \"Fremd\""));
        Assert.Contains(s.Install(other).Errors, x => x.Contains("derselben Kennung"));
        Assert.Equal(before, s.All()[0].Sha256);
        Assert.Empty(Directory.GetDirectories(s.Root, ".*")); // keine Reste vom Austausch
    }
}
