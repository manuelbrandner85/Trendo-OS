using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-03: Baukasten ohne Programmieren – Seite aus Bausteinen zusammenklicken, als JSON speichern, als HTML veröffentlichen.
/// T-0765: <paramref name="Data"/> verweist bei Daten-Bausteinen (Formular, Datenliste, Auswertung) auf die Datensammlung; bei Knöpfen steht in
/// <paramref name="Url"/> auch „seite:&lt;id&gt;“ (Sprung zu einer anderen Seite der App), bei der Auswertung „anzahl“ oder „summe:&lt;feld&gt;“.</summary>
public sealed record Block(string Kind, string Text = "", string Url = "", [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Data = null);

/// <summary>T-0765: eine Seite einer App (Bildschirm) mit fester Kennung – Links „zu Seite X“ zeigen auf die Kennung, Umbenennen bricht sie nicht.</summary>
public sealed class AppScreen
{
    public string Id { get; set; } = "start";
    public string Name { get; set; } = "Start";
    public List<Block> Blocks { get; set; } = [];
}

/// <summary>T-0765: ein Feld einer Datensammlung. <paramref name="Type"/> ist „text“ oder „zahl“.</summary>
public sealed record DataField(string Id, string Label, string Type = "text", bool Required = false);

/// <summary>T-0765: Datensammlung (z. B. „Einkäufe“). Die Einträge selbst speichert die fertige App im Browser (localStorage) – ohne Server.</summary>
public sealed class DataCollection
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<DataField> Fields { get; set; } = [];
}

public sealed class SitePage : IJsonOnDeserialized
{
    /// <summary>T-0716: Fassung von <c>seite.json</c>, die dieses Trendo schreibt und versteht. Dateien ohne Angabe (vor T-0716) gelten als Fassung 1;
    /// eine neuere Fassung wird nicht geöffnet (beim Speichern gingen sonst Inhalte verloren). Migration: Stufe für Stufe in <see cref="FromJson"/>.
    /// T-0765: Fassung 2 = App mit mehreren Seiten und Datensammlungen. Eine einfache Seite (eine Seite, keine Daten) wird weiter als Fassung 1 geschrieben –
    /// ältere Trendo-Fassungen lesen sie dann vollständig.</summary>
    public const int FormatVersion = 2;
    [JsonPropertyOrder(-1)] public int Version { get; set; } = FormatVersion;
    public string Title { get; set; } = "Meine Seite";
    public string Accent { get; set; } = "#2F86F6";
    /// <summary>T-0765: feste Kennung der App – Schlüssel der im Browser gespeicherten Einträge (ändert sich beim Umbenennen nicht).</summary>
    public string AppId { get; set; } = "";
    public List<AppScreen> Pages { get; set; } = [new AppScreen()];
    public List<DataCollection> Collections { get; set; } = [];

    /// <summary>Bausteine der ersten Seite (so hieß das Feld in Fassung 1).</summary>
    [JsonIgnore]
    public List<Block> Blocks
    {
        get { if (Pages.Count == 0) Pages.Add(new AppScreen()); return Pages[0].Blocks; }
        set { if (Pages.Count == 0) Pages.Add(new AppScreen()); Pages[0].Blocks = value ?? []; }
    }

    /// <summary>Nur zum Lesen von Fassung 1 (<c>"Blocks"</c> oben in der Datei).</summary>
    [JsonPropertyName("Blocks"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Block>? LegacyBlocks { get; set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        Pages ??= []; Collections ??= [];
        Pages.RemoveAll(p => p is null); Collections.RemoveAll(c => c is null);
        if (Pages.Count == 0) Pages.Add(new AppScreen());
        if (LegacyBlocks is { } old) { if (Pages[0].Blocks is null || Pages[0].Blocks.Count == 0) Pages[0].Blocks = old; LegacyBlocks = null; }
        Normalize();
    }

    /// <summary>T-0765 (Gegenprüfung M1, N1, N2): Daten aus einer Datei (seite.json, ZIP) sind fremd. Kennungen werden zu sicheren, eindeutigen Slugs
    /// (Seiten, Sammlungen, Felder; Verweise darauf folgen), fehlende Texte (<c>null</c>) werden leer – so entsteht nie Markup aus einer Kennung und
    /// keine NullReferenceException, auch nicht bei Fassung-1-Dateien mit <c>"Url": null</c>.</summary>
    void Normalize()
    {
        Title ??= ""; Accent ??= "#2F86F6"; AppId ??= "";
        AppId = Regex.IsMatch(AppId, "^[A-Za-z0-9-]{1,40}$") ? AppId : "";
        var pageMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenPages = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in Pages)
        {
            s.Blocks ??= []; s.Blocks.RemoveAll(b => b is null); s.Name ??= "";
            var old = s.Id ?? ""; var id = Unique(Slug(old), x => seenPages.Contains(x)); seenPages.Add(id);
            pageMap.TryAdd(old, id); s.Id = id;
        }
        var collMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var fieldMaps = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var seenColl = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in Collections)
        {
            c.Fields ??= []; c.Fields.RemoveAll(f => f is null); c.Name ??= "";
            var old = c.Id ?? ""; var id = Unique(Slug(old), x => seenColl.Contains(x)); seenColl.Add(id);
            collMap.TryAdd(old, id); c.Id = id;
            var fm = new Dictionary<string, string>(StringComparer.Ordinal); var seenF = new HashSet<string>(StringComparer.Ordinal);
            for (var k = 0; k < c.Fields.Count; k++)
            {
                var f = c.Fields[k]; var fid = Unique(Slug(f.Id ?? f.Label), x => seenF.Contains(x)); seenF.Add(fid);
                fm.TryAdd(f.Id ?? "", fid);
                c.Fields[k] = new DataField(fid, f.Label ?? "", f.Type == "zahl" ? "zahl" : "text", f.Required);
            }
            fieldMaps[id] = fm;
        }
        foreach (var s in Pages)
            for (var i = 0; i < s.Blocks.Count; i++)
            {
                var b = s.Blocks[i];
                string kind = b.Kind ?? "", text = b.Text ?? "", url = b.Url ?? "";
                string? data = b.Data is null ? null : collMap.TryGetValue(b.Data, out var cd) ? cd : Slug(b.Data);
                if (url.StartsWith("seite:", StringComparison.Ordinal)) url = "seite:" + (pageMap.TryGetValue(url[6..], out var pid) ? pid : Slug(url[6..]));
                else if (kind == "stat") url = url.StartsWith("summe:", StringComparison.Ordinal)
                    ? "summe:" + (data is not null && fieldMaps.TryGetValue(data, out var fm) && fm.TryGetValue(url[6..], out var fid) ? fid : Slug(url[6..]))
                    : "anzahl";
                s.Blocks[i] = new Block(kind, text, url, data);
            }
    }

    public static readonly (string Kind, string Label)[] Kinds =
        [("h1", "Überschrift"), ("p", "Text"), ("img", "Bild"), ("button", "Knopf mit Link"), ("list", "Liste"), ("contact", "Kontakt per E-Mail"), ("hr", "Trennlinie"),
         ("form", "Eingabe-Formular"), ("datalist", "Datenliste"), ("stat", "Auswertung")];

    /// <summary>T-0765: Bausteine, die mit einer Datensammlung arbeiten.</summary>
    public static bool IsDataKind(string kind) => kind is "form" or "datalist" or "stat";

    /// <summary>T-0765: App statt einfacher Seite – mehrere Seiten, Daten oder Seiten-Links.</summary>
    [JsonIgnore]
    public bool IsApp => Pages.Count > 1 || Collections.Count > 0 || Pages.Any(p => p.Blocks.Any(b => IsDataKind(b.Kind) || b.Url.StartsWith("seite:", StringComparison.Ordinal)));

    public void Move(int i, int delta)
    {
        var j = i + delta;
        if (i < 0 || j < 0 || i >= Blocks.Count || j >= Blocks.Count) return;
        (Blocks[i], Blocks[j]) = (Blocks[j], Blocks[i]);
    }

    // ── T-0765: Seiten ──

    /// <summary>Kennung aus einem Namen: klein, ohne Umlaute, nur a–z, 0–9 und Bindestrich (sicher in HTML-Kennungen und Adressen).</summary>
    public static string Slug(string? s)
    {
        var t = (s ?? "").Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var sb = new StringBuilder();
        foreach (var c in t) sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        var r = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return r.Length == 0 ? "x" : r.Length > 40 ? r[..40].Trim('-') : r;
    }

    static string Unique(string baseId, Func<string, bool> taken)
    {
        var id = baseId; for (var k = 2; taken(id); k++) id = $"{baseId}-{k}";
        return id;
    }

    public int PageIndex(string id) => Pages.FindIndex(p => p.Id == id);

    public AppScreen AddPage(string name, int at = -1)
    {
        name = name.Trim().Length > 0 ? name.Trim() : "Neue Seite";
        var s = new AppScreen { Id = Unique(Slug(name), id => PageIndex(id) >= 0), Name = name, Blocks = [new("h1", name)] };
        if (at < 0 || at > Pages.Count) Pages.Add(s); else Pages.Insert(at, s);
        return s;
    }

    public void RenamePage(int i, string name)
    {
        if (i < 0 || i >= Pages.Count || name.Trim().Length == 0) return;
        Pages[i].Name = name.Trim(); // Kennung bleibt: Links auf die Seite halten
    }

    public void MovePage(int i, int delta)
    {
        var j = i + delta;
        if (i < 0 || j < 0 || i >= Pages.Count || j >= Pages.Count) return;
        (Pages[i], Pages[j]) = (Pages[j], Pages[i]);
    }

    /// <summary>Seite löschen (die letzte bleibt). Knöpfe, die dorthin führten, zeigen danach auf die erste Seite.</summary>
    public bool DeletePage(int i)
    {
        if (i < 0 || i >= Pages.Count || Pages.Count == 1) return false;
        var id = Pages[i].Id; Pages.RemoveAt(i);
        foreach (var s in Pages)
            for (var k = 0; k < s.Blocks.Count; k++)
                if (s.Blocks[k].Url == "seite:" + id) s.Blocks[k] = s.Blocks[k] with { Url = "seite:" + Pages[0].Id };
        return true;
    }

    // ── T-0765: Daten ──

    public DataCollection? Collection(string? id) => Collections.FirstOrDefault(c => c.Id == id);

    public DataCollection AddCollection(string name, IEnumerable<DataField> fields)
    {
        name = name.Trim().Length > 0 ? name.Trim() : "Einträge";
        var c = new DataCollection { Id = Unique(Slug(name), id => Collection(id) is not null), Name = name };
        foreach (var f in fields) AddField(c, f.Label, f.Type, f.Required);
        if (c.Fields.Count == 0) AddField(c, "Eintrag", "text", true);
        Collections.Add(c); EnsureAppId();
        return c;
    }

    public static DataField AddField(DataCollection c, string label, string type = "text", bool required = false)
    {
        label = label.Trim().Length > 0 ? label.Trim() : "Feld";
        var f = new DataField(Unique(Slug(label), id => c.Fields.Any(x => x.Id == id)), label, type == "zahl" ? "zahl" : "text", required);
        c.Fields.Add(f); return f;
    }

    /// <summary>Felder aus Kurzschrift: „Artikel*, Menge (Zahl), Preis (Zahl)“ – Stern = Pflichtfeld, „(Zahl)“ = Zahlenfeld.</summary>
    public static List<DataField> ParseFields(string spec)
    {
        var list = new List<DataField>();
        foreach (var raw in spec.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = raw; var req = t.Contains('*'); t = t.Replace("*", "").Trim();
            var num = Regex.IsMatch(t, @"\((zahl|nummer|betrag)\)", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s*\((zahl|nummer|betrag|text)\)\s*", " ", RegexOptions.IgnoreCase).Trim();
            if (t.Length == 0) continue;
            list.Add(new DataField(Slug(t), t, num ? "zahl" : "text", req));
        }
        return list;
    }

    /// <summary>T-0765 (Gegenprüfung M3): Seiten und Datensammlungen einer anderen App anhängen – Vorhandenes bleibt unverändert, neue Kennungen werden
    /// eindeutig gemacht und Verweise (Knopf „zu Seite X“, Datensammlung der Bausteine) folgen.</summary>
    public void Absorb(SitePage other)
    {
        var collMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in other.Collections)
        {
            var id = Unique(c.Id, x => Collection(x) is not null);
            collMap[c.Id] = id;
            Collections.Add(new DataCollection { Id = id, Name = c.Name, Fields = [.. c.Fields] });
        }
        var pageMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(Pages.Select(p => p.Id), StringComparer.Ordinal);
        foreach (var s in other.Pages) { var id = Unique(s.Id, x => taken.Contains(x)); taken.Add(id); pageMap[s.Id] = id; }
        foreach (var s in other.Pages)
            Pages.Add(new AppScreen { Id = pageMap[s.Id], Name = s.Name, Blocks = [.. s.Blocks.Select(b => b with {
                Data = b.Data is not null && collMap.TryGetValue(b.Data, out var cd) ? cd : b.Data,
                Url = b.Url.StartsWith("seite:", StringComparison.Ordinal) && pageMap.TryGetValue(b.Url[6..], out var pid) ? "seite:" + pid : b.Url })] });
        if (Collections.Count > 0) EnsureAppId();
    }

    public void EnsureAppId() { if (AppId.Length == 0) AppId = Guid.NewGuid().ToString("N")[..12]; }

    // ── T-0765: Vorlagen ──

    public static readonly (string Id, string Name, string Hint)[] Templates =
        [("einkaufsliste", "Einkaufsliste", "Zwei Seiten: Liste mit Anzahl und Summe, Formular zum Hinzufügen. Speichert im Browser.")];

    public static SitePage? Template(string id) => id switch
    {
        "einkaufsliste" => ShoppingList(),
        _ => null,
    };

    static SitePage ShoppingList()
    {
        var p = new SitePage { Title = "Einkaufsliste", Accent = "#C8A04A", Pages = [] };
        p.AddCollection("Einkäufe", [new("artikel", "Artikel", "text", true), new("menge", "Menge", "zahl"), new("preis", "Preis (€)", "zahl")]);
        p.Pages.Add(new AppScreen { Id = "liste", Name = "Liste", Blocks = [
            new("h1", "Einkaufsliste"),
            new("stat", "Noch zu kaufen", "anzahl", "einkaeufe"),
            new("stat", "Summe in €", "summe:preis", "einkaeufe"),
            new("datalist", "Noch nichts auf der Liste – tippe auf „Etwas hinzufügen“.", "", "einkaeufe"),
            new("button", "Etwas hinzufügen", "seite:neu")] });
        p.Pages.Add(new AppScreen { Id = "neu", Name = "Hinzufügen", Blocks = [
            new("h1", "Etwas hinzufügen"),
            new("p", "Felder mit * müssen ausgefüllt sein."),
            new("form", "Auf die Liste", "", "einkaeufe"),
            new("button", "Zur Liste", "seite:liste")] });
        return p;
    }

    public static string SafeUrl(string u)
    {
        u = u.Trim();
        return Uri.TryCreate(u, UriKind.Absolute, out var x) && x.Scheme is "https" or "http" or "mailto" ? x.AbsoluteUri
            : !u.Contains(':') && FreeAssets.SafeRel(u) ? u : "#";
    }

    /// <summary>T-0765: Linkziel eines Knopfs – „seite:&lt;id&gt;“ wird zum Sprung auf die Seite (nur wenn es sie gibt), sonst <see cref="SafeUrl"/>.</summary>
    public string LinkHref(string u) =>
        u.StartsWith("seite:", StringComparison.Ordinal) ? (PageIndex(u[6..]) >= 0 ? "#seite-" + Slug(u[6..]) : "#") : SafeUrl(u);

    /// <summary>T-0765: Text, den die Auswertung zeigt, wenn keiner eingetragen ist („Anzahl der Einträge“, „Summe Preis“).</summary>
    public string MeasureLabel(Block b)
    {
        var c = Collection(b.Data);
        if (b.Url.StartsWith("summe:", StringComparison.Ordinal))
            return "Summe " + (c?.Fields.FirstOrDefault(f => f.Id == b.Url[6..])?.Label ?? "?");
        return "Anzahl der Einträge";
    }

    static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>T-0765 (Gegenprüfung N3): ehrlicher Satz, wo die Einträge liegen – im Baukasten und als Fußzeile der fertigen App.</summary>
    public const string StorageNote = "Deine Einträge liegen nur in diesem Browser auf diesem Gerät – kein Abgleich mit anderen Geräten. Wer die Browserdaten löscht, löscht auch die Einträge. Als Datei geöffnet (file://) können andere Seiten auf diesem Gerät sie mitlesen.";

    string Accent6 => Regex.IsMatch(Accent, "^#[0-9A-Fa-f]{6}$") ? Accent : "#2F86F6";

    string BlockHtml(Block b) => b.Kind switch
    {
        "h1" => $"<h1>{E(b.Text)}</h1>\n",
        "p" => $"<p>{E(b.Text).Replace("\n", "<br>")}</p>\n",
        "img" => $"<p><img src=\"{E(SafeUrl(b.Url))}\" alt=\"{E(b.Text)}\"></p>\n",
        "button" => $"<p><a class=\"btn\" href=\"{E(LinkHref(b.Url))}\">{E(b.Text)}</a></p>\n",
        "list" => "<ul>\n" + string.Concat(b.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => $"<li>{E(l)}</li>\n")) + "</ul>\n",
        "contact" => $"<p><a class=\"btn\" href=\"mailto:{E(Uri.EscapeDataString(b.Url.Trim()).Replace("%40", "@"))}\">{E(b.Text.Length > 0 ? b.Text : "Schreib mir")}</a></p>\n",
        "hr" => "<hr>\n",
        "form" => FormHtml(b),
        "datalist" => Collection(b.Data) is { } c ? $"<ul class=\"t-liste\" data-sammlung=\"{E(c.Id)}\" data-leer=\"{E(EmptyText(b))}\"><li class=\"t-leer\">{E(EmptyText(b))}</li></ul>\n" : "",
        "stat" => Collection(b.Data) is { } c ? $"<p class=\"t-wert\" data-sammlung=\"{E(c.Id)}\" data-mass=\"{E(b.Url.StartsWith("summe:", StringComparison.Ordinal) ? "summe:" + Slug(b.Url[6..]) : "anzahl")}\"><span>{E(b.Text.Length > 0 ? b.Text : MeasureLabel(b))}</span> <strong>0</strong></p>\n" : "",
        _ => "",
    };

    static string EmptyText(Block b) => b.Text.Length > 0 ? b.Text : "Noch keine Einträge.";

    string FormHtml(Block b)
    {
        if (Collection(b.Data) is not { } c) return "";
        var sb = new StringBuilder($"<form class=\"t-form\" data-sammlung=\"{E(c.Id)}\" novalidate>\n");
        foreach (var f in c.Fields)
            sb.Append($"<label>{E(f.Label)}{(f.Required ? " *" : "")}<input name=\"{E(f.Id)}\" {(f.Type == "zahl" ? "type=\"number\" step=\"any\" inputmode=\"decimal\"" : "type=\"text\"")}{(f.Required ? " required" : "")}></label>\n");
        sb.Append("<p class=\"t-fehler\" role=\"alert\" hidden></p>\n");
        sb.Append($"<button type=\"submit\" class=\"btn\">{E(b.Text.Length > 0 ? b.Text : "Speichern")}</button>\n</form>\n");
        return sb.ToString();
    }

    /// <summary>T-0765: Laufzeit der fertigen App – klein, eingebettet, ohne fremde Skripte, ohne eval, ohne innerHTML (alle Werte über textContent).
    /// Seitenwechsel über die Adresse (#seite-…), Einträge im Browser (localStorage, Schlüssel je App), Pflichtfelder, Anzahl und Summe.</summary>
    public static readonly string AppScript = """
(function () {
  'use strict';
  var cfgEl = document.getElementById('trendo-app');
  var app; try { app = JSON.parse(cfgEl ? cfgEl.textContent : ''); } catch (e) { return; }
  if (!app || !app.Pages) return;
  var pre = 'trendo-app/' + (app.AppId || 'app') + '/';
  function alle(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }
  function sammlung(id) { var c = app.Collections || []; for (var i = 0; i < c.length; i++) if (c[i].Id === id) return c[i]; return null; }
  function lade(id) { try { var t = window.localStorage.getItem(pre + id); var a = t ? JSON.parse(t) : []; return Array.isArray(a) ? a : []; } catch (e) { return []; } }
  function speichere(id, a) { try { window.localStorage.setItem(pre + id, JSON.stringify(a)); return true; } catch (e) { return false; } }
  function zahl(v) { var n = parseFloat(String(v == null ? '' : v).replace(',', '.')); return isFinite(n) ? n : 0; }
  var GRENZE = 1e12;
  function fmt(n) { if (!isFinite(n) || Math.abs(n) >= 1e15) return 'zu groß'; try { return n.toLocaleString('de-DE', { maximumFractionDigits: 2 }); } catch (e) { return String(n); } }
  function zeichne() {
    alle('ul.t-liste').forEach(function (ul) {
      var id = ul.getAttribute('data-sammlung'); var c = sammlung(id); var a = lade(id);
      while (ul.firstChild) ul.removeChild(ul.firstChild);
      if (!c || a.length === 0) { var leer = document.createElement('li'); leer.className = 't-leer'; leer.textContent = ul.getAttribute('data-leer') || ''; ul.appendChild(leer); return; }
      a.forEach(function (e, i) {
        var li = document.createElement('li'); var s = document.createElement('span'); var teile = [];
        c.Fields.forEach(function (f, k) { var v = e[f.Id]; if (v === undefined || v === null || v === '') return; teile.push(k === 0 ? String(v) : f.Label + ': ' + (f.Type === 'zahl' ? fmt(zahl(v)) : String(v))); });
        s.textContent = teile.join(' · ');
        var weg = document.createElement('button'); weg.type = 'button'; weg.className = 't-weg'; weg.textContent = 'Entfernen';
        weg.addEventListener('click', function () { var x = lade(id); x.splice(i, 1); speichere(id, x); zeichne(); });
        li.appendChild(s); li.appendChild(weg); ul.appendChild(li);
      });
    });
    alle('.t-wert').forEach(function (p) {
      var a = lade(p.getAttribute('data-sammlung')); var m = p.getAttribute('data-mass') || 'anzahl'; var w = a.length;
      if (m.indexOf('summe:') === 0) { var f = m.slice(6); w = 0; a.forEach(function (e) { w += zahl(e[f]); }); }
      var st = p.querySelector('strong'); if (st) st.textContent = fmt(w);
    });
  }
  alle('form.t-form').forEach(function (form) {
    form.addEventListener('submit', function (ev) {
      ev.preventDefault();
      var id = form.getAttribute('data-sammlung'); var c = sammlung(id); var fehler = form.querySelector('.t-fehler');
      if (!c) return;
      var e = {}; var fehlt = []; var gross = [];
      c.Fields.forEach(function (f) {
        var inp = form.elements.namedItem(f.Id); var v = inp && 'value' in inp ? String(inp.value).trim() : '';
        if (f.Required && v === '') fehlt.push(f.Label);
        if (f.Type === 'zahl' && v !== '' && (!isFinite(parseFloat(v.replace(',', '.'))) || Math.abs(parseFloat(v.replace(',', '.'))) > GRENZE)) gross.push(f.Label);
        e[f.Id] = f.Type === 'zahl' && v !== '' ? zahl(v) : v;
      });
      if (fehlt.length) { fehler.textContent = 'Bitte ausfüllen: ' + fehlt.join(', ') + '.'; fehler.hidden = false; return; }
      if (gross.length) { fehler.textContent = 'Diese Zahl ist zu groß oder keine Zahl: ' + gross.join(', ') + ' (höchstens 1 Billion).'; fehler.hidden = false; return; }
      e._t = Date.now();
      var a = lade(id); a.push(e);
      if (!speichere(id, a)) { fehler.textContent = 'Konnte nicht speichern – der Browser erlaubt hier keinen Speicher.'; fehler.hidden = false; return; }
      fehler.textContent = 'Gespeichert.'; fehler.hidden = false; form.reset(); zeichne();
    });
  });
  function zeige() {
    var ziel = (location.hash || '').replace(/^#seite-/, ''); var secs = alle('section[data-seite]');
    if (!secs.some(function (s) { return s.getAttribute('data-seite') === ziel; })) ziel = secs.length ? secs[0].getAttribute('data-seite') : '';
    secs.forEach(function (s) { s.hidden = s.getAttribute('data-seite') !== ziel; });
    alle('nav.t-nav a').forEach(function (l) { if (l.getAttribute('href') === '#seite-' + ziel) l.setAttribute('aria-current', 'page'); else l.removeAttribute('aria-current'); });
  }
  window.addEventListener('hashchange', function () { zeige(); window.scrollTo(0, 0); });
  window.addEventListener('storage', zeichne);
  zeige(); zeichne();
  document.documentElement.setAttribute('data-app', 'bereit');
})();
""".ReplaceLineEndings("\n"); // HTML-Parser macht aus CRLF ein LF – der Hash muss zum gelesenen Skript passen

    /// <summary>Hash des eingebetteten Skripts für die Sicherheitsregel (CSP) der fertigen App: nur genau dieses Skript darf laufen.</summary>
    public static readonly string ScriptHash = "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(AppScript)));

    /// <summary>Projekt als JSON zum Einbetten in die fertige Seite (Rundreise und Laufzeit der App). Der Standard-Kodierer maskiert &lt; &gt; &amp; ' –
    /// ein „&lt;/script&gt;“ im Inhalt kann das Skript-Element nicht schließen.</summary>
    string EmbeddedJson() => JsonSerializer.Serialize(Model(), Compact);

    public string ToHtml()
    {
        var acc = Accent6;
        var app = IsApp;
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\">\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        if (app) sb.Append($"<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src 'self' data: https: http: file:; style-src 'unsafe-inline'; script-src '{ScriptHash}'; form-action 'none'; base-uri 'none'\">");
        sb.Append($"<title>{E(Title)}</title>\n");
        sb.Append($"<style>body{{font-family:system-ui,sans-serif;max-width:760px;margin:0 auto;padding:2rem 1rem;line-height:1.55;color:#1b2333}}h1{{color:{acc}}}a.btn{{display:inline-block;background:{acc};color:#fff;padding:.6rem 1.2rem;border-radius:8px;text-decoration:none}}img{{max-width:100%;border-radius:10px}}hr{{border:0;border-top:1px solid #dde}}</style>\n");
        if (app) sb.Append($"<style>nav.t-nav{{display:flex;gap:.4rem;flex-wrap:wrap;margin-bottom:1rem}}nav.t-nav a{{padding:.35rem .8rem;border-radius:999px;border:1px solid #d5dae3;color:#1b2333;text-decoration:none}}nav.t-nav a[aria-current]{{background:{acc};border-color:{acc};color:#fff}}section[hidden]{{display:none}}form.t-form{{display:grid;gap:.7rem;max-width:420px}}form.t-form label{{display:grid;gap:.2rem;font-weight:600}}form.t-form input{{font:inherit;padding:.55rem .7rem;border:1px solid #c5ccd8;border-radius:8px}}button.btn{{font:inherit;background:{acc};color:#fff;border:0;padding:.6rem 1.2rem;border-radius:8px;cursor:pointer;justify-self:start}}.t-fehler{{margin:0;color:#8a4b00}}ul.t-liste{{list-style:none;padding:0;display:grid;gap:.4rem}}ul.t-liste li{{display:flex;justify-content:space-between;gap:.6rem;align-items:center;padding:.55rem .8rem;border:1px solid #e3e7ee;border-radius:8px}}ul.t-liste li.t-leer{{color:#6b7385;border-style:dashed}}button.t-weg{{font:inherit;font-size:.85rem;background:none;border:1px solid #c5ccd8;border-radius:6px;padding:.2rem .6rem;cursor:pointer}}p.t-wert{{display:flex;justify-content:space-between;max-width:420px;padding:.5rem .8rem;background:#f5f6f9;border-radius:8px}}p.t-wert strong{{font-size:1.2rem}}footer.t-hinweis{{margin-top:2.5rem;font-size:.8rem;color:#6b7385;border-top:1px solid #e3e7ee;padding-top:.6rem}}</style>\n");
        sb.Append("</head>\n<body>\n");
        if (!app)
            foreach (var b in Blocks) sb.Append(BlockHtml(b));
        else
        {
            if (Pages.Count > 1)
            {
                sb.Append("<nav class=\"t-nav\" aria-label=\"Seiten\">");
                foreach (var s in Pages) sb.Append($"<a href=\"#seite-{E(s.Id)}\">{E(s.Name)}</a>");
                sb.Append("</nav>\n");
            }
            foreach (var s in Pages)
            {
                sb.Append($"<section id=\"seite-{E(s.Id)}\" data-seite=\"{E(s.Id)}\" aria-label=\"{E(s.Name)}\">\n");
                foreach (var b in s.Blocks) sb.Append(BlockHtml(b));
                sb.Append("</section>\n");
            }
        }
        if (Collections.Count > 0) sb.Append("<footer class=\"t-hinweis\">").Append(E(StorageNote)).Append("</footer>\n");
        sb.Append("<script type=\"application/json\" id=\"trendo-app\">").Append(EmbeddedJson()).Append("</script>\n");
        if (app) sb.Append("<script>").Append(AppScript).Append("</script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    static readonly JsonSerializerOptions Compact = new();

    /// <summary>Gespeicherte Form: einfache Seite als Fassung 1 (Version, Title, Accent, Blocks – lesbar auch für ältere Trendo), App als Fassung 2.</summary>
    object Model() => IsApp
        ? new { Version = FormatVersion, Title, Accent, AppId, Pages, Collections }
        : new { Version = 1, Title, Accent, Blocks };

    public string ToJson() => JsonSerializer.Serialize(Model(), Indented);
    /// <summary>T-0716: absturzsicher (Zwischendatei, dann austauschen) – ein Abbruch beim Speichern hinterlässt nie eine halbe Seite.</summary>
    public void Save(string file) => AtomicFile.WriteAllText(file, ToJson());
    public static SitePage Load(string file) => FromJson(File.ReadAllText(file));

    /// <summary>Seite aus JSON. Fehlende Fassung = 1 (Dateien vor T-0716), neuere Fassung → <see cref="InvalidDataException"/> mit verständlicher Meldung.</summary>
    public static SitePage FromJson(string json)
    {
        var p = JsonSerializer.Deserialize<SitePage>(json) ?? new();
        if (p.Version <= 0) p.Version = 1;
        if (p.Version > FormatVersion)
            throw new InvalidDataException($"Diese Seite wurde mit einer neueren Trendo-Fassung gespeichert (Dateiformat {p.Version}, dieses Trendo kennt {FormatVersion}). Bitte aktualisiere Trendo – die Datei bleibt so lange unverändert.");
        // Stufe 1 → 2 (T-0765): „Blocks“ wird die erste Seite (in OnDeserialized), Sammlungen leer.
        p.Title ??= ""; p.Accent ??= "#2F86F6"; p.AppId ??= "";
        p.Version = p.IsApp ? FormatVersion : 1; // Fassung, in der diese Seite gespeichert wird (einfache Seite bleibt 1)
        if (p.IsApp) p.EnsureAppId();
        return p;
    }

    /// <summary>T-0765 (Gegenprüfung M4): Grenzen beim Wiederöffnen – gezählt werden die tatsächlich gelesenen Bytes, nicht die Angaben im ZIP-Kopf.</summary>
    public const long MaxImportHtmlBytes = 20L * 1024 * 1024, MaxImportImageBytes = 25L * 1024 * 1024, MaxImportImagesTotal = 200L * 1024 * 1024;
    public const int MaxImportEntries = 2000, MaxImportImages = 300;
    static readonly string[] ImportImageTypes = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    /// <summary>Höchstens <paramref name="max"/> Bytes lesen; mehr = verständliche Ablehnung (auch wenn der ZIP-Kopf eine kleinere Länge behauptet).</summary>
    static byte[] ReadBounded(Stream s, long max, string what)
    {
        using var ms = new MemoryStream();
        var buf = new byte[81920]; int n;
        while ((n = s.Read(buf, 0, buf.Length)) > 0)
        {
            if (ms.Length + n > max) throw new InvalidDataException($"{what} ist zu groß (mehr als {max / (1024 * 1024)} MB) – nicht geöffnet, nichts wurde geschrieben.");
            ms.Write(buf, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>T-0765 (Rundreise): App aus einer exportierten ZIP, einem fertigen Ordner oder einer index.html wieder öffnen –
    /// das Projekt steckt eingebettet in der Seite (<c>&lt;script type="application/json" id="trendo-app"&gt;</c>).
    /// Gegenprüfung M4/M5: erst alles lesen und prüfen (Größen, Anzahl, nur Bilder .png/.jpg/.gif/.webp), dann schreiben. Bilder landen unter
    /// <paramref name="assetDir"/>/<paramref name="assetPrefix"/>; eine vorhandene Datei wird nie überschrieben (gleicher Inhalt = übernehmen, sonst neuer Name);
    /// die Verweise der Bausteine folgen.</summary>
    public static SitePage FromExport(string path, string? assetDir = null, string assetPrefix = "")
    {
        Func<string, Stream?> open;
        string html;
        System.IO.Compression.ZipArchive? zip = null;
        try
        {
            if (Directory.Exists(path) || !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var file = Directory.Exists(path) ? Path.Combine(path, "index.html") : Path.GetFullPath(path);
                var root = Path.GetDirectoryName(file)!;
                if (new FileInfo(file).Length > MaxImportHtmlBytes) throw new InvalidDataException($"Die Seite ist zu groß (mehr als {MaxImportHtmlBytes / (1024 * 1024)} MB) – nicht geöffnet, nichts wurde geschrieben.");
                using (var fs = File.OpenRead(file)) html = Encoding.UTF8.GetString(ReadBounded(fs, MaxImportHtmlBytes, "Die Seite"));
                open = rel => { var f = Path.GetFullPath(Path.Combine(root, rel)); return f.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(f) ? File.OpenRead(f) : null; };
            }
            else
            {
                zip = System.IO.Compression.ZipFile.OpenRead(path);
                if (zip.Entries.Count > MaxImportEntries) throw new InvalidDataException($"Die ZIP hat mehr als {MaxImportEntries} Einträge – das ist keine Baukasten-App. Nichts wurde geschrieben.");
                var entry = zip.Entries.Where(e => e.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName.Length).FirstOrDefault()
                    ?? throw new InvalidDataException("In der ZIP fehlt die Seite index.html – das ist keine Baukasten-App.");
                if (entry.Length > MaxImportHtmlBytes) throw new InvalidDataException($"Die Seite in der ZIP ist zu groß (mehr als {MaxImportHtmlBytes / (1024 * 1024)} MB) – nicht geöffnet, nichts wurde geschrieben.");
                var prefix = entry.FullName[..^entry.Name.Length];
                using (var es = entry.Open()) html = Encoding.UTF8.GetString(ReadBounded(es, MaxImportHtmlBytes, "Die Seite in der ZIP"));
                var z = zip; open = rel => z.GetEntry(prefix + rel) is { } e2 ? (e2.Length > MaxImportImageBytes ? throw new InvalidDataException($"Das Bild „{rel}“ ist zu groß (mehr als {MaxImportImageBytes / (1024 * 1024)} MB) – nicht geöffnet, nichts wurde geschrieben.") : e2.Open()) : null;
            }
            var m = Regex.Match(html, "<script type=\"application/json\" id=\"trendo-app\">(.*?)</script>", RegexOptions.Singleline);
            if (!m.Success) throw new InvalidDataException("Diese Seite stammt nicht aus dem Baukasten (es fehlen die eingebetteten App-Daten).");
            var page = FromJson(m.Groups[1].Value);
            if (assetDir is null) return page;

            // 1) lesen und prüfen – noch nichts schreiben
            var wanted = page.Pages.SelectMany(s => s.Blocks).Where(b => b.Kind == "img" && !b.Url.Contains(':') && FreeAssets.SafeRel(b.Url)
                && ImportImageTypes.Contains(Path.GetExtension(b.Url).ToLowerInvariant())).Select(b => b.Url).Distinct(StringComparer.Ordinal).ToList();
            if (wanted.Count > MaxImportImages) throw new InvalidDataException($"Die App verweist auf mehr als {MaxImportImages} Bilder – nicht geöffnet, nichts wurde geschrieben.");
            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal); long total = 0;
            foreach (var rel in wanted)
            {
                using var s = open(rel); if (s is null) continue;
                var data = ReadBounded(s, MaxImportImageBytes, $"Das Bild „{rel}“");
                total += data.Length;
                if (total > MaxImportImagesTotal) throw new InvalidDataException($"Die Bilder sind zusammen zu groß (mehr als {MaxImportImagesTotal / (1024 * 1024)} MB) – nicht geöffnet, nichts wurde geschrieben.");
                bytes[rel] = data;
            }
            // 2) schreiben – nie eine vorhandene Datei überschreiben
            var pre = assetPrefix.Trim('/', '\\').Replace('\\', '/');
            if (pre.Length > 0 && !FreeAssets.SafeRel(pre)) throw new ArgumentException("Unsicherer Zielordner für Bilder.", nameof(assetPrefix));
            var moved = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (rel, data) in bytes)
            {
                var target = pre.Length > 0 ? pre + "/" + rel : rel;
                var dst = Path.Combine(assetDir, target);
                for (var k = 2; File.Exists(dst) && !File.ReadAllBytes(dst).AsSpan().SequenceEqual(data); k++)
                {
                    target = (Path.GetDirectoryName(pre.Length > 0 ? pre + "/" + rel : rel) is { Length: > 0 } d ? d.Replace('\\', '/') + "/" : "") + Path.GetFileNameWithoutExtension(rel) + "-" + k + Path.GetExtension(rel);
                    dst = Path.Combine(assetDir, target);
                }
                if (!File.Exists(dst)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.WriteAllBytes(dst, data); }
                moved[rel] = target;
            }
            foreach (var s in page.Pages)
                for (var i = 0; i < s.Blocks.Count; i++)
                    if (s.Blocks[i].Kind == "img" && moved.TryGetValue(s.Blocks[i].Url, out var to)) s.Blocks[i] = s.Blocks[i] with { Url = to };
            return page;
        }
        catch (JsonException e) { throw new InvalidDataException("Die App-Daten in der Seite sind beschädigt: " + e.Message, e); }
        finally { zip?.Dispose(); }
    }

    /// <summary>Schreibt index.html (und kopiert lokale Bilder mit) in den Ausgabeordner.</summary>
    public string Publish(string outDir, string? assetDir = null)
    {
        Directory.CreateDirectory(outDir);
        if (assetDir is not null)
            foreach (var b in Pages.SelectMany(s => s.Blocks).Where(b => b.Kind == "img" && !b.Url.Contains(':') && FreeAssets.SafeRel(b.Url)))
            {
                var src = Path.Combine(assetDir, b.Url); var dst = Path.Combine(outDir, b.Url);
                if (File.Exists(src)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Copy(src, dst, true); }
            }
        var p = Path.Combine(outDir, "index.html"); AtomicFile.WriteAllText(p, ToHtml()); return p; // T-0716: atomar (UTF-8 ohne BOM wie vorher)
    }
}
