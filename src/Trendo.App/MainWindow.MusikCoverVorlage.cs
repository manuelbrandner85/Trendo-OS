using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik – Wunsch 05.11.2026: „auf den Song in Trendo Musik klicken können, somit werden alle Einstellungen übernommen“.
/// Ein Treffer der Cover-Suche ist direkt die Vorlage für die Generierung: Titel, Länge, Stil (Merkmale des Künstlers ohne Namen, dazu dein Wunsch-Genre),
/// Tonart und Tempo (AcousticBrainz, soweit gemessen) landen im Profi-Formular. Den Liedtext fügst du selbst ein (keine fremden Texte), keine Promi-Stimmen.</summary>
public partial class MainWindow
{
    /// <summary>Was zuletzt aus einem Treffer übernommen wurde (für Tests und die Anzeige).</summary>
    public IReadOnlyList<string> MsCoverTaken { get; private set; } = [];

    /// <summary>Treffer anklicken = als Vorlage übernehmen (öffentlich für Tests und die KI).</summary>
    public async Task MsCoverUse(int i)
    {
        if (i < 0 || i >= _cwHits.Count) return;
        MsCoverPick(i);
        var h = _cwHits[i]; var taken = new List<string>(); var missing = new List<string>();
        _msPro = true; _msProdParent = null;
        _msTitle = $"{h.Title} (Cover)"; taken.Add("Titel");
        if (h.Seconds > 0) { _msLen = Enumerable.Range(0, MusicLengths.Length).MinBy(k => Math.Abs(MusicLengths[k].Seconds - h.Seconds)); taken.Add($"Länge {CoverStudio.Time(h.Seconds)}"); }
        if (_cwLyrics.Trim().Length > 0) { _msLyrics = _cwLyrics; _msInstr = false; taken.Add("dein Liedtext"); } else missing.Add("Liedtext (fügst du selbst ein)");
        _msStyle = _cwGenre.Trim(); _msGenres.Clear(); _msLike = h.Artist;
        CwSay($"„{h.Title}“ wird übernommen – Stil, Tonart und Tempo werden nachgeschlagen …");
        // Stil: Merkmale des Künstlers (ohne Namen) – wie „Im Stil von“
        if (h.Artist.Trim().Length > 1 && await MsStyleLike(h.Artist) is not null) taken.Add(_cwGenre.Trim().Length > 0 ? $"Stil ({_cwGenre.Trim()} + Merkmale des Originals)" : "Stil (Merkmale des Originals)");
        else if (_msStyle.Length > 0) taken.Add($"Stil ({_msStyle})");
        else missing.Add("Stil (oben wählen)");
        // Tonart und Tempo aus freien Messdaten
        _msBpm = ""; _msKey = "";
        using (var http = MsWebClient())
            if (await MsWebGet(http, new Uri($"https://acousticbrainz.org/api/v1/{h.Mbid}/low-level")) is { } ab && SongTools.ParseAcousticBrainz(ab) is { } f)
            {
                _msBpm = Math.Round(f.Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture); taken.Add($"{f.Bpm:0} BPM");
                if (AceStepClient.Keys.Contains(f.Key)) { _msKey = f.Key; taken.Add("Tonart " + SongTools.KeyDe(f.Key)); }
            }
            else missing.Add("Tonart und Tempo (keine Messung – mit eigener Aufnahme unter Schritt 2 genau)");
        MsCoverTaken = taken;
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.cover.vorlage", h.Mbid);
        _msMsg = $"„{h.Title}“ als Vorlage übernommen: {string.Join(", ", taken)}." + (missing.Count > 0 ? $" Noch offen: {string.Join(", ", missing)}." : "") + " Jetzt links „Erstellen“ – Melodie und Text entstehen neu, ein Cover fremder Lieder nur privat nutzen.";
        CwSay(_msMsg);
    }
}
