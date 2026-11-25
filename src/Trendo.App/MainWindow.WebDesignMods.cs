using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>A4 im Studio „Webseite bauen“: Mods prüfen, nach Zustimmung ins Projekt holen, Vorlagen einfügen, Aktionen ausführen, entfernen.</summary>
public partial class MainWindow
{
    string _wdModNote = "";
    WebDesignMods.Prepared? _wdModPrep;
    public string WebDesignModNote => _wdModNote;

    /// <summary>Mod prüfen und zur Zustimmung zeigen. Nichts wird installiert.</summary>
    public void WebDesignModCheck(string source)
    {
        WebDesignMods.Cleanup(_wdModPrep);
        _wdModPrep = WebDesignMods.Prepare(source);
        _wdModNote = _wdModPrep.Mod is { } m ? $"Geprüft: „{m.Name}“ {m.Version}. Lies Beschreibung, Rechte und Lizenz und stimme zu, wenn du sie installieren willst." : "Nicht installierbar: " + _wdModPrep.Error;
        Show("dev");
    }

    /// <summary>Zustimmen und installieren (nur für genau die geprüfte Mod).</summary>
    public void WebDesignModInstall(bool zustimmung)
    {
        if (_wdModPrep is not { } prep) { _wdModNote = "Erst eine Mod prüfen."; Show("dev"); return; }
        var err = WebDesignMods.Install(WebDesignDir, prep, zustimmung ? prep.Fingerprint : null);
        _wdModNote = err ?? $"„{prep.Mod!.Name}“ ist im Projekt installiert (Ordner mods/{prep.Mod.Id}). Die KI kann ihre Aktionen jetzt nutzen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.mod.installieren", err ?? prep.Mod!.Id);
        if (err is null) { WebDesignMods.Cleanup(prep); _wdModPrep = null; }
        Show("dev");
    }

    public void WebDesignModInsert(string modId, string template)
    {
        if (WebDesignMods.Installed(WebDesignDir).FirstOrDefault(m => m.Id == modId) is not { } mod) { _wdModNote = "Diese Mod ist nicht installiert."; Show("dev"); return; }
        var (json, err) = WebDesignMods.InsertTemplate(WebDesignCurrent(), mod, template);
        if (json is null) { _wdModNote = err!; Show("dev"); return; }
        WebDesignKeepDraft("Vor Mod-Vorlage");
        WebDesignStates.SaveContent(WebDesignDir, "Vor Mod-Vorlage", WebDesignStates.Read(WebDesignDir, _wdVariant), true, _wdVariant);
        var werr = WebDesignStates.Write(WebDesignDir, json, _wdVariant);
        _wdModNote = werr ?? $"Vorlage „{template}“ aus „{mod.Name}“ eingefügt.";
        if (werr is null) WebDesignReload(WebDesignStates.Read(WebDesignDir, _wdVariant));
        Show("dev");
    }

    /// <summary>Mod-Aktion selbst ausführen: derselbe Weg wie bei der KI (Schritte werden zu geprüften Aktionen).</summary>
    public void WebDesignModRun(string modId, string action, IReadOnlyDictionary<string, string> werte)
    {
        var plan = new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = $"mod:{modId}/{action}", ["werte"] = new JsonObject(werte.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))) }) }.ToJsonString();
        var (expanded, err) = WebDesignMods.Expand(plan, WebDesignMods.Installed(WebDesignDir));
        if (expanded is null) { _wdModNote = err!; Show("dev"); return; }
        WebDesignHumanAction(expanded);
        _wdModNote = _wdNote;
        Show("dev");
    }

    public void WebDesignModRemove(string id) { _wdModNote = WebDesignMods.Uninstall(WebDesignDir, id) ?? "Mod entfernt. Bereits eingefügte Bausteine bleiben auf der Seite; Bilder aus der Mod fehlen dort jetzt und lassen sich in den Medien ersetzen."; Show("dev"); }

    Control WebDesignModsPane()
    {
        var sp = new StackPanel { Spacing = 6, Name = "web-mods" };
        sp.Children.Add(new TextBlock { Text = "Mods", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Erweiterungen mit Vorlagen, Aktionen oder Fachwissen für die KI. Mods enthalten nur Daten, keinen Programmcode, und liegen nur in diesem Projekt. Installiert wird erst nach deiner Zustimmung.", Muted, 12));
        var src = new TextBox { Name = "web-mod-quelle", PlaceholderText = "Mod-Ordner oder ZIP-Datei auf diesem PC", Width = 380 };
        var pick = Act("web-mod-waehlen", "ZIP wählen …", async () =>
        {
            var files = await PickOpenFiles(new FilePickerOpenOptions { Title = "Mod wählen", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("Mod (ZIP)") { Patterns = ["*.zip"] }] });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } lp) src.Text = lp;
        });
        sp.Children.Add(new WrapPanel { Children = { src, new Border { Width = 6 }, pick, new Border { Width = 6 }, Act("web-mod-pruefen", "Prüfen", () => WebDesignModCheck(src.Text ?? "")) } });
        if (_wdModPrep?.Mod is { } m)
        {
            var info = new StackPanel { Spacing = 3, Name = "web-mod-zustimmung", Margin = new Thickness(0, 4, 0, 0) };
            info.Children.Add(new TextBlock { Text = $"{m.Name} {m.Version}{(m.Autor.Length > 0 ? " · " + m.Autor : "")} · Lizenz {m.Lizenz}", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13 });
            info.Children.Add(P(m.Beschreibung, Text, 12.5));
            info.Children.Add(P("Rechte: " + string.Join("; ", m.Rechte.Select(r => WebDesignMods.RightNames[r])), Muted, 12));
            info.Children.Add(P($"Inhalt: {m.Vorlagen.Count} Vorlage(n), {m.Aktionen.Count} Aktion(en), {m.Skills.Count} Skill(s), {m.Dateien.Count} Dateien, {m.Bytes / 1024.0:0.#} KB", Muted, 12));
            foreach (var a in m.Aktionen.Take(8)) info.Children.Add(P($"• Aktion {a.Name}: {a.Beschreibung}", Muted, 11.5));
            info.Children.Add(new WrapPanel { Children = { Act("web-mod-ja", "Ja, diese Mod ins Projekt holen", () => WebDesignModInstall(true), true), new Border { Width = 8 }, Act("web-mod-nein", "Abbrechen", () => { WebDesignMods.Cleanup(_wdModPrep); _wdModPrep = null; _wdModNote = ""; Show("dev"); }) } });
            sp.Children.Add(info);
        }
        var mi = 0;
        foreach (var mod in WebDesignMods.Installed(WebDesignDir))
        {
            var md = mod; var n = mi++;
            sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = { new TextBlock { Text = $"{md.Name} {md.Version} · {md.Lizenz}", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, new Border { Width = 8 }, Act($"web-mod-weg-{n}", "Entfernen", () => WebDesignModRemove(md.Id)) } });
            var ti = 0;
            foreach (var t in md.Vorlagen) { var tt = t; sp.Children.Add(new WrapPanel { Children = { P($"Vorlage „{tt.Name}“{(tt.Beschreibung.Length > 0 ? ": " + tt.Beschreibung : "")}", Muted, 12), new Border { Width = 8 }, Act($"web-mod-vorlage-{n}-{ti++}", "Einfügen", () => WebDesignModInsert(md.Id, tt.Name)) } }); }
            foreach (var a in md.Aktionen) sp.Children.Add(P($"Aktion „{a.Name}“ (für die KI und unter „Mit KI bauen“ nutzbar): {a.Beschreibung}", Muted, 12));
            foreach (var s in md.Skills.Keys) sp.Children.Add(P($"Skill „{s}“ geht als Fachwissen an die KI.", Muted, 12));
        }
        if (_wdModNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-mod-note", Text = _wdModNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-mods-card", sp);
    }

    // ---------- K7: eigenes Supabase-Projekt (optional) ----------
    string _wdDataNote = "";

    public void WebDesignSupabaseSave(string url, string key)
    {
        var err = WebDesignData.SetSupabase(WebDesignDir, url, key);
        _wdDataNote = err ?? "Supabase ist verbunden. Einträge der Datenlisten gehen zusätzlich an dein Projekt. Bitte dort einmal das SQL ausführen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.daten.supabase", err is null ? "verbunden" : "abgelehnt");
        Show("dev");
    }

    public void WebDesignSupabaseRemove() { WebDesignData.RemoveSupabase(WebDesignDir); _wdDataNote = "Supabase ist getrennt. Alles bleibt auf dem Gerät der Besucher."; Show("dev"); }

    /// <summary>SQL für alle Datenlisten der Website (alle Seiten).</summary>
    public string WebDesignSupabaseSqlText()
    {
        var lists = new List<System.Text.Json.Nodes.JsonObject>();
        var std = WebDesignSite.Load(WebDesignDir).Standardsprache;
        foreach (var slug in WebDesignSite.Pages(WebDesignDir))
            try { var f = WebDesignSite.FileOf(WebDesignDir, slug, std, std); if (File.Exists(f) && System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f)) is { } doc) lists.AddRange(WebDesignSeo.Blocks(doc, "Datenliste")); }
            catch (Exception e) when (e is System.Text.Json.JsonException or IOException) { }
        return WebDesignData.SupabaseSql(lists);
    }

    Control WebDesignDataPane()
    {
        var sp = new StackPanel { Spacing = 6, Name = "web-daten" };
        sp.Children.Add(new TextBlock { Text = "Daten der Website", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Datenlisten und Anmeldung speichern auf dem Gerät der Besucher. Optional zusätzlich in deinem eigenen Supabase-Projekt – nur mit dem öffentlichen Schlüssel, nie mit dem geheimen.", Muted, 12));
        var cur = WebDesignData.Load(WebDesignDir);
        var url = new TextBox { Name = "web-supabase-url", PlaceholderText = "https://deinprojekt.supabase.co", Width = 300, Text = cur?.Url ?? "" };
        var key = new TextBox { Name = "web-supabase-key", PlaceholderText = "öffentlicher Schlüssel (sb_publishable_… oder anon)", Width = 300, Text = cur?.Schluessel ?? "" };
        sp.Children.Add(new WrapPanel { Children = { url, new Border { Width = 6 }, key } });
        var row = new WrapPanel { Children = { Act("web-supabase-speichern", "Verbinden", () => WebDesignSupabaseSave(url.Text ?? "", key.Text ?? ""), true) } };
        if (cur is not null)
        {
            row.Children.Add(new Border { Width = 8 }); row.Children.Add(Act("web-supabase-trennen", "Trennen", WebDesignSupabaseRemove));
            row.Children.Add(new Border { Width = 8 }); row.Children.Add(Act("web-supabase-sql", "SQL für Supabase zeigen", () => { _wdDataNote = "SQL für den SQL-Editor deines Supabase-Projekts (Trendo führt nichts aus):"; _wdDataSql = WebDesignSupabaseSqlText(); Show("dev"); }));
        }
        sp.Children.Add(row);
        if (_wdDataNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-daten-note", Text = _wdDataNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        if (_wdDataSql.Length > 0 && cur is not null) sp.Children.Add(new TextBox { Name = "web-supabase-sqltext", Text = _wdDataSql, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11.5, MaxHeight = 220 });
        return Card("web-daten-card", sp);
    }
    string _wdDataSql = "";
}
