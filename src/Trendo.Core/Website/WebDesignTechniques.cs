using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>A2: Verzeichnis moderner Web-Techniken mit Browser-Stand, Quelle, Ersatzweg und eigenem Beispiel. Die KI wählt daraus, statt Techniken zu erfinden.
/// Stand jeweils am Datum <see cref="Checked"/> auf den Baseline-Angaben der verlinkten MDN-Seite abgelesen (Stichprobe aller Einträge).</summary>
public static class WebDesignTechniques
{
    public const string Checked = "13.11.2026";

    /// <summary><paramref name="Nutzung"/>: „direkt“ oder „mit Ersatzweg“. <paramref name="Stand"/>: Baseline-Stand mit Jahr.</summary>
    public sealed record Technik(string Id, string Name, string Nutzung, string Stand, string Quelle, string Wofuer, string? Ersatzweg, string Beispiel, IReadOnlyList<string> Stichworte);

    public static readonly IReadOnlyList<Technik> All =
    [
        new("container-queries", "Container Queries", "direkt", "Baseline weitgehend verfügbar, seit Februar 2023", "https://developer.mozilla.org/en-US/docs/Web/CSS/@container",
            "Bausteine passen sich der Breite ihres Behälters an, nicht nur dem Bildschirm.", null,
            ".karte-box { container-type: inline-size; }\n@container (max-width: 480px) { .karte { grid-template-columns: 1fr; } }", ["container", "behälter", "spalten", "responsiv", "breite"]),
        new("subgrid", "Subgrid", "direkt", "Baseline weitgehend verfügbar, seit September 2023", "https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_grid_layout/Subgrid",
            "Karten in einem Raster richten Titel, Text und Knopf über alle Karten gleich aus.", null,
            ".raster { display: grid; grid-template-columns: repeat(3, 1fr); }\n.karte { display: grid; grid-row: span 3; grid-template-rows: subgrid; }", ["subgrid", "raster", "grid", "karten", "ausrichten"]),
        new("has", ":has()", "direkt", "Baseline weitgehend verfügbar, seit Dezember 2023", "https://developer.mozilla.org/en-US/docs/Web/CSS/:has",
            "Ein Element nach seinem Inhalt gestalten, etwa eine Karte mit Bild anders als eine ohne.", null,
            ".karte:has(img) { padding-top: 0; }", ["has", "eltern", "selektor", "inhalt"]),
        new("clamp", "Flüssige Schriftgrößen mit clamp()", "direkt", "Baseline weitgehend verfügbar, seit Juli 2020", "https://developer.mozilla.org/en-US/docs/Web/CSS/clamp",
            "Schrift wächst stufenlos mit der Breite, zwischen einer Unter- und einer Obergrenze.", null,
            "h1 { font-size: clamp(2rem, 1.2rem + 3vw, 4rem); }", ["clamp", "schrift", "schriftgröße", "typografie", "flüssig"]),
        new("view-transitions", "View Transitions im Dokument", "direkt", "Baseline neu verfügbar, seit Oktober 2025", "https://developer.mozilla.org/en-US/docs/Web/API/Document/startViewTransition",
            "Weicher Übergang, wenn sich Inhalte auf derselben Seite austauschen.", "Ohne Unterstützung die Änderung sofort zeigen: vorher prüfen, ob document.startViewTransition vorhanden ist.",
            "if (document.startViewTransition) document.startViewTransition(() => zeige(neu)); else zeige(neu);", ["view transition", "übergang", "wechsel", "animation"]),
        new("scroll-animationen", "Scrollgesteuerte Animationen (animation-timeline)", "mit Ersatzweg", "Baseline neu verfügbar, seit Oktober 2026", "https://developer.mozilla.org/en-US/docs/Web/CSS/animation-timeline",
            "Bewegung, die am Scroll-Fortschritt hängt, ohne Skript.", "Nur in @supports (animation-timeline: view()) verwenden; ältere Browser zeigen den Endzustand ohne Bewegung. In Trendo bindet die Zeitleiste „Beim Scrollen“ per Skript.",
            "@supports (animation-timeline: view()) { .auftritt { animation: einblenden linear both; animation-timeline: view(); animation-range: entry; } }", ["scroll", "scrollen", "animation-timeline", "parallax", "bewegung"]),
        new("anchor-positioning", "Anchor Positioning", "mit Ersatzweg", "Baseline neu verfügbar, seit Januar 2026; einzelne Teile unterschiedlich unterstützt", "https://developer.mozilla.org/en-US/docs/Web/CSS/anchor-name",
            "Hinweise und Menüs hängen sich an ein anderes Element, ohne Positionsberechnung im Skript.", "Nur in @supports (anchor-name: --a) verwenden; sonst den Hinweis unter dem Element im normalen Fluss zeigen.",
            ".knopf { anchor-name: --knopf; }\n@supports (anchor-name: --a) { .hinweis { position: absolute; position-anchor: --knopf; top: anchor(bottom); } }", ["anchor", "anker", "tooltip", "hinweis", "menü"]),
        new("seitenuebergaenge", "Übergänge zwischen Seiten (@view-transition)", "mit Ersatzweg", "Eingeschränkt verfügbar, kein Baseline (nicht in allen verbreiteten Browsern)", "https://developer.mozilla.org/en-US/docs/Web/CSS/@view-transition",
            "Weicher Übergang beim Wechsel auf eine andere Seite derselben Website.", "Ohne Unterstützung wechselt die Seite normal; nichts darf von dem Übergang abhängen.",
            "@view-transition { navigation: auto; }", ["seitenübergang", "seitenwechsel", "navigation", "übergang"]),
    ];

    /// <summary>Techniken passend zur Aufgabe (Stichwort am Wortanfang); ohne Treffer keine.</summary>
    public static IReadOnlyList<Technik> ForTask(string? task) => string.IsNullOrWhiteSpace(task) ? [] :
        All.Where(t => t.Stichworte.Any(k => Regex.IsMatch(task, @"\b" + Regex.Escape(k), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))).ToList();

    /// <summary>Verzeichnis als Text für die KI, höchstens <paramref name="maxChars"/> Zeichen, nur ganze Einträge.</summary>
    public static string PromptText(IEnumerable<Technik> list, int maxChars)
    {
        var sb = new StringBuilder();
        foreach (var t in list)
        {
            var part = $"- {t.Name} ({t.Nutzung}; {t.Stand}): {t.Wofuer}{(t.Ersatzweg is null ? "" : " Ersatzweg: " + t.Ersatzweg)}\n";
            if (sb.Length + part.Length > maxChars) break;
            sb.Append(part);
        }
        return sb.ToString().TrimEnd();
    }
}
