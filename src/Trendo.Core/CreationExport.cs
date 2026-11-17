using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>T-0725 (Audit D7, X-11 „Neues Projekt starten“: „kein Export des Plans“): Schritte als CSV für Excel/LibreOffice Calc
/// (Semikolon, UTF-8 mit BOM, deutsches Datum) und der ganze Zeitplan als Kalenderdatei (.ics) – je Schritt mit Zeitraum ein ganztägiger Termin
/// über die volle Dauer, Meilensteine als eintägiger Termin. Schreibt nichts selbst; das übernimmt die Oberfläche atomar.</summary>
public static partial class CreationSuite
{
    static readonly CultureInfo DeCi = CultureInfo.GetCultureInfo("de-DE");

    static string StateName(Step s) => s.Done ? "erledigt" : s.State switch { 1 => "in Arbeit", _ => "offen" };

    static string Cell(string s) => s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>Kopfzeile + eine Zeile je Schritt (auch Schritte ohne Termin), in der Reihenfolge des Vorhabens.</summary>
    public static string ExportCsv(string dir)
    {
        var steps = Steps(dir); var bars = Bars(steps).ToDictionary(b => b.No);
        var sb = new StringBuilder("Nr;Schritt;Status;Beginn;Ende;Tage;Meilenstein;Nach Schritt;Kritischer Pfad\r\n");
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i]; bars.TryGetValue(i + 1, out var b);
            sb.Append(CultureInfo.InvariantCulture, $"{i + 1};{Cell(s.Text)};{StateName(s)};")
              .Append(b is null ? ";;" : $"{b.Start.ToString("dd.MM.yyyy", DeCi)};{b.End.ToString("dd.MM.yyyy", DeCi)};{b.Days.ToString(CultureInfo.InvariantCulture)}")
              .Append(';').Append(s.Milestone ? "ja" : "nein")
              .Append(';').Append(string.Join(",", s.After ?? []))
              .Append(';').Append(b is { Critical: true } ? "ja" : "nein")
              .Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Zeitplan als Kalenderdatei: feste Kennungen je Vorhaben und Schritt (erneuter Import ersetzt statt zu verdoppeln), Erinnerung am Vortag.</summary>
    public static string ExportIcs(string dir)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(dir));
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dir).ToLowerInvariant())))[..10].ToLowerInvariant();
        var events = Bars(Steps(dir)).Select(b => new Sys.CalEvent($"vorhaben-plan-{key}-{b.Step.Line}", b.Start, b.End.AddDays(1),
            $"{(b.Step.Milestone ? "◆ " : "")}{name}: {b.Step.Text}", Notes: $"Schritt {b.No} im Vorhaben „{name}“ · {StateName(b.Step)}" + (b.Critical ? " · kritischer Pfad" : ""), AllDay: true, RemindMin: 24 * 60)).ToList();
        if (events.Count == 0) throw new InvalidOperationException("Im Zeitplan steht noch kein Schritt mit Termin – gib zuerst einem Schritt ein Datum.");
        return Sys.Ics.Export(events);
    }
}
