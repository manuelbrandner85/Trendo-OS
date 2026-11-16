using System.Globalization;
using System.Text;
using Trendo.Core.Business;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-PRODUCTPHOTO-01 Produktfotos für Shops: Bildmaße gegen die Vorgabe eines Marktplatzes prüfen und sagen, was zu tun ist.
/// Bearbeitet werden die Fotos im Studio.</summary>
public sealed class ProduktfotoVorgabeAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-PRODUCTPHOTO-01";
    public string ActionId => "produktfoto.vorgabe";
    public string Label => "Foto gegen Marktplatz-Vorgabe prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ShopStudiosTests.Produktfoto_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["marktplatz"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "marktplatz") ? [] : [new("marktplatz", "marktplatz", "Für welchen Marktplatz? Schreib z. B. „Marktplatz: amazon“, dazu „Breite: 1200“, „Hoehe: 900“, „Format: jpg“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "marktplatz") && ProduktFotos.Vorgaben.All(v => v.Id != FieldKit.Text(parameters, "marktplatz").ToLowerInvariant()))
            f.Add(new("marktplatz", "Marktplatz: " + string.Join(", ", ProduktFotos.Vorgaben.Select(v => v.Id)) + ".", true));
        foreach (var k in new[] { "breite", "hoehe", "kb" })
            if (FieldKit.Has(parameters, k) && (!int.TryParse(FieldKit.Text(parameters, k), out var n) || n <= 0)) f.Add(new(k, $"„{k}“ als ganze Zahl, z. B. 1600.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters; var v = ProduktFotos.Vorgabe(FieldKit.Text(p, "marktplatz").ToLowerInvariant());
        var sb = new StringBuilder();
        sb.Append("Vorgabe ").Append(v.Name).Append(" (Stand ").Append(ProduktFotos.Stand).Append("): ").Append(v.Hinweis).Append('\n');
        sb.Append("Quelle: ").Append(v.Quelle).Append(" – vor dem Hochladen in der Hilfe des Marktplatzes gegenprüfen.\n");
        sb.Append(CultureInfo.InvariantCulture, $"Zielgröße im Studio: {v.Breite} × {v.Hoehe} px\n");
        var ok = true;
        if (int.TryParse(FieldKit.Text(p, "breite"), out var w) && int.TryParse(FieldKit.Text(p, "hoehe"), out var h))
        {
            int lang = Math.Max(w, h), kurz = Math.Min(w, h);
            var size = lang >= v.MinLangeSeite && kurz >= v.MinKurzeSeite;
            ok &= size;
            sb.Append(CultureInfo.InvariantCulture, $"Größe {w} × {h} px: ").Append(size ? "erfüllt" : $"zu klein – längste Seite ab {v.MinLangeSeite} px, kürzeste ab {v.MinKurzeSeite} px").Append('\n');
            if (Math.Abs(w / (double)h - v.Breite / (double)v.Hoehe) > 0.02) sb.Append(CultureInfo.InvariantCulture, $"Seitenverhältnis: empfohlen {v.Breite} × {v.Hoehe} – im Studio mittig mit Rand zuschneiden.\n");
        }
        if (FieldKit.Has(p, "format"))
        {
            var fmt = FieldKit.Text(p, "format").TrimStart('.').ToLowerInvariant().Replace("jpeg", "jpg");
            var fo = v.Formate.Contains(fmt); ok &= fo;
            sb.Append("Format ").Append(fmt.ToUpperInvariant()).Append(": ").Append(fo ? "erlaubt" : "nicht erlaubt – erlaubt: " + string.Join(", ", v.Formate)).Append('\n');
        }
        if (int.TryParse(FieldKit.Text(p, "kb"), out var kb))
        {
            var so = kb * 1024L <= v.MaxBytes; ok &= so || v.Id == "etsy";
            sb.Append(CultureInfo.InvariantCulture, $"Dateigröße {kb} KB: ").Append(so ? "passt" : $"zu groß – höchstens {v.MaxBytes / 1024 / 1024} MB").Append('\n');
        }
        if (v.WeissPflicht) sb.Append("Hauptbild: Hintergrund reinweiß (255/255/255) – im Studio „Freistellen“ und Hintergrund #FFFFFF; Produkt füllt mindestens 85 %.\n");
        if (!v.WasserzeichenErlaubt) sb.Append("Kein Wasserzeichen und kein Text im Bild.\n");
        var qa = new QaResult(ActionId, ok, ok ? "Erfüllt die angegebenen Punkte." : "Mindestens ein Pflichtpunkt ist nicht erfüllt.", "px", v.Id, "ProduktFotos");
        return FieldKit.Save(call, FieldKit.Slot("produktfoto", call.Version, "vorgabe.txt"), sb.ToString(), $"{v.Name}: {(ok ? "passt" : "nachbessern")}", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Hochgeladen wird von dir – Trendo bearbeitet und prüft nur.");
}

/// <summary>STUDIO-REVIEWS-01 Kundenmeinungen: echte Bewertungen auswerten (Sterne, Themen, Stimmung) und Antwort-Entwürfe schreiben.
/// Erzeugt keine Bewertungen und sendet nichts.</summary>
public sealed class KundenmeinungAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-REVIEWS-01";
    public string ActionId => "kundenmeinung.auswerten";
    public string Label => "Bewertungen auswerten";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ShopStudiosTests.Kundenmeinung_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["bewertungen"];
    public IReadOnlyList<string> OutputKinds => ["md"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var f = FieldKit.Read(prompt, known);
        if (Kundenmeinungen.FragtNachFake(prompt)) f["_fake"] = "ja";
        return new(ActionId, f);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "bewertungen") || FieldKit.Has(parameters, "_fake") ? [] : [new("bewertungen", "bewertungen", "Füge die Bewertungen ein: „Bewertungen:“ und darunter je Zeile Sterne und Text, z. B. „5 | Schnelle Lieferung“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "_fake")) f.Add(new("bewertungen", "Trendo erzeugt keine Bewertungen – erfundene oder gekaufte Bewertungen sind verboten. Ich helfe gern beim Auswerten und Beantworten echter Bewertungen.", true));
        else if (FieldKit.Has(parameters, "bewertungen") && Lesen(FieldKit.Text(parameters, "bewertungen")).Count == 0) f.Add(new("bewertungen", "Keine Bewertung erkannt – je Zeile Sterne (1–5) und Text, z. B. „4 | Gute Qualität“.", true));
        return f;
    }

    static List<Kundenmeinung> Lesen(string raw)
    {
        // eine Bewertung je Zeile: „5 | Text“ – oder Absätze (Leerzeile dazwischen)
        var text = string.Join("\n\n", raw.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0));
        return Kundenmeinungen.TextLesen(text).Neu;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var s = new MeinungsSammlung { Firma = FieldKit.Text(p, "firma") };
        Kundenmeinungen.Uebernehmen(s, Lesen(FieldKit.Text(p, "bewertungen")));
        var heute = Kundenmeinungen.DatumAus(FieldKit.Text(p, "stichtag")) is { } d ? DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateTime.Today;
        var sb = new StringBuilder(Kundenmeinungen.Bericht(s, heute));
        var worst = s.Meinungen.OrderBy(m => m.Sterne).FirstOrDefault();
        if (worst is not null)
        {
            var ton = FieldKit.Has(p, "ton") ? FieldKit.Text(p, "ton").ToLowerInvariant() : "freundlich";
            sb.Append("\n## Antwort-Entwurf auf die kritischste Bewertung (").Append(worst.Sterne).Append(" Sterne)\n\n").Append(Kundenmeinungen.Entwurf(worst, s.Firma, ton)).Append("\n\n_Entwurf – veröffentlicht wird er von dir auf der Plattform._\n");
        }
        var st = Kundenmeinungen.Statistik(s.Meinungen);
        var qa = new QaResult(ActionId, st.Anzahl > 0, $"{st.Anzahl} Bewertungen, Schnitt {st.Schnitt.ToString("0.00", CultureInfo.InvariantCulture)}.", "Sterne", null, "Kundenmeinungen");
        return FieldKit.Save(call, FieldKit.Slot("kundenmeinungen", call.Version, "auswertung.md"), sb.ToString(), $"{st.Anzahl} Bewertungen, {st.Schnitt.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"))} Sterne", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Antworten veröffentlichst du selbst – Trendo sendet nichts und erzeugt keine Bewertungen.");
}

/// <summary>STUDIO-MARKET-01 Marktanalyse: Marktgröße TAM/SAM/SOM mit nachvollziehbarer Rechnung; jede Zahl mit Quelle oder als Annahme gekennzeichnet.</summary>
public sealed class MarktgroesseAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-MARKET-01";
    public string ActionId => "markt.groesse";
    public string Label => "Marktgröße rechnen (TAM/SAM/SOM)";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ShopStudiosTests.Markt_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["kunden", "umsatz"];
    public IReadOnlyList<string> OutputKinds => ["md"];

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> p)
    {
        var q = new List<ClarificationQuestion>();
        if (!FieldKit.Has(p, "kunden")) q.Add(new("kunden", "kunden", "Wie viele mögliche Kunden gibt es? Schreib z. B. „Kunden: 2.000.000“ und „Quelle-Kunden: Statistikamt 2025, https://…“ (oder „Annahme“).", true));
        if (!FieldKit.Has(p, "umsatz")) q.Add(new("umsatz", "umsatz", "Wie viel gibt ein Kunde im Jahr aus? Schreib z. B. „Umsatz: 120“ und „Quelle-Umsatz: …“ (oder „Annahme“).", true));
        return q;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> p)
    {
        var f = new List<ValidationFinding>();
        foreach (var k in new[] { "kunden", "umsatz", "sam", "som" })
            if (FieldKit.Has(p, k) && Num(FieldKit.Text(p, k)) is null) f.Add(new(k, $"„{k}“ als Zahl, z. B. 1.500.000 oder 12,5.", true));
        foreach (var k in new[] { "sam", "som" })
            if (Num(FieldKit.Text(p, k)) is { } v && v is < 0 or > 100) f.Add(new(k, "Anteil in Prozent zwischen 0 und 100.", true));
        return f;
    }

    public static double? Num(string s)
    {
        var t = (s ?? "").Replace("%", "").Replace("€", "").Replace(" ", "").Trim(); if (t.Length == 0) return null;
        if (t.Contains(',')) t = t.Replace(".", "").Replace(',', '.');
        else if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d{1,3}(\.\d{3})+$")) t = t.Replace(".", "");
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    static Kennzahl K(Marktmappe m, IReadOnlyDictionary<string, string> p, string key)
    {
        var k = new Kennzahl(Num(FieldKit.Text(p, key)));
        var src = FieldKit.Text(p, "quelle-" + key);
        if (src.Length == 0) return k;
        if (src.Equals("annahme", StringComparison.OrdinalIgnoreCase) || src.StartsWith("annahme", StringComparison.OrdinalIgnoreCase)) { k.Annahme = true; return k; }
        var url = src.Split(' ', ',').FirstOrDefault(x => x.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? "";
        var q = new MarktQuelle { Id = "q" + (m.Quellen.Count + 1), Titel = src.Replace(url, "").Trim().Trim(',', ' '), Url = url, Art = "Statistik" };
        if (q.Titel.Length == 0) q.Titel = url;
        m.Quellen.Add(q); k.QuelleId = q.Id; return k;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var m = new Marktmappe { Thema = FieldKit.Text(p, "thema"), Region = FieldKit.Has(p, "region") ? FieldKit.Text(p, "region") : "Deutschland", EigenerPreis = Num(FieldKit.Text(p, "preis")) ?? 0 };
        m.Groesse = new Marktgroesse { Kunden = K(m, p, "kunden"), UmsatzJeKunde = K(m, p, "umsatz"), SamAnteil = K(m, p, "sam"), SomAnteil = K(m, p, "som") };
        var g = Marktanalyse.Groesse(m);
        var sb = new StringBuilder("# Marktgröße").Append(m.Thema.Length > 0 ? ": " + m.Thema : "").Append("\n\n");
        foreach (var z in g.Zeilen) sb.Append("- ").Append(z).Append('\n');
        if (g.Fehlt.Count > 0) { sb.Append("\n## Offen\n\n"); foreach (var x in g.Fehlt) sb.Append("- ⚠ ").Append(x).Append('\n'); }
        if (m.Quellen.Count > 0) { sb.Append("\n## Quellen\n\n"); for (var i = 0; i < m.Quellen.Count; i++) sb.Append($"[Q{i + 1}] ").Append(m.Quellen[i].Titel).Append(m.Quellen[i].Url.Length > 0 ? " – " + m.Quellen[i].Url : "").Append('\n'); }
        sb.Append("\n_Keine erfundenen Zahlen: was ohne Quelle ist, steht als „Quelle fehlt“ da._\n");
        var qa = new QaResult(ActionId, g.Tam is not null, g.Tam is null ? "Kunden und Umsatz je Kunde fehlen." : $"TAM {Marktanalyse.Zahl(g.Tam.Value)}, {g.Fehlt.Count} offene Punkte.", m.Waehrung, null, "Marktanalyse");
        return FieldKit.Save(call, FieldKit.Slot("marktanalyse", call.Version, "marktgroesse.md"), sb.ToString(), g.Tam is null ? "nicht berechenbar" : $"TAM {Marktanalyse.Zahl(g.Tam.Value)} {m.Waehrung}", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts veröffentlicht – die Analyse bleibt bei dir.");
}
