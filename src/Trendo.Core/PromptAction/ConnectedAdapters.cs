using System.Text;

namespace Trendo.Core.PromptAction;

/// <summary>Welche Aufträge die gesperrte Variante meinen. Die sichere Variante gilt sonst.</summary>
static class Intent
{
    public static bool Yes(IReadOnlyDictionary<string, string> fields, params string[] keys) =>
        keys.Any(key => FieldKit.Yes(fields, key));

    public static bool Filled(IReadOnlyDictionary<string, string> fields, params string[] keys) =>
        keys.Any(key => FieldKit.Has(fields, key));

    public static bool Camera(IReadOnlyDictionary<string, string> fields) => Yes(fields, "starten", "aufnahme", "foto", "bewegen", "greifen");
    public static bool OtherLanguage(IReadOnlyDictionary<string, string> fields)
    {
        if (Yes(fields, "eindeutschen") || Filled(fields, "programm")) return true;
        var ziel = PromptFields.Norm(FieldKit.Text(fields, "ziel"));
        if (ziel.Length == 0) return false;
        return ziel is not ("de" or "en" or "deutsch" or "englisch" or "german" or "english" or "de-de" or "en-us" or "en-gb");
    }
    public static bool Desktop(IReadOnlyDictionary<string, string> fields) => Yes(fields, "aendern") || Filled(fields, "theme", "hintergrund", "tapete");
    public static bool Theme(IReadOnlyDictionary<string, string> fields) => Yes(fields, "aendern") || Filled(fields, "farbe", "schrift", "groesse");
    public static bool Secret(IReadOnlyDictionary<string, string> fields) => Yes(fields, "setzen", "loeschen") || Filled(fields, "wert");
    public static bool Repair(IReadOnlyDictionary<string, string> fields) => Yes(fields, "reparieren", "aendern");
    public static bool Phone(IReadOnlyDictionary<string, string> fields) => Yes(fields, "verbinden", "spiegel", "foto");
    public static bool Fusion(IReadOnlyDictionary<string, string> fields) => Yes(fields, "koppeln", "verbinden");
    public static bool Download(IReadOnlyDictionary<string, string> fields) => Yes(fields, "laden", "herunterladen");
    public static bool Install(IReadOnlyDictionary<string, string> fields) => Yes(fields, "installieren");
    public static bool Share(IReadOnlyDictionary<string, string> fields) => Yes(fields, "teilen");
    public static bool Program(IReadOnlyDictionary<string, string> fields) => Yes(fields, "starten");
    public static bool Online(IReadOnlyDictionary<string, string> fields) => Yes(fields, "online");
    public static bool Explorer(IReadOnlyDictionary<string, string> fields) => Yes(fields, "explorer", "oeffnen");
}

static class Connected
{
    public static PromptServices Svc => PromptAmbient.Current;

    public static AdapterOutcome Text(AdapterCall call, string relative, string body, string preview, string check, string tool, IReadOnlyList<string>? warnings = null)
    {
        call.Projects.WriteFile(call.ProjectId, relative, body);
        var read = call.Projects.ReadFile(call.ProjectId, relative);
        var ok = read == body;
        var detail = ok ? "Gelesen." : "Die Datei stimmt nach dem Lesen nicht.";
        var ext = Path.GetExtension(relative);
        var format = ext.Length > 1 ? ext[1..] : "txt";
        return new(true, [new ProducedFile(relative, format, true)], [new QaResult(check, ok, detail, null, null, tool)], preview, warnings ?? [], ok ? [] : [detail], null);
    }

    public static AdapterOutcome Run(Func<AdapterOutcome> work)
    {
        try { return work(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return FieldKit.Blocked(ex.Message);
        }
    }

    public static string Short(string text)
    {
        var line = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 160 ? line : line[..160];
    }

    public static KiAnswer? Ask(string question, string? system, CancellationToken ct)
    {
        var ask = Svc.Ask;
        if (ask is null) return null;
        return ask(question, system, ct).GetAwaiter().GetResult();
    }
}

/// <summary>CORE-09: Namen aus sysfs. Es wird keine Kamera geöffnet.</summary>
public sealed class CameraListAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-09";
    public string ActionId => "kamera.nennen";
    public string Label => "Kameras nennen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Kamera_nennt_geraete_ohne_aufnahme";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Camera(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var cams = WebcamScan.ReadVideo4Linux(Connected.Svc.VideoSys);
        var sb = new StringBuilder();
        sb.AppendLine("Kameras, nur gelesen. Es wurde keine Kamera geöffnet, nichts aufgenommen und kein Mikrofon benutzt.");
        if (cams.Count == 0) sb.AppendLine("Keine Kamera gesehen.");
        foreach (var cam in cams) sb.Append("- ").Append(cam.Name.Replace('\n', ' ')).Append(" (").Append(cam.Id).AppendLine(")");
        var body = sb.ToString();
        return Connected.Text(call, FieldKit.Slot("kamera", call.Version, "liste.txt"), body, cams.Count + " Kameras gelesen", "kamera.liste", "WebcamScan", ["Es wurde keine Kamera geöffnet."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Die Kamera ist nicht angeschlossen. Es gibt keine Bewegung.");
}


/// <summary>SYS-08: dieselbe Leseprüfung wie die Recherche. Offline und abgelehnte Adressen holen nichts.</summary>
public sealed class ResearchAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-08";
    public string ActionId => "seite.lesen";
    public string Label => "Seite lesen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Recherche_liest_nur_die_erlaubte_seite";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["adresse"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "adresse") ? [] : [new("adresse", "adresse", "Adresse fehlt. Schreib „Adresse: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "adresse").Length > 2000 ? [new("adresse", "Die Adresse ist zu lang.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var raw = FieldKit.Text(call.Parameters, "adresse");
        if (raw.Length == 0) return FieldKit.Fail("Adresse fehlt.");
        var verdict = ResearchDesk.Classify(raw);
        if (verdict.Kind != UrlKind.Lesemodus) return FieldKit.Blocked(verdict.Reason);
        if (Connected.Svc.Offline)
            return Connected.Text(call, FieldKit.Slot("recherche", call.Version, "seite.txt"), "Offline: die Seite wurde nicht gelesen.\n" + verdict.Reason + "\n", "Nichts gelesen", "seite.offline", "ResearchDesk", ["Es wurde nichts aus dem Netz gelesen."]);
        if (Connected.Svc.ReadBytes is null)
            return FieldKit.Fail("Es ist kein Lesen angeschlossen. Es wurde nichts aus dem Netz geholt.");
        try
        {
            var (type, bytes, final) = Connected.Svc.ReadBytes(verdict.Uri!, call.Cancellation).GetAwaiter().GetResult();
            if (type is not ("text/html" or "text/plain" or "application/xhtml+xml"))
                return FieldKit.Fail("Das ist ein Download, keine Seite. Im Lesemodus werden keine Dateien geladen.");
            var html = Encoding.UTF8.GetString(bytes);
            var page = type == "text/plain" ? new ReaderPage(final.Host, html, []) : ResearchDesk.ToReader(html, final);
            var body = page.Title + "\n\n" + page.Text + "\n";
            if (body.Length > 200_000) body = body[..200_000] + "\n…\n";
            return Connected.Text(call, FieldKit.Slot("recherche", call.Version, "seite.txt"), body, page.Title + " · " + page.Text.Length + " Zeichen", "seite.gelesen", "ResearchDesk", ["Nur gelesen. Nichts angemeldet, nichts heruntergeladen."]);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return FieldKit.Fail("Die Seite wurde nicht gelesen: " + ex.Message);
        }
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts heruntergeladen.");
}

/// <summary>CORE-02: Frage an die angeschlossene lokale KI. Ohne Antwort keine Datei.</summary>
public sealed class AskAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-02";
    public string ActionId => "ki.fragen";
    public string Label => "KI fragen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Ki_fragt_die_angegebene_quelle";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["frage"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "frage") ? [] : [new("frage", "frage", "Frage fehlt. Schreib „Frage: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "frage").Length > 8000 ? [new("frage", "Die Frage ist zu lang.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var question = FieldKit.Text(call.Parameters, "frage");
        if (question.Length == 0) return FieldKit.Fail("Frage fehlt.");
        var answer = Connected.Ask(question, null, call.Cancellation);
        if (answer is not { Ok: true } || string.IsNullOrWhiteSpace(answer.Text))
            return FieldKit.Fail(answer?.Text ?? "Es ist keine lokale KI angeschlossen. Es gibt keine Antwort.");
        var body = answer.Text.Trim() + "\n";
        return Connected.Text(call, FieldKit.Slot("ki", call.Version, "antwort.txt"), body, Connected.Short(answer.Text), "ki.antwort", answer.Backend ?? "lokal");
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird keine externe KI gefragt.");
}

/// <summary>CORE-03: Team aus AgentCenter. Die KI kommt nur über den angeschlossenen Dienst.</summary>
public sealed class TeamAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-03";
    public string ActionId => "team.fragen";
    public string Label => "Team fragen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Team_fragt_die_rollen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["aufgabe"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "aufgabe") ? [] : [new("aufgabe", "aufgabe", "Aufgabe fehlt. Schreib „Aufgabe: …“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (FieldKit.Text(parameters, "aufgabe").Length > 4000) return [new("aufgabe", "Die Aufgabe ist zu lang.", true)];
        var team = FieldKit.Text(parameters, "team");
        if (team.Length > 0 && AgentCenter.Teams.All(t => !string.Equals(t.Id, team, StringComparison.OrdinalIgnoreCase)))
            return [new("team", "Dieses Team gibt es nicht. Möglich: text, code, projekt.", true)];
        return [];
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        var goal = FieldKit.Text(call.Parameters, "aufgabe");
        if (goal.Length == 0) return FieldKit.Fail("Aufgabe fehlt.");
        if (Connected.Svc.Ask is null || Connected.Svc.DataRoot is null || Connected.Svc.Audit is null || Connected.Svc.Gate is null || Connected.Svc.Projects is null)
            return FieldKit.Fail("Es ist keine lokale KI angeschlossen. Es gibt keinen Rat.");
        var team = FieldKit.Text(call.Parameters, "team");
        if (team.Length == 0) team = "text";
        var ki = new IntelligenceCore(Connected.Svc.DataRoot, Connected.Svc.Audit, () => [new AskBackend(Connected.Svc.Ask)]);
        var center = new AgentCenter(Connected.Svc.DataRoot, Connected.Svc.Audit, Connected.Svc.Gate, Connected.Svc.Projects);
        var tasks = center.RunTeamAsync(team, goal, ki, call.ProjectId, null, call.Cancellation).GetAwaiter().GetResult();
        if (tasks.Count == 0 || tasks.Any(t => t.State != AgentTaskState.Fertig))
            return FieldKit.Fail(tasks.LastOrDefault()?.Result is { Length: > 0 } reason ? reason : "Das Team hat keinen Rat geliefert.");
        var sb = new StringBuilder();
        foreach (var task in tasks)
        {
            sb.Append("## ").AppendLine(AgentCenter.Role(task.RoleId).Name);
            sb.AppendLine(task.Result.Trim());
            sb.AppendLine();
        }
        var body = sb.ToString();
        return Connected.Text(call, FieldKit.Slot("team", call.Version, "rat.txt"), body, Connected.Short(tasks[^1].Result), "team.rat", "AgentCenter");
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gespeichert und nichts weitergegeben.");

    sealed class AskBackend(Func<string, string?, CancellationToken, Task<KiAnswer>> ask) : IKiBackend
    {
        public string Name => "lokale-quelle";
        public string BaseUrl => "http://127.0.0.1:11434";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["lokal"]);
        public async Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct)
        {
            var system = messages.FirstOrDefault(m => m.Role == "system")?.Content;
            var user = string.Join("\n", messages.Where(m => m.Role == "user").Select(m => m.Content));
            var answer = await ask(user, system, ct);
            if (answer is not { Ok: true } || string.IsNullOrWhiteSpace(answer.Text))
                throw new InvalidOperationException(answer?.Text ?? "Keine KI.");
            return answer.Text;
        }
    }
}

/// <summary>X-05: nur Loopback-Adressen aus dem Katalog. Kein Chat, keine Online-Konten.</summary>
public sealed class ProbeAdapter : IStudioActionAdapter
{
    public string StudioId => "X-05";
    public string ActionId => "quelle.pruefen";
    public string Label => "Quelle prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Quelle_prueft_nur_loopback";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Online(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var sources = Connected.Svc.DataRoot is { } root ? KiSourceSettings.Load(root).Active() : KiCatalog.Sources;
        IReadOnlyList<(string Name, bool Open)> found;
        var contacted = !Connected.Svc.Offline && Connected.Svc.Probe is not null;
        if (!contacted)
            found = sources.Select(s => (s.Name, false)).ToList();
        else
            found = Connected.Svc.Probe!(sources, call.Cancellation).GetAwaiter().GetResult();
        var sb = new StringBuilder();
        sb.AppendLine(contacted ? "Nur Adressen auf diesem PC, kurzer Verbindungsversuch." : "Quellen wurden nicht kontaktiert.");
        sb.AppendLine("Online-Dienste wurden nicht gefragt.");
        foreach (var (name, open) in found)
            sb.Append("- ").Append(name).Append(": ").AppendLine(open ? "erreichbar" : "nicht erreichbar");
        var body = sb.ToString();
        var openCount = found.Count(f => f.Open);
        return Connected.Text(call, FieldKit.Slot("ki-quellen", call.Version, "status.txt"), body, openCount + " von " + found.Count + " erreichbar", "quelle.status", "KiCatalog", ["Online-Dienste wurden nicht gefragt."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Online-KI ist nicht angeschlossen.");
}

/// <summary>CORE-01: Theme, Akzent und Hintergrund nur zeigen.</summary>
public sealed class DesktopShowAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-01";
    public string ActionId => "desktop.zeigen";
    public string Label => "Desktop zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Desktop_zeigt_und_aendert_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Desktop(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        if (Connected.Svc.DataRoot is null) return FieldKit.Fail("Die Einstellungen sind nicht erreichbar.");
        var settings = UserSettings.Load(Connected.Svc.DataRoot);
        var body = "Design: " + settings.Theme + "\nAkzent: " + settings.Accent + "\nHintergrund: " + settings.Wallpaper + "\nEs wurde nichts geändert. Design 41 bleibt.\n";
        return Connected.Text(call, FieldKit.Slot("desktop", call.Version, "anzeige.txt"), body, settings.Theme + " · " + settings.Wallpaper, "desktop.anzeige", "UserSettings", ["Es wurde nichts geändert."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Der Desktop ist nicht angeschlossen. Design 41 bleibt.");
}

/// <summary>CORE-11: nur der Name des Speichers. Get wird nicht gerufen.</summary>
public sealed class SecretShowAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-11";
    public string ActionId => "geheimnis.zeigen";
    public string Label => "Speicher zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Geheimnis_zeigt_nur_den_speicher";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Secret(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var name = Connected.Svc.SecretBackend?.Invoke() ?? "Kein Speicher angeschlossen";
        var body = "Speicher: " + name + "\nEs wird kein Wert gezeigt, gesetzt oder gelöscht.\n";
        return Connected.Text(call, FieldKit.Slot("schutz", call.Version, "speicher.txt"), body, name, "geheimnis.speicher", "ISecretStore", ["Es wird kein Wert gezeigt."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Ein zweites Freigabesystem ist nicht angeschlossen.");
}

/// <summary>SYS-03: Plan, die Datei erst nach der Freigabe.</summary>
public sealed class DownloadAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-03";
    public string ActionId => "datei.laden";
    public string Label => "Datei laden";
    public SideEffectClass Effect => SideEffectClass.Message;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Download_wartet_auf_freigabe";
    public bool CanPerformSideEffect => true;
    public IReadOnlyList<string> RequiredFields => ["adresse"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Download(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "adresse") ? [] : [new("adresse", "adresse", "Adresse fehlt.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Text(parameters, "adresse").Length > 2000 ? [new("adresse", "Die Adresse ist zu lang.", true)] : [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        if (Connected.Svc.Offline) return FieldKit.Blocked("Offline: es wird nichts geladen.");
        var verdict = ResearchDesk.Classify(FieldKit.Text(call.Parameters, "adresse"));
        if (verdict.Kind != UrlKind.Lesemodus) return FieldKit.Blocked(verdict.Reason);
        var body = "Plan: " + verdict.Uri + "\nNoch nichts geladen. Die Datei kommt erst nach der Freigabe, höchstens 2 MB.\n";
        return Connected.Text(call, FieldKit.Slot("downloads", call.Version, "plan.txt"), body, "Noch nichts geladen", "datei.plan", "ResearchDesk", ["Noch nichts geladen."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => Connected.Run(() =>
    {
        if (!call.SideEffectAuthorized) return FieldKit.Blocked("Die Freigabe fehlt. Es wurde nichts geladen.");
        if (Connected.Svc.Offline) return FieldKit.Blocked("Offline: es wird nichts geladen.");
        if (Connected.Svc.ReadBytes is null) return FieldKit.Blocked("Es ist kein Lesen angeschlossen. Es wurde nichts geladen.");
        var verdict = ResearchDesk.Classify(FieldKit.Text(call.Parameters, "adresse"));
        if (verdict.Kind != UrlKind.Lesemodus || verdict.Uri is null) return FieldKit.Blocked(verdict.Reason);
        var (_, bytes, final) = Connected.Svc.ReadBytes(verdict.Uri, call.Cancellation).GetAwaiter().GetResult();
        var name = SafeName(final);
        var relative = FieldKit.Slot("downloads", call.Version, name);
        call.Projects.WriteBytes(call.ProjectId, relative, bytes);
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        if (!File.Exists(full) || new FileInfo(full).Length != bytes.Length) return FieldKit.Blocked("Die Datei liegt nach dem Laden nicht im Projekt.", attempted: true);
        return new AdapterOutcome(true, [], [], "Geladen: " + name + ", " + bytes.Length + " Bytes.", [], [], "Geladen: " + name + ", " + bytes.Length + " Bytes.");
    });

    static string SafeName(Uri uri)
    {
        var name = Path.GetFileName(uri.AbsolutePath);
        var clean = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').ToArray());
        if (clean.Length == 0 || clean.StartsWith('.')) clean = "datei.bin";
        return clean.Length > 80 ? clean[..80] : clean;
    }
}

/// <summary>SYS-03 ohne Ladewunsch: nur der Hinweis.</summary>
public sealed class DownloadNoteAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-03";
    public string ActionId => "datei.hinweis";
    public string Label => "Download-Hinweis";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Download_ohne_wunsch_holt_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Download(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        const string body = "Es wird nichts heruntergeladen. Schreib „Laden: ja“ und eine Adresse, wenn eine Datei nach der Freigabe geholt werden soll.\n";
        return Connected.Text(call, FieldKit.Slot("downloads", call.Version, "hinweis.txt"), body, "Nichts geladen", "datei.hinweis", "ResearchDesk", ["Es wird nichts heruntergeladen."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts heruntergeladen.");
}

/// <summary>SYS-04: nur ein Katalogprogramm, nur mit Lizenzdatei und Freigabe.</summary>
public sealed class InstallAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-04";
    public string ActionId => "programm.installieren";
    public string Label => "Programm installieren";
    public SideEffectClass Effect => SideEffectClass.Security;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Installation_nur_nach_zustimmung_und_freigabe";
    public bool CanPerformSideEffect => true;
    public IReadOnlyList<string> RequiredFields => ["programm"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Install(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "programm") || FieldKit.Has(parameters, "name") ? [] : [new("programm", "programm", "Programm fehlt. Zum Beispiel „Programm: ffmpeg“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var component = Catalog(call.Parameters);
        if (component is null) return FieldKit.Blocked("Dieses Programm steht nicht im Katalog. Es wird nichts installiert.");
        if (component.Mode == DeliveryMode.AutoElevated)
            return FieldKit.Blocked(component.Name + " braucht eine Installation für alle Benutzer mit Administrator-Abfrage. Das geht nicht über einen Auftrag – bitte unter Einstellungen → Zusatzprogramme einrichten.");
        if (component.Mode is not DeliveryMode.AutoUser)
            return FieldKit.Blocked(component.Name + " wird nicht automatisch installiert.");
        var body = "Plan: " + component.Name + " (" + component.Id + ")\nwinget: " + component.WingetId + "\nEs wurde nichts installiert. Die Installation wartet auf die Freigabe und die bestehende Lizenz-Zustimmung.\n";
        return Connected.Text(call, FieldKit.Slot("installation", call.Version, "plan.txt"), body, "Noch nichts installiert", "programm.plan", "DependencyManager", ["Es wurde nichts installiert."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => Connected.Run(() =>
    {
        if (!call.SideEffectAuthorized) return FieldKit.Blocked("Die Freigabe fehlt. Es wurde nichts installiert.");
        var component = Catalog(call.Parameters);
        // Sicherheitsreview B3: aus einem Auftrag nur Installationen für den eigenen Benutzer, nie systemweit mit Admin-Abfrage
        if (component is null || component.Mode is not DeliveryMode.AutoUser)
            return FieldKit.Blocked("Es wird nichts installiert.");
        if (Connected.Svc.Runner is null || Connected.Svc.Audit is null)
            return FieldKit.Blocked("Es ist kein Installationsprogramm angeschlossen. Es wurde nichts installiert.");
        var consent = Connected.Svc.ConsentFile;
        if (consent is null || !File.Exists(consent))
            return FieldKit.Blocked("Die Lizenz-Zustimmung fehlt. Es wurde nichts installiert.");
        var manager = new DependencyManager(Connected.Svc.Audit, Connected.Svc.Runner, fileExists: Connected.Svc.FileExists) { ConsentFile = consent };
        bool ok;
        try { ok = manager.InstallAsync(Principal.LocalOwner, component, null, call.Cancellation).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var consentMiss = ex.Message.Contains("Zustimmung", StringComparison.Ordinal);
            return FieldKit.Blocked(ex.Message, attempted: !consentMiss);
        }
        if (!ok) return FieldKit.Blocked("Die Installation ist nicht gelungen. Es wird kein Erfolg behauptet.", attempted: true);
        var receipt = "Installiert: " + component.Name + ". Das Programm ist danach vorhanden.";
        return new AdapterOutcome(true, [], [], receipt, [], [], receipt);
    });

    public static Component? Catalog(IReadOnlyDictionary<string, string> parameters)
    {
        var name = FieldKit.Text(parameters, "programm", "name");
        if (name.Length == 0) return null;
        return DependencyCatalog.All.FirstOrDefault(c =>
            c.Id.Equals(name, StringComparison.OrdinalIgnoreCase)
            || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || (c.WingetId?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}

/// <summary>SYS-04 ohne Installationswunsch: nur nachsehen.</summary>
public sealed class InstallCheckAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-04";
    public string ActionId => "programm.pruefen";
    public string Label => "Programm prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Programm_pruefen_installiert_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Install(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var component = InstallAdapter.Catalog(call.Parameters);
        string body;
        if (component is null)
            body = "Es wurde nichts installiert. Nenne ein Programm aus dem Katalog, zum Beispiel ffmpeg.\n";
        else if (Connected.Svc.Audit is null)
            body = component.Name + "\nEs wurde nichts installiert.\n";
        else
        {
            var manager = new DependencyManager(Connected.Svc.Audit, new IdleRunner(), fileExists: Connected.Svc.FileExists);
            var here = manager.IsInstalled(component);
            body = component.Name + ": " + (here ? "vorhanden" : "nicht vorhanden") + "\nEs wurde nichts installiert.\n";
        }
        return Connected.Text(call, FieldKit.Slot("installation", call.Version, "pruefung.txt"), body, "Nichts installiert", "programm.pruefung", "DependencyManager", ["Es wurde nichts installiert."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts installiert.");
}

/// <summary>SYS-06: Ordner im Projekt, Server erst nach der Freigabe.</summary>
public sealed class ShareAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-06";
    public string ActionId => "datei.teilen";
    public string Label => "Ordner teilen";
    public SideEffectClass Effect => SideEffectClass.Publish;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Teilen_startet_nur_nach_freigabe";
    public bool CanPerformSideEffect => true;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Share(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!Hours(parameters, out var hours)) return [new("stunden", "Stunden ist eine Zahl von 1 bis 24.", true)];
        if (hours is < 1 or > 24) return [new("stunden", "Höchstens 24 Stunden.", true)];
        return [];
    }
    public AdapterOutcome Produce(AdapterCall call)
    {
        if (!TryFolder(call, out var relative, out var error)) return FieldKit.Fail(error);
        var hours = Hours(call.Parameters, out var value) ? value : 1;
        var body = "Plan: Ordner " + relative + " für " + hours + " Stunden, nur lesen.\nNoch nichts geteilt.\n";
        call.Projects.WriteFile(call.ProjectId, relative.TrimEnd('/') + "/liesmich.txt", "Nur lesen. Die Freigabe startet erst nach der Freigabe.\n");
        return Connected.Text(call, FieldKit.Slot("teilen", call.Version, "plan.txt"), body, "Noch nichts geteilt", "teilen.plan", "FolderShare", ["Noch nichts geteilt."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => Connected.Run(() =>
    {
        if (!call.SideEffectAuthorized) return FieldKit.Blocked("Die Freigabe fehlt. Es wurde nichts geteilt.");
        if (Connected.Svc.StartShare is null) return FieldKit.Blocked("Die Freigabe ist auf diesem Weg nicht angeschlossen. Es wurde nichts geteilt.");
        if (!TryFolder(call, out var relative, out var error)) return FieldKit.Blocked(error);
        var full = call.Projects.ResolveInProject(call.ProjectId, relative);
        if (!Directory.Exists(full)) return FieldKit.Blocked("Der Ordner liegt nicht im Projekt.");
        var hours = Hours(call.Parameters, out var value) ? value : 1;
        string receipt;
        try { receipt = Connected.Svc.StartShare(full, TimeSpan.FromHours(hours)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return FieldKit.Blocked(ex.Message, attempted: true); }
        if (string.IsNullOrWhiteSpace(receipt)) return FieldKit.Blocked("Die Freigabe ist nicht gestartet.", attempted: true);
        return new AdapterOutcome(true, [], [], receipt, [], [], receipt);
    });

    static bool TryFolder(AdapterCall call, out string relative, out string error)
    {
        relative = FieldKit.Text(call.Parameters, "ordner");
        if (relative.Length == 0) relative = "teilen";
        relative = relative.Replace('\\', '/').Trim('/');
        error = "";
        if (relative.Split('/').Any(part => part is ".." or "." or "" || part.StartsWith('.')))
        {
            error = "Der Ordner bleibt im Projekt.";
            return false;
        }
        try
        {
            var full = call.Projects.ResolveInProject(call.ProjectId, relative);
            Directory.CreateDirectory(full);
            return true;
        }
        catch (Exception ex) when (ex is ProjectIsolationException or IOException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    static bool Hours(IReadOnlyDictionary<string, string> parameters, out int hours)
    {
        hours = 1;
        if (!FieldKit.Has(parameters, "stunden")) return true;
        if (!FieldKit.TryNum(parameters, "stunden", out var value)) return false;
        hours = (int)value;
        return Math.Abs(value - hours) < 0.001;
    }
}

/// <summary>SYS-06 ohne Teilungswunsch.</summary>
public sealed class ShareNoteAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-06";
    public string ActionId => "teilen.hinweis";
    public string Label => "Teilen-Hinweis";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Teilen_ohne_wunsch_startet_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Share(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        const string body = "Es wird nichts geteilt. Schreib „Teilen: ja“, wenn ein Ordner im Projekt nach der Freigabe erreichbar sein soll.\n";
        return Connected.Text(call, FieldKit.Slot("teilen", call.Version, "hinweis.txt"), body, "Nichts geteilt", "teilen.hinweis", "FolderShare", ["Es wird nichts geteilt."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts geteilt.");
}

/// <summary>SYS-07: Geräte nur lesen. Diff schreibt nicht.</summary>
public sealed class DeviceListAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-07";
    public string ActionId => "geraete.nennen";
    public string Label => "Geräte nennen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Geraete_werden_gelesen";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var list = Connected.Svc.Devices?.Invoke() ?? new DeviceScanner(new IdleRunner(), Connected.Svc.DataRoot ?? Path.GetTempPath()).ScanSys();
        var sb = new StringBuilder();
        sb.AppendLine("Geräte, nur gelesen. Es wurde nichts gestartet.");
        if (list.Count == 0) sb.AppendLine("Keine Geräte gelesen.");
        foreach (var device in list) sb.Append("- ").Append(device.Category).Append(": ").Append(device.Name.Replace('\n', ' ')).AppendLine();
        var body = sb.ToString();
        return Connected.Text(call, FieldKit.Slot("geraete", call.Version, "liste.txt"), body, list.Count + " Geräte gelesen", "geraete.liste", "DeviceScanner", ["Es wurde nichts gestartet."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird kein Gerät gestartet.");
}

/// <summary>SYS-11: Programm aus der Liste mit einer Datei im Projekt, Start erst nach der Freigabe.</summary>
public sealed class ProgramStartAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-11";
    public string ActionId => "programm.starten";
    public string Label => "Programm starten";
    public SideEffectClass Effect => SideEffectClass.Machine;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Programm_startet_nur_nach_freigabe";
    public bool CanPerformSideEffect => true;
    public IReadOnlyList<string> RequiredFields => ["programm", "datei"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Program(fields) ? new ActionDraft(ActionId, fields) : null;
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => ProgramPlanAdapter.Need(parameters);
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => ProgramPlanAdapter.Plan(call, waiting: true);
    public AdapterOutcome SideEffect(AdapterCall call) => Connected.Run(() =>
    {
        if (!call.SideEffectAuthorized) return FieldKit.Blocked("Die Freigabe fehlt. Es wurde nichts gestartet.");
        if (Connected.Svc.Launch is null) return FieldKit.Blocked("Der Start ist auf diesem Weg nicht angeschlossen. Es wurde nichts gestartet.");
        if (!ProgramPlanAdapter.Ready(call, out var app, out var full, out var error)) return FieldKit.Blocked(error);
        IReadOnlyList<string> args;
        try { args = ExternalApps.Plan(app, full); }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return FieldKit.Blocked(ex.Message); }
        string receipt;
        try { receipt = Connected.Svc.Launch(app.Exe[0], args); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return FieldKit.Blocked(ex.Message, attempted: true); }
        if (string.IsNullOrWhiteSpace(receipt)) return FieldKit.Blocked("Das Programm wurde nicht gestartet.", attempted: true);
        return new AdapterOutcome(true, [], [], receipt, [], [], receipt);
    });
}

/// <summary>SYS-11 ohne Startwunsch: nur die Argumente.</summary>
public sealed class ProgramPlanAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-11";
    public string ActionId => "programm.planen";
    public string Label => "Programm planen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Programm_planen_startet_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["programm", "datei"];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Program(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => Need(parameters);

    public static IReadOnlyList<ClarificationQuestion> Need(IReadOnlyDictionary<string, string> parameters)
    {
        var list = new List<ClarificationQuestion>();
        if (!FieldKit.Has(parameters, "programm")) list.Add(new("programm", "programm", "Programm fehlt. Zum Beispiel „Programm: vscode“.", true));
        if (!FieldKit.Has(parameters, "datei")) list.Add(new("datei", "datei", "Datei im Projekt fehlt. Schreib „Datei: notiz.txt“.", true));
        return list;
    }
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => Plan(call, waiting: false);
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Es wird nichts gestartet.");

    public static AdapterOutcome Plan(AdapterCall call, bool waiting)
    {
        if (!Ready(call, out var app, out var full, out var error)) return FieldKit.Fail(error);
        IReadOnlyList<string> args;
        try { args = ExternalApps.Plan(app, full); }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return FieldKit.Fail(ex.Message); }
        var body = "Programm: " + app.Name + "\nDatei: " + FieldKit.Text(call.Parameters, "datei") + "\nArgumente: " + string.Join(" ", args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a)) + "\nEs wurde nichts gestartet.\n";
        var preview = waiting ? "Noch nichts gestartet" : "Nichts gestartet";
        return Connected.Text(call, FieldKit.Slot("programme", call.Version, "plan.txt"), body, preview, "programm.plan", "ExternalApps", ["Es wurde nichts gestartet."]);
    }

    public static bool Ready(AdapterCall call, out ExternalApp app, out string full, out string error)
    {
        app = ExternalApps.All[0];
        full = "";
        error = "";
        var name = FieldKit.Text(call.Parameters, "programm");
        var found = ExternalApps.All.FirstOrDefault(a => a.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found is null) { error = "Dieses Programm steht nicht in der Liste."; return false; }
        app = found;
        var relative = FieldKit.Text(call.Parameters, "datei").Replace('\\', '/').Trim();
        if (relative.Length == 0 || relative.Split('/').Any(part => part is ".." or ".")) { error = "Die Datei bleibt im Projekt."; return false; }
        try { full = call.Projects.ResolveInProject(call.ProjectId, relative); }
        catch (ProjectIsolationException ex) { error = ex.Message; return false; }
        if (!File.Exists(full) && !Directory.Exists(full)) { error = "Die Datei liegt nicht im Projekt."; return false; }
        if (!ExternalApps.Accepts(app, full)) { error = app.Name + " kann diese Datei nicht öffnen."; return false; }
        return true;
    }
}

/// <summary>SYS-12: Schrift und Größe nur zeigen.</summary>
public sealed class ThemeShowAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-12";
    public string ActionId => "darstellung.zeigen";
    public string Label => "Darstellung zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Darstellung_zeigt_und_aendert_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Theme(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        if (Connected.Svc.DataRoot is null) return FieldKit.Fail("Die Einstellungen sind nicht erreichbar.");
        var settings = UserSettings.Load(Connected.Svc.DataRoot);
        var body = "Design: " + settings.Theme + "\nAkzent: " + settings.Accent + "\nSchrift-Minimum: " + settings.FontMin + "\nAnzeige: " + settings.UiScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "\nEs wurde nichts geändert.\n";
        return Connected.Text(call, FieldKit.Slot("darstellung", call.Version, "anzeige.txt"), body, settings.Theme + " · Schrift " + settings.FontMin, "darstellung.anzeige", "UserSettings", ["Es wurde nichts geändert."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Das Theme ist nicht angeschlossen. Design 41 bleibt.");
}

/// <summary>SYS-13: Protokoll und Prüfung. Der Explorer bleibt zu.</summary>
public sealed class SelfTestAdapter : IStudioActionAdapter
{
    public string StudioId => "SYS-13";
    public string ActionId => "pruefung.starten";
    public string Label => "Prüfung schreiben";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Pruefung_schreibt_protokoll";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Explorer(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => HealText.Write(call, "pruefung", "Der Explorer wurde nicht geöffnet.");
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Der Explorer ist nicht angeschlossen.");
}

/// <summary>X-02: Prüfung ohne Reparatur. Verbesserung 35 bleibt offen.</summary>
public sealed class RepairShowAdapter : IStudioActionAdapter
{
    public string StudioId => "X-02";
    public string ActionId => "reparatur.zeigen";
    public string Label => "Reparatur zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Reparatur_prueft_und_aendert_nichts";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Repair(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call) => HealText.Write(call, "reparatur", "Es wurde nichts geändert. Verbesserung 35 bleibt offen.");
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Die Reparatur ist nicht angeschlossen. Verbesserung 35 bleibt offen.");
}

static class HealText
{
    public static AdapterOutcome Write(AdapterCall call, string folder, string limit)
    {
        var intact = Connected.Svc.AuditIntact?.Invoke() ?? false;
        var sb = new StringBuilder();
        sb.Append("Protokoll: ").AppendLine(intact ? "vollständig und unverändert" : "beschädigt");
        if (Connected.Svc.DataRoot is { } root && Connected.Svc.Audit is { } audit)
        {
            foreach (var item in new SelfHealer(root, audit).Run(false, DateTime.Now))
                sb.Append("- ").Append(item.Title).Append(": ").Append(item.State).Append(" · ").Append(item.Detail.Replace('\n', ' ')).AppendLine();
        }
        else sb.AppendLine("Die Prüfung ist hier nicht angeschlossen.");
        sb.AppendLine(limit);
        var body = sb.ToString();
        return Connected.Text(call, FieldKit.Slot(folder, call.Version, "protokoll.txt"), body, intact ? "Protokoll in Ordnung" : "Protokoll prüfen", folder + ".protokoll", "AuditLog", [limit]);
    }
}

/// <summary>X-08: adb devices -l, wenn adb schon da ist. Sonst kein Installieren.</summary>
public sealed class PhoneListAdapter : IStudioActionAdapter
{
    public string StudioId => "X-08";
    public string ActionId => "handy.nennen";
    public string Label => "Handys nennen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Handy_listet_ohne_verbindung";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Phone(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var adb = Connected.Svc.FindAdb is { } find
            ? find()
            : MobileBridge.FindAdb(File.Exists, Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("PATH"));
        string body;
        if (string.IsNullOrWhiteSpace(adb))
            body = "adb ist nicht da. Trendo installiert es aus diesem Auftrag nicht.\n";
        else if (Connected.Svc.Runner is null)
            body = "adb ist da. Es wurde nicht gestartet. Es gibt keine Verbindung, keinen Spiegel und kein Foto.\n";
        else
        {
            var (exit, output) = Connected.Svc.Runner.RunAsync(adb, "devices -l", null, call.Cancellation).GetAwaiter().GetResult();
            var phones = exit == 0 ? MobileBridge.ParseDevices(output) : [];
            var sb = new StringBuilder();
            sb.AppendLine("Nur die Liste. Es wurde nichts verbunden, nichts gespiegelt und kein Foto geholt.");
            if (exit != 0) sb.AppendLine("adb hat keine Liste geliefert.");
            else if (phones.Count == 0) sb.AppendLine("Kein Handy in der Liste.");
            foreach (var phone in phones) sb.Append("- ").Append(phone.Model).Append(" · ").Append(phone.State).Append(" · ").AppendLine(phone.Link.ToString());
            body = sb.ToString();
        }
        var count = body.Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal));
        return Connected.Text(call, FieldKit.Slot("handy", call.Version, "liste.txt"), body, count + " Handys gelesen", "handy.liste", "MobileBridge", ["Es wurde nichts verbunden."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Das Handy ist nicht angeschlossen. Es wird keine Verbindung geöffnet.");
}

/// <summary>CORE-04: gespeicherte Geräte nur zeigen.</summary>
public sealed class FusionShowAdapter : IStudioActionAdapter
{
    public string StudioId => "CORE-04";
    public string ActionId => "verbund.zeigen";
    public string Label => "Verbund zeigen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "ConnectedPromptTests.Verbund_zeigt_ohne_kopplung";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => [];
    public IReadOnlyList<string> OutputKinds => ["txt"];
    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known)
    {
        var fields = FieldKit.Read(prompt, known);
        return Intent.Fusion(fields) ? null : new ActionDraft(ActionId, fields);
    }
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) => [];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters) => [];
    public AdapterOutcome Produce(AdapterCall call)
    {
        var nodes = Connected.Svc.DataRoot is { } root && Connected.Svc.Audit is { } audit ? new FusionStore(root, audit).Nodes() : [];
        var sb = new StringBuilder();
        sb.AppendLine("Gespeicherte Geräte, nur gelesen. Es wurde nichts gekoppelt.");
        if (nodes.Count == 0) sb.AppendLine("Kein gekoppeltes Gerät.");
        foreach (var node in nodes) sb.Append("- ").Append(node.Name).Append(" · ").Append(node.Kind).AppendLine();
        var body = sb.ToString();
        return Connected.Text(call, FieldKit.Slot("verbund", call.Version, "liste.txt"), body, nodes.Count + " Geräte im Verbund", "verbund.liste", "FusionStore", ["Es wurde nichts gekoppelt."]);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Der Verbund ist nicht angeschlossen. Es wird nichts gekoppelt.");
}

sealed class IdleRunner : IProcessRunner
{
    public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) =>
        throw new InvalidOperationException("Es wird kein Programm gestartet.");
}
