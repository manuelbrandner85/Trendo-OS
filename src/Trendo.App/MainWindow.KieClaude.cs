using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Kie;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>K3 (Freigabe 13.11.2026): Claude Code in Trendo nutzt kie.ai über Trendos eigene Brücke statt über ein fremdes Paket.
/// Claude sieht den Schlüssel nie. Vor jedem kostenpflichtigen Auftrag fragt die Karte „Ja, machen / Nein“ im Chat – mit Kosten,
/// vollständigen Pfaden der hochgeladenen Dateien sowie Beschreibung und Liedtext, damit die Person sieht, was entsteht.
/// Lange Aufträge laufen in Trendo weiter; Claude fragt den Stand mit kie_ergebnis ab, statt den Aufruf minutenlang offen zu halten.</summary>
public partial class MainWindow
{
    IEnumerable<McpTool> KieTools(BridgeRun run)
    {
        McpTool T(string name, string text, JsonObject schema, Func<JsonObject, Task<McpResult>> body) => new(name, text, schema, a => BridgeUi(run, name, body, a));
        string Studio() => ClaudeStudioOf(run.Page ?? CurrentView) ?? "STUDIO-IMAGEAI-01";
        static string Short(string s) => s.Length > 240 ? KreativSafe.Cut(s, 240) + " …" : s;

        yield return T("kie_status", "Zeigt, ob die Person kie.ai eingerichtet hat, ihr Guthaben und die Modelle, die zum aktuellen Studio passen (Kennung, Art, Richtpreis).", TrendoMcp.Schema(), async _ =>
        {
            var credits = KieOn ? await KieBalance() : null;
            var kinds = KieModels.ForStudio(Studio());
            return new McpResult(new JsonObject
            {
                ["eingerichtet"] = KieOn,
                ["guthaben_credits"] = credits,
                ["hinweis"] = KieOn ? "Jeder Auftrag kostet Guthaben; Trendo fragt die Person vorher." : "Kein Schlüssel. Die Person trägt ihn selbst im Studio über „kie.ai“ ein – nie in den Chat.",
                ["modelle"] = new JsonArray(KieModels.All.OrderBy(m => kinds.Contains(m.Kind) ? kinds.ToList().IndexOf(m.Kind) : 99).Select(m => (JsonNode)new JsonObject
                {
                    ["modell"] = m.Id, ["name"] = m.Label, ["art"] = KieModels.KindLabel(m.Kind), ["preis"] = KieModels.CostText(m), ["braucht_bild"] = m.NeedsImage, ["braucht_audio"] = m.NeedsAudio, ["geprueft"] = m.Verified,
                }).ToArray()),
            }.ToJsonString());
        });

        yield return T("kie_erzeugen", "Startet bei kie.ai ein Bild, Video, Lied, eine Sprachausgabe oder eine sprechende Figur. Kostet Guthaben – die Person bestätigt vorher im Chat. Gibt sofort eine Auftragsnummer zurück; das Ergebnis holst du mit kie_ergebnis. Liedtexte nur von der Person selbst; keine echten Personen, keine Stimmen oder Stile bekannter Künstler.",
            TrendoMcp.Schema(("modell", "string", "Kennung aus kie_status", true), ("beschreibung", "string", "Was entstehen soll (bei Sprache: der Text)", false), ("seitenverhaeltnis", "string", "z. B. 1:1, 16:9, 9:16", false),
                ("bild", "string", "Pfad zu einem eigenen Bild (PNG, JPG, WEBP) auf diesem Rechner", false), ("audio", "string", "Pfad zu einer eigenen Sprachaufnahme (MP3, WAV, M4A, OGG)", false),
                ("titel", "string", "Titel (Musik)", false), ("stil", "string", "Genre, Stimmung, Instrumente (Musik)", false), ("liedtext", "string", "Liedtext der Person (Musik)", false),
                ("instrumental", "boolean", "Ohne Gesang (Musik)", false), ("sekunden", "number", "Länge (Video 5–15, Musik 10–360)", false), ("stimme", "string", "Stimm-ID (Sprache, leer = Standard)", false)), async a =>
        {
            if (!KieOn) return McpResult.Fail("kie.ai ist nicht eingerichtet. Die Person trägt ihren Schlüssel selbst im Studio über „kie.ai“ ein.");
            var m = KieModels.Find(Need(a, "modell")) ?? throw new ArgumentException("Dieses Modell gibt es nicht. Liste mit kie_status abrufen.");
            var sec = a["sekunden"] is JsonValue sv && sv.TryGetValue<double>(out var s) && double.IsFinite(s) ? (int)Math.Clamp(s, 1, 600) : m.Kind == KieKind.Musik ? 120 : 5;
            var input = new KieInput(Prompt: a["beschreibung"].AsText() ?? "", Aspect: a["seitenverhaeltnis"].AsText() ?? (m.Kind == KieKind.Video ? "16:9" : "1:1"), Seconds: sec,
                Title: a["titel"].AsText() ?? "", Style: a["stil"].AsText() ?? "", Lyrics: a["liedtext"].AsText() ?? "", Instrumental: a["instrumental"].AsBool(), Voice: a["stimme"].AsText() ?? "");
            // Dateien nur, wenn das Modell sie nutzt, und nur passende Arten
            var usesImage = m.NeedsImage || m.Kind == KieKind.Video;
            var images = usesImage && a["bild"].AsText() is { Length: > 0 } img ? new List<string> { Path.GetFullPath(img) } : [];
            var audio = m.NeedsAudio && a["audio"].AsText() is { Length: > 0 } au ? Path.GetFullPath(au) : null;
            if (images.Any(x => !KieModels.ImageExt.Contains(Path.GetExtension(x).ToLowerInvariant()))) return McpResult.Fail("Als Bild gehen nur PNG, JPG oder WEBP.");
            if (audio is not null && !KieModels.AudioExt.Contains(Path.GetExtension(audio).ToLowerInvariant())) return McpResult.Fail("Als Aufnahme gehen nur MP3, WAV, M4A oder OGG.");
            if (KieModels.Blocked(m, input with { ImageUrls = images.Count > 0 ? images : null, AudioUrl = audio }, consent: true) is { } why) return McpResult.Fail(why);
            var lines = new List<string> { $"bei kie.ai „{m.Label}“ erzeugen – das kostet {KieModels.CostText(m, input)} von deinem kie.ai-Guthaben" };
            if (input.Prompt.Length > 0) lines.Add("Beschreibung: „" + Short(input.Prompt) + "“");
            if (input.Title.Length > 0) lines.Add("Titel: „" + Short(input.Title) + "“, Stil: „" + Short(input.Style) + "“");
            if (input.Lyrics.Length > 0) lines.Add("Liedtext: „" + Short(input.Lyrics) + "“ – mit „Ja“ bestätigst du, dass dieser Text von dir stammt");
            foreach (var x in images.Concat(audio is null ? [] : [audio])) lines.Add("Hochgeladen wird: " + x);
            if (m.Kind == KieKind.Avatar) lines.Add("Mit „Ja“ bestätigst du: Das Bild zeigt dich oder eine erfundene Figur, und die Stimme gehört dir oder du hast die Einwilligung");
            if (!await BridgeAsk(run, string.Join(". ", lines))) return McpResult.Fail("Die Person hat abgelehnt. Es wurde nichts erzeugt und nichts berechnet.");
            var studio = Studio();
            var before = _kieJobs.Count;
            _ = KieRun(studio, m, input, images, audio, consent: m.Kind == KieKind.Avatar); // läuft in Trendo weiter
            var job = _kieJobs.Count > before ? _kieJobs[0] : null;
            return new McpResult(new JsonObject { ["auftrag"] = job?.Id, ["hinweis"] = "Läuft. Stand und Dateien mit kie_ergebnis abfragen (Bilder meist unter 1 Minute, Video und Musik mehrere Minuten)." }.ToJsonString());
        });

        yield return T("kie_ergebnis", "Stand eines kie.ai-Auftrags aus dieser Sitzung: wartet bis zu 90 Sekunden und liefert dann die gespeicherten Dateien oder den aktuellen Stand.", TrendoMcp.Schema(("auftrag", "string", "Nummer aus kie_erzeugen", true)), async a =>
        {
            var id = Need(a, "auftrag");
            var job = _kieJobs.FirstOrDefault(j => j.Id == id) ?? throw new ArgumentException("Diesen Auftrag gibt es in dieser Sitzung nicht.");
            for (var i = 0; i < 90 && !job.Done; i++) await Task.Delay(1000);
            return job.Error is { } err && job.Done ? McpResult.Fail(err)
                : new McpResult(new JsonObject { ["fertig"] = job.Done, ["stand"] = job.State, ["credits"] = job.Credits, ["dateien"] = new JsonArray(job.Files.Select(x => (JsonNode)x).ToArray()) }.ToJsonString());
        });
    }
}
