using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stufe 3 im Studio „Webseite bauen“: mehrere Seiten, Website-Adresse, Übergang und Weiterleitungen (W3, W4), Sprachen mit KI-Vorschlag (Z5), Inhalte im CMS (W2),
/// Export der ganzen Website mit Vorrendern und Prüfung auf dem Export.</summary>
public partial class MainWindow
{
    string _wdSiteNote = "", _wdLangNote = "", _wdCmsNote = "";
    string? _wdDeleteAsk, _wdLangDeleteAsk, _wdCollDeleteAsk, _wdCmsSel, _wdCmsEntry, _wdCmsLang;
    IReadOnlyList<WebDesignSeo.Fund>? _wdSeo;
    bool _wdTranslating;
    public string WebDesignSiteNote => _wdSiteNote;
    public string WebDesignLangNote => _wdLangNote;
    public string WebDesignCmsNote => _wdCmsNote;

    // ---------- Seiten ----------

    /// <summary>Seite (in einer Sprache) zum Bearbeiten öffnen. Ein ungespeicherter Entwurf der bisherigen Seite bleibt als automatischer Stand erhalten.</summary>
    public void WebDesignOpenPage(string adresse, string? sprache = null)
    {
        WebDesignKeepDraft("Vor Seitenwechsel");
        WebDesignCodeClose(); // der gezeigte Code gehört zur bisherigen Seite
        var err = WebDesignSite.Activate(WebDesignDir, adresse, sprache);
        if (err is not null) { _wdSiteNote = err; Show("dev"); return; }
        _wdVariant = null; WebDesignClearSelection();
        _wdSiteNote = $"Bearbeitet wird jetzt: {PageLabel(adresse)}{(sprache is null || sprache == WebDesignSite.Load(WebDesignDir).Standardsprache ? "" : $" ({WebDesignI18n.LangName(sprache)})")}.";
        WebDesignReload(WebDesignStates.Read(WebDesignDir));
        Show("dev");
    }

    static string PageLabel(string adresse) => adresse == WebDesignSite.Index ? "Startseite" : "/" + adresse;

    public void WebDesignCreatePage(string titel, string adresse, bool kopie)
    {
        var slug = adresse.Trim().Length > 0 ? adresse.Trim() : WebDesignSite.SlugOf(titel);
        WebDesignKeepDraft("Vor neuer Seite");
        var err = WebDesignSite.Create(WebDesignDir, slug, titel.Trim().Length > 0 ? titel.Trim() : slug, kopie ? WebDesignSite.Active(WebDesignDir).Adresse : null);
        _wdSiteNote = err ?? $"Seite /{slug} angelegt und geöffnet.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.seite.anlegen", err ?? slug);
        if (err is null) { _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir)); }
        Show("dev");
    }

    /// <summary>Seite löschen: erster Klick fragt nach, zweiter löscht (die Dateien wandern in den Papierkorb des Projekts).</summary>
    public void WebDesignDeletePage(string adresse)
    {
        if (_wdDeleteAsk != adresse) { _wdDeleteAsk = adresse; _wdSiteNote = $"/{adresse} mit allen Übersetzungen löschen? Noch einmal auf „Löschen“ klicken. Die Dateien landen im Papierkorb des Projekts."; Show("dev"); return; }
        _wdDeleteAsk = null;
        var wasActive = WebDesignSite.Active(WebDesignDir).Adresse == adresse;
        if (wasActive) WebDesignKeepDraft("Vor Löschen der Seite");
        var err = WebDesignSite.Delete(WebDesignDir, adresse);
        _wdSiteNote = err ?? $"/{adresse} gelöscht (liegt im Papierkorb des Projekts).";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.seite.loeschen", err ?? adresse);
        if (err is null && wasActive) { _wdDraft = null; _wdVariant = null; WebDesignReload(WebDesignStates.Read(WebDesignDir)); }
        Show("dev");
    }

    public void WebDesignSetBase(string basis) { _wdSiteNote = WebDesignSite.SetBase(WebDesignDir, basis) ?? "Adresse der Website gespeichert. Sitemap und Sprachverweise nutzen sie."; Show("dev"); }
    public void WebDesignSetTransition(string ueb) { _wdSiteNote = WebDesignSite.SetTransition(WebDesignDir, ueb) ?? "Übergang zwischen Seiten gespeichert."; Show("dev"); }
    public void WebDesignAddRedirect(string von, string nach) { _wdSiteNote = WebDesignSite.AddRedirect(WebDesignDir, von, nach) ?? "Weiterleitung angelegt."; Show("dev"); }
    public void WebDesignCheckSite() { _wdSeo = WebDesignSeo.Check(WebDesignDir); _wdSiteNote = _wdSeo.Count == 0 ? "Keine Befunde." : $"{_wdSeo.Count(f => f.Ebene == "Fehler")} Fehler, {_wdSeo.Count(f => f.Ebene != "Fehler")} Hinweise."; Show("dev"); }

    static readonly Dictionary<string, string> TransitionNames = new() { ["keiner"] = "Keiner", ["einblenden"] = "Einblenden", ["schieben"] = "Schieben" };

    Control WebDesignPagesPane()
    {
        var dir = WebDesignDir; var site = WebDesignSite.Load(dir); var active = WebDesignSite.Active(dir);
        var sp = new StackPanel { Spacing = 6, Name = "web-seiten" };
        sp.Children.Add(new TextBlock { Text = "Seiten der Website", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Jede Seite hat eine eigene Adresse. Gestaltung und Komponenten gelten für alle Seiten. Links auf eine Seite schreibst du als ihre Adresse, z. B. „ueber-uns“.", Muted, 12));
        var i = 0;
        foreach (var page in WebDesignSite.Pages(dir))
        {
            var p = page; var n = i++;
            var isActive = active.Adresse == p && active.Sprache == site.Standardsprache;
            var label = $"{PageLabel(p)} · {WebDesignSite.TitleOf(dir, p)}" + (WebDesignCms.Load(dir).FirstOrDefault(c => WebDesignCms.TemplateOf(dir, c.Id) == p) is { } tc ? $" · Vorlage für „{tc.Name}“" : "");
            var row = new WrapPanel { Name = $"web-seite-{n}" };
            row.Children.Add(new TextBlock { Text = (isActive ? "✎ " : "") + label, Foreground = isActive ? AccentText : Text, FontSize = 12.5, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            if (!isActive) row.Children.Add(Act($"web-seite-oeffnen-{n}", "Bearbeiten", () => WebDesignOpenPage(p)));
            if (p != WebDesignSite.Index)
            {
                row.Children.Add(Act($"web-seite-hoch-{n}", "↑", () => { WebDesignSite.Move(dir, p, -1); Show("dev"); }));
                row.Children.Add(Act($"web-seite-runter-{n}", "↓", () => { WebDesignSite.Move(dir, p, 1); Show("dev"); }));
                row.Children.Add(Act($"web-seite-loeschen-{n}", _wdDeleteAsk == p ? "Wirklich löschen" : "Löschen", () => WebDesignDeletePage(p)));
            }
            sp.Children.Add(row);
        }
        var title = new TextBox { Name = "web-seite-titel", PlaceholderText = "Titel, z. B. Über uns", Width = 200 };
        var slug = new TextBox { Name = "web-seite-adresse", PlaceholderText = "Adresse (leer: aus dem Titel)", Width = 200 };
        var copy = new CheckBox { Name = "web-seite-kopie", Content = "als Kopie der aktuellen Seite", Foreground = Text };
        sp.Children.Add(new WrapPanel { Children = { title, new Border { Width = 6 }, slug, new Border { Width = 6 }, copy, new Border { Width = 6 }, Act("web-seite-anlegen", "Seite anlegen", () => WebDesignCreatePage(title.Text ?? "", slug.Text ?? "", copy.IsChecked == true), true) } });

        sp.Children.Add(new TextBlock { Text = "Adresse und Übergang", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 6, 0, 0) });
        var basis = new TextBox { Name = "web-basis", Text = site.Basis, PlaceholderText = "https://www.beispiel.de", Width = 260 };
        var ueb = new ComboBox { Name = "web-uebergang", ItemsSource = WebDesignSite.Transitions.Select(t => TransitionNames[t]).ToList(), SelectedIndex = Array.IndexOf(WebDesignSite.Transitions, site.Uebergang), Width = 140 };
        ueb.SelectionChanged += (_, _) => { if (ueb.SelectedIndex >= 0 && WebDesignSite.Transitions[ueb.SelectedIndex] != WebDesignSite.Load(dir).Uebergang) WebDesignSetTransition(WebDesignSite.Transitions[ueb.SelectedIndex]); };
        sp.Children.Add(new WrapPanel { Children = { basis, new Border { Width = 6 }, Act("web-basis-speichern", "Adresse speichern", () => WebDesignSetBase(basis.Text ?? "")), new Border { Width = 16 },
            new TextBlock { Text = "Übergang zwischen Seiten:", Foreground = Muted, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }, ueb } });
        sp.Children.Add(P("Die Adresse brauchen Sitemap, kanonische Adressen und Sprachverweise. Der Übergang wirkt in Browsern, die View Transitions können; bei „weniger Bewegung“ wechselt die Seite einfach.", Muted, 11.5));

        sp.Children.Add(new TextBlock { Text = "Weiterleitungen", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 6, 0, 0) });
        var ri = 0;
        foreach (var r in site.Weiterleitungen) { var rr = r; sp.Children.Add(new WrapPanel { Children = { P($"/{r.Von} → {(r.Nach.StartsWith("https://", StringComparison.Ordinal) ? r.Nach : "/" + (r.Nach == WebDesignSite.Index ? "" : r.Nach))}", Text, 12.5), new Border { Width = 8 }, Act($"web-weiter-weg-{ri++}", "Entfernen", () => { WebDesignSite.RemoveRedirect(dir, rr.Von); Show("dev"); }) } }); }
        var von = new TextBox { Name = "web-weiter-von", PlaceholderText = "alte Adresse, z. B. /angebot-alt", Width = 220 };
        var nach = new TextBox { Name = "web-weiter-nach", PlaceholderText = "Ziel, z. B. angebot", Width = 200 };
        sp.Children.Add(new WrapPanel { Children = { von, new Border { Width = 6 }, nach, new Border { Width = 6 }, Act("web-weiter-anlegen", "Weiterleitung anlegen", () => WebDesignAddRedirect(von.Text ?? "", nach.Text ?? "")) } });

        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { Act("web-pruefen", "Website prüfen (Titel, Adressen, Übersetzungen, Inhalte)", WebDesignCheckSite) } });
        if (_wdSeo is { Count: > 0 } f)
            foreach (var x in f.Take(30)) sp.Children.Add(P((x.Ebene == "Fehler" ? "✗ " : "• ") + x.Text, x.Ebene == "Fehler" ? Brush.Parse("#F5A524") : Muted, 12));
        if (_wdSiteNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-seiten-note", Text = _wdSiteNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-seiten-card", sp);
    }

    // ---------- Sprachen (Z5) ----------

    public void WebDesignAddLanguage(string code) { _wdLangNote = WebDesignSite.AddLanguage(WebDesignDir, code) ?? $"{WebDesignI18n.LangName(code.Trim().ToLowerInvariant())} angelegt. Übersetze die Seiten über „Bearbeiten“ oder lass die KI einen Vorschlag machen."; Show("dev"); }

    public void WebDesignRemoveLanguage(string code)
    {
        if (_wdLangDeleteAsk != code) { _wdLangDeleteAsk = code; _wdLangNote = $"{WebDesignI18n.LangName(code)} mit allen Übersetzungen entfernen? Noch einmal klicken. Die Dateien landen im Papierkorb des Projekts."; Show("dev"); return; }
        _wdLangDeleteAsk = null;
        var wasActive = WebDesignSite.Active(WebDesignDir).Sprache == code;
        if (wasActive) WebDesignKeepDraft("Vor Entfernen der Sprache");
        _wdLangNote = WebDesignSite.RemoveLanguage(WebDesignDir, code) ?? $"{WebDesignI18n.LangName(code)} entfernt.";
        if (wasActive) { _wdDraft = null; _wdVariant = null; WebDesignReload(WebDesignStates.Read(WebDesignDir)); }
        Show("dev");
    }

    /// <summary>Z5: KI übersetzt die Texte einer Seite als Vorschlag. Erst „Übernehmen“ ändert die Seite in der Sprache.</summary>
    public async Task WebDesignTranslateAi(string adresse, string sprache)
    {
        if (_wdTranslating) return;
        _wdTranslating = true;
        try
        {
            var site = WebDesignSite.Load(WebDesignDir);
            var original = JsonNode.Parse(WebDesign.LoadFile(WebDesignSite.FileOf(WebDesignDir, adresse, site.Standardsprache, site.Standardsprache), adresse)) as JsonObject;
            var texts = original is null ? [] : WebDesignI18n.Texts(original);
            if (texts.Count == 0) { _wdLangNote = "Auf dieser Seite gibt es keine Texte zum Übersetzen."; return; }
            _wdLangNote = $"Die KI übersetzt {PageLabel(adresse)} nach {WebDesignI18n.LangName(sprache)} …"; Show("dev");
            var (claude, local, backends) = await WebKiState();
            var choice = WebDesignKi.Choose(KiBedarf.Text, claude, local, false);
            if (!choice.Ok) { _wdLangNote = choice.Note; return; }
            var answer = await WebKiAsk("Du übersetzt Website-Texte. Antworte nur mit JSON.", WebDesignI18n.Prompt(texts, site.Standardsprache, sprache), backends);
            var (map, err) = WebDesignI18n.ParseAnswer(answer, texts);
            if (map is null) { _wdLangNote = "Die Übersetzung der KI wurde abgelehnt: " + err; return; }
            _wdLangNote = WebDesignI18n.SaveProposal(WebDesignDir, adresse, sprache, map) ?? $"Vorschlag von {_wdKiWho}: {map.Count} von {texts.Count} Texten übersetzt. Die Seite ändert sich erst mit „Übernehmen“.";
            _core.Audit.Append(Principal.LocalOwner.Name, "web.sprache.vorschlag", $"{adresse} {sprache} {map.Count}");
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { _wdLangNote = e.Message; }
        finally { _wdTranslating = false; Show("dev"); }
    }

    public void WebDesignAdoptTranslation(string adresse, string sprache)
    {
        WebDesignKeepDraft("Vor Übernahme einer Übersetzung");
        var err = WebDesignI18n.AdoptProposal(WebDesignDir, adresse, sprache);
        _wdLangNote = err ?? $"Übersetzung übernommen: {PageLabel(adresse)} ({WebDesignI18n.LangName(sprache)}). Der vorige Inhalt liegt als Stand bereit.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.sprache.uebernehmen", err ?? $"{adresse} {sprache}");
        if (err is null) { _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir)); }
        Show("dev");
    }

    Control WebDesignLanguagesPane()
    {
        var dir = WebDesignDir; var site = WebDesignSite.Load(dir); var active = WebDesignSite.Active(dir);
        var sp = new StackPanel { Spacing = 6, Name = "web-sprachen" };
        sp.Children.Add(new TextBlock { Text = "Sprachen der Website", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Jede Sprache bekommt eigene Adressen (z. B. /en/ueber-uns.html) mit Sprachverweisen. Die KI übersetzt nur als Vorschlag. Trendo selbst bleibt deutsch.", Muted, 12));
        sp.Children.Add(P($"Standardsprache: {WebDesignI18n.LangName(site.Standardsprache)} ({site.Standardsprache})", Text, 12.5));
        var report = WebDesignI18n.Report(dir);
        var li = 0;
        foreach (var lang in site.Sprachen.Where(l => l != site.Standardsprache))
        {
            var l = lang; var ln = li++;
            sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { new TextBlock { Text = $"{WebDesignI18n.LangName(l)} ({l})", FontWeight = FontWeight.SemiBold, Foreground = Text, FontSize = 13, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, new Border { Width = 10 },
                Act($"web-sprache-weg-{ln}", _wdLangDeleteAsk == l ? "Wirklich entfernen" : "Entfernen", () => WebDesignRemoveLanguage(l)) } });
            var pi = 0;
            foreach (var st in report.Where(r => r.Sprache == l))
            {
                var s = st; var pn = pi++;
                var state = s.Fehlt ? "fehlt" : s.Offen == 0 ? "übersetzt" : s.Offen == s.Gesamt ? "noch nicht übersetzt" : $"{s.Offen} von {s.Gesamt} Texten offen";
                var row = new WrapPanel { Name = $"web-uebersetzung-{ln}-{pn}" };
                var editing = active.Adresse == s.Adresse && active.Sprache == l;
                row.Children.Add(new TextBlock { Text = $"{(editing ? "✎ " : "")}{PageLabel(s.Adresse)}: {state}", Foreground = s.Offen == 0 && !s.Fehlt ? Muted : Brush.Parse("#F5A524"), FontSize = 12.5, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                if (!editing) row.Children.Add(Act($"web-uebersetzung-bearbeiten-{ln}-{pn}", "Bearbeiten", () => WebDesignOpenPage(s.Adresse, l)));
                row.Children.Add(Act($"web-uebersetzung-ki-{ln}-{pn}", _wdTranslating ? "KI arbeitet …" : "KI-Vorschlag", () => { _ = WebDesignTranslateAi(s.Adresse, l); }));
                if (WebDesignI18n.Proposal(dir, s.Adresse, l) is { } prop)
                {
                    row.Children.Add(Act($"web-uebersetzung-ok-{ln}-{pn}", "Vorschlag übernehmen", () => WebDesignAdoptTranslation(s.Adresse, l), true));
                    row.Children.Add(Act($"web-uebersetzung-weg-{ln}-{pn}", "Verwerfen", () => { WebDesignI18n.DiscardProposal(dir, s.Adresse, l); _wdLangNote = "Vorschlag verworfen."; Show("dev"); }));
                    sp.Children.Add(row);
                    sp.Children.Add(P("Vorschlag: " + string.Join(" · ", prop.Values.Take(4).Select(v => v.Length > 50 ? v[..50] + "…" : v)) + (prop.Count > 4 ? $" · (+{prop.Count - 4})" : ""), Muted, 11.5));
                }
                else sp.Children.Add(row);
            }
        }
        var code = new TextBox { Name = "web-sprache-code", PlaceholderText = "Sprachcode, z. B. en", Width = 160 };
        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { code, new Border { Width = 6 }, Act("web-sprache-anlegen", "Sprache hinzufügen", () => WebDesignAddLanguage(code.Text ?? "")) } });
        if (active.Sprache != site.Standardsprache) sp.Children.Add(Act("web-sprache-zurueck", $"Zurück zur Standardsprache ({WebDesignI18n.LangName(site.Standardsprache)})", () => WebDesignOpenPage(active.Adresse)));
        if (_wdLangNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-sprachen-note", Text = _wdLangNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-sprachen-card", sp);
    }

    // ---------- Inhalte (W2) ----------

    public void WebDesignCreateCollection(string name, string id, string spec)
    {
        var slug = id.Trim().Length > 0 ? id.Trim() : WebDesignSite.SlugOf(name);
        var err = WebDesignCms.CreateCollection(WebDesignDir, slug, name, spec);
        _wdCmsNote = err ?? $"Sammlung „{name.Trim()}“ angelegt. Lege Einträge an und eine Vorlage-Seite, dann gibt es je Eintrag eine Seite.";
        if (err is null) _wdCmsSel = slug;
        Show("dev");
    }

    public void WebDesignDeleteCollection(string id)
    {
        if (_wdCollDeleteAsk != id) { _wdCollDeleteAsk = id; _wdCmsNote = "Sammlung mit allen Einträgen löschen? Noch einmal klicken."; Show("dev"); return; }
        _wdCollDeleteAsk = null;
        _wdCmsNote = WebDesignCms.DeleteCollection(WebDesignDir, id) ?? "Sammlung gelöscht.";
        if (_wdCmsSel == id) _wdCmsSel = null;
        WebDesignReloadIfOpen();
        Show("dev");
    }

    public void WebDesignSaveEntry(string collection, string adresse, IReadOnlyDictionary<string, string> werte, string? sprache)
    {
        var err = WebDesignCms.SetEntry(WebDesignDir, collection, adresse.Trim(), werte, sprache);
        _wdCmsNote = err ?? $"Eintrag „{adresse.Trim()}“ gespeichert.";
        if (err is null) { _wdCmsEntry = adresse.Trim(); WebDesignReloadIfOpen(); }
        Show("dev");
    }

    /// <summary>Vorlage-Seite für eine Sammlung anlegen: je Feld ein Baustein mit Platzhalter, z. B. Überschrift „{{titel}}“.</summary>
    public void WebDesignCreateTemplate(string collection)
    {
        if (WebDesignCms.Load(WebDesignDir).FirstOrDefault(c => c.Id == collection) is not { } c) { _wdCmsNote = "Diese Sammlung gibt es nicht."; Show("dev"); return; }
        var slug = WebDesignSite.SlugOf(collection + "-vorlage");
        WebDesignKeepDraft("Vor neuer Vorlage");
        var err = WebDesignSite.Create(WebDesignDir, slug, c.Name);
        if (err is not null) { _wdCmsNote = err; Show("dev"); return; }
        var content = new JsonArray();
        var n = 0;
        foreach (var f in c.Felder)
        {
            var id = $"v{n++}";
            content.Add(f.Art switch
            {
                "bild" => new JsonObject { ["type"] = "Bild", ["props"] = new JsonObject { ["id"] = id, ["src"] = "{{" + f.Name + "}}", ["alt"] = c.TitelFeld is { } tf ? "{{" + tf + "}}" : "" } },
                "text" when f.Name == c.TitelFeld => new JsonObject { ["type"] = "Ueberschrift", ["props"] = new JsonObject { ["id"] = id, ["text"] = "{{" + f.Name + "}}", ["level"] = "h2" } },
                "verweis" => new JsonObject { ["type"] = "Knopf", ["props"] = new JsonObject { ["id"] = id, ["label"] = "{{" + f.Name + "}}", ["href"] = "{{" + f.Name + ".link}}" } },
                _ => new JsonObject { ["type"] = "Text", ["props"] = new JsonObject { ["id"] = id, ["text"] = "{{" + f.Name + "}}", ["align"] = "left" } },
            });
        }
        var doc = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = c.Name, ["description"] = c.TitelFeld is { } t ? "{{" + t + "}}" : "", ["vorlage"] = c.Id } }, ["content"] = content, ["zones"] = new JsonObject() };
        var werr = WebDesignStates.Write(WebDesignDir, doc.ToJsonString());
        _wdCmsNote = werr ?? $"Vorlage /{slug} angelegt und geöffnet. Platzhalter wie {{{{{c.TitelFeld}}}}} werden je Eintrag ersetzt; gestalte die Seite frei.";
        _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir));
        Show("dev");
    }

    void WebDesignReloadIfOpen() { if (_wdDraft is null) WebDesignReload(WebDesignStates.Read(WebDesignDir, _wdVariant)); } // Listen aus dem CMS neu zeigen, ein Entwurf bleibt unberührt

    Control WebDesignCmsPane()
    {
        var dir = WebDesignDir; var site = WebDesignSite.Load(dir); var all = WebDesignCms.Load(dir);
        var sp = new StackPanel { Spacing = 6, Name = "web-cms" };
        sp.Children.Add(new TextBlock { Text = "Inhalte (CMS)", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Sammlungen wie Rezepte, Termine oder Mitarbeitende: Felder festlegen, Einträge pflegen. Der Baustein „Sammlung“ zeigt sie als Liste, eine Vorlage-Seite macht aus jedem Eintrag eine eigene Seite.", Muted, 12));
        var ci = 0;
        foreach (var coll in all)
        {
            var c = coll; var n = ci++;
            var tpl = WebDesignCms.TemplateOf(dir, c.Id);
            var row = new WrapPanel { Name = $"web-cms-sammlung-{n}" };
            row.Children.Add(new TextBlock { Text = $"{(_wdCmsSel == c.Id ? "▸ " : "")}{c.Name} ({c.Id}) · {c.Eintraege.Count} Einträge · {(tpl is null ? "keine Vorlage" : "Vorlage /" + tpl)}", Foreground = Text, FontSize = 12.5, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            row.Children.Add(Act($"web-cms-waehlen-{n}", "Einträge", () => { _wdCmsSel = c.Id; _wdCmsEntry = null; Show("dev"); }));
            if (tpl is null) row.Children.Add(Act($"web-cms-vorlage-{n}", "Vorlage-Seite anlegen", () => WebDesignCreateTemplate(c.Id)));
            row.Children.Add(Act($"web-cms-loeschen-{n}", _wdCollDeleteAsk == c.Id ? "Wirklich löschen" : "Löschen", () => WebDesignDeleteCollection(c.Id)));
            sp.Children.Add(row);
        }
        var cname = new TextBox { Name = "web-cms-name", PlaceholderText = "Name, z. B. Rezepte", Width = 170 };
        var cid = new TextBox { Name = "web-cms-id", PlaceholderText = "Adresse (leer: aus dem Namen)", Width = 190 };
        var spec = new TextBox { Name = "web-cms-felder", PlaceholderText = "Felder: titel:text*, bild:bild, datum:datum, text:langtext", Width = 380 };
        sp.Children.Add(new WrapPanel { Children = { cname, new Border { Width = 6 }, cid, new Border { Width = 6 }, spec, new Border { Width = 6 }, Act("web-cms-anlegen", "Sammlung anlegen", () => WebDesignCreateCollection(cname.Text ?? "", cid.Text ?? "", spec.Text ?? "")) } });
        sp.Children.Add(P("Feldarten: text, langtext, bild, datum, verweis>sammlung. Ein * macht ein Feld zur Pflicht.", Muted, 11.5));

        if (all.FirstOrDefault(x => x.Id == _wdCmsSel) is { } sel)
        {
            sp.Children.Add(new TextBlock { Text = $"Einträge in „{sel.Name}“", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 6, 0, 0) });
            var ei = 0;
            foreach (var entry in sel.Eintraege)
            {
                var e = entry; var en = ei++;
                var title = sel.TitelFeld is { } tf ? e.Werte.GetValueOrDefault(tf, e.Adresse) : e.Adresse;
                sp.Children.Add(new WrapPanel { Children = { P($"{(_wdCmsEntry == e.Adresse ? "▸ " : "")}{title} ({e.Adresse})", Text, 12.5), new Border { Width = 8 },
                    Act($"web-cms-eintrag-{en}", "Bearbeiten", () => { _wdCmsEntry = e.Adresse; Show("dev"); }),
                    Act($"web-cms-eintrag-weg-{en}", "Löschen", () => { _wdCmsNote = WebDesignCms.DeleteEntry(dir, sel.Id, e.Adresse) ?? "Eintrag gelöscht."; if (_wdCmsEntry == e.Adresse) _wdCmsEntry = null; WebDesignReloadIfOpen(); Show("dev"); }) } });
            }
            // Formular: Eintrag in der Standardsprache oder Übersetzung der Textfelder
            var current = sel.Eintraege.FirstOrDefault(x => x.Adresse == _wdCmsEntry);
            var langs = site.Sprachen.ToList();
            var lang = langs.Contains(_wdCmsLang ?? "") ? _wdCmsLang! : site.Standardsprache;
            var form = new StackPanel { Spacing = 4, Name = "web-cms-formular", Margin = new Thickness(0, 6, 0, 0) };
            form.Children.Add(new TextBlock { Text = current is null ? "Neuer Eintrag" : $"Eintrag „{current.Adresse}“ bearbeiten", FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Text });
            if (langs.Count > 1)
            {
                var lb = new ComboBox { Name = "web-cms-sprache", ItemsSource = langs.Select(x => $"{WebDesignI18n.LangName(x)} ({x})").ToList(), SelectedIndex = langs.IndexOf(lang), Width = 200 };
                lb.SelectionChanged += (_, _) => { if (lb.SelectedIndex >= 0 && langs[lb.SelectedIndex] != lang) { _wdCmsLang = langs[lb.SelectedIndex]; Show("dev"); } };
                form.Children.Add(lb);
            }
            var adr = new TextBox { Name = "web-cms-adresse", Text = current?.Adresse ?? "", PlaceholderText = "Adresse, z. B. bauernbrot", Width = 240, IsEnabled = current is null };
            form.Children.Add(adr);
            var inputs = new Dictionary<string, Func<string>>();
            foreach (var f in sel.Felder.Where(f => lang == site.Standardsprache || f.Art is "text" or "langtext"))
            {
                var label = $"{f.Name} ({WebDesignCms.ArtNamen[f.Art]}{(f.Pflicht ? ", Pflicht" : "")})";
                var value = current is null ? "" : lang == site.Standardsprache ? current.Werte.GetValueOrDefault(f.Name, "") : current.Sprachen.TryGetValue(lang, out var t) ? t.GetValueOrDefault(f.Name, "") : "";
                Control box;
                if (f.Art == "verweis")
                {
                    var targets = all.FirstOrDefault(x => x.Id == f.Ziel)?.Eintraege.Select(x => x.Adresse).ToList() ?? [];
                    var cb = new ComboBox { Name = $"web-cms-feld-{f.Name}", ItemsSource = targets.Prepend("– keiner –").ToList(), SelectedIndex = Math.Max(0, targets.IndexOf(value) + 1), Width = 240 };
                    inputs[f.Name] = () => cb.SelectedIndex > 0 ? targets[cb.SelectedIndex - 1] : "";
                    box = cb;
                }
                else
                {
                    var tb = new TextBox { Name = $"web-cms-feld-{f.Name}", Text = value, Width = f.Art == "langtext" ? 520 : 320, AcceptsReturn = f.Art == "langtext", TextWrapping = f.Art == "langtext" ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = f.Art == "langtext" ? 70 : 0,
                        PlaceholderText = f.Art switch { "datum" => "JJJJ-MM-TT", "bild" => "bilder/datei.jpg (aus den Medien)", _ => lang == site.Standardsprache ? "" : current?.Werte.GetValueOrDefault(f.Name, "") ?? "" } };
                    inputs[f.Name] = () => tb.Text ?? "";
                    box = tb;
                }
                form.Children.Add(new StackPanel { Children = { new TextBlock { Text = label, FontSize = 11.5, Foreground = Muted }, box } });
            }
            form.Children.Add(new WrapPanel { Children = { Act("web-cms-speichern", "Eintrag speichern", () => WebDesignSaveEntry(sel.Id, adr.Text ?? "", inputs.ToDictionary(kv => kv.Key, kv => kv.Value()), lang == site.Standardsprache ? null : lang), true),
                new Border { Width = 8 }, Act("web-cms-neu", "Neuer Eintrag", () => { _wdCmsEntry = null; Show("dev"); }) } });
            sp.Children.Add(form);
        }
        foreach (var f in WebDesignCms.Check(dir).Take(12)) sp.Children.Add(P((f.Ebene == "Fehler" ? "✗ " : "• ") + f.Text, f.Ebene == "Fehler" ? Brush.Parse("#F5A524") : Muted, 12));
        if (_wdCmsNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-cms-note", Text = _wdCmsNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-cms-card", sp);
    }

    Border Card(string name, Control child) => new() { Name = name, Padding = new Thickness(14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = child };

    // ---------- Export der ganzen Website ----------

    /// <summary>Ganze Website exportieren, Inhalt jeder Seite vorrendern (Chrome/Edge) und den Export prüfen (W3).</summary>
    public async Task WebDesignExportSiteAsync()
    {
        Trendo.Core.WebDesignExport.Result r;
        try { r = Trendo.Core.WebDesignExport.ExportSite(WebDesignDir, WebDesignEditorDir); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _wdNote = "Export abgebrochen: " + e.Message; Show("dev"); return; }
        if (r.Error is not null) { _wdNote = r.Error; Show("dev"); return; }
        _wdNote = $"Exportiert: {r.Pages.Count} Seite(n). Lege den Inhalt ins HTML …"; Show("dev");
        string? renderNote = null;
        try { (_, renderNote) = await WebsitePreview.RenderStaticAsync(r.Dir!, r.Pages.Select(p => p.RelPath).ToList()); }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or IOException or System.Net.WebSockets.WebSocketException or OperationCanceledException) { renderNote = "Vorrendern abgebrochen (" + e.Message + "). Seiten ohne vorgerenderten Inhalt laufen trotzdem, Suchmaschinen sehen den Text dort aber erst nach dem Laden."; }
        var check = Trendo.Core.WebDesignExport.CheckExport(r.Dir!, r.Pages);
        _wdExportFindings = check; _wdPlan = null; _wdConsent = null; // K8: neue Prüfung nötig
        var lines = new List<string> { $"Fertige Website: {Path.Combine(r.Dir!, "index.html")} – {r.Pages.Count} Seite(n), läuft ohne Internet." };
        if (renderNote is not null) lines.Add(renderNote);
        lines.AddRange(r.Notes.Take(6)); lines.AddRange(check.Take(10));
        _wdNote = string.Join("\n", lines);
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.export", $"{r.Pages.Count} Seiten, {check.Count} Befunde");
        Show("dev");
    }
}
