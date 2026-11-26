namespace Trendo.Core.Wissen;

/// <summary>Größe in einer Formel: Zeichen, Bedeutung, übliche Einheit; <see cref="Konstante"/>: fester Wert (z. B. g, R) als Vorgabe.</summary>
public sealed record FormelGroesse(string Zeichen, string Bedeutung, string Einheit, string? Konstante = null);

/// <summary>Formel der Formelsammlung: Gleichung „links = rechts“ und für jede Größe die nach ihr umgestellte Form.</summary>
public sealed record PhysikFormel(string Id, string Bereich, string Name, string Gleichung, IReadOnlyList<FormelGroesse> Groessen, IReadOnlyDictionary<string, string> Umgestellt);

/// <summary>Eingabe einer bekannten Größe: Zahl (Text, deutsch), Einheit, optional Unsicherheit.</summary>
public sealed record GroesseEingabe(string Wert, string Einheit, string Unsicherheit = "");

/// <summary>Ergebnis einer Formel-Rechnung mit Rechenweg und Probe.</summary>
public sealed record PhysikErgebnis(PhysikFormel Formel, string Gesucht, Groesse Ergebnis, string Einheit, string Text, IReadOnlyList<Schritt> Schritte, IReadOnlyList<Probe> Proben)
{
    public bool ProbeOk => Proben.Count > 0 && Proben.All(p => p.Ok);
}

/// <summary>T-0781 Naturwissenschaft: Formelsammlung Physik (und Stöchiometrie) mit Rechnen in Einheiten und Dimensionsprüfung.
/// Konstanten nach CODATA 2022 (NIST, Version 9.0 vom 09.05.2024) bzw. exakt seit der SI-Revision 2019.</summary>
public static class Formelsammlung
{
    static PhysikFormel F(string id, string bereich, string name, string gleichung, FormelGroesse[] g, params (string Z, string Form)[] um) =>
        new(id, bereich, name, gleichung, g, um.ToDictionary(x => x.Z, x => x.Form, StringComparer.Ordinal));
    static FormelGroesse G(string z, string b, string e, string? k = null) => new(z, b, e, k);

    public const string Gn = "9,80665";

    public static readonly IReadOnlyList<PhysikFormel> Alle =
    [
        F("geschwindigkeit", "Mechanik", "Gleichförmige Bewegung", "v = s/t", [G("v", "Geschwindigkeit", "m/s"), G("s", "Weg", "m"), G("t", "Zeit", "s")], ("v", "s/t"), ("s", "v*t"), ("t", "s/v")),
        F("beschleunigung", "Mechanik", "Beschleunigung", "a = Δv/t", [G("a", "Beschleunigung", "m/s²"), G("Δv", "Geschwindigkeitsänderung", "m/s"), G("t", "Zeit", "s")], ("a", "Δv/t"), ("Δv", "a*t"), ("t", "Δv/a")),
        F("weg-beschleunigt", "Mechanik", "Weg bei gleichmäßiger Beschleunigung aus dem Stand", "s = 1/2·a·t²", [G("s", "Weg", "m"), G("a", "Beschleunigung", "m/s²"), G("t", "Zeit", "s")], ("s", "1/2*a*t^2"), ("a", "2*s/t^2"), ("t", "sqrt(2*s/a)")),
        F("freier-fall", "Mechanik", "Freier Fall (Fallzeit)", "h = 1/2·g·t²", [G("h", "Fallhöhe", "m"), G("g", "Fallbeschleunigung", "m/s²", Gn), G("t", "Fallzeit", "s")], ("h", "1/2*g*t^2"), ("g", "2*h/t^2"), ("t", "sqrt(2*h/g)")),
        F("kraft", "Mechanik", "Grundgleichung der Mechanik", "F = m·a", [G("F", "Kraft", "N"), G("m", "Masse", "kg"), G("a", "Beschleunigung", "m/s²")], ("F", "m*a"), ("m", "F/a"), ("a", "F/m")),
        F("gewichtskraft", "Mechanik", "Gewichtskraft", "F_G = m·g", [G("F_G", "Gewichtskraft", "N"), G("m", "Masse", "kg"), G("g", "Fallbeschleunigung", "m/s²", Gn)], ("F_G", "m*g"), ("m", "F_G/g"), ("g", "F_G/m")),
        F("e-kin", "Mechanik", "Bewegungsenergie", "E_kin = 1/2·m·v²", [G("E_kin", "Bewegungsenergie", "J"), G("m", "Masse", "kg"), G("v", "Geschwindigkeit", "m/s")], ("E_kin", "1/2*m*v^2"), ("m", "2*E_kin/v^2"), ("v", "sqrt(2*E_kin/m)")),
        F("e-pot", "Mechanik", "Lageenergie", "E_pot = m·g·h", [G("E_pot", "Lageenergie", "J"), G("m", "Masse", "kg"), G("g", "Fallbeschleunigung", "m/s²", Gn), G("h", "Höhe", "m")], ("E_pot", "m*g*h"), ("m", "E_pot/(g*h)"), ("h", "E_pot/(m*g)"), ("g", "E_pot/(m*h)")),
        F("arbeit", "Mechanik", "Mechanische Arbeit", "W = F·s", [G("W", "Arbeit", "J"), G("F", "Kraft", "N"), G("s", "Weg", "m")], ("W", "F*s"), ("F", "W/s"), ("s", "W/F")),
        F("leistung", "Mechanik", "Leistung", "P = W/t", [G("P", "Leistung", "W"), G("W", "Arbeit oder Energie", "J"), G("t", "Zeit", "s")], ("P", "W/t"), ("W", "P*t"), ("t", "W/P")),
        F("druck", "Mechanik", "Druck", "p = F/A", [G("p", "Druck", "Pa"), G("F", "Kraft", "N"), G("A", "Fläche", "m²")], ("p", "F/A"), ("F", "p*A"), ("A", "F/p")),
        F("dichte", "Mechanik", "Dichte", "ρ = m/V", [G("ρ", "Dichte", "kg/m³"), G("m", "Masse", "kg"), G("V", "Volumen", "m³")], ("ρ", "m/V"), ("m", "ρ*V"), ("V", "m/ρ")),
        F("impuls", "Mechanik", "Impuls", "p = m·v", [G("p", "Impuls", "kg·m/s"), G("m", "Masse", "kg"), G("v", "Geschwindigkeit", "m/s")], ("p", "m*v"), ("m", "p/v"), ("v", "p/m")),
        F("pendel", "Mechanik", "Fadenpendel (kleine Ausschläge)", "T = 2π·√(l/g)", [G("T", "Schwingungsdauer", "s"), G("l", "Pendellänge", "m"), G("g", "Fallbeschleunigung", "m/s²", Gn)], ("T", "2*pi*sqrt(l/g)"), ("l", "g*(T/(2*pi))^2"), ("g", "l*(2*pi/T)^2")),
        F("gravitation", "Mechanik", "Gravitationsgesetz", "F = G·m_1·m_2/r²", [G("F", "Anziehungskraft", "N"), G("G", "Gravitationskonstante", "m³/(kg·s²)", "6,67430e-11"), G("m_1", "Masse 1", "kg"), G("m_2", "Masse 2", "kg"), G("r", "Abstand", "m")],
            ("F", "G*m_1*m_2/r^2"), ("m_1", "F*r^2/(G*m_2)"), ("m_2", "F*r^2/(G*m_1)"), ("r", "sqrt(G*m_1*m_2/F)")),
        F("ohm", "Elektrizität", "Ohmsches Gesetz", "U = R·I", [G("U", "Spannung", "V"), G("R", "Widerstand", "Ω"), G("I", "Stromstärke", "A")], ("U", "R*I"), ("R", "U/I"), ("I", "U/R")),
        F("el-leistung", "Elektrizität", "Elektrische Leistung", "P = U·I", [G("P", "Leistung", "W"), G("U", "Spannung", "V"), G("I", "Stromstärke", "A")], ("P", "U*I"), ("U", "P/I"), ("I", "P/U")),
        F("ladung", "Elektrizität", "Ladung", "Q = I·t", [G("Q", "Ladung", "C"), G("I", "Stromstärke", "A"), G("t", "Zeit", "s")], ("Q", "I*t"), ("I", "Q/t"), ("t", "Q/I")),
        F("el-energie", "Elektrizität", "Elektrische Energie", "E = P·t", [G("E", "Energie", "kWh"), G("P", "Leistung", "W"), G("t", "Zeit", "h")], ("E", "P*t"), ("P", "E/t"), ("t", "E/P")),
        F("waerme", "Wärme", "Wärmemenge", "Q = c·m·ΔT", [G("Q", "Wärmemenge", "J"), G("c", "spezifische Wärmekapazität", "J/(kg·K)", "4186"), G("m", "Masse", "kg"), G("ΔT", "Temperaturänderung", "K")],
            ("Q", "c*m*ΔT"), ("m", "Q/(c*ΔT)"), ("ΔT", "Q/(c*m)"), ("c", "Q/(m*ΔT)")),
        F("gas", "Wärme", "Allgemeine Gasgleichung", "p·V = n·R·T", [G("p", "Druck", "Pa"), G("V", "Volumen", "m³"), G("n", "Stoffmenge", "mol"), G("R", "molare Gaskonstante", "J/(mol·K)", "8,314462618"), G("T", "Temperatur", "K")],
            ("p", "n*R*T/V"), ("V", "n*R*T/p"), ("n", "p*V/(R*T)"), ("T", "p*V/(n*R)")),
        F("welle", "Schwingungen & Wellen", "Wellengleichung", "c = λ·f", [G("c", "Ausbreitungsgeschwindigkeit", "m/s"), G("λ", "Wellenlänge", "m"), G("f", "Frequenz", "Hz")], ("c", "λ*f"), ("λ", "c/f"), ("f", "c/λ")),
        F("periode", "Schwingungen & Wellen", "Periodendauer und Frequenz", "T = 1/f", [G("T", "Periodendauer", "s"), G("f", "Frequenz", "Hz")], ("T", "1/f"), ("f", "1/T")),
        F("photon", "Schwingungen & Wellen", "Energie eines Photons", "E = h·f", [G("E", "Energie", "eV"), G("h", "Planck-Konstante", "J·s", "6,62607015e-34"), G("f", "Frequenz", "Hz")], ("E", "h*f"), ("f", "E/h")),
        F("linse", "Optik", "Linsengleichung", "1/f = 1/g + 1/b", [G("f", "Brennweite", "cm"), G("g", "Gegenstandsweite", "cm"), G("b", "Bildweite", "cm")], ("f", "1/(1/g + 1/b)"), ("g", "1/(1/f - 1/b)"), ("b", "1/(1/f - 1/g)")),
        F("stoffmenge", "Chemie", "Stoffmenge aus der Masse", "n = m/M", [G("n", "Stoffmenge", "mol"), G("m", "Masse", "g"), G("M", "molare Masse", "g/mol")], ("n", "m/M"), ("m", "n*M"), ("M", "m/n")),
        F("konzentration", "Chemie", "Stoffmengenkonzentration", "c = n/V", [G("c", "Konzentration", "mol/l"), G("n", "Stoffmenge", "mol"), G("V", "Volumen", "l")], ("c", "n/V"), ("n", "c*V"), ("V", "n/c")),
    ];

    public static PhysikFormel? Finde(string id) => Alle.FirstOrDefault(f => f.Id == id);
    public static IEnumerable<string> Bereiche => Alle.Select(f => f.Bereich).Distinct();

    /// <summary>Gesuchte Größe aus den übrigen berechnen. Werte in beliebigen passenden Einheiten; das Ergebnis in <paramref name="zielEinheit"/> (leer = übliche Einheit).</summary>
    public static PhysikErgebnis Rechne(string id, string gesucht, IReadOnlyDictionary<string, GroesseEingabe> werte, string? zielEinheit = null)
    {
        var f = Finde(id) ?? throw new ArgumentException("Unbekannte Formel.");
        if (!f.Umgestellt.TryGetValue(gesucht, out var form)) throw new ArgumentException($"Nach „{gesucht}“ lässt sich diese Formel hier nicht umstellen.");
        var schritte = new List<Schritt>();
        var t = TermLeser.Lies(Ersatz(form));
        schritte.Add(new Schritt(form == f.Gleichung.Split('=')[1].Trim().Replace("·", "*") ? "Formel" : $"Formel nach {gesucht} umstellen", $"{f.Gleichung}  ⇒  {gesucht} = {TermText.Anzeige(t).Replace("Δv", "Δv")}"));
        var map = new Dictionary<string, Groesse>(StringComparer.Ordinal);
        var einsetzen = new List<string>(); var dimPruef = new List<string>();
        foreach (var g in f.Groessen.Where(x => x.Zeichen != gesucht))
        {
            var e = werte.GetValueOrDefault(g.Zeichen);
            var wert = e?.Wert is { Length: > 0 } w ? w : g.Konstante ?? throw new ArgumentException($"Für {g.Zeichen} ({g.Bedeutung}) fehlt ein Wert.");
            var einheit = e?.Wert is { Length: > 0 } ? e.Einheit : g.Einheit;
            if (!double.TryParse(wert.Replace(" ", "").Replace('−', '-'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var zahl)
                && !double.TryParse(wert.Replace(" ", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out zahl))
                throw new FormatException($"„{wert}“ ist keine Zahl ({g.Zeichen}).");
            var u = 0.0;
            if (e?.Unsicherheit is { Length: > 0 } us && !double.TryParse(us.Replace('−', '-'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out u)) throw new FormatException($"„{us}“ ist keine Zahl (Unsicherheit von {g.Zeichen}).");
            var q = Einheiten.InSi(zahl, einheit, Math.Abs(u));
            var soll = Einheiten.Lies(g.Einheit).Dim;
            if (q.Dim != soll) throw new ArgumentException($"{g.Zeichen} ({g.Bedeutung}) braucht eine Einheit für {soll.Beschreibung()} wie {g.Einheit} – „{einheit}“ ist {q.Dim.Beschreibung()}.");
            map[Ersatz(g.Zeichen)] = q;
            einsetzen.Add($"{g.Zeichen} = {Einheiten.Text(zahl, u, einheit)}" + (Math.Abs(q.Wert - zahl) > 1e-12 * Math.Abs(zahl) || einheit != Einheiten.SiEinheit(q.Dim) ? $" = {Einheiten.Text(q.Wert, q.Unsicherheit, Einheiten.SiEinheit(q.Dim))}" : ""));
            dimPruef.Add($"[{g.Zeichen}] = {q.Dim.SiText()}");
        }
        schritte.Add(new Schritt("Werte in SI-Einheiten umrechnen und einsetzen", string.Join(";  ", einsetzen)));
        var r = Einheiten.Werte(t, map);
        var zielSoll = Einheiten.Lies(f.Groessen.First(x => x.Zeichen == gesucht).Einheit).Dim;
        var dimOk = r.Dim == zielSoll;
        schritte.Add(new Schritt("Dimensionsprüfung: die Einheiten mitrechnen", $"[{gesucht}] = {r.Dim.SiText()} ({r.Dim.Beschreibung()}) – {(dimOk ? "passt" : "passt NICHT")} zu {gesucht} ({f.Groessen.First(x => x.Zeichen == gesucht).Bedeutung}, {zielSoll.SiText()})"));
        if (!dimOk) throw new ArgumentException("Die Einheiten passen nicht zusammen – bitte die Eingaben prüfen.");
        var ziel = zielEinheit is { Length: > 0 } z ? z : f.Groessen.First(x => x.Zeichen == gesucht).Einheit;
        var (wz, uz) = Einheiten.Aus(r, ziel);
        var text = $"{gesucht} = {Einheiten.Text(wz, uz, ziel)}";
        if (ziel != Einheiten.SiEinheit(r.Dim)) schritte.Add(new Schritt("Ergebnis in SI und in der gewünschten Einheit", $"{gesucht} = {Einheiten.Text(r.Wert, r.Unsicherheit, Einheiten.SiEinheit(r.Dim))} = {Einheiten.Text(wz, uz, ziel)}"));
        else schritte.Add(new Schritt("Ergebnis", text));
        if (r.Unsicherheit > 0) schritte.Add(new Schritt("Messunsicherheit nach Gauß (Fehlerfortpflanzung wie im Studio „Messwerte auswerten“)", $"Δ{gesucht} = {Einheiten.Zahl(uz, 2)} {ziel} ({(r.Unsicherheit / Math.Abs(r.Wert) * 100).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} %)"));
        // Probe: Ergebnis in die Ausgangsgleichung einsetzen (beide Seiten getrennt, SI)
        var proben = new List<Probe>();
        var alle = new Dictionary<string, Groesse>(map) { [Ersatz(gesucht)] = new Groesse(r.Wert, r.Dim) };
        var seiten = f.Gleichung.Replace("·", "*").Split('=');
        var links = Einheiten.Werte(TermLeser.Lies(Ersatz(seiten[0])), alle.ToDictionary(k => k.Key, k => k.Value with { Unsicherheit = 0 }));
        var rechts = Einheiten.Werte(TermLeser.Lies(Ersatz(seiten[1])), alle.ToDictionary(k => k.Key, k => k.Value with { Unsicherheit = 0 }));
        var ok = links.Dim == rechts.Dim && Math.Abs(links.Wert - rechts.Wert) <= 1e-9 * Math.Max(Math.Abs(links.Wert), Math.Abs(rechts.Wert));
        proben.Add(new Probe(ok, $"{(ok ? "✓" : "✗")} Einsetzen in {f.Gleichung}: links {Einheiten.Text(links.Wert, 0, Einheiten.SiEinheit(links.Dim))}, rechts {Einheiten.Text(rechts.Wert, 0, Einheiten.SiEinheit(rechts.Dim))}."));
        proben.Add(new Probe(dimOk, $"{(dimOk ? "✓" : "✗")} Dimension: {string.Join(", ", dimPruef)} ergibt [{gesucht}] = {r.Dim.SiText()}."));
        return new PhysikErgebnis(f, gesucht, r, ziel, text, schritte, proben);
    }

    /// <summary>Griechische Zeichen und Δ als Variablennamen des Termlesers („ρ“ → „rho_“, „ΔT“ bleibt).</summary>
    public static string Ersatz(string s) => s.Replace("ρ", "r_ho").Replace("λ", "l_ambda").Replace("·", "*");
}
