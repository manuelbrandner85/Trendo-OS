using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>SYS-12 (Katalog freigegeben 21.10.2026): Barrierefreiheit und Anpassung – reine Regeln, ohne Oberfläche testbar.</summary>
public static class A11y
{
    // P07/P14 Kontrast nach WCAG 2.1
    public static (double R, double G, double B) Rgb(string hex)
    {
        var h = hex.TrimStart('#'); if (h.Length == 8) h = h[2..]; if (h.Length != 6) throw new ArgumentException("Farbe als #RRGGBB angeben.");
        return (int.Parse(h[..2], NumberStyles.HexNumber) / 255.0, int.Parse(h[2..4], NumberStyles.HexNumber) / 255.0, int.Parse(h[4..], NumberStyles.HexNumber) / 255.0);
    }
    public static double Luminance(string hex)
    {
        static double L(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (r, g, b) = Rgb(hex); return 0.2126 * L(r) + 0.7152 * L(g) + 0.0722 * L(b);
    }
    public static double Contrast(string fg, string bg) { var a = Luminance(fg); var b = Luminance(bg); return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05); }
    /// <summary>AA: 4,5:1 für normalen Text, 3:1 für großen Text (ab 18 pt bzw. 14 pt fett).</summary>
    public static bool MeetsAA(string fg, string bg, bool large = false) => Contrast(fg, bg) >= (large ? 3.0 : 4.5);
    /// <summary>Hellt bzw. dunkelt eine Akzentfarbe so weit ab, dass sie auf dem Hintergrund AA erreicht (Farbton bleibt).</summary>
    public static string AccentForText(string accent, string bg)
    {
        var (r, g, b) = Rgb(accent); var toWhite = Luminance(bg) < 0.4;
        for (var i = 0; i <= 20 && !MeetsAA(Hex(r, g, b), bg); i++)
        { var t = 0.1; (r, g, b) = toWhite ? (r + (1 - r) * t, g + (1 - g) * t, b + (1 - b) * t) : (r * (1 - t), g * (1 - t), b * (1 - t)); }
        return Hex(r, g, b);
    }
    static string Hex(double r, double g, double b) => $"#{(int)Math.Round(r * 255):X2}{(int)Math.Round(g * 255):X2}{(int)Math.Round(b * 255):X2}";

    // P03 Schriftgröße
    public static readonly int[] FontMins = [0, 12, 14, 16];
    public static double EffectiveFont(double size, int min) => min > 0 && size < min ? min : size;

    // P09 Status ohne Farbe
    public enum Level { Gut, Hinweis, Problem }
    public static string Symbol(Level l) => l switch { Level.Gut => "✓", Level.Hinweis => "⚠", _ => "✕" };
    public static string WithSymbol(Level l, string text) => $"{Symbol(l)} {text}";

    // P10 Leichte Sprache
    static readonly (string Hard, string Easy)[] Words =
    [
        ("Konfiguration", "Einstellung"), ("konfigurieren", "einstellen"), ("installieren", "einrichten"), ("Installation", "Einrichtung"), ("Komponente", "Teil"),
        ("verfügbar", "da"), ("aktualisieren", "auf den neuen Stand bringen"), ("Zugangsschlüssel", "Schlüssel"), ("Arbeitsspeicher", "Speicher"), ("regelmäßig", "oft"),
    ];
    /// <summary>Höchstens zwei Sätze, jeder höchstens 12 Wörter (Rest wird weggelassen), schwere Wörter ersetzt, jeder Satz in eigener Zeile.</summary>
    public static string Easy(string text)
    {
        foreach (var (h, e) in Words) text = Regex.Replace(text, h, e, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*\([^)]*\)", ""); text = Regex.Replace(text, @"\s*–[^.!?–]*(–\s*)?", " ").Replace(" .", ".");
        var sentences = Regex.Split(text.Trim(), @"(?<=[.!?])\s+").Where(s => s.Length > 0).Take(2)
            .Select(s => { var w = s.Split(' ', StringSplitOptions.RemoveEmptyEntries); return w.Length <= 12 ? s.Trim() : string.Join(' ', w.Take(12)).TrimEnd(',', ':') + "."; });
        return string.Join("\n", sentences);
    }

    // P15 Navigation höchstens 2 Ebenen
    public static List<string> NavTooDeep(IEnumerable<(string Key, int Depth)> pages) => pages.Where(p => p.Depth > 2).Select(p => p.Key).ToList();

    // P16 Deutsche Beschriftung
    static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase)
    { "save", "cancel", "open", "close", "delete", "settings", "search", "submit", "loading", "error", "warning", "file", "edit", "view", "help", "back", "next", "yes", "no", "ok?", "run", "start now", "upload", "download now", "click", "here", "the", "and", "please", "retry", "update", "remove", "add", "new", "browse" };
    /// <summary>Beschriftungen mit englischen Wörtern (Eigennamen wie PDF, KI, Tor, Chrome sind erlaubt).</summary>
    public static List<string> EnglishLabels(IEnumerable<string> labels) =>
        labels.Where(l => Regex.Matches(l, @"[A-Za-zÄÖÜäöüß]+").Any(m => English.Contains(m.Value))).Distinct().ToList();

    // P18 Benachrichtigung
    public static readonly string[] NotifyStyles = ["normal", "leise"];
    /// <summary>Darf eine Meldung das Fenster nach vorn holen? Leise: nie; Fokuszeit: nie.</summary>
    public static bool MayInterrupt(string style, bool focusActive) => style != "leise" && !focusActive;

    // P20 Vorlesen (offline)
    public static (string Exe, string[] Args)? SpeakCommand(string text, bool windows, Func<string, string?> find)
    {
        text = text.Replace("\r", " ").Replace("\n", " ").Trim(); if (text.Length == 0) return null;
        if (windows)
        {
            var ps = find("powershell") ?? "powershell";
            var esc = text.Replace("'", "''");
            return (ps, ["-NoProfile", "-NonInteractive", "-Command", $"Add-Type -AssemblyName System.Speech; $s=New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Speak('{esc}')"]);
        }
        var es = find("espeak-ng") ?? find("espeak"); return es is null ? null : (es, ["-v", "de", text]);
    }

    // P23 Fensterposition
    public sealed record Rect(int X, int Y, int W, int H);
    /// <summary>Gespeicherte Position nur verwenden, wenn das Fenster mit mindestens 100×100 px auf einem vorhandenen Bildschirm liegt; sonst null (Standardplatz).</summary>
    public static Rect? RestorePosition(Rect? saved, IEnumerable<Rect> screens)
    {
        if (saved is null || saved.W < 200 || saved.H < 150) return null;
        foreach (var s in screens)
        {
            var w = Math.Min(saved.X + saved.W, s.X + s.W) - Math.Max(saved.X, s.X); var h = Math.Min(saved.Y + saved.H, s.Y + s.H) - Math.Max(saved.Y, s.Y);
            if (w >= 100 && h >= 100) return saved with { W = Math.Min(saved.W, s.W), H = Math.Min(saved.H, s.H) };
        }
        return null;
    }

    // P24 Einfacher Modus
    public static readonly string[] SimpleTiles = ["STUDIO-LETTER-01", "SYS-10", "CORE-02", "CORE-08"];
    public const double SimpleButtonFont = 15;

    // P22 Einstellungen sichern
    public static void ExportSettings(UserSettings s, string file) => File.WriteAllText(file, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
    public static UserSettings ImportSettings(string file)
    {
        UserSettings? s; try { s = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(file)); } catch (JsonException) { throw new InvalidDataException("Das ist keine Trendo-Einstellungsdatei."); }
        if (s is null || !UserSettings.Themes.Contains(s.Theme)) throw new InvalidDataException("Die Datei enthält ungültige Einstellungen.");
        s.UiScale = UserSettings.ClampScale(s.UiScale); if (!FontMins.Contains(s.FontMin)) s.FontMin = 0; if (!NotifyStyles.Contains(s.NotifyStyle)) s.NotifyStyle = "normal";
        return s;
    }

    // P25 Bericht
    public sealed record PageCheck(string Page, int Controls, List<string> Unnamed, List<string> EnglishLabels, List<string> LowContrast, int Depth);
    public static string Report(IEnumerable<PageCheck> pages, string theme)
    {
        var l = pages.ToList(); var bad = l.Count(p => p.Unnamed.Count + p.EnglishLabels.Count + p.LowContrast.Count > 0 || p.Depth > 2);
        var sb = new System.Text.StringBuilder($"# Barrierefreiheits-Bericht\n\n{DateTime.Now:dd.MM.yyyy HH:mm} · Design: {theme} · {l.Count} Seiten geprüft · {(bad == 0 ? "✓ keine Befunde" : $"⚠ {bad} Seiten mit Befunden")}\n\nAutomatisch geprüft: Namen für Bildschirmleser, englische Beschriftungen, Textkontrast (AA), Navigationstiefe. Nicht geprüft: echtes Vorlesen mit Narrator (PC-Prüfung offen).\n\n| Seite | Elemente | ohne Namen | englisch | Kontrast | Ebene |\n|---|---|---|---|---|---|\n");
        foreach (var p in l) sb.AppendLine($"| {p.Page} | {p.Controls} | {Cell(p.Unnamed)} | {Cell(p.EnglishLabels)} | {Cell(p.LowContrast)} | {p.Depth} |");
        return sb.ToString();
        static string Cell(List<string> x) => x.Count == 0 ? "✓" : "⚠ " + string.Join(", ", x.Take(3)).Replace("|", "/") + (x.Count > 3 ? $" (+{x.Count - 3})" : "");
    }
}
