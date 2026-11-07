using System.Globalization;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>Vorschlag 11 (freigegeben 07.11.2026): Testspieler-Bot. Der Unreal-Startlauf (-trendocheck) spielt das Spiel selbst an – Figur, Boden,
/// Gegner, Wegfindung, Kampf, Blut, Wagen, Flackern – und schreibt Messzeilen „TRENDO-…“. Hier wird daraus ein Testbericht wie von einem
/// Testspieler: je Prüfpunkt ✓ oder ⚠ mit Messwert und Bildern aus dem Lauf. Nichts wird geschätzt: fehlt eine Messzeile, fehlt der Punkt.</summary>
public static class GamePlaytest
{
    public sealed record Finding(string Area, bool Ok, string Text);

    static string After(string line, string tag) { var i = line.IndexOf(tag, StringComparison.Ordinal); return i < 0 ? "" : line[(i + tag.Length)..].Trim(); }
    static double Num(string s, string pattern) => Regex.Match(s, pattern) is { Success: true } m && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    /// <summary>Prüfpunkte aus dem Protokoll des Startlaufs (jeweils die letzte Zeile je Art).</summary>
    public static List<Finding> Read(string log)
    {
        var lines = (log ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Contains("TRENDO-")).ToList();
        string? Last(string tag, string? must = null) => lines.LastOrDefault(l => l.Contains(tag) && (must is null || l.Contains(must)));
        var f = new List<Finding>();
        if (Last("TRENDO-FIGUR ") is { } fig) f.Add(new("Spielfigur", fig.Contains("Mensch=ja"), fig.Contains("MetaHuman=ja") ? "Echter Mensch (MetaHuman) als Spielfigur" : fig.Contains("Mensch=ja") ? "Menschliche Figur" : "Keine menschliche Figur: " + After(fig, "TRENDO-FIGUR")));
        if (Last("TRENDO-BODEN ") is { } bo)
        {
            var cm = Num(bo, @"Boden=(-?[\d.]+) cm");
            f.Add(new("Boden", !double.IsNaN(cm) && Math.Abs(cm) < 10, double.IsNaN(cm) ? "Bodenabstand nicht gemessen" : $"Gegner steht auf dem Boden (Füße {cm.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} cm darüber)"));
        }
        if (Last("TRENDO-GEGNER ") is { } ge)
        {
            var a = Num(ge, @"vorher=([\d.]+)"); var b = Num(ge, @"nachher=([\d.]+)");
            var ok = ge.Contains("verfolgt");
            f.Add(new("Gegner-KI", ok, ok ? $"Gegner bemerkt die Spielfigur und verfolgt sie{(ge.Contains("angegriffen") ? " und greift an" : "")}" : "Gegner reagiert nicht auf die Spielfigur" + (double.IsNaN(a) ? "" : $" (Abstand {a / 100:0} m → {b / 100:0} m)")));
        }
        if (Last("TRENDO-WEG ", "Hindernis ") is { } we && !we.Contains("Hindernis-Probe"))
            f.Add(new("Wegfindung", we.Contains("drumherum"), we.Contains("drumherum") ? "Weg um ein Hindernis gefunden: " + After(we, "versperrt,").TrimEnd('.') : "Pfad führt durch ein Hindernis"));
        else if (Last("TRENDO-WEG ", "Wegnetz=") is { } wn) f.Add(new("Wegfindung", wn.Contains("Wegnetz=ja"), wn.Contains("Wegnetz=ja") ? "Wegnetz vorhanden" : "Kein Wegnetz – Gegner gehen nur geradeaus"));
        if (Last("TRENDO-FLACKERN ", "in der Ferne") is { } fl && !fl.Contains("nicht gemessen"))
            f.Add(new("Darstellung", fl.Contains("(ruhig)"), fl.Contains("(ruhig)") ? "Gegner in der Ferne bewegen sich ruhig (kein Flackern)" : "Gegner in der Ferne flackern: " + After(fl, "Bilder,")));
        if (Last("TRENDO-KAMPF ", "besiegt=") is { } ka)
        {
            var ok = ka.Contains("besiegt=ja") && ka.Contains("(liegt)");
            f.Add(new("Kampf", ok, ok ? "Gegner besiegt, fällt und bleibt liegen" + (ka.Contains("abgetrennt=1") ? ", ein Glied abgetrennt" : "") : ka.Contains("unter dem Boden") ? "Besiegter Gegner fällt durch den Boden" : ka.Contains("besiegt=nein") ? "Gegner ließ sich nicht besiegen" : "Besiegter Gegner bleibt stehen"));
        }
        if (Last("TRENDO-TIER ", "flieht=") is { } ti)
        {
            var ok = ti.Contains("flieht=ja");
            f.Add(new("Tiere", ok, ok ? "Tier flieht vor der Spielfigur: " + After(ti, "Abstand").Split(" Fluchten")[0] : "Tier reagiert nicht auf die Spielfigur"));
        }
        if (Last("TRENDO-BLUT ") is { } bl) f.Add(new("Blut", Num(bl, @"min: (\d+)") > 0, "Blutflecken trocknen mit der Zeit ein"));
        if (Last("TRENDO-WAGEN ", "gesteuert=") is { } wa)
        {
            var m = Num(wa, @"Strecke=([\d.]+) m"); var kmh = Num(wa, @"Tempo=([\d.]+) km/h"); var spin = wa.Contains("Reifen drehen sich");
            var ok = wa.Contains("gesteuert=ja") && m >= 15 && spin;
            f.Add(new("Fahrzeug", ok, ok ? $"Wagen fährt {m.ToString("0", CultureInfo.InvariantCulture)} m, Räder drehen sich mit" : wa.Contains("gesteuert=nein") ? "Wagen lässt sich nicht steuern" : !spin ? "Räder drehen sich beim Fahren nicht" : $"Wagen kommt nur {m.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} m weit (zuletzt {kmh:0} km/h) – steckt fest?"));
        }
        if (Last("TRENDO-FEHLER") is { } er) f.Add(new("Start", false, After(er, "TRENDO-FEHLER")));
        else if (Last("TRENDO-OK") is not null) f.Add(new("Start", true, "Spiel startet und läuft"));
        return f;
    }

    static readonly (string File, string Label)[] ShotNames =
        [("trendo-start.png", "Start"), ("trendo-objekte.png", "Objekte"), ("trendo-gegner.png", "Gegner"), ("trendo-tier.png", "Tiere"), ("trendo-kampf.png", "Kampf"), ("trendo-glied.png", "Nahaufnahme"), ("trendo-blut-alt.png", "5 Minuten später"), ("trendo-wagen.png", "Wagen")];

    /// <summary>Bilder des Testspielers aus Saved/Screenshots in Spielreihenfolge (nur vorhandene).</summary>
    public static List<(string Path, string Label)> Shots(string built)
    {
        var dir = string.IsNullOrEmpty(built) ? "" : Path.Combine(built, "Saved", "Screenshots");
        return dir.Length == 0 || !Directory.Exists(dir) ? [] : [.. ShotNames.Select(s => (Path.Combine(dir, s.File), s.Label)).Where(s => File.Exists(s.Item1))];
    }
}
