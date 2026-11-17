using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0718 (Audit C4): Website veröffentlichen nur über das Freigabe-Tor. Ohne Freigabe verweigert der Kern und es entsteht keine Datei;
/// mit Freigabe genau einmal, für genau diesen Ort und genau diese Dateien; alles im Protokoll. Danach Link- und Dateiprüfung mit ehrlicher Rückmeldung.</summary>
public class T0718WebVeroeffentlichen
{
    /// <summary>Kleine echte Website: Startseite, Unterseite als Ordner-Link, Stil, Bild (PNG-Kopf), Bild per CSS.</summary>
    internal static string Site(string root)
    {
        var d = Path.Combine(root, "veroeffentlichen");
        Directory.CreateDirectory(Path.Combine(d, "bilder")); Directory.CreateDirectory(Path.Combine(d, "ueber"));
        File.WriteAllText(Path.Combine(d, "index.html"), """
            <!doctype html><html lang="de"><head><meta charset="utf-8"><title>Bäckerei Muster</title><link rel="stylesheet" href="stil.css"></head>
            <body><h1>Frisches Brot</h1><img src="bilder/logo.png" alt="Logo" srcset="bilder/logo.png 1x"><a href="ueber/">Über uns</a> <a href="#kontakt">Kontakt</a>
            <a href="https://example.org/">Netz</a> <a href="mailto:info@example.org">Mail</a><p id="kontakt">Ruf an</p></body></html>
            """);
        File.WriteAllText(Path.Combine(d, "ueber", "index.html"), """<!doctype html><html lang="de"><head><meta charset="utf-8"><title>Über uns</title><link rel="stylesheet" href="../stil.css"></head><body><a href="../index.html">Start</a></body></html>""");
        File.WriteAllText(Path.Combine(d, "stil.css"), "body{background:url('bilder/logo.png') no-repeat}");
        File.WriteAllBytes(Path.Combine(d, "bilder", "logo.png"), [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]);
        return d;
    }

    static string Frei(Fixture f, string action, string target, Principal? by = null)
    {
        var r = f.Core.Approvals.Request(by ?? Principal.LocalOwner, action, "Test", target);
        f.Core.Approvals.Approve(r.Id, Principal.LocalOwner);
        return r.Id;
    }

    [Fact(DisplayName = "T-0718 Ordner: ohne Freigabe, offen, anderes Ziel, andere Aktion oder zweites Mal verweigert der Kern (keine Datei) – mit Freigabe genau einmal, Prüfung grün, im Protokoll")]
    public void Ordner_nur_mit_Freigabe_genau_einmal()
    {
        using var f = new Fixture();
        var src = Site(f.Root); var dest = Path.Combine(f.Root, "ziel");
        var plan = WebDesignPublish.PrepareFolder(src);
        Assert.True(plan.Ok, string.Join("; ", plan.Blockers));
        Assert.Equal(4, plan.Files.Count);
        var target = WebPublishing.Target(dest, plan.Fingerprint);

        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, "gibt-es-nicht", src, plan, dest));
        var offen = f.Core.Approvals.Request(Principal.LocalOwner, WebPublishing.ActionOrdner, "offen", target);
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, offen.Id, src, plan, dest));
        var anderesZiel = Frei(f, WebPublishing.ActionOrdner, WebPublishing.Target(Path.Combine(f.Root, "anders"), plan.Fingerprint));
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, anderesZiel, src, plan, dest));
        var andereAktion = Frei(f, WebPublishing.ActionNetlify, target);
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, andereAktion, src, plan, dest));
        var andereDateien = Frei(f, WebPublishing.ActionOrdner, WebPublishing.Target(dest, "0000"));
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, andereDateien, src, plan, dest));
        Assert.False(Directory.Exists(dest)); // nichts geschrieben

        var id = Frei(f, WebPublishing.ActionOrdner, target);
        var r = WebPublishing.ToFolder(f.Core.Approvals, id, src, plan, dest);
        Assert.True(r.Ok, r.Text);
        Assert.Equal(4, r.Dateien); Assert.Equal(dest, r.Wo);
        Assert.Equal(6, r.Geprueft); // href/src/srcset/url() der beiden Seiten und des Stils (gleiche Verweise je Datei einmal)
        Assert.Contains("Veröffentlicht in " + dest, r.Text); Assert.Contains("alles gefunden", r.Text);
        foreach (var file in plan.Files) Assert.Equal(File.ReadAllBytes(Path.Combine(src, file.Rel)), File.ReadAllBytes(Path.Combine(dest, file.Rel)));
        Assert.True(File.Exists(Path.Combine(dest, WebPublishing.Marker)));
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, id, src, plan, dest)); // verbraucht

        var log = f.Core.Audit.ReadAll();
        Assert.Single(log, a => a.Action == "execution.start" && a.Detail == $"{id} {WebPublishing.ActionOrdner} {target}");
        Assert.Contains(log, a => a.Action == "execution.done" && a.Detail == id);
        Assert.Equal(6, log.Count(a => a.Action == "execution.blocked"));
        Assert.True(f.Core.Audit.Verify());
    }

    [Fact(DisplayName = "T-0718 Ordner: Export nach der Freigabe geändert → nichts veröffentlicht, Meldung mit „Was tun“; fremder Ordner wird nie überschrieben; neue Veröffentlichung räumt nur eigene alte Dateien weg")]
    public void Ordner_Aenderung_fremde_Dateien_und_Aufraeumen()
    {
        using var f = new Fixture();
        var src = Site(f.Root); var dest = Path.Combine(f.Root, "ziel");
        var plan = WebDesignPublish.PrepareFolder(src);
        var id = Frei(f, WebPublishing.ActionOrdner, WebPublishing.Target(dest, plan.Fingerprint));
        File.AppendAllText(Path.Combine(src, "stil.css"), "h1{color:gold}");
        var r = WebPublishing.ToFolder(f.Core.Approvals, id, src, plan, dest);
        Assert.False(r.Ok); Assert.Equal(0, r.Dateien);
        Assert.Contains("geändert", r.Text); Assert.Contains("Was tun:", r.Text);
        Assert.False(File.Exists(Path.Combine(dest, "index.html")));

        // fremder, nicht leerer Ordner: schon vor der Rückfrage abgelehnt
        var fremd = Path.Combine(f.Root, "fremd"); Directory.CreateDirectory(fremd); File.WriteAllText(Path.Combine(fremd, "wichtig.txt"), "x");
        var fe = WebPublishing.FolderError(src, fremd);
        Assert.NotNull(fe); Assert.Contains("nicht leer", fe!.Text); Assert.Contains("Was tun:", fe.Text);
        Assert.NotNull(WebPublishing.FolderError(src, Path.Combine(src, "unter"))); // nicht in den Export selbst
        Assert.Null(WebPublishing.FolderError(src, dest));

        // erste Veröffentlichung, dann eine Datei weniger: die alte eigene Datei verschwindet, fremde Dateien im Ziel bleiben
        plan = WebDesignPublish.PrepareFolder(src);
        Assert.True(WebPublishing.ToFolder(f.Core.Approvals, Frei(f, WebPublishing.ActionOrdner, WebPublishing.Target(dest, plan.Fingerprint)), src, plan, dest).Ok);
        File.WriteAllText(Path.Combine(dest, "eigene-notiz.txt"), "bleibt");
        Assert.Null(WebPublishing.FolderError(src, dest)); // Ordner mit Merkzettel darf erneut veröffentlicht werden
        Directory.Delete(Path.Combine(src, "ueber"), true);
        File.WriteAllText(Path.Combine(src, "index.html"), File.ReadAllText(Path.Combine(src, "index.html")).Replace("<a href=\"ueber/\">Über uns</a>", ""));
        plan = WebDesignPublish.PrepareFolder(src);
        var r2 = WebPublishing.ToFolder(f.Core.Approvals, Frei(f, WebPublishing.ActionOrdner, WebPublishing.Target(dest, plan.Fingerprint)), src, plan, dest);
        Assert.True(r2.Ok, r2.Text);
        Assert.False(File.Exists(Path.Combine(dest, "ueber", "index.html")));
        Assert.Equal("bleibt", File.ReadAllText(Path.Combine(dest, "eigene-notiz.txt")));
    }

    [Fact(DisplayName = "T-0718 Linkprüfung: fehlendes Bild, fehlende Unterseite und Verweis aus der Website heraus werden gemeldet; Netz, Anker und Mail nicht")]
    public void Linkpruefung_findet_Fehler()
    {
        using var f = new Fixture();
        var src = Site(f.Root);
        Assert.Empty(WebPublishing.CheckLinks(src).Befunde);
        File.Delete(Path.Combine(src, "bilder", "logo.png"));
        Directory.Delete(Path.Combine(src, "ueber"), true);
        File.AppendAllText(Path.Combine(src, "index.html"), "<a href=\"../../geheim.txt\">raus</a>");
        var (n, befunde) = WebPublishing.CheckLinks(src);
        Assert.True(n > 0);
        Assert.Contains(befunde, b => b.Contains("bilder/logo.png") && b.StartsWith("index.html"));
        Assert.Contains(befunde, b => b.Contains("stil.css") && b.Contains("bilder/logo.png"));
        Assert.Contains(befunde, b => b.Contains("„ueber/“"));
        Assert.Contains(befunde, b => b.Contains("aus der Website heraus"));
        Assert.DoesNotContain(befunde, b => b.Contains("example.org") || b.Contains("#kontakt"));
    }

    [Fact(DisplayName = "T-0718 ZIP: nur mit Freigabe, genau einmal; Paket enthält genau die freigegebenen Dateien (wieder geöffnet und verglichen)")]
    public void Zip_nur_mit_Freigabe()
    {
        using var f = new Fixture();
        var src = Site(f.Root); var zipDir = Path.Combine(f.Root, "pakete");
        var plan = WebDesignPublish.PrepareFolder(src);
        Assert.Throws<ApprovalException>(() => WebPublishing.ToZip(f.Core.Approvals, "nein", src, plan, zipDir));
        Assert.False(Directory.Exists(zipDir));
        var id = Frei(f, WebPublishing.ActionZip, WebPublishing.Target(zipDir, plan.Fingerprint));
        var r = WebPublishing.ToZip(f.Core.Approvals, id, src, plan, zipDir);
        Assert.True(r.Ok, r.Text);
        Assert.Contains("Es wurde nichts hochgeladen", r.Text);
        using (var z = ZipFile.OpenRead(r.Wo))
            Assert.Equal(plan.Files.Select(x => x.Rel).Order(), z.Entries.Select(e => e.FullName).Order());
        Assert.Throws<ApprovalException>(() => WebPublishing.ToZip(f.Core.Approvals, id, src, plan, zipDir));
        Assert.Single(Directory.GetFiles(zipDir, "*.zip"));
    }

    [Fact(DisplayName = "T-0718 Kinderprofil: darf eine Veröffentlichung anfragen, aber nie freigeben – nichts wird geschrieben")]
    public void Kinderprofil_veroeffentlicht_nicht()
    {
        using var f = new Fixture();
        var src = Site(f.Root); var dest = Path.Combine(f.Root, "ziel");
        var plan = WebDesignPublish.PrepareFolder(src);
        var kind = Principal.ForProfile(true);
        var r = f.Core.Approvals.Request(kind, WebPublishing.ActionOrdner, "Website", WebPublishing.Target(dest, plan.Fingerprint));
        Assert.Throws<UnauthorizedAccessException>(() => f.Core.Approvals.Approve(r.Id, kind));
        Assert.Throws<ApprovalException>(() => WebPublishing.ToFolder(f.Core.Approvals, r.Id, src, plan, dest));
        Assert.False(Directory.Exists(dest));
    }

    [Fact(DisplayName = "T-0718 Rückfrage: nennt Wirkung, Ziel, Dateien und Größe; Vorschau listet jede Datei; Ziel der Freigabe enthält den Fingerabdruck")]
    public void Rueckfrage_nennt_alles()
    {
        using var f = new Fixture();
        var src = Site(f.Root);
        var plan = WebDesignPublish.PrepareFolder(src);
        foreach (var weg in Enum.GetValues<WebPublishing.Weg>())
        {
            var q = WebPublishing.Describe(weg, "https://mein-server.de/site/", plan);
            Assert.Equal(WebPublishing.ActionOf(weg), q.Action);
            Assert.EndsWith("#" + plan.Fingerprint, q.Target);
            foreach (var t in new[] { "Wirkung:", "Ziel: https://mein-server.de/site/", "Dateien: 4", "Größe:" }) Assert.Contains(t, q.Text);
            foreach (var x in plan.Files) Assert.Contains(x.Rel, q.Preview);
        }
        Assert.Contains("öffentlich", WebPublishing.Describe(WebPublishing.Weg.Netlify, "neu", plan).Text);
        Assert.Contains("Heimnetz", WebPublishing.Describe(WebPublishing.Weg.Server, "http://192.168.1.20/site/", plan).Text);
        Assert.Contains("im Internet", WebPublishing.Describe(WebPublishing.Weg.Server, "https://mein-server.de/site/", plan).Text);
    }

    [Fact(DisplayName = "T-0718 Server-Adresse: https überall, http nur auf diesem PC/im Heimnetz, keine Anmeldung in der Adresse; Ordnerliste und Fehlertexte mit „Was tun“")]
    public void Server_adresse_und_meldungen()
    {
        Assert.Equal("https://mein-server.de/dav/site/", WebPublishing.ServerAddress("https://mein-server.de/dav/site").Url!.ToString());
        Assert.NotNull(WebPublishing.ServerAddress("http://127.0.0.1:8080/site/").Url);
        Assert.NotNull(WebPublishing.ServerAddress("http://192.168.1.20/site/").Url);
        Assert.Contains("Heimnetz", WebPublishing.ServerAddress("http://mein-server.de/site/").Error);
        Assert.Contains("Benutzername", WebPublishing.ServerAddress("https://nutzer:geheim@mein-server.de/").Error);
        Assert.NotNull(WebPublishing.ServerAddress("ftp://mein-server.de/").Error);
        Assert.NotNull(WebPublishing.ServerAddress("").Error);
        Assert.Equal(["bilder/", "ueber/", "a/", "a/b/"], WebPublishing.ServerFolders(["index.html", "bilder/logo.png", "ueber/index.html", "a/b/c.css"]));
        Assert.Equal("https://x.de/site/ueber%20uns/index.html", WebPublishing.ServerUrl(new Uri("https://x.de/site/"), "ueber uns/index.html").AbsoluteUri);
        Assert.Contains("Passwort", WebPublishing.ServerFehler(401).Meldung);
        Assert.Contains("WebDAV", WebPublishing.ServerFehler(405).WasTun);
    }
}
