using Trendo.Core;
using Trendo.Core.Kie;
using Trendo.Core.Kreativ;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>S2 (Freigabe 13.11.2026): eigene Stimme für Suno über kie.ai. Zwei kostenlose Schritte – Prüfsatz holen, Prüfsatz einsprechen –,
/// danach singt Suno auf Wunsch mit der eigenen Stimme. Nur nach Bestätigung, dass es die eigene Stimme ist; die Stimm-ID liegt im
/// verschlüsselten Speicher der Person und lässt sich jederzeit entfernen.</summary>
public partial class MainWindow
{
    const string KieVoiceSecret = "kie.ai.stimme";

    string _kieVoiceMsg = "";
    string? _kieVoicePhraseTask, _kieVoicePhrase;
    bool _kieVoiceBusy;
    public string KieVoiceMessage => _kieVoiceMsg;
    public string? KieVoicePhraseText => _kieVoicePhrase;
    public bool KieVoiceBusy => _kieVoiceBusy;

    /// <summary>Gespeicherte eigene Stimme (Stimm-ID) dieser Person oder null.</summary>
    public string? KieVoiceId
    {
        get { try { return _core.Secrets.Get(KieVoiceSecret) is { Length: > 0 } v ? v : null; } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { return null; } }
    }

    Func<TimeSpan, CancellationToken, Task> KieDelay => KieDelayOverride ?? ((d, c) => Task.Delay(d, c));

    async Task<string> KieVoiceUpload(KieClient client, string file, CancellationToken ct)
    {
        if (!Ext(file, KieModels.AudioExt) || !KreativSafe.LocalFile(file, KieMaxAudio)) throw new InvalidOperationException("Die Aufnahme muss MP3, WAV, M4A oder OGG auf diesem Rechner sein, höchstens 100 MB.");
        return await client.UploadAsync(await File.ReadAllBytesAsync(file, ct), Path.GetFileName(file), ct);
    }

    string KieVoiceError(Exception e) => e switch
    {
        KieException or InvalidOperationException or ArgumentException => e.Message,
        _ => "Die Verbindung zu kie.ai ist abgerissen. Bitte noch einmal versuchen.",
    };

    /// <summary>Schritt 1: Aufnahme der eigenen Stimme hochladen und den Prüfsatz holen (kostenlos). Wartet höchstens 3 Minuten auf den Satz.</summary>
    public async Task<bool> KieVoicePhrase(string recording, int startS, int endS, string language, bool ownVoice, CancellationToken ct = default)
    {
        if (!ownVoice) { _kieVoiceMsg = "Bitte bestätige, dass die Aufnahme deine eigene Stimme ist. Stimmen anderer Personen legt Trendo nicht an."; KieRefresh(); return false; }
        if (KieKey is not { } key) { _kieVoiceMsg = "Es ist kein kie.ai-Schlüssel eingetragen."; KieRefresh(); return false; }
        _kieVoiceBusy = true; _kieVoicePhrase = null; _kieVoicePhraseTask = null; _kieVoiceMsg = "Aufnahme wird hochgeladen …"; KieRefresh();
        try
        {
            var client = new KieClient(KieWeb, key);
            var url = await KieVoiceUpload(client, recording, ct);
            var task = await client.CreateAsync(KieVoice.PhraseModel, KieVoice.PhraseInput(url, startS, endS, language), ct);
            _core.Audit.Append(Principal.LocalOwner.Name, "kie.stimme.pruefsatz", task);
            _kieVoiceMsg = "kie.ai erstellt den Prüfsatz …"; KieRefresh();
            var until = DateTime.UtcNow + TimeSpan.FromMinutes(3); var wait = TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < until)
            {
                KieTask? t = null;
                try { t = await client.GetAsync(task, ct); } catch (Exception e) when (!ct.IsCancellationRequested && KieClient.Transient(e)) { }
                if (t is not null)
                {
                    if (KieVoice.Phrase(t.ResultJson) is { } phrase) { _kieVoicePhraseTask = task; _kieVoicePhrase = phrase; _kieVoiceMsg = "Lies oder singe jetzt genau diesen Satz und wähle die Aufnahme davon."; return true; }
                    if (t.State == "fail") throw new KieException(501, KieClient.Explain(501, t.FailMessage));
                }
                await KieDelay(wait, ct); wait = TimeSpan.FromSeconds(Math.Min(10, wait.TotalSeconds * 1.5));
            }
            throw new KieException(408, "Der Prüfsatz kam nicht rechtzeitig. Bitte später noch einmal versuchen.");
        }
        catch (Exception e) when (e is KieException or InvalidOperationException or ArgumentException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        { _kieVoiceMsg = KieVoiceError(e); return false; }
        finally { _kieVoiceBusy = false; KieRefresh(); }
    }

    /// <summary>Schritt 2: Aufnahme des Prüfsatzes hochladen und die Stimme anlegen (kostenlos). Fertig, wenn der Prüfsatz-Auftrag „success“ meldet.</summary>
    public async Task<bool> KieVoiceCreate(string verifyRecording, string name, string style, CancellationToken ct = default)
    {
        if (_kieVoicePhraseTask is not { } phraseTask) { _kieVoiceMsg = "Hole zuerst den Prüfsatz."; KieRefresh(); return false; }
        if (KieKey is not { } key) { _kieVoiceMsg = "Es ist kein kie.ai-Schlüssel eingetragen."; KieRefresh(); return false; }
        _kieVoiceBusy = true; _kieVoiceMsg = "Aufnahme des Prüfsatzes wird hochgeladen …"; KieRefresh();
        try
        {
            var client = new KieClient(KieWeb, key);
            var url = await KieVoiceUpload(client, verifyRecording, ct);
            await client.CreateAsync(KieVoice.CreateModel, KieVoice.CreateInput(phraseTask, url, name, style), ct);
            _kieVoiceMsg = "kie.ai prüft die Aufnahme …"; KieRefresh();
            var t = await client.WaitAsync(phraseTask, KieDelay, TimeSpan.FromMinutes(5), null, ct);
            if (!t.Ok) { _kieVoiceMsg = "Die Aufnahme hat die Prüfung nicht bestanden. Sprich oder singe den Satz noch einmal deutlich und ohne Hintergrundgeräusche ein."; return false; }
            _core.Secrets.Set(KieVoiceSecret, phraseTask);
            _core.Audit.Append(Principal.LocalOwner.Name, "kie.stimme.angelegt", phraseTask);
            _kieVoicePhrase = null; _kieVoicePhraseTask = null;
            _kieVoiceMsg = "✓ Deine Stimme ist angelegt. Bei Liedern kannst du jetzt „Mit meiner Stimme singen“ wählen.";
            return true;
        }
        catch (Exception e) when (e is KieException or InvalidOperationException or ArgumentException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        { _kieVoiceMsg = KieVoiceError(e); return false; }
        finally { _kieVoiceBusy = false; KieRefresh(); }
    }

    /// <summary>Eigene Stimme auf diesem Rechner vergessen (bei kie.ai bleibt sie im eigenen Konto, bis man sie dort löscht).</summary>
    public void KieVoiceForget()
    {
        try { _core.Secrets.Delete(KieVoiceSecret); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "kie.stimme.entfernt", "");
        _kieVoiceMsg = "Die eigene Stimme ist auf diesem Rechner entfernt.";
        KieRefresh();
    }
}
