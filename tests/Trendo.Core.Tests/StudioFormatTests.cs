using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Lieferformat je Studio: damit Claudes Ergebnis sofort live im Programm erscheint.
public class StudioFormatTests
{
    [Fact(DisplayName = "Studio-Format: Webseite/App und Spiel als index.html, Text als Markdown, Tabelle als CSV, 3D als glTF/OBJ, Fachprogramm als .tdoc; Vorschau wählt die Hauptdatei")]
    public void Formats()
    {
        Assert.Equal("index.html", StudioFormat.For("CORE-07", "dev")!.Main);
        Assert.Contains("HTML5", StudioFormat.For("CORE-06", "spiele")!.Rule);
        Assert.Contains("Markdown", StudioFormat.For("SYS-10", "text")!.Rule);
        Assert.Contains("CSV", StudioFormat.For("SYS-10", "tabelle")!.Rule);
        Assert.Contains(".gltf", StudioFormat.For("CORE-05", "3d")!.Opens);
        Assert.Contains(".tdoc", StudioFormat.For("SPEC-01", "architektur", shell: true)!.Rule);
        Assert.Contains("Markdown", StudioFormat.For("CORE-11", "sicherung")!.Rule); // jedes Studio hat ein Profil
        Assert.Null(StudioFormat.For("UNBEKANNT", "x"));
        Assert.Equal("web", StudioFormat.PreviewKind("a/index.html")); Assert.Equal("web", StudioFormat.PreviewKind("logo.svg"));
        Assert.Equal("text", StudioFormat.PreviewKind("spiel.js")); Assert.Equal("none", StudioFormat.PreviewKind("x.exe"));
        Assert.Equal("index.html", StudioFormat.PickPreview(StudioFormat.For("CORE-07", null), ["style.css", "index.html", "app.js"]));
        Assert.Equal("logo.svg", StudioFormat.PickPreview(StudioFormat.For("CORE-08", null), ["notizen.md", "logo.svg"]));
        Assert.Null(StudioFormat.PickPreview(null, ["x.exe"]));
        var sys = ClaudeChats.System("Spiele bauen", "Du baust Spiele.", [], StudioFormat.For("CORE-06", null), null);
        Assert.Contains("ausschließlich für das Studio „Spiele bauen“", sys); Assert.Contains("Lieferformat", sys);
        Assert.Contains("„Plan.tdoc“", ClaudeChats.System("Grundriss", "x", [], StudioFormat.For("SPEC-01", "architektur", true), "Plan.tdoc"));
    }
}
