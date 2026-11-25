using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>B4 (T-0706): Freigaben in den Kern. Eine Wirkung mit Folgen (installieren, laden, Programm starten, Daten nach außen geben …)
/// läuft nur so: Anfrage im <see cref="ApprovalGate"/> → eigene Rückfrage an die Person (was genau passiert, „Abbrechen“ vorbelegt) →
/// Freigabe durch die angemeldete Person (<see cref="CurrentPrincipal"/>, Rollenprüfung im Kern) → Ausführung über den Kern-Dienst mit
/// genau dieser Freigabe (<c>ApprovalGate.Execute</c>, einmalig, im Protokoll). Ein Klick auf den Aktionsknopf allein führt nichts aus.</summary>
public partial class MainWindow
{
    /// <summary>Was Trendo gleich tun will – Inhalt der Rückfrage. Action/Target binden die Freigabe im Kern.</summary>
    public sealed record Wirkung(string Action, string Target, string Title, string Text, string YesLabel, string? Preview = null);

    /// <summary>Tests: ersetzt den Rückfrage-Dialog (die Antwort der Person). Ohne ausdrückliches Ja (false) wird nichts ausgeführt.</summary>
    public Func<Wirkung, Task<bool>>? FreigabeConfirm { get; set; }
    /// <summary>Zuletzt gestellte Rückfrage und zugehörige Anfrage (für Tests und Anzeige).</summary>
    public Wirkung? LastWirkung { get; private set; }
    public ApprovalRequest? LastFreigabe { get; private set; }

    /// <summary>B4: die angemeldete Person mit ihrer Rolle aus dem Profil. Kinderprofil = Rolle Benutzer – der Kern lehnt dessen Freigaben ab.</summary>
    public Principal CurrentPrincipal => Principal.ForProfile(_roleChild);

    /// <summary>T-0764 (14. Prüfrunde, H1/H2): Rolle aus der geschützten Quelle (<see cref="ProfileRole"/>), nie aus settings.json. Bis zum Laden: Kind (fail-closed).</summary>
    bool _roleChild = true;

    /// <summary>Die EINZIGE Stelle, die <c>_settings</c> ersetzt (Start, Einlesen, Zurücksetzen, Wiederherstellen): Rolle neu aus der geschützten Quelle,
    /// <see cref="UserSettings.Child"/> nur als Spiegel.</summary>
    void UseSettings(UserSettings s)
    {
        _roleChild = ProfileRole.IsChild(_core.DataRoot);
        s.Child = _roleChild;
        _settings = s;
    }

    /// <summary>T-0764 (14. Prüfrunde, M): Projektordner – für Kinder nur in der Freigabe (sonst „projekte“ im Datenordner, fail-closed). Erwachsene: wie eingestellt.</summary>
    internal string ProjectsBase(string fallback)
    {
        var pf = _settings.ProjectsFolder;
        var want = string.IsNullOrEmpty(pf) || !Path.IsPathRooted(pf) ? fallback : pf;
        if (!_roleChild) return want;
        return Trendo.Core.Sys.ChildWrite.Problem(CurrentPrincipal, want, checkName: false) is null ? want : Path.Combine(_core.DataRoot, "projekte");
    }

    /// <summary>T-0764 (14. Prüfrunde): Einrichtung, Zurücksetzen, Wiederherstellen – nur Erwachsene. Kinder bekommen eine Seite mit Hinweis.</summary>
    Control KidSetupPage() => Page("Einrichtung", new StackPanel { Spacing = 8, Children = { P(Rbac.NurErwachseneEinstellungen, Text, 13) } });
    /// <summary>Darf die angemeldete Person Wirkungen freigeben? (Anzeige; durchgesetzt wird es im Kern.)</summary>
    public bool CanApprove => Rbac.Has(CurrentPrincipal, Permission.ApproveAction);

    /// <summary>T-0764: Darf die angemeldete Person Programme installieren, aktualisieren und entfernen? (Anzeige; durchgesetzt im Kern.)</summary>
    public bool CanManageSoftware => Rbac.Has(CurrentPrincipal, Permission.ManageSoftware);

    /// <summary>T-0764: Rechteprüfung im Kern vor einem Installations- oder Entfernen-Weg, der erst lädt und dann den Kern ruft.
    /// Ohne Recht: Protokoll „rbac.denied“, verständliche Meldung auf der Seite, false.</summary>
    internal bool DarfSoftware(string what, string view)
    {
        try { Rbac.DemandSoftware(CurrentPrincipal, what, _core.Audit); return true; }
        catch (UnauthorizedAccessException e) { Say(e.Message, view); Toast(e.Message); return false; }
    }

    /// <summary>Hinweis, wenn ein Kinderprofil etwas freigeben müsste.</summary>
    public const string NurErwachsene = "Frag einen Erwachsenen – das geht nur in einem Erwachsenen-Profil. Es wurde nichts geändert.";

    /// <summary>Anfrage anlegen, Person fragen, bei Ja freigeben. Gibt die Id der erteilten Freigabe zurück, sonst null (nichts passiert).</summary>
    internal Task<string?> FreigabeAsync(Wirkung w) =>
        FreigabeAsync(w, () => _core.Approvals.Request(CurrentPrincipal, w.Action, w.Title, w.Target, preview: w.Preview));

    /// <summary>Wie oben, aber die Anfrage legt ein Kern-Dienst an (z. B. ein Ablauf mit Probelauf als Vorschau).</summary>
    internal async Task<string?> FreigabeAsync(Wirkung w, Func<ApprovalRequest> request)
    {
        LastWirkung = w;
        ApprovalRequest r;
        try { r = request(); }
        catch (UnauthorizedAccessException) { Toast(NurErwachsene); return null; }
        LastFreigabe = r;
        if (!CanApprove)
        {
            // Kinderprofil: gar nicht erst fragen – der Kern würde die Freigabe ohnehin ablehnen
            Zurueckziehen(r.Id);
            _core.Audit.Append(CurrentPrincipal.Name, "freigabe.nicht.erlaubt", $"{w.Action} {w.Target}");
            Toast(NurErwachsene); return null;
        }
        bool yes;
        try { yes = await (FreigabeConfirm ?? FreigabeDialog)(w); }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { yes = false; }
        if (!yes)
        {
            try { _core.Approvals.Deny(r.Id, CurrentPrincipal); } // „Abbrechen“ der Person = Ablehnung im Protokoll
            catch (ApprovalException) { } // Not-Stopp o. Ä. hat sie schon verworfen
            _core.Audit.Append(CurrentPrincipal.Name, "freigabe.nein", $"{w.Action} {w.Target}");
            return null;
        }
        return FreigabeErteilen(r.Id) ? r.Id : null;
    }

    /// <summary>„Ja“ auf eine sichtbare Rückfrage-Karte der Seite selbst (die Karte nennt die Wirkung und bietet „Später“/„Abbrechen“).
    /// Nur aus dem Ja-Knopf einer solchen Karte aufrufen – Aktionsknöpfe ohne eigene Rückfrage nehmen <see cref="FreigabeAsync(Wirkung)"/>.
    /// Die erlaubten Aufrufer stehen im Architekturtest (FreigabenKernTests).</summary>
    internal string? FreigabeAusRueckfrage(Wirkung w)
    {
        LastWirkung = w;
        ApprovalRequest r;
        try { r = _core.Approvals.Request(CurrentPrincipal, w.Action, w.Title, w.Target, preview: w.Preview); }
        catch (UnauthorizedAccessException) { Toast(NurErwachsene); return null; }
        LastFreigabe = r;
        return FreigabeErteilen(r.Id) ? r.Id : null;
    }

    /// <summary>Dauerfreigabe, die die Person selbst als Einstellung gesetzt hat (z. B. „Freie Modelle automatisch laden“). Gilt nur in einem
    /// Erwachsenen-Profil; ein Kinderprofil lädt nichts automatisch (Kern lehnt ab, Anfrage wird zurückgezogen). Steht im Protokoll.</summary>
    internal bool FreigabeAusEinstellung(string id, string setting)
    {
        if (!CanApprove) { Zurueckziehen(id); return false; }
        try { _core.Approvals.Approve(id, CurrentPrincipal); }
        catch (Exception ex) when (ex is ApprovalException or UnauthorizedAccessException) { return false; }
        _core.Audit.Append(CurrentPrincipal.Name, "freigabe.einstellung", $"{id} {setting}");
        return true;
    }

    /// <summary>T-0713: „Nein“ der Person auf eine Rückfrage. Erwachsene lehnen im Kern ab (im Protokoll mit ihrer Rolle), ein Kinderprofil zieht die
    /// eigene Anfrage zurück – nie mehr „im Namen des Besitzers“. Schon entschiedene oder abgelaufene Anfragen bleiben, wie sie sind.</summary>
    internal void Ablehnen(string id)
    {
        if (!CanApprove) { Zurueckziehen(id); return; }
        try { _core.Approvals.Deny(id, CurrentPrincipal); }
        catch (ApprovalException) { }
    }

    /// <summary>Offene Anfrage zurückziehen (Kinderprofil, das selbst angefragt hat). Fremde Anfragen bleiben offen und verfallen nach Ablauf.</summary>
    void Zurueckziehen(string id)
    {
        try { _core.Approvals.Withdraw(id, CurrentPrincipal); }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Freigabe einer offenen Anfrage durch die angemeldete Person – nur aus einem eigenen Entscheidungsschritt aufrufen
    /// (Rückfrage, Seite „Freigaben“, zweiter Knopf nach „anfragen“). Kinderprofil oder Not-Stopp: false, nichts freigegeben.</summary>
    internal bool FreigabeErteilen(string id)
    {
        try { _core.Approvals.Approve(id, CurrentPrincipal); return true; }
        catch (UnauthorizedAccessException)
        {
            Zurueckziehen(id); // Anfrage nicht offen liegen lassen
            Toast(NurErwachsene); return false;
        }
        catch (ApprovalException ex) { Toast(ex.Message); return false; }
    }

    /// <summary>Rückfrage in einem eigenen Fenster wie beim App-Center: „Abbrechen“ ist vorbelegt, Esc und Schließen lehnen ab.</summary>
    async Task<bool> FreigabeDialog(Wirkung w)
    {
        var dlg = new Window { Title = w.Title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Name = "freigabe-ja", Content = w.YesLabel, Background = Accent };
        var no = new Button { Name = "freigabe-nein", Content = "Abbrechen", IsDefault = true, IsCancel = true };
        yes.Click += (_, _) => dlg.Close(true);
        no.Click += (_, _) => dlg.Close(false);
        dlg.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; dlg.Close(false); } };
        var body = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = w.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "freigabe-text", Text = w.Text, TextWrapping = TextWrapping.Wrap } } };
        if (w.Preview is { Length: > 0 } pv)
            body.Children.Add(new SelectableTextBlock { Name = "freigabe-vorschau", Text = pv.Length > 1500 ? pv[..1500] + " …" : pv, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } });
        dlg.Content = body;
        dlg.Opened += (_, _) => no.Focus();
        return await dlg.ShowDialog<bool>(this);
    }

    /// <summary>Aktionsname für „KI-Modell laden“.</summary>
    public const string KiPullAction = "ki.pull";

    /// <summary>Rückfrage-Text für „KI-Modell laden“ (Größe, Lizenz, Herkunft).</summary>
    internal static Wirkung KiPullWirkung(string target, string name, double sizeGb, string license) => new(KiPullAction, target, $"KI „{name}“ laden?",
        $"KI-Modell: {name}\nGröße: etwa {sizeGb:0.0} GB\nLizenz: {license}\nWoher: offizielle Quelle, Prüfsumme wird kontrolliert.\n\nTrendo lädt das Modell auf diesen PC. Entfernen kannst du es jederzeit in der KI-Bibliothek.", "Laden");

    /// <summary>Rückfrage-Text für „Zusatzprogramm einrichten“ aus dem Katalog (Name, Lizenz, Größe, Herkunft).</summary>
    internal static Wirkung InstallWirkung(Component c, string wozu) => new(DependencyManager.InstallAction, c.Id, $"„{c.Name}“ einrichten?",
        $"Programm: {c.Name}\nWofür: {wozu}\nLizenz: {c.License}{(c.ApproxMb > 0 ? $"\nGröße: etwa {c.ApproxMb} MB" : "")}\nWoher: offizielle Quelle, Prüfsumme wird kontrolliert.\n\nTrendo lädt das Programm und richtet es auf diesem PC ein.", "Einrichten");

    /// <summary>Wie <see cref="InstallMitFreigabe"/>, aber mit der Fortschrittsanzeige der Komponenten-Seite (InstallOne). null = abgelehnt.</summary>
    internal async Task<bool?> InstallOneMitFreigabe(Component c, string wozu, bool refresh = true) =>
        await FreigabeAsync(InstallWirkung(c, wozu)) is { } id ? await InstallOne(c, approvalId: id, refresh: refresh) : null;

    /// <summary>Zusatzprogramm nach eigener Rückfrage einrichten – Ausführung nur über den Kern mit genau dieser Freigabe.
    /// null = abgelehnt bzw. nicht erlaubt (nichts geändert); sonst Ergebnis der Installation.</summary>
    internal async Task<bool?> InstallMitFreigabe(Component c, string wozu, Action? starting = null, Action<string>? progress = null)
    {
        if (await FreigabeAsync(InstallWirkung(c, wozu)) is not { } id) return null;
        if (!_deps.HasConsent) _deps.GiveConsent(CurrentPrincipal);
        starting?.Invoke();
        if (_opt.Isolated) { _core.Approvals.Execute(id, DependencyManager.InstallAction, c.Id, () => false); return false; } // Testmodus: Freigabe einlösen, nichts installieren
        return await Task.Run(() => _deps.InstallApprovedAsync(_core.Approvals, id, CurrentPrincipal, c, progress));
    }

    // ───────────── T-0713: Sammel-Rückfrage für Ersteinrichtung, „alle fehlenden installieren“ und „Reparieren“ ─────────────

    /// <summary>Tests: ersetzt den Sammel-Dialog. Bekommt die Liste, gibt die Ids der gewählten Komponenten zurück (null = Abbrechen).
    /// Ohne diesen Ersatz, aber mit <see cref="FreigabeConfirm"/>, gilt dessen Antwort für die ganze Liste (alle oder keine).</summary>
    public Func<IReadOnlyList<Component>, Task<IReadOnlyCollection<string>?>>? SammelConfirm { get; set; }
    /// <summary>Zuletzt erteilte Sammel-Freigaben (Komponente → Freigabe-Id), für Tests und Anzeige.</summary>
    public IReadOnlyList<(Component C, string Id)> LastSammel { get; private set; } = [];

    /// <summary>Gültigkeit einer Sammel-Freigabe: lang genug für eine ganze Einrichtung nacheinander. Was nicht eingelöst wird, verwirft
    /// <see cref="SammelVerwerfen"/> am Ende sofort.</summary>
    internal static readonly TimeSpan SammelTtl = TimeSpan.FromHours(2);

    /// <summary>Woher das Programm kommt (für die Rückfrage).</summary>
    internal static string Herkunft(Component c) => c switch
    {
        { Mode: DeliveryMode.Included } => "Trendo-Vollpaket (Prüfsumme wird kontrolliert)" + (c.WingetId is { } w ? $", sonst offizielle Quelle über winget ({w})" : ""),
        { PostInstall: not null } => "Ollama-Modellbibliothek (offizielle Quelle)",
        { PortableZip: { } url } when Uri.TryCreate(url, UriKind.Absolute, out var u) => $"offizieller Download von {u.Host}, feste Prüfsumme",
        { WingetId: { } w } => $"offizielle Quelle über winget ({w})",
        { OfficialUrl: { } o } when Uri.TryCreate(o, UriKind.Absolute, out var u) => "offizielle Quelle " + u.Host,
        _ => "offizielle Quelle",
    };

    /// <summary>Was die Einrichtung auf dem PC bewirkt (für die Rückfrage).</summary>
    internal static string WirkungVon(Component c) => c.Mode == DeliveryMode.AutoElevated
        ? "wird für alle Benutzer installiert – Windows fragt einmal nach Administratorrechten"
        : c.PostInstall is not null ? "wird in die lokale KI geladen (bleibt auf diesem PC)" : "wird nur für dich eingerichtet, ohne Administratorrechte";

    /// <summary>Eine Zeile je Komponente: Wofür, Wirkung, Lizenz, Größe, Herkunft.</summary>
    internal static string SammelZeile(Component c) =>
        $"Wofür: {c.Purpose}\nWirkung: {WirkungVon(c)}\nLizenz: {c.License} · Größe: {(c.ApproxMb > 0 ? $"etwa {c.ApproxMb} MB" : "klein")}\nWoher: {Herkunft(c)}";

    /// <summary>Eine Rückfrage mit der Liste aller Komponenten (je Eintrag abwählbar, „Abbrechen“ vorbelegt). Für jede gewählte Komponente entsteht
    /// genau EINE Freigabe im Kern (Anfrage → Freigabe durch die angemeldete Person); eingelöst wird sie nur über <see cref="InstallFreigegebenAsync"/>
    /// bzw. <c>DependencyManager.InstallApprovedAsync</c>. Kinderprofil: keine Rückfrage, keine Freigabe. Leere Liste = nichts freigegeben.</summary>
    internal async Task<IReadOnlyList<(Component C, string Id)>> SammelFreigabeAsync(IReadOnlyList<Component> comps, string titel)
    {
        LastSammel = [];
        comps = comps.DistinctBy(c => c.Id).ToList();
        if (comps.Count == 0) return [];
        if (!CanApprove)
        {
            _core.Audit.Append(CurrentPrincipal.Name, "freigabe.nicht.erlaubt", $"{DependencyManager.InstallAction} sammel:{string.Join(",", comps.Select(c => c.Id))}");
            Toast(NurErwachsene); return [];
        }
        IReadOnlyCollection<string>? chosen;
        try
        {
            chosen = SammelConfirm is { } sc ? await sc(comps)
                : FreigabeConfirm is { } fc ? (await fc(new Wirkung(DependencyManager.InstallAction, "sammel", titel, string.Join("\n\n", comps.Select(c => c.Name + "\n" + SammelZeile(c))), "Einrichten")) ? comps.Select(c => c.Id).ToList() : null)
                : await SammelDialog(comps, titel);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { chosen = null; }
        var pick = comps.Where(c => chosen?.Contains(c.Id) == true).ToList();
        _core.Audit.Append(CurrentPrincipal.Name, pick.Count == 0 ? "freigabe.nein" : "freigabe.sammel",
            $"gewählt={string.Join(",", pick.Select(c => c.Id))} abgewählt={string.Join(",", comps.Except(pick).Select(c => c.Id))}");
        if (pick.Count == 0) return [];
        var granted = new List<(Component, string)>();
        foreach (var c in pick)
        {
            ApprovalRequest r;
            try { r = _core.Approvals.Request(CurrentPrincipal, DependencyManager.InstallAction, $"„{c.Name}“ einrichten", c.Id, SammelTtl, SammelZeile(c)); }
            catch (UnauthorizedAccessException) { Toast(NurErwachsene); break; }
            LastFreigabe = r;
            if (!FreigabeErteilen(r.Id)) { SammelVerwerfen(granted.Select(g => g.Item2).Append(r.Id)); return []; } // Not-Stopp o. Ä.: nichts halb freigegeben lassen
            granted.Add((c, r.Id));
        }
        return LastSammel = granted;
    }

    /// <summary>Nicht eingelöste Sammel-Freigaben sofort verwerfen (Abbruch, Fehler). Danach kann keine davon mehr etwas ausführen.</summary>
    internal void SammelVerwerfen(IEnumerable<string> ids)
    {
        foreach (var id in ids)
            try { _core.Approvals.Discard(id, CurrentPrincipal); }
            catch (UnauthorizedAccessException) { }
    }

    /// <summary>Eine Komponente mit genau ihrer Sammel-Freigabe einrichten. Testmodus: Freigabe einlösen, nichts installieren (wie <see cref="InstallMitFreigabe"/>);
    /// mit <see cref="InstallOverride"/> läuft der Ersatz im Tor.</summary>
    internal async Task<bool> InstallFreigegebenAsync(Component c, string approvalId)
    {
        if (!_deps.HasConsent) _deps.GiveConsent(CurrentPrincipal);
        if (InstallOverride is { } fake) return await _core.Approvals.Execute(approvalId, DependencyManager.InstallAction, c.Id, () => fake(c));
        if (_opt.Isolated)
        {
            _core.Approvals.Execute(approvalId, DependencyManager.InstallAction, c.Id, () => false);
            _core.Audit.Append(CurrentPrincipal.Name, "deps.install.testmodus", c.Id);
            return false;
        }
        return await InstallOne(c, approvalId: approvalId, refresh: false);
    }

    /// <summary>Sammel-Rückfrage im eigenen Fenster: je Komponente ein Haken (vorbelegt) mit Wofür, Wirkung, Lizenz, Größe und Herkunft.
    /// „Abbrechen“ ist vorbelegt, Esc und Schließen lehnen alles ab. Ohne Haken ist „Einrichten“ gesperrt.</summary>
    async Task<IReadOnlyCollection<string>?> SammelDialog(IReadOnlyList<Component> comps, string titel)
    {
        var dlg = new Window { Title = titel, Width = 560, MaxHeight = 720, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var yes = new Button { Name = "sammel-ja", Background = Accent };
        var no = new Button { Name = "sammel-nein", Content = "Abbrechen", IsDefault = true, IsCancel = true };
        var sum = new TextBlock { Name = "sammel-summe", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var boxes = new List<(CheckBox Box, Component C)>();
        var list = new StackPanel { Spacing = 8 };
        foreach (var c in comps)
        {
            var cb = new CheckBox { Name = "sammel-" + c.Id, IsChecked = true, Content = new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = c.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Name = "sammel-text-" + c.Id, Text = SammelZeile(c), FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } } };
            boxes.Add((cb, c)); list.Children.Add(cb);
        }
        void Update()
        {
            var on = boxes.Where(b => b.Box.IsChecked == true).Select(b => b.C).ToList();
            sum.Text = $"{on.Count} von {comps.Count} ausgewählt · zusammen etwa {on.Sum(c => c.ApproxMb)} MB";
            yes.Content = on.Count == 0 ? "Nichts ausgewählt" : $"{on.Count} einrichten";
            yes.IsEnabled = on.Count > 0;
        }
        foreach (var (b, _) in boxes) b.IsCheckedChanged += (_, _) => Update();
        Update();
        yes.Click += (_, _) => dlg.Close(boxes.Where(b => b.Box.IsChecked == true).Select(b => b.C.Id).ToList());
        no.Click += (_, _) => dlg.Close(null);
        dlg.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; dlg.Close(null); } };
        dlg.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = titel, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "sammel-intro", Text = "Trendo lädt diese Programme und richtet sie auf diesem PC ein. Nimm den Haken weg bei allem, was du nicht möchtest. Jedes Programm wird nur mit deiner Freigabe eingerichtet und steht im Protokoll.", TextWrapping = TextWrapping.Wrap },
            new ScrollViewer { MaxHeight = 440, Content = list },
            sum,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } } } };
        dlg.Opened += (_, _) => no.Focus();
        return await dlg.ShowDialog<List<string>?>(this);
    }
}
