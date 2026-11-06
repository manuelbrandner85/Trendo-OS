using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3 (06.11.2026): „Film ausliefern“ im Videoschnitt – Film, Stems Dialog/Musik, EDL, OTIO, Cue-Liste und Prüfbericht mit SHA-256 in einem Ordner.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: ersetzt den FFmpeg-Aufruf (Argumente → Ergebnis).</summary>
    public Func<IReadOnlyList<string>, Cli.Result>? FilmRunOverride { get; set; }

    /// <summary>Letztes Ergebnis der Auslieferung (für Anzeige und Tests).</summary>
    public FilmDelivery.Result? FilmLastDelivery { get; private set; }

    public async Task FilmDeliver(ProgramDocument d)
    {
        if (_veExport is not null) { _progMsg[VK] = "Ein Export läuft schon – danach ausliefern."; Show(VK); return; }
        if (VideoEdit.Clips(d.Data).Count == 0) { _progMsg[VK] = "Erst Stücke in die Zeitleiste legen."; Show(VK); return; }
        var data = (JsonObject)d.Data.DeepClone();
        var baseName = "Auslieferung – " + DevStudio.SafeName(d.Title);
        var folder = Path.Combine(WorkFolder(Programs[VK].Folder), baseName);
        for (var i = 2; Directory.Exists(folder); i++) folder = Path.Combine(WorkFolder(Programs[VK].Folder), $"{baseName} {i}");
        Func<IReadOnlyList<string>, Cli.Result> run;
        if (FilmRunOverride is { } fake) run = fake;
        else
        {
            if (FfmpegPath is not { } ff) { _progMsg[VK] = "Für die Auslieferung fehlt FFmpeg – unter Zusatzprogramme einrichten."; Show(VK); return; }
            if (_opt.Isolated) { _progMsg[VK] = "Im abgeschotteten Testmodus startet Trendo kein FFmpeg."; Show(VK); return; }
            run = args => Cli.Run(ff, args, Path.GetDirectoryName(folder)!, 30 * 60_000);
        }
        _progMsg[VK] = "Auslieferung läuft: Film, Stems, Schnittliste, Prüfbericht …"; Show(VK);
        try
        {
            var r = await Task.Run(() => FilmDelivery.Deliver(run, data, folder, d.Title, "FFmpeg"));
            FilmLastDelivery = r;
            _core.Audit.Append(Principal.LocalOwner.Name, "video.deliver", Path.GetFileName(r.Folder));
            var red = r.Checks.Count(c => c.Light == "rot"); var yellow = r.Checks.Count(c => c.Light == "gelb");
            _progMsg[VK] = $"Ausgeliefert: {r.Files.Count} Dateien + Prüfbericht in {r.Folder}" + (red > 0 ? $" – {red} rot, bitte Prüfbericht lesen." : yellow > 0 ? $" – {yellow} Hinweise im Prüfbericht." : " – alle Prüfungen grün.");
            NotifyFileResult(_progMsg[VK]);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[VK] = ex.Message; }
        Show(VK);
    }
}
