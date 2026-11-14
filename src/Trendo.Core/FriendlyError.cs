using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Was die Person bei einem Fehler tun kann – ein Knopf neben der Meldung.</summary>
public enum ErrorFix { None, Retry, Setup, PickFolder, AskClaude }

/// <summary>E7 (T-0616, freigegeben 14.11.2026): Ausnahme → Alltagssatz und ein Vorschlag „Was tun?“. Der technische Text bleibt aufklappbar erhalten.
/// Eigene deutsche Meldungen von Trendo bleiben unverändert; nur Systemmeldungen (meist englisch) werden übersetzt. Ohne Netz-Bibliothek (QA-019: Typnamen statt Typen).</summary>
public sealed record FriendlyError(string Kind, string Sentence, ErrorFix Fix, string Technical)
{
    public string FixLabel => LabelOf(Fix);

    public static string LabelOf(ErrorFix f) => f switch
    {
        ErrorFix.Retry => "Nochmal versuchen",
        ErrorFix.Setup => "Einrichten",
        ErrorFix.PickFolder => "Anderen Ort wählen",
        ErrorFix.AskClaude => "Claude fragen",
        _ => "",
    };

    // Typische Wörter englischer Systemmeldungen – kommt keines vor, ist es eine eigene (deutsche) Meldung von Trendo
    static readonly Regex English = new(@"\b(the|could not|cannot|can't|unable|denied|not found|does not|was not|is not|failed|invalid|value cannot|input string|access to|no such|timed out|refused|operation|exception|error)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Stammt die Meldung von Trendo selbst (deutsch, für Menschen geschrieben)?</summary>
    static readonly Regex German = new(@"[äöüßÄÖÜ„“]|\b(nicht|kein|keine|bitte|wurde|wird|ist|sind|gibt|fehlt|zuerst|oder|und|der|die|das|den|dem|ein|eine|mit|für|noch|schon|hier|diese|dieser|dieses|es|aus|zu|zum|zur|von|vom|auf|im|bei|als|nur|kann|muss|braucht|mindestens|sich|zwei|drei|wähle|prüfe)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsOwnMessage(string? message)
    {
        if (message is not { Length: > 0 } m || m.Contains("0x", StringComparison.Ordinal)) return false;
        var head = m.Split(": ", 2)[0]; // „Eigener Satz: angehängte Systemmeldung“ – der eigene Teil zählt
        if (German.IsMatch(head) && !English.IsMatch(head)) return true;
        // kurzer Kopf wie „Routing-Kreis: …“: dann zählt die ganze Meldung
        return !German.IsMatch(head) && !English.IsMatch(head) && German.IsMatch(m) && !English.IsMatch(m);
    }

    /// <summary>Eigener Satz vor einer angehängten englischen Systemmeldung: nur der eigene Teil (mit Punkt), sonst null.</summary>
    static string? OwnPart(string message)
    {
        if (!IsOwnMessage(message)) return null;
        var parts = message.Split(": ", 2);
        var cut = parts.Length == 2 && !IsOwnMessage(parts[1]) && !German.IsMatch(parts[1]) ? parts[0].TrimEnd('.') + "." : message;
        return System.Text.RegularExpressions.Regex.Replace(cut, @"\s*\(Parameter '[^']*'\)", "").Trim();
    }

    public static FriendlyError From(Exception ex)
    {
        while (ex is AggregateException { InnerExceptions.Count: 1 } ae) ex = ae.InnerExceptions[0];
        if (ex is System.Reflection.TargetInvocationException { InnerException: { } inner }) ex = inner;
        var tech = $"{ex.GetType().Name}: {ex.Message}";
        var type = ex.GetType().Name;
        var own = ex is NullReferenceException or IndexOutOfRangeException or InvalidCastException or DivideByZeroException or ArithmeticException ? null : OwnPart(ex.Message);
        FriendlyError E(string kind, string sentence, ErrorFix fix) => new(kind, sentence, fix, tech);

        // Netz: Typnamen genügen, der Kern kennt keine Netz-Bibliothek
        if (type == "HttpRequestException" && (ex.GetType().GetProperty("StatusCode")?.GetValue(ex) is not null || Regex.IsMatch(ex.Message, @"status code|\b(401|403|404|429|5\d\d)\b")))
            return E("dienst", own ?? "Der Dienst hat die Anfrage abgelehnt. Prüfe Zugangsschlüssel und Guthaben – oder versuch es später nochmal.", ErrorFix.Setup);
        if (type is "HttpRequestException" or "SocketException" or "WebException" || ex.InnerException?.GetType().Name is "SocketException" or "HttpRequestException")
            return E("netz", own ?? "Keine Verbindung zum Internet oder zum Dienst. Prüfe die Verbindung und versuch es nochmal.", ErrorFix.Retry);
        if (ex is TimeoutException || type == "TaskCanceledException" && ex.InnerException is TimeoutException)
            return E("zeit", own ?? "Das hat zu lange gedauert. Versuch es gleich nochmal.", ErrorFix.Retry);
        if (ex is OperationCanceledException)
            return E("abbruch", own ?? "Der Vorgang wurde abgebrochen.", ErrorFix.None);
        if (ex is System.ComponentModel.Win32Exception w && w.NativeErrorCode is 2 or 3)
            return E("programm", "Ein benötigtes Programm fehlt auf diesem PC. Trendo kann es für dich einrichten.", ErrorFix.Setup);
        if (ex is FileNotFoundException or DirectoryNotFoundException or DriveNotFoundException)
        {
            var name = ex is FileNotFoundException { FileName: { Length: > 0 } fn } ? Path.GetFileName(fn) : null;
            return E("fehlt", own ?? (name is { Length: > 0 } ? $"Die Datei „{name}“ wurde nicht gefunden." : "Die Datei oder der Ordner wurde nicht gefunden."), ErrorFix.PickFolder);
        }
        if (ex is UnauthorizedAccessException)
            return E("zugriff", own ?? "Trendo darf an diesem Ort nicht lesen oder schreiben. Wähle einen anderen Ort, z. B. in „Dokumente“.", ErrorFix.PickFolder);
        if (ex is PathTooLongException)
            return E("pfad", own ?? "Der Name oder Ordnerpfad ist zu lang. Wähle einen kürzeren Ort.", ErrorFix.PickFolder);
        if (ex is IOException io)
        {
            var code = io.HResult & 0xFFFF;
            if (code is 32 or 33) return E("belegt", "Die Datei ist gerade in einem anderen Programm geöffnet. Schließ sie dort und versuch es nochmal.", ErrorFix.Retry);
            if (code is 39 or 112) return E("voll", "Auf dem Laufwerk ist kein Platz mehr. Mach Platz frei oder wähle ein anderes Laufwerk.", ErrorFix.PickFolder);
            return E("datei", own ?? "Die Datei ließ sich nicht lesen oder schreiben. Versuch es nochmal.", ErrorFix.Retry);
        }
        if (ex is InvalidDataException or FormatException || type == "JsonException")
            return E("format", own ?? "Die Datei ist beschädigt oder hat ein Format, das Trendo nicht lesen kann.", ErrorFix.AskClaude);
        return E("sonst", own ?? "Das hat nicht geklappt. Claude kann dir sagen, woran es liegt.", own is null ? ErrorFix.AskClaude : ErrorFix.None);
    }

    /// <summary>Kurz für Meldungstexte: nur der Alltagssatz.</summary>
    public static string Say(Exception ex) => From(ex).Sentence;
}
