using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>W3: Prüfung der Website vor dem Export – Titel und Beschreibung je Seite, doppelte Adressen, Weiterleitungen ins Leere, fehlende Website-Adresse.
/// Dazu W2 (Pflichtfelder) und Z5 (fehlende Übersetzungen). Die Prüfung auf dem fertigen Export steht in <see cref="WebDesignExport.CheckExport"/>.</summary>
public static class WebDesignSeo
{
    public sealed record Fund(string Text, string Ebene);
    public const int TitleMax = 60, DescriptionMax = 160;

    public static IReadOnlyList<Fund> Check(string dir)
    {
        var s = WebDesignSite.Load(dir); var res = new List<Fund>();
        var pages = WebDesignSite.Pages(dir);
        var cms = WebDesignCms.Load(dir);
        var titles = new Dictionary<string, string>();
        foreach (var p in pages)
        {
            JsonObject? props = null;
            try { props = JsonNode.Parse(WebDesign.LoadFile(WebDesignSite.FileOf(dir, p, s.Standardsprache, s.Standardsprache), p))?["root"]?["props"] as JsonObject; }
            catch (JsonException) { }
            if (props?["vorlage"] is not null) continue; // Vorlagen bekommen Titel aus den Einträgen
            var title = props?["title"]?.ToString() ?? ""; var desc = props?["description"]?.ToString() ?? "";
            var name = p == WebDesignSite.Index ? "Startseite" : "/" + p;
            if (title.Length == 0) res.Add(new($"{name}: kein Seitentitel.", "Fehler"));
            else if (title.Length > TitleMax) res.Add(new($"{name}: Titel ist mit {title.Length} Zeichen lang; Suchmaschinen zeigen etwa {TitleMax}.", "Hinweis"));
            if (desc.Length == 0) res.Add(new($"{name}: keine Beschreibung für Suchmaschinen.", "Hinweis"));
            else if (desc.Length > DescriptionMax) res.Add(new($"{name}: Beschreibung ist mit {desc.Length} Zeichen lang; gezeigt werden etwa {DescriptionMax}.", "Hinweis"));
            if (title.Length > 0 && titles.TryGetValue(title, out var other)) res.Add(new($"{name} und {other} haben denselben Titel „{title}“.", "Hinweis"));
            else if (title.Length > 0) titles[title] = name;
        }
        // Doppelte Adressen: Seite gegen Sammlung (Ordner der Eintragsseiten) und gegen Weiterleitungen
        foreach (var c in cms.Where(c => pages.Contains(c.Id))) res.Add(new($"Die Adresse „{c.Id}“ ist doppelt: Seite und Sammlung. Bitte eine davon umbenennen.", "Fehler"));
        var targets = pages.Concat(cms.Where(c => WebDesignCms.TemplateOf(dir, c.Id) is not null).SelectMany(c => c.Eintraege.Select(e => c.Id + "/" + e.Adresse))).ToHashSet();
        foreach (var r in s.Weiterleitungen)
        {
            if (pages.Contains(r.Von)) res.Add(new($"Die Adresse „/{r.Von}“ ist doppelt: Seite und Weiterleitung.", "Fehler"));
            if (!r.Nach.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !targets.Contains(r.Nach))
                res.Add(new($"Weiterleitung /{r.Von} zeigt auf „{r.Nach}“, diese Seite gibt es nicht.", "Fehler"));
            if (s.Weiterleitungen.Any(x => x.Von == r.Nach)) res.Add(new($"Weiterleitung /{r.Von} führt auf eine weitere Weiterleitung. Bitte direkt auf das Ziel zeigen.", "Hinweis"));
        }
        if (s.Basis.Length == 0) res.Add(new("Adresse der Website fehlt: ohne sie gibt es keine Sitemap, keine kanonischen Adressen und keine Sprachverweise.", "Hinweis"));
        foreach (var st in WebDesignI18n.Report(dir).Where(x => x.Fehlt || x.Offen > 0))
        {
            var name = st.Adresse == WebDesignSite.Index ? "Startseite" : "/" + st.Adresse;
            res.Add(new(st.Fehlt || st.Offen == st.Gesamt
                ? $"{name} ist in {WebDesignI18n.LangName(st.Sprache)} nicht übersetzt."
                : $"{name} ({WebDesignI18n.LangName(st.Sprache)}): {st.Offen} von {st.Gesamt} Texten noch wie im Original.", "Hinweis"));
        }
        if (s.Sprachen.Count > 1 && WebDesignComponents.Load(dir).Count > 0)
            res.Add(new("Komponenten sind einsprachig: ihre Texte erscheinen in jeder Sprache gleich. Für übersetzte Navigation oder Fußzeile die Bausteine direkt auf die Seiten legen.", "Hinweis"));
        res.AddRange(WebDesignCms.Check(dir).Select(f => new Fund(f.Text, f.Ebene)));
        return res;
    }
}
