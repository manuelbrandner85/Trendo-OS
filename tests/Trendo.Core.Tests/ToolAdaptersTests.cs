using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 2: typst (Office), mediamtx (Live streamen), streamlink (TV, nur Öffentlich-Rechtliche). Programmläufe nachgebildet; echte Läufe: reports/alle-studios/runde2-werkzeuge-e2e.md.</summary>
public class ToolAdaptersTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "trendo-tools-test-" + Guid.NewGuid().ToString("N"));
    readonly string _exe;
    public ToolAdaptersTests() { Directory.CreateDirectory(_root); _exe = Path.Combine(_root, "werkzeug.exe"); File.WriteAllText(_exe, "x"); }
    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }
    static Cli.Result R(int code, string o = "", string e = "") => new(code, o, e);
    string Typ(string text = "= Hallo") { var f = Path.Combine(_root, "brief.typ"); File.WriteAllText(f, text); return f; }

    // ---------- typst ----------
    [Fact(DisplayName = "typst +1: Argumente mit --root (Projektgrenze), PNG nur Seite 1 mit Auflösung")]
    public void Typst_args()
    {
        Assert.Equal(["compile", "--root", _root, "a.typ", "a.pdf"], TypstDoc.Args("a.typ", "a.pdf", _root));
        Assert.Equal(["compile", "--root", _root, "a.typ", "a.png", "--ppi", "600", "--pages", "1"], TypstDoc.Args("a.typ", "a.png", _root, 5000));
    }

    [Fact(DisplayName = "typst +2: Übersetzen schreibt PDF, Programm bekommt genau die Argumente")]
    public void Typst_compile()
    {
        var input = Typ(); var output = Path.Combine(_root, "export", "brief.pdf"); IReadOnlyList<string>? seen = null;
        var (ok, msg) = TypstDoc.Compile(_exe, input, output, _root, run: (e, a, t) => { seen = a; File.WriteAllText(output, "%PDF"); return R(0); });
        Assert.True(ok, msg); Assert.Equal(output, msg); Assert.Equal(output, seen![4]);
    }

    [Fact(DisplayName = "typst +3: Brief mit Posten – Summe, deutsche Zahlen, Umlaute bleiben")]
    public void Typst_letter()
    {
        var src = TypstDoc.Letter("Rechnung 17", "Sehr geehrte Frau Muster,\n\nvielen Dank für Ihre Bestellung.", [(2, "Stühle", 59.5m), (1, "Tisch", 349m)]);
        Assert.Contains("= Rechnung 17", src); Assert.Contains("[Stühle]", src); Assert.Contains("119,00 €", src); Assert.Contains("Gesamt: 468,00 €", src);
        Assert.Contains("#set text(lang: \"de\"", src);
    }

    [Fact(DisplayName = "typst −1: Text mit typst-Befehlen wird geschützt (kein #-Befehl, keine Formel)")]
    public void Typst_escape()
    {
        var e = TypstDoc.Escape("#read(\"/etc/passwd\") $x$ *fett*");
        Assert.DoesNotContain(" #", " " + e.Replace("\\#", "")); Assert.StartsWith("\\#read", e); Assert.Contains("\\$x\\$", e); Assert.Contains("\\*fett\\*", e);
        var src = TypstDoc.Letter("#import \"x\"", "#eval(\"1\")"); Assert.DoesNotContain("\n#import", src); Assert.DoesNotContain("\n#eval", src);
    }

    [Fact(DisplayName = "typst −2: fehlende Datei, falsche Endung, Pfad außerhalb des Projekts, Programm fehlt")]
    public void Typst_refuse()
    {
        Assert.Contains("gibt es nicht", TypstDoc.Check(Path.Combine(_root, "x.typ"), Path.Combine(_root, "x.pdf"), _root));
        var txt = Path.Combine(_root, "a.txt"); File.WriteAllText(txt, "x"); Assert.Contains(".typ", TypstDoc.Check(txt, Path.Combine(_root, "x.pdf"), _root));
        Assert.Contains("PDF, PNG oder SVG", TypstDoc.Check(Typ(), Path.Combine(_root, "x.docx"), _root));
        Assert.Contains("Projektordner", TypstDoc.Check(Typ(), Path.Combine(Path.GetTempPath(), "x.pdf"), _root));
        Assert.Contains("nicht eingerichtet", TypstDoc.Compile(Path.Combine(_root, "fehlt.exe"), Typ(), Path.Combine(_root, "x.pdf"), _root).Message);
    }

    [Fact(DisplayName = "typst −3: Fehler von typst wird verständlich weitergegeben, keine leere Datei als Erfolg")]
    public void Typst_error()
    {
        var out1 = Path.Combine(_root, "x.pdf");
        var (ok, msg) = TypstDoc.Compile(_exe, Typ(), out1, _root, run: (_, _, _) => R(1, "", "error: unknown variable: foo\n  ┌─ brief.typ:1:2"));
        Assert.False(ok); Assert.Equal("typst meldet: error: unknown variable: foo", msg);
        Assert.False(TypstDoc.Compile(_exe, Typ(), out1, _root, run: (_, _, _) => R(0)).Ok);
    }

    // ---------- mediamtx ----------
    [Fact(DisplayName = "mediamtx +1: Standard nur auf diesem Rechner, ohne API/WebRTC, RTSP+HLS")]
    public void Mtx_config_local()
    {
        var c = LiveServer.Config();
        Assert.Contains("rtspAddress: 127.0.0.1:8554", c); Assert.Contains("hlsAddress: 127.0.0.1:8888", c);
        Assert.Contains("api: no", c); Assert.Contains("webrtc: no", c); Assert.Contains("rtmp: no", c); Assert.DoesNotContain("\r", c);
    }

    [Fact(DisplayName = "mediamtx +2: Heimnetz nur auf Wunsch (ohne 127.0.0.1), Konfiguration im Arbeitsordner")]
    public void Mtx_lan_and_prepare()
    {
        Assert.Contains("rtspAddress: :9554", LiveServer.Config(9554, 9888, lan: true));
        var args = LiveServer.Prepare(Path.Combine(_root, "live"), 9554, 9888);
        Assert.Single(args); Assert.Contains("127.0.0.1:9554", File.ReadAllText(args[0]));
    }

    [Fact(DisplayName = "mediamtx +3: Adressen für OBS (RTSP) und zum Ansehen (HLS)")]
    public void Mtx_urls()
    {
        Assert.Equal("rtsp://127.0.0.1:8554/sendung-1", LiveServer.PublishUrl("sendung-1"));
        Assert.Equal("http://127.0.0.1:8888/sendung-1/index.m3u8", LiveServer.WatchUrl("sendung-1"));
    }

    [Fact(DisplayName = "mediamtx −1: unsichere Namen und Ports werden abgelehnt")]
    public void Mtx_refuse()
    {
        Assert.Throws<ArgumentException>(() => LiveServer.PublishUrl("../x")); Assert.Throws<ArgumentException>(() => LiveServer.WatchUrl("A B"));
        Assert.False(LiveServer.ValidPath("")); Assert.False(LiveServer.ValidPath(new string('a', 41)));
        Assert.Throws<ArgumentException>(() => LiveServer.Config(80)); Assert.Throws<ArgumentException>(() => LiveServer.Config(8554, 8554));
    }

    // ---------- streamlink ----------
    [Fact(DisplayName = "streamlink +1: nur ARD/Das Erste und arte über https erlaubt")]
    public void Sl_allowed()
    {
        Assert.All(LiveResolve.Channels, c => Assert.True(LiveResolve.Allowed(c.Url)));
        Assert.True(LiveResolve.Allowed("https://www.ardmediathek.de/live/abc"));
        Assert.False(LiveResolve.Allowed("http://www.daserste.de/live/")); Assert.False(LiveResolve.Allowed("https://www.netflix.com/watch/1"));
        Assert.False(LiveResolve.Allowed("https://daserste.de.boese.example/live")); Assert.False(LiveResolve.Allowed("https://user@www.arte.tv/de/live/"));
        Assert.False(LiveResolve.Allowed("https://www.arte.tv:8443/de/live/")); Assert.False(LiveResolve.Allowed("kein link"));
    }

    [Fact(DisplayName = "streamlink +2: JSON auslesen – Titel und Qualitäten, best/worst am Ende")]
    public void Sl_parse()
    {
        var (title, q, err) = LiveResolve.Parse("{\"plugin\":\"ard_mediathek\",\"metadata\":{\"title\":\"Das Erste\"},\"streams\":{\"270p50\":{},\"worst\":{},\"best\":{},\"720p50\":{}}}");
        Assert.Null(err); Assert.Equal("Das Erste", title); Assert.Equal(["270p50", "720p50", "worst", "best"], q);
        Assert.Equal("No plugin", LiveResolve.Parse("{\"error\":\"No plugin\"}").Error); Assert.NotNull(LiveResolve.Parse("<html>").Error);
    }

    [Fact(DisplayName = "streamlink +3: Stream-Adresse für den Player, ohne Konfiguration und ohne nachgeladene Plugins")]
    public void Sl_stream_url()
    {
        IReadOnlyList<string>? seen = null;
        var (ok, msg) = LiveResolve.StreamUrl(_exe, LiveResolve.Channels[0].Url, "720p50", run: (_, a, _) => { seen = a; return R(0, "https://example.invalid/master.m3u8\n"); });
        Assert.True(ok); Assert.Equal("https://example.invalid/master.m3u8", msg);
        Assert.Equal(["--no-config", "--no-plugin-sideloading", "--stream-url", LiveResolve.Channels[0].Url, "720p50"], seen);
    }

    [Fact(DisplayName = "streamlink −1: fremde Seiten, seltsame Qualität und fehlendes Programm werden abgelehnt, ohne Programmstart")]
    public void Sl_refuse()
    {
        var started = false; Cli.Result Run(string e, IReadOnlyList<string> a, int t) { started = true; return R(0, "https://x/y.m3u8"); }
        Assert.False(LiveResolve.StreamUrl(_exe, "https://www.netflix.com/x", run: Run).Ok);
        Assert.False(LiveResolve.StreamUrl(_exe, LiveResolve.Channels[1].Url, "best --record x", run: Run).Ok);
        Assert.False(LiveResolve.Info(_exe, "https://pluto.tv/de", run: Run).Ok);
        Assert.False(started);
        Assert.Contains("nicht eingerichtet", LiveResolve.Info(Path.Combine(_root, "fehlt.exe"), LiveResolve.Channels[0].Url).Message);
        Assert.False(LiveResolve.StreamUrl(_exe, LiveResolve.Channels[0].Url, run: (_, _, _) => R(1, "", "error: No playable streams")).Ok);
    }
}
