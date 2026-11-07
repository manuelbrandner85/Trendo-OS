using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Trendo Musik V3/V6 (freigegeben 07.11.2026, „Alle ja“): Stimmen-Bibliothek – gelernte Stimmen mit Name, Herkunft und Einwilligungsnachweis.
/// Feste Regeln: nur die eigene Stimme oder eine Person, die eingewilligt hat (Name, Datum, Bestätigung); Stimmen bekannter Künstlerinnen und Künstler sind gesperrt.
/// Löschen entfernt Aufnahmen, Trainingsdaten und den Nachweis.</summary>
public sealed class VoiceBank(string dataRoot)
{
    public sealed record Consent(string Kind, string Person, DateTimeOffset Date, string Statement);
    public sealed record Voice(string Id, string Name, string Source, Consent Consent, bool Ready, DateTimeOffset Created, string? FromSong = null);

    public const int SegmentSeconds = 30, MinSegments = 6;
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    public string Root => Path.Combine(dataRoot, "stimmen");
    public string Dir(string id) => Path.Combine(Root, id);
    public string TakesDir(string id) => Path.Combine(Dir(id), "aufnahmen");

    public List<Voice> All()
    {
        if (!Directory.Exists(Root)) return [];
        var r = new List<Voice>();
        foreach (var d in Directory.GetDirectories(Root))
        {
            var f = Path.Combine(d, "stimme.json");
            try { if (File.Exists(f) && JsonSerializer.Deserialize<Voice>(File.ReadAllText(f), Opt) is { } v) r.Add(v); } catch (JsonException) { }
        }
        return [.. r.OrderBy(v => v.Created)];
    }

    public Voice? Get(string id) => All().FirstOrDefault(v => v.Id == id);

    void Save(Voice v) { Directory.CreateDirectory(Dir(v.Id)); File.WriteAllText(Path.Combine(Dir(v.Id), "stimme.json"), JsonSerializer.Serialize(v, Opt)); }

    /// <summary>Einwilligung prüfen: eigene Stimme (Bestätigung) oder Person mit Name und ausdrücklicher Erklärung; bekannte Künstler und Prominente gesperrt.</summary>
    public static string? ConsentProblem(Consent c, bool notFamous)
    {
        if (!notFamous) return "Stimmen bekannter Künstlerinnen und Künstler oder Prominenter sind gesperrt – bitte bestätigen, dass es keine bekannte Person ist.";
        if (c.Kind == "eigen") return c.Statement.Trim().Length == 0 ? "Bitte bestätigen: Das ist meine eigene Stimme." : null;
        if (c.Kind != "person") return "Herkunft der Stimme fehlt.";
        if (c.Person.Trim().Length < 2) return "Name der Person fehlt.";
        if (c.Statement.Trim().Length < 10) return "Die Erklärung der Person fehlt (z. B. „Erika Muster erlaubt am … das Lernen ihrer Stimme für eigene Lieder“).";
        return null;
    }

    /// <summary>Neue Stimme anlegen (noch nicht gelernt). Id ist ein ASCII-Name für das Stimm-Modell.</summary>
    public Voice Add(string name, string source, Consent consent, bool notFamous, string? fromSong = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name der Stimme fehlt.");
        if (ConsentProblem(consent, notFamous) is { } p) throw new InvalidOperationException(p);
        var baseId = "stimme-" + Regex.Replace(name.Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss"), "[^a-z0-9]+", "-").Trim('-');
        var id = baseId; for (var n = 2; Directory.Exists(Dir(id)); n++) id = $"{baseId}-{n}";
        var v = new Voice(id, name.Trim(), source, consent, false, DateTimeOffset.Now, fromSong);
        Save(v); return v;
    }

    public void MarkReady(string id) { if (Get(id) is { } v) Save(v with { Ready = true }); }

    public bool Delete(string id) { if (!Directory.Exists(Dir(id))) return false; Directory.Delete(Dir(id), true); return true; }

    /// <summary>Gesangsspur in Abschnitte zu 30 s schneiden (Trainingsdaten); fast stille Abschnitte werden weggelassen. Liefert die Anzahl.</summary>
    public int Segment(string id, Media.AudioBuf vocal)
    {
        var dir = TakesDir(id); Directory.CreateDirectory(dir);
        var len = SegmentSeconds * vocal.Rate; var n = 0;
        for (var s = 0; s + len / 2 <= vocal.Length; s += len)
        {
            var part = vocal.Slice(s, Math.Min(vocal.Length, s + len));
            double e = 0; foreach (var v in part.Data[0]) e += v * (double)v;
            if (Math.Sqrt(e / Math.Max(1, part.Length)) < 0.01) continue; // kaum Gesang
            Media.AudioFile.WriteWav(part, Path.Combine(dir, $"teil{n:00}.wav")); n++;
        }
        return n;
    }
}
