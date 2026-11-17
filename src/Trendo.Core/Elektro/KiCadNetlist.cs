using System.Globalization;
using System.Text;

namespace Trendo.Core.Elektro;

/// <summary>S-Ausdruck (Lisp-artige Klammerschreibweise von KiCad): Atom oder Liste.</summary>
public sealed class SExpr
{
    public string? Atom { get; init; }
    public List<SExpr> Items { get; } = [];
    public string Head => Items.Count > 0 && Items[0].Atom is { } a ? a : "";
    public IEnumerable<SExpr> All(string head) => Items.Where(i => i.Head == head);
    public SExpr? First(string head) => Items.FirstOrDefault(i => i.Head == head);
    /// <summary>Wert einer Unterliste „(name wert)“, sonst null.</summary>
    public string? Value(string head) => First(head) is { Items.Count: >= 2 } x ? x.Items[1].Atom : null;

    public static SExpr Parse(string text)
    {
        var i = 0; SkipWs(text, ref i);
        if (i >= text.Length || text[i] != '(') throw new FormatException("Die Datei beginnt nicht mit „(“ – keine KiCad-Datei.");
        var root = ReadList(text, ref i, 0);
        return root;
    }

    static void SkipWs(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static SExpr ReadList(string s, ref int i, int depth)
    {
        if (depth > 200) throw new FormatException("Die Datei ist zu tief verschachtelt.");
        i++; var list = new SExpr();
        while (true)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("Die Datei endet mitten in einer Klammer – unvollständig.");
            var c = s[i];
            if (c == ')') { i++; return list; }
            if (c == '(') { list.Items.Add(ReadList(s, ref i, depth + 1)); continue; }
            if (c == '"')
            {
                var sb = new StringBuilder(); i++;
                while (i < s.Length && s[i] != '"') { if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] }); } else sb.Append(s[i]); i++; }
                if (i >= s.Length) throw new FormatException("Ein Text in Anführungszeichen ist nicht abgeschlossen.");
                i++; list.Items.Add(new SExpr { Atom = sb.ToString() }); continue;
            }
            var start = i; while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '(' && s[i] != ')') i++;
            list.Items.Add(new SExpr { Atom = s[start..i] });
        }
    }
}

/// <summary>Ergebnis des Imports: Schaltung plus ehrliche Liste dessen, was nicht übernommen werden konnte.
/// <see cref="Notes"/>: übernommen, aber auffällig (offenes Netz mit nur einem Anschluss, unlesbarer Wert → Standardwert).</summary>
public sealed record KiCadImportResult(Circuit Circuit, int Parts, int Nets, List<string> Skipped, List<string>? Notes = null)
{
    public string Text => $"{Parts} Bauteile und {Nets} Netze aus KiCad übernommen" + (Skipped.Count == 0 ? "." : $" – nicht übernommen: {string.Join("; ", Skipped)}.")
        + (Notes is { Count: > 0 } n ? $" Hinweis: {string.Join("; ", n.Take(8))}{(n.Count > 8 ? $" (und {n.Count - 8} weitere)" : "")}." : "");
}

/// <summary>T-0748 (SPEC-02): Import einer KiCad-Netzliste (.net, „KiCad-S-Ausdruck“ ab KiCad 5, Dateien mit „(export (version …“).
/// Bauteile werden über Bibliothek/Bauteilname und Bezeichner erkannt (R, C, L, D, LED, Q NPN/PNP, Batterie/Spannungsquelle, Schalter),
/// Anschlüsse über Pin-Funktion (A/K, B/C/E, +/−) oder die Pin-Nummer. Der Schaltplan wird neu angeordnet: Bauteile nebeneinander,
/// darunter je Netz eine Sammelleitung, jeder Anschluss mit einem eigenen senkrechten Draht – Drähte kreuzen sich nur, ohne sich zu verbinden.</summary>
public static class KiCadNetlist
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const int Slot = 10, PartY = 0, FirstBus = 4, BusGap = 2;

    sealed record Comp(string Ref, string Value, string Lib, string PartName);
    sealed record PinRef(string Ref, string Pin, string Function);

    public static KiCadImportResult Import(string text, string name = "KiCad-Import")
    {
        var root = SExpr.Parse(text);
        if (root.Head != "export") throw new FormatException("Das ist keine KiCad-Netzliste (erwartet „(export …“). Im KiCad-Schaltplan: Datei → Exportieren → Netzliste → KiCad.");
        var comps = (root.First("components")?.All("comp") ?? []).Select(c => new Comp(c.Value("ref") ?? "", c.Value("value") ?? "",
            c.First("libsource")?.Value("lib") ?? "", c.First("libsource")?.Value("part") ?? "")).Where(c => c.Ref.Length > 0).ToList();
        // Pin-Namen aus den Bibliotheksteilen (ältere Netzlisten haben am Knoten keine „pinfunction“)
        var libPins = new Dictionary<(string, string), Dictionary<string, string>>();
        foreach (var lp in root.First("libparts")?.All("libpart") ?? [])
            libPins[(lp.Value("lib") ?? "", lp.Value("part") ?? "")] = (lp.First("pins")?.All("pin") ?? []).Where(p => p.Value("num") is not null)
                .GroupBy(p => p.Value("num")!).ToDictionary(g => g.Key, g => g.First().Value("name") ?? "");
        var nets = new List<(string Name, List<PinRef> Pins)>();
        foreach (var n in root.First("nets")?.All("net") ?? [])
        {
            var pins = n.All("node").Select(x => new PinRef(x.Value("ref") ?? "", x.Value("pin") ?? "", x.Value("pinfunction") ?? "")).Where(p => p.Ref.Length > 0).ToList();
            nets.Add((n.Value("name") ?? "", pins));
        }
        if (comps.Count == 0) throw new FormatException("Die Netzliste enthält keine Bauteile.");
        // T-0748-fix: doppelte Bezeichner („R?“) – die Netze lassen sich dann keinem Bauteil zuordnen
        var dup = comps.GroupBy(x => x.Ref).Where(g => g.Count() > 1).Select(g => $"„{g.Key}“ ({g.Count()}-mal)").ToList();
        if (dup.Count > 0)
            throw new FormatException($"Die Schaltung ist nicht annotiert: Bezeichner {string.Join(", ", dup.Take(5))}{(dup.Count > 5 ? $" und {dup.Count - 5} weitere" : "")} kommen mehrfach vor. "
                + "In KiCad zuerst „Schaltplan annotieren“ (Menü Werkzeuge) ausführen und die Netzliste neu exportieren.");

        var c = new Circuit { Name = name }; var skipped = new List<string>(); var notes = new List<string>();
        // T-0748-fix: Verzeichnisse je Anschluss (linear statt jede Suche über alle Netze)
        var netOf = new Dictionary<(string Ref, string Pin), int>(); var fnOf = new Dictionary<(string Ref, string Pin), string>(); var pinsOf = new Dictionary<string, List<string>>();
        for (var k = 0; k < nets.Count; k++)
            foreach (var p in nets[k].Pins)
            {
                if (netOf.TryAdd((p.Ref, p.Pin), k)) { if (!pinsOf.TryGetValue(p.Ref, out var l)) pinsOf[p.Ref] = l = []; l.Add(p.Pin); }
                else netOf[(p.Ref, p.Pin)] = k;
                if (p.Function.Length > 0) fnOf.TryAdd((p.Ref, p.Pin), p.Function);
            }
        string Fn(Comp comp, string pin) =>
            fnOf.TryGetValue((comp.Ref, pin), out var f) ? f
            : libPins.TryGetValue((comp.Lib, comp.PartName), out var lp) && lp.TryGetValue(pin, out var nm) ? nm : "";
        var compPins = comps.ToDictionary(x => x.Ref, x => (pinsOf.GetValueOrDefault(x.Ref) ?? []).OrderBy(p => p, StringComparer.Ordinal).ToList());

        // Bauteile setzen; je Anschluss: (Punkt, Netz, Fallrichtung über eine eigene Spalte)
        var drops = new List<(int X, int Y, int Net, int Column)>(); var slot = 0; var placed = 0;
        foreach (var comp in comps)
        {
            var pins = compPins[comp.Ref];
            var kind = KindOf(comp, pins.Count);
            if (kind is null) { skipped.Add($"{comp.Ref} ({(comp.PartName.Length > 0 ? comp.PartName : comp.Value)}) – Bauteilart gibt es in Trendo nicht"); continue; }
            // Trendo-Anschluss → KiCad-Pin
            var map = PinMap(kind, comp, pins, Fn);
            if (map is null) { skipped.Add($"{comp.Ref} – Anschlüsse nicht eindeutig"); continue; }
            var x0 = slot * Slot; slot++;
            Part p;
            switch (kind)
            {
                case "npn" or "pnp": p = c.Add(kind, x0 + 2, PartY, x0 + 6, PartY, 100); break;
                default: p = c.Add(kind, x0 + 2, PartY, x0 + 6, PartY); break;
            }
            p.Name = comp.Ref;
            if (Number(comp.Value, kind) is { } v) p.Value = v;
            else if (kind is "widerstand" or "kondensator" or "spule" or "spannung" or "wechsel" && comp.Value.Length > 0)
                notes.Add($"{comp.Ref}: Wert „{comp.Value}“ nicht lesbar – Standardwert {p.Value.ToString("G4", Inv)} gesetzt");
            if (kind == "led") p.Color = comp.Value.ToLowerInvariant() switch { var s when s.Contains("green") || s.Contains("grün") || s.Contains("gruen") => "gruen", var s when s.Contains("blue") || s.Contains("blau") => "blau", var s when s.Contains("yellow") || s.Contains("gelb") => "gelb", var s when s.Contains("white") || s.Contains("weiß") || s.Contains("weiss") => "weiss", _ => "rot" };
            placed++;
            void Drop((int X, int Y) at, string pin, int column) { if (netOf.TryGetValue((comp.Ref, pin), out var net)) drops.Add((at.X, at.Y, net, column)); }
            Drop((p.X1, p.Y1), map[0], x0 + 2); Drop((p.X2, p.Y2), map[1], x0 + 6);
            if (map.Length > 2 && p.Pin3() is { } p3) Drop(p3, map[2], x0 + 8); // Basis: Umweg über eine eigene Spalte, nicht durch das Bauteil
        }
        // Sammelleitungen je benutztem Netz; Masse-Netz bekommt das Massezeichen
        var used = drops.Select(d => d.Net).Distinct().OrderBy(n => IsGround(nets[n].Name) ? 0 : 1).ThenBy(n => n).ToList();
        var dropsOf = drops.ToLookup(d => d.Net);
        var right = slot * Slot;
        for (var k = 0; k < used.Count; k++)
        {
            var y = FirstBus + k * BusGap; var net = used[k];
            var mine = dropsOf[net].ToList();
            foreach (var d in mine)
            {
                if (d.Column != d.X) c.Add("draht", d.X, d.Y, d.Column, d.Y); // seitlich zur eigenen Spalte
                c.Add("draht", d.Column, d.Y, d.Column, y);
            }
            if (mine.Count > 1 || IsGround(nets[net].Name)) c.Add("draht", -2, y, right, y);
            if (IsGround(nets[net].Name)) c.Add("masse", -2, y, -2, y + 1);
        }
        // übernommen, aber offen: kein „nicht übernommen“, sondern ein Hinweis (in KiCad oft ein vergessener Draht)
        foreach (var n in nets.Where(n => n.Pins.Count == 1)) notes.Add($"Netz „{n.Name}“ hat nur einen Anschluss ({n.Pins[0].Ref} Pin {n.Pins[0].Pin}) – offenes Ende");
        return new(c, placed, used.Count, skipped, notes);
    }

    static bool IsGround(string net) => net.Trim('/').ToUpperInvariant() is "GND" or "0" or "MASSE" or "GNDREF" or "VSS" or "GNDA" or "GNDD";

    /// <summary>Bauteilart aus Bibliothek, Bauteilname und Bezeichner.</summary>
    static string? KindOf(Comp c, int pins)
    {
        var part = c.PartName.ToUpperInvariant(); var pre = new string(c.Ref.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant(); var val = c.Value.ToUpperInvariant();
        if (part.Contains("LED") || pre == "LED") return pins == 2 ? "led" : null;
        if (part.StartsWith("BATTERY") || part is "VDC" or "VSOURCE" or "V" && pre == "V" || pre == "BT") return pins == 2 ? "spannung" : null;
        if (part is "VSIN" || part.StartsWith("VSIN")) return pins == 2 ? "wechsel" : null;
        if (pre == "Q" || part.StartsWith("Q_")) return pins == 3 ? part.Contains("PNP") || val.Contains("PNP") ? "pnp" : part.Contains("NPN") || val.Contains("NPN") ? "npn" : null : null;
        if (pre is "SW" || part.StartsWith("SW_")) return pins == 2 ? "schalter" : null;
        if (pre == "R" || part is "R" or "R_SMALL" or "R_US") return pins == 2 ? "widerstand" : null;
        if (pre == "C" || part.StartsWith("C_") || part is "C" or "CP") return pins == 2 ? "kondensator" : null;
        if (pre == "L" || part is "L" or "L_SMALL") return pins == 2 ? "spule" : null;
        if (pre == "D" || part.StartsWith("D_") || part == "D") return pins == 2 ? "diode" : null;
        return null;
    }

    /// <summary>Trendo-Anschlüsse in Reihenfolge 1, 2(, 3) als KiCad-Pin-Nummern.</summary>
    static string[]? PinMap(string kind, Comp comp, List<string> pins, Func<Comp, string, string> fn)
    {
        string? ByFn(params string[] names) => pins.FirstOrDefault(p => names.Any(n => string.Equals(fn(comp, p), n, StringComparison.OrdinalIgnoreCase)));
        switch (kind)
        {
            case "diode" or "led": // Trendo: 1 = Anode, 2 = Kathode; KiCad Device:D/LED: 1 = K, 2 = A
                { var a = ByFn("A", "Anode", "+") ?? "2"; var k = ByFn("K", "Kathode", "Cathode", "-") ?? "1"; return pins.Contains(a) && pins.Contains(k) && a != k ? [a, k] : null; }
            case "spannung" or "wechsel": // Trendo: 2 = Pluspol; KiCad Battery/VDC: 1 = +, 2 = −
                { var plus = ByFn("+", "P", "V+") ?? "1"; var minus = ByFn("-", "−", "N", "V-") ?? "2"; return pins.Contains(plus) && pins.Contains(minus) && plus != minus ? [minus, plus] : null; }
            case "npn" or "pnp": // Trendo: 1 = Kollektor, 2 = Emitter, 3 = Basis
                {
                    var b = ByFn("B", "Base"); var cc = ByFn("C", "Collector"); var e = ByFn("E", "Emitter");
                    if (b is null || cc is null || e is null)
                    {   // Q_NPN_BCE, Q_PNP_EBC …: Reihenfolge der Buchstaben = Pin 1, 2, 3
                        var order = comp.PartName.Split('_').LastOrDefault()?.ToUpperInvariant() ?? "";
                        if (order.Length != 3 || order.Distinct().Count() != 3 || !order.All(ch => ch is 'B' or 'C' or 'E')) return null;
                        b = (order.IndexOf('B') + 1).ToString(Inv); cc = (order.IndexOf('C') + 1).ToString(Inv); e = (order.IndexOf('E') + 1).ToString(Inv);
                    }
                    return new[] { cc, e, b }.All(pins.Contains) ? [cc, e, b] : null;
                }
            default: return pins.Count == 2 ? [pins[0], pins[1]] : null;
        }
    }

    /// <summary>Wert aus KiCad-Schreibweise: „4k7“, „10K“, „100n“, „2.2uF“, „4R7“, „9V“, „dc 5“, „470µ“. Nicht lesbar (null): leerer Zahlteil („k“),
    /// unendlich/NaN, und für Widerstand, Kondensator und Spule Werte ≤ 0.</summary>
    public static double? Number(string value, string kind)
    {
        var r = Parse(value, kind);
        if (r is not { } v || !double.IsFinite(v)) return null;
        if (kind is "widerstand" or "kondensator" or "spule" && v <= 0) return null;
        return v;
    }

    static double? Parse(string value, string kind)
    {
        if (kind is "led" or "diode" or "schalter" or "npn" or "pnp") return null;
        var v = value.Trim().Replace("µ", "u").Replace("μ", "u").Replace(',', '.');
        if (v.StartsWith("dc", StringComparison.OrdinalIgnoreCase)) v = v[2..].Trim();
        foreach (var unit in new[] { "Ohm", "Ω", "F", "H", "V" }) if (v.EndsWith(unit, StringComparison.OrdinalIgnoreCase) && v.Length > unit.Length) v = v[..^unit.Length];
        var mult = new Dictionary<char, double> { ['p'] = 1e-12, ['n'] = 1e-9, ['u'] = 1e-6, ['m'] = 1e-3, ['k'] = 1e3, ['K'] = 1e3, ['M'] = 1e6, ['G'] = 1e9, ['R'] = 1, ['r'] = 1 };
        // Buchstabe als Komma: 4k7, 4R7, 2u2
        for (var i = 0; i < v.Length; i++)
            if (mult.TryGetValue(v[i], out var m))
            {
                if (v.Length >= 3 && v.Substring(i).Equals("meg", StringComparison.OrdinalIgnoreCase)) { m = 1e6; }
                var head = v[..i]; var tail = v[(i + 1)..];
                if (tail.Equals("eg", StringComparison.OrdinalIgnoreCase)) tail = "";
                if (head.Length == 0 || !double.TryParse(head, NumberStyles.Float, Inv, out var a)) return null; // „k“ allein ist kein Wert
                if (tail.Length > 0 && !tail.All(char.IsDigit)) return null;
                var frac = tail.Length > 0 ? double.Parse("0." + tail, Inv) : 0;
                return (a + frac) * m;
            }
        return double.TryParse(v, NumberStyles.Float, Inv, out var d) ? d : null; // NumberStyles.Float: kein Tausendertrenner, „∞“ fällt danach heraus
    }
}
