using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Trendo.Core;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Welle 0: Ersteinrichtung, Basispaket, Studio-Pakete, KI nach Hardware, Updates & Sicherung, Systemcheck.</summary>
public sealed partial class MainWindow
{
    private UserSettings _settings = new();
    private int _wizStep;
    private bool _wizChangeLock;
    private readonly HashSet<string> _wizPacks = [];
    private string _updateInfo = "Noch nicht geprüft";

    private Backup Backups => new(_core.DataRoot, Path.Combine(_core.DataRoot, "backups"), _core.Audit);

    private void InitSetup(AppOptions opt)
    {
        if (string.IsNullOrEmpty(_settings.ProjectsFolder)) _settings.ProjectsFolder = opt.Isolated ? Path.Combine(_core.DataRoot, "Projekte") : UserSettings.DefaultProjectsFolder();
        foreach (var p in SetupPacks.Studios.Where(p => p.Default)) _wizPacks.Add(p.Id);
        _showMap["einrichtung"] = SetupWizardView; _showMap["systemcheck"] = SystemCheckView; _showMap["sicherung"] = BackupView;
        for (var i = 0; i < 9; i++) { var step = i; _showMap[$"einrichtung-{i}"] = () => { _wizStep = step; return SetupWizardView(); }; }
        // Geplante Sicherung beim Start (im Hintergrund, ohne Rückfrage – betrifft nur eigene Daten)
        if (_settings.SetupDone && Backups.Due(DateTime.Now, _settings.BackupEveryHours))
            _ = Task.Run(() => { try { Backups.Create(DateTime.Now); } catch (Exception ex) { _core.Audit.Append("app", "backup.failed", ex.GetType().Name); } });
        // Updates (U1–U6): siehe MainWindow.Updates.cs – startet nach dem Öffnen des Fensters
    }

    private bool NeedsSetup(AppOptions opt) => opt.FirstRun && !_settings.SetupDone && opt.Page is null;

    // ---------- Ersteinrichtung – Wunsch 27.10.2026 (B1–B8): „wie bei Windows, aber viel besser“ ----------
    private ProfileLock.Kind _wizLockKind = ProfileLock.Kind.None;
    private string _wizSecret = "", _wizSecret2 = "", _wizMsg = "";
    private string? _wizRecovery;
    private bool _wizRecoveryNoted, _wizBookmarks = true, _wizWinFolders = true, _wizPersonChild;
    private string _wizPersonName = "";
    static readonly string[] AvatarEmojis = ["🙂", "🦊", "🐼", "🦁", "🐬", "🌻", "🚀", "🎨", "🎮", "🐱", "🐶", "⭐"];
    public static readonly string[] WizardTitles = ["Willkommen", "Anmeldung", "Ordner & Daten", "Aussehen", "Wofür nutzt du Trendo?", "Datenschutz", "Handy verbinden", "Weitere Personen", "Fertig"];
    public ProfileLock Lock => new(_core.DataRoot);
    public string AvatarFile => Path.Combine(_core.DataRoot, "profil", "bild.png");

    /// <summary>Profilbild: Foto (falls gewählt) oder Emoji im runden, gold umrandeten Kreis.</summary>
    Control AvatarView(double size, string? avatar = null, string? root = null)
    {
        avatar ??= _settings.Avatar;
        var file = Path.Combine(root ?? _core.DataRoot, "profil", "bild.png");
        Control inner = avatar == "bild" && File.Exists(file)
            ? new Image { Source = new Avalonia.Media.Imaging.Bitmap(file), Stretch = Stretch.UniformToFill, Width = size, Height = size }
            : new TextBlock { Text = avatar.StartsWith("emoji:", StringComparison.Ordinal) ? avatar[6..] : "🙂", FontSize = size * 0.55, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), ClipToBounds = true, BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(2),
            Background = new RadialGradientBrush { GradientStops = { new GradientStop(Color.Parse("#2B4C86"), 0), new GradientStop(Color.Parse("#0E1B3A"), 1) } }, Child = inner };
    }

    async Task PickAvatarPhoto()
    {
        var req = new PickRequest("wiz-avatar-photo", false, false, ["png", "jpg", "jpeg", "webp", "bmp"]);
        string[]? got = PickOverride is { } f ? await f(req)
            : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Profilbild wählen", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }] }))
                .Select(x => x.TryGetLocalPath()).OfType<string>().ToArray();
        if (got is not { Length: > 0 }) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AvatarFile)!);
            using (var s = File.OpenRead(got[0]))
            {
                using var bmp = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(s, 256);
                bmp.Save(AvatarFile, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            _settings.Avatar = "bild"; _settings.Save(_core.DataRoot);
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { _wizMsg = "Das Bild ließ sich nicht öffnen: " + e.Message; }
        Show("einrichtung");
    }

    private Control SetupWizardView()
    {
        var sp = new StackPanel { Spacing = 10, MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Left };
        var titles = WizardTitles;
        // Fortschritt: Punkte statt nur Zahlen
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var i = 0; i < titles.Length; i++)
            dots.Children.Add(new Border { Width = i == _wizStep ? 26 : 9, Height = 9, CornerRadius = new CornerRadius(5), Background = i < _wizStep ? Brush.Parse("#4ADE80") : i == _wizStep ? Brush.Parse("#D4A84B") : Brush.Parse("#33FFFFFF") });
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { dots, P($"Schritt {_wizStep + 1} von {titles.Length}", null, 11) } });
        sp.Children.Add(H(titles[_wizStep], 20));
        Button Next(string label = "Weiter", Func<bool>? check = null)
        {
            var b = new Button { Name = "wiz-next", Content = label, Background = Accent, Padding = new Thickness(16, 6) };
            b.Click += (_, _) => { if (check is not null && !check()) { Show("einrichtung"); return; } _wizMsg = ""; _wizChangeLock = false; _wizStep++; _settings.SetupStep = Math.Max(_settings.SetupStep, _wizStep); _settings.Save(_core.DataRoot); Show("einrichtung"); };
            return b;
        }
        var back = new Button { Name = "wiz-back", Content = "Zurück", IsVisible = _wizStep > 0 }; back.Click += (_, _) => { _wizMsg = ""; _wizStep--; Show("einrichtung"); };
        Button? next = null;
        switch (_wizStep)
        {
            case 0: // B1: Name und Profilbild
            {
                sp.Children.Add(P("Schön, dass du da bist! Trendo OS richtet sich jetzt in ein paar Schritten für dich ein. Alles bleibt auf diesem PC – ein Online-Konto brauchst du nicht.", Text, 13));
                var name = new TextBox { Name = "wiz-name", Text = _settings.Name, PlaceholderText = "Wie dürfen wir dich nennen?", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
                var hello = new TextBlock { Name = "wiz-hello", Text = string.IsNullOrWhiteSpace(_settings.Name) ? "Hallo!" : $"Hallo {_settings.Name}!", Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                name.TextChanged += (_, _) => { _settings.Name = name.Text?.Trim() ?? ""; hello.Text = _settings.Name.Length == 0 ? "Hallo!" : $"Hallo {_settings.Name}!"; };
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { AvatarView(84), hello } });
                sp.Children.Add(name);
                sp.Children.Add(P("Dein Profilbild:", Text, 12.5));
                var row = new WrapPanel { Name = "wiz-avatars" };
                for (var i = 0; i < AvatarEmojis.Length; i++)
                {
                    var e = AvatarEmojis[i]; var on = _settings.Avatar == "emoji:" + e;
                    var b = new Button { Name = "wiz-avatar-" + i, Content = e, FontSize = 22, Width = 48, Height = 48, Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(24), Background = on ? Accent : Brush.Parse("#22FFFFFF") };
                    b.Click += (_, _) => { _settings.Avatar = "emoji:" + e; Show("einrichtung"); };
                    row.Children.Add(b);
                }
                sp.Children.Add(row);
                var photo = new Button { Name = "wiz-avatar-photo", Content = "📷 Eigenes Foto wählen …" };
                photo.Click += async (_, _) => await PickAvatarPhoto();
                var cam = new Button { Name = "wiz-avatar-camera", Content = "Mit der Kamera aufnehmen" };
                ToolTip.SetTip(cam, "Öffnet die Windows-Kamera. Danach das Foto mit „Eigenes Foto wählen“ übernehmen (Ordner Bilder › Eigene Aufnahmen).");
                cam.Click += (_, _) => Launch("microsoft.windows.camera:");
                sp.Children.Add(new WrapPanel { Children = { Sp(photo), Sp(cam) } });
                sp.Children.Add(P("Sprache: Deutsch", null, 11));
                break;
            }
            case 1: // B2: Anmeldung
            {
                sp.Children.Add(P("Wie möchtest du Trendo entsperren? Alles bleibt auf diesem PC.", Text, 13));
                foreach (var (kind, id, label, hint) in new[] {
                             (ProfileLock.Kind.None, "none", "Ohne Anmeldung", "Trendo startet sofort – gut, wenn nur du den PC nutzt."),
                             (ProfileLock.Kind.Pin, "pin", "Mit PIN (empfohlen)", "4 bis 12 Ziffern – schnell und sicher."),
                             (ProfileLock.Kind.Password, "password", "Mit Passwort", "Mindestens 8 Zeichen.") })
                {
                    var rb = new RadioButton { Name = "wiz-lock-" + id, GroupName = "wiz-lock", IsChecked = _wizLockKind == kind, Foreground = Text,
                        Content = new StackPanel { Children = { new TextBlock { Text = label, Foreground = Text, FontWeight = FontWeight.SemiBold }, P(hint, null, 11) } } };
                    var k = kind;
                    rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true && _wizLockKind != k) { _wizLockKind = k; _wizRecovery = null; Show("einrichtung"); } };
                    sp.Children.Add(rb);
                }
                if (Lock.Enabled && !_wizChangeLock && _wizRecovery is null)
                {
                    // Schon eingerichtet (z. B. nach einem Neustart mitten in der Einrichtung): nicht noch einmal fragen
                    sp.Children.Add(P($"✓ Deine Anmeldung ist eingerichtet ({(Lock.Current == ProfileLock.Kind.Pin ? "PIN" : "Passwort")}).", Brush.Parse("#4ADE80"), 13));
                    var redo = new Button { Name = "wiz-lock-change", Content = "Anders festlegen" };
                    redo.Click += (_, _) => { _wizChangeLock = true; Show("einrichtung"); };
                    sp.Children.Add(redo);
                    next = Next();
                }
                else if (_wizRecovery is { } code)
                {
                    sp.Children.Add(H("Dein Wiederherstellungs-Code", 15));
                    sp.Children.Add(P("Falls du die PIN einmal vergisst: Mit diesem Code setzt du sie neu. Schreib ihn auf oder speichere ihn – er wird nur jetzt gezeigt. Alternativ geht es auch mit deiner Windows-Anmeldung.", Text, 12.5));
                    sp.Children.Add(new Border { Background = Brush.Parse("#1AD4A84B"), BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10), HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new SelectableTextBlock { Name = "wiz-recovery-code", Text = code, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 24, Foreground = Text } });
                    var copy = new Button { Name = "wiz-recovery-copy", Content = "Kopieren" };
                    copy.Click += async (_, _) => { if (Clipboard is { } cb) await cb.SetTextAsync(code); };
                    var save = new Button { Name = "wiz-recovery-save", Content = "Als Datei in „Dokumente“ speichern" };
                    save.Click += (_, _) =>
                    {
                        var dir = _opt.Isolated ? _core.DataRoot : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        var f = Path.Combine(dir, "Wiederherstellungs-Code.txt");
                        File.WriteAllText(f, $"Wiederherstellungs-Code für deine Anmeldung\r\n\r\n{code}\r\n\r\nGut aufbewahren. Damit setzt du eine vergessene PIN bzw. ein vergessenes Passwort neu.\r\n");
                        _wizMsg = "✓ Gespeichert: " + f; _wizRecoveryNoted = true; Show("einrichtung");
                    };
                    var noted = new CheckBox { Name = "wiz-recovery-noted", Content = "Ich habe den Code aufgeschrieben oder gespeichert", IsChecked = _wizRecoveryNoted, Foreground = Text };
                    noted.IsCheckedChanged += (_, _) => { _wizRecoveryNoted = noted.IsChecked == true; Show("einrichtung"); };
                    sp.Children.Add(new WrapPanel { Children = { Sp(copy), Sp(save) } });
                    sp.Children.Add(noted);
                    next = Next(check: () => _wizRecoveryNoted || (_wizMsg = "Bitte bestätige, dass du den Code aufgeschrieben hast.") == "");
                    next.IsEnabled = _wizRecoveryNoted;
                }
                else if (_wizLockKind != ProfileLock.Kind.None)
                {
                    var pin = _wizLockKind == ProfileLock.Kind.Pin;
                    var s1 = new TextBox { Name = "wiz-secret", PasswordChar = '•', Text = _wizSecret, PlaceholderText = pin ? "PIN (4–12 Ziffern)" : "Passwort (mind. 8 Zeichen)", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
                    var s2 = new TextBox { Name = "wiz-secret2", PasswordChar = '•', Text = _wizSecret2, PlaceholderText = pin ? "PIN wiederholen" : "Passwort wiederholen", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
                    s1.TextChanged += (_, _) => _wizSecret = s1.Text ?? ""; s2.TextChanged += (_, _) => _wizSecret2 = s2.Text ?? "";
                    sp.Children.Add(s1); sp.Children.Add(s2);
                    next = Next("Festlegen", () =>
                    {
                        _wizSecret = s1.Text ?? ""; _wizSecret2 = s2.Text ?? "";
                        if (ProfileLock.Problem(_wizLockKind, _wizSecret) is { } p) { _wizMsg = p; return false; }
                        if (_wizSecret != _wizSecret2) { _wizMsg = "Die beiden Eingaben sind nicht gleich."; return false; }
                        _wizRecovery = Lock.Set(_wizLockKind, _wizSecret); _wizSecret = _wizSecret2 = ""; _wizRecoveryNoted = false;
                        _core.Audit.Append(Principal.LocalOwner.Name, "profile.lock.set", _wizLockKind.ToString());
                        _wizStep--; // gleich hier den Code zeigen
                        return true;
                    });
                }
                else next = Next(check: () => { Lock.Set(ProfileLock.Kind.None); return true; });
                break;
            }
            case 2: // Ordner und B8: Daten aus Windows
            {
                sp.Children.Add(P("Hier speichert Trendo deine Projekte, Briefe und Vorlagen. Deine Daten bleiben auf diesem PC.", Text, 13));
                var folder = new TextBox { Name = "wiz-folder", Text = _settings.ProjectsFolder, PlaceholderText = "Ordner für Projekte", Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
                folder.TextChanged += (_, _) => _settings.ProjectsFolder = folder.Text?.Trim() ?? "";
                sp.Children.Add(folder);
                sp.Children.Add(H("Aus Windows übernehmen", 15));
                var found = _opt.Isolated ? 0 : BrowserImport.BookmarkFiles(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).Count();
                var bm = new CheckBox { Name = "wiz-import-bookmarks", IsChecked = _wizBookmarks && found > 0, IsEnabled = found > 0, Foreground = Text, Content = found > 0 ? "Lesezeichen aus Edge und Chrome übernehmen" : "Lesezeichen übernehmen (kein Edge oder Chrome gefunden)" };
                bm.IsCheckedChanged += (_, _) => _wizBookmarks = bm.IsChecked == true;
                var wf = new CheckBox { Name = "wiz-import-folders", IsChecked = _wizWinFolders, Foreground = Text, Content = "Dokumente und Bilder aus Windows in der Trendo-Suche finden" };
                wf.IsCheckedChanged += (_, _) => _wizWinFolders = wf.IsChecked == true;
                sp.Children.Add(bm); sp.Children.Add(wf);
                sp.Children.Add(P("Es wird nichts kopiert oder verschoben – deine Dateien bleiben, wo sie sind. Musik und Videos aus Windows zeigt Trendo ohnehin.", null, 11));
                sp.Children.Add(P($"Einstellungen und Sicherungen liegen getrennt in {_core.DataRoot} und bleiben auch bei einer Deinstallation erhalten.", null, 11));
                break;
            }
            case 3: // B4: Aussehen mit Live-Vorschau
            {
                sp.Children.Add(P("Wie soll Trendo aussehen? Die Vorschau zeigt es sofort.", Text, 13));
                foreach (var (id, label) in new[] { ("dunkel", "Dunkel Premium (empfohlen)"), ("hell", "Hell"), ("kontrast", "Hoher Kontrast (gut lesbar)") })
                {
                    var rb = new RadioButton { Name = "wiz-theme-" + id, Content = label, GroupName = "theme", IsChecked = _settings.Theme == id, Foreground = Text };
                    rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true && _settings.Theme != id) { _settings.Theme = id; Show("einrichtung"); } };
                    sp.Children.Add(rb);
                }
                var accents = new WrapPanel { Name = "wiz-accents" };
                foreach (var a in new[] { "#2F86F6", "#D4A84B", "#22C55E", "#A855F7", "#EF4444", "#06B6D4" })
                {
                    var on = string.Equals(_settings.Accent, a, StringComparison.OrdinalIgnoreCase);
                    var b = new Button { Name = "wiz-accent-" + a.TrimStart('#'), Width = 34, Height = 34, Margin = new Thickness(0, 0, 8, 6), CornerRadius = new CornerRadius(17), Background = Brush.Parse(a), BorderBrush = Brushes.White, BorderThickness = new Thickness(on ? 3 : 0) };
                    var col = a; b.Click += (_, _) => { _settings.Accent = col; _settings.AccentFromWallpaper = false; Show("einrichtung"); };
                    accents.Children.Add(b);
                }
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "Farbe:", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, accents } });
                var walls = new WrapPanel { Name = "wiz-wallpapers" };
                foreach (var wname in UserSettings.Wallpapers)
                {
                    var on = _settings.Wallpaper == wname;
                    Control thumb = LoadWallpaper(wname) is { } bmp ? new Image { Source = bmp, Stretch = Stretch.UniformToFill } : new Border { Background = Grad("#1A3A6B", "#0A1A36") };
                    var b = new Button { Name = "wiz-wall-" + wname, Width = 96, Height = 56, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 8), ClipToBounds = true, CornerRadius = new CornerRadius(8), BorderBrush = on ? Brush.Parse("#D4A84B") : Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(on ? 3 : 1), Content = thumb };
                    var w = wname; b.Click += (_, _) => { _settings.Wallpaper = w; _settings.WallpaperByTime = false; Show("einrichtung"); };
                    walls.Children.Add(b);
                }
                sp.Children.Add(P("Hintergrund:", Text, 12)); sp.Children.Add(walls);
                var sizes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var (v, l) in new[] { (1.0, "Normal"), (1.15, "Größer"), (1.3, "Sehr groß") })
                {
                    var b = new Button { Name = "wiz-size-" + l.Replace(' ', '-').ToLowerInvariant(), Content = l, Background = Math.Abs(_settings.UiScale - v) < 0.01 ? Accent : Brushes.Transparent };
                    var val = v; b.Click += (_, _) => { _settings.UiScale = val; Show("einrichtung"); };
                    sizes.Children.Add(b);
                }
                var simple = new CheckBox { Name = "wiz-simple", Content = "Einfacher Modus (größere Knöpfe, weniger auf einmal)", IsChecked = _settings.SimpleMode, Foreground = Text };
                simple.IsCheckedChanged += (_, _) => { _settings.SimpleMode = simple.IsChecked == true; Show("einrichtung"); };
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "Schriftgröße:", Foreground = Text, VerticalAlignment = VerticalAlignment.Center }, sizes } });
                sp.Children.Add(simple);
                // Live-Vorschau: kleiner Schreibtisch mit Hintergrund, Fenster in der gewählten Farbe und Schriftgröße
                var (bg, fg, edge) = _settings.Theme switch { "hell" => ("#F4F7FC", "#0F1B2E", "#335C99"), "kontrast" => ("#000000", "#FFFFFF", "#FFFF00"), _ => ("#0B1730", "#E6EDF7", "#2F86F6") };
                var themeLabel = _settings.Theme switch { "hell" => "Hell", "kontrast" => "Hoher Kontrast", _ => "Dunkel Premium" };
                var k = _settings.UiScale * (_settings.SimpleMode ? 1.15 : 1);
                var desk = new Grid { Height = 170, Width = 320, ClipToBounds = true };
                if (LoadWallpaper(_settings.Wallpaper) is { } wp) desk.Children.Add(new Image { Source = wp, Stretch = Stretch.UniformToFill });
                desk.Children.Add(new Border { Name = "wiz-theme-preview", Margin = new Thickness(28, 22), Background = Brush.Parse(bg), BorderBrush = Brush.Parse(_settings.Theme == "kontrast" ? edge : _settings.Accent), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
                    Child = new StackPanel { Spacing = 5, Children = {
                        new TextBlock { Text = "Vorschau: " + themeLabel, Foreground = Brush.Parse(fg), FontWeight = FontWeight.SemiBold, FontSize = 13 * k },
                        new TextBlock { Text = "So sehen Fenster und Texte aus.", Foreground = Brush.Parse(fg), FontSize = 11 * k },
                        new Border { Background = Brush.Parse(_settings.Accent), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "Knopf", Foreground = Brushes.White, FontSize = 11 * k } } } } });
                sp.Children.Add(new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = desk });
                sp.Children.Add(new TextBlock { Name = "wiz-theme-choice", Text = $"Gewählt: {themeLabel} – gilt nach „Weiter“ für ganz Trendo.", Foreground = Text, FontSize = 11 });
                break;
            }
            case 4: // B3: Interessen + Pakete
            {
                sp.Children.Add(P("Tipp an, wofür du Trendo nutzen möchtest. Trendo legt passende Studios auf deinen Desktop und aufs Handy und richtet die nötigen Programme ein.", Text, 13));
                var chips = new WrapPanel { Name = "wiz-interests" };
                foreach (var it in Interests.All)
                {
                    var on = _settings.Interests.Contains(it.Id);
                    var b = new Button { Name = "wiz-interest-" + it.Id, Content = $"{it.Emoji}  {it.Label}", Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(18), Background = on ? Accent : Brush.Parse("#22FFFFFF") };
                    var iid = it.Id;
                    b.Click += (_, _) =>
                    {
                        if (!_settings.Interests.Remove(iid)) _settings.Interests.Add(iid);
                        _wizPacks.Clear(); foreach (var p in SetupPacks.Studios.Where(p => p.Default)) _wizPacks.Add(p.Id);
                        foreach (var p in Interests.PacksFor(_settings.Interests)) if (SetupPacks.Studios.Any(x => x.Id == p)) _wizPacks.Add(p);
                        Show("einrichtung");
                    };
                    chips.Children.Add(b);
                }
                sp.Children.Add(chips);
                var hw = HardwareProbe.Read(); var (model, why) = KiAdvisor.Choose(hw);
                sp.Children.Add(P("Das Basispaket ist immer dabei: " + SetupPacks.Base.Description + ". Zusätzlich:", Text, 12.5));
                foreach (var p in SetupPacks.Studios)
                {
                    var cb = new CheckBox { Name = "wiz-pack-" + p.Id, IsChecked = _wizPacks.Contains(p.Id), Foreground = Text,
                        Content = new StackPanel { Children = { new TextBlock { Text = p.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, P(p.Description + (p.Id == "ki" ? $" · Modell: {model}" : ""), null, 11) } } };
                    cb.IsCheckedChanged += (_, _) => { var changed = cb.IsChecked == true ? _wizPacks.Add(p.Id) : _wizPacks.Remove(p.Id); if (changed) Show("einrichtung"); };
                    sp.Children.Add(cb);
                }
                sp.Children.Add(new TextBlock { Name = "wiz-pack-summary", Text = "Gewählt: " + string.Join(", ", new[] { SetupPacks.Base.Name }.Concat(SetupPacks.Studios.Where(x => _wizPacks.Contains(x.Id)).Select(x => x.Name))), Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(P("KI-Empfehlung für diesen PC: " + why, null, 11));
                var comps = SetupPacks.ComponentsFor(_wizPacks).Where(c => !_deps.IsInstalled(c)).ToList();
                sp.Children.Add(P(comps.Count == 0 ? "Alles Ausgewählte ist schon vorhanden." :
                    $"Wird installiert: {string.Join(", ", comps.Select(c => c.Name))} (ca. {comps.Sum(c => c.ApproxMb)} MB). Mit „Zustimmen & installieren“ akzeptierst du die jeweiligen Lizenzen (siehe THIRD_PARTY.md). Programme, die nur systemweit installierbar sind, fragen einmal nach Administratorrechten.", null, 11));
                var go = new Button { Name = "wiz-install", Content = comps.Count == 0 ? "Weiter" : "Zustimmen & installieren", Background = Accent, Padding = new Thickness(16, 6), IsEnabled = !_depBusy };
                go.Click += async (_, _) =>
                {
                    go.IsEnabled = false; back.IsEnabled = false; go.Content = "Wird eingerichtet …"; // ausgegraut, kein zweiter Klick
                    _settings.StudioPacks = _wizPacks.ToList();
                    if (comps.Count > 0) BeginSetupOverlay("Trendo OS wird eingerichtet");
                    try { await RunSetupInstall(model); } finally { EndSetupOverlay(); }
                    _wizStep = 5; Show("einrichtung");
                };
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { back, go } });
                sp.Children.Add(_depProgress.Parent is null ? _depProgress : new Border()); sp.Children.Add(Detach(_depLog));
                return Page("Ersteinrichtung", sp);
            }
            case 5: // B5: Datenschutz in einfachen Worten
            {
                sp.Children.Add(P("Trendo arbeitet auf deinem PC. Es gibt keine Werbung und keine Nutzungsdaten, die heimlich verschickt werden. Nur diese Dinge können ins Internet gehen – du entscheidest:", Text, 13));
                var set = KiSourceSettings.Load(_core.DataRoot);
                CheckBox Row(string name, string label, string hint, bool on, Action<bool> apply)
                {
                    var cb = new CheckBox { Name = name, IsChecked = on, Foreground = Text, Content = new StackPanel { Children = { new TextBlock { Text = label, Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, P(hint, null, 11) } } };
                    cb.IsCheckedChanged += (_, _) => apply(cb.IsChecked == true);
                    sp.Children.Add(cb); return cb;
                }
                Row("wiz-priv-updates", "Updates von Trendo automatisch laden", "Fragt GitHub nach einer neuen Version – ohne persönliche Daten.", _settings.AutoUpdate, v => _settings.AutoUpdate = v);
                Row("wiz-priv-ki", "Internet-KI darf aushelfen", "Nur nach deiner Einrichtung (z. B. kostenlose KIs von OpenRouter). Aus = alles bleibt auf dem PC.", set.RemoteMode != "chat" && set.Cloud.Count > 0,
                    v => KiSourceSettings.Update(_core.DataRoot, s => s.RemoteMode = v ? "ersatz" : "chat"));
                Row("wiz-priv-phone", "Handy-Zugang beim Start einschalten", "Nur für Handys, die du selbst verbunden hast – nichts ist öffentlich.", _settings.PhoneAutoStart, v => _settings.PhoneAutoStart = v);
                sp.Children.Add(P("Ein Klick auf „Weiter“ übernimmt diese Wahl. Alles lässt sich unter „Einstellungen“ ändern.", null, 11));
                break;
            }
            case 6: // B6: Handy gleich verbinden
            {
                sp.Children.Add(P("Verbinde dein Handy gleich mit – dann schickst du Fotos hin und her und nutzt Trendo auch unterwegs als App. Das geht auch später unter „Handy“.", Text, 13));
                sp.Children.Add(PhoneEasyCard());
                next = Next("Weiter");
                next.Content = PhoneAccess.Devices().Count > 0 ? "Weiter" : "Überspringen";
                break;
            }
            case 7: // B7: Weitere Personen
            {
                sp.Children.Add(P("Nutzen noch andere diesen PC – Partner, Familie, Kinder? Jede Person bekommt ihr eigenes Trendo mit eigenen Projekten und eigener Anmeldung.", Text, 13));
                var persons = new PersonStore(PersonStore.BaseOf(_core.DataRoot));
                foreach (var p in persons.All())
                    sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { AvatarView(36, p.Avatar, persons.RootOf(p.Id)), new TextBlock { Text = p.Name + (p.Child ? " (Kind)" : ""), Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } });
                var pname = new TextBox { Name = "wiz-person-name", Text = _wizPersonName, PlaceholderText = "Name der Person", Width = 220 };
                pname.TextChanged += (_, _) => _wizPersonName = pname.Text ?? "";
                var child = new CheckBox { Name = "wiz-person-child", Content = "Kind (einfache Bedienung, Installieren nur durch Erwachsene)", IsChecked = _wizPersonChild, Foreground = Text };
                child.IsCheckedChanged += (_, _) => _wizPersonChild = child.IsChecked == true;
                var add = new Button { Name = "wiz-person-add", Content = "＋ Person hinzufügen" };
                add.Click += (_, _) =>
                {
                    _wizPersonName = pname.Text ?? ""; _wizPersonChild = child.IsChecked == true;
                    try { var p = persons.Add(_wizPersonName, "emoji:" + AvatarEmojis[persons.All().Count % AvatarEmojis.Length], _wizPersonChild, _core.Audit); _wizMsg = $"✓ {p.Name} ist angelegt. Beim Start von Trendo wählt jede Person sich selbst."; _wizPersonName = ""; _wizPersonChild = false; }
                    catch (ArgumentException e) { _wizMsg = e.Message; }
                    Show("einrichtung");
                };
                sp.Children.Add(new WrapPanel { Children = { Sp(pname), Sp(add) } });
                sp.Children.Add(child);
                next = Next(persons.All().Count > 0 ? "Weiter" : "Überspringen");
                break;
            }
            case 8:
                FinishSetup();
                sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { AvatarView(64), P($"Alles bereit{(string.IsNullOrEmpty(_settings.Name) ? "" : ", " + _settings.Name)}! Hier ist dein Systemcheck:", Text, 14) } });
                sp.Children.Add(SystemCheckPanel());
                var done = new Button { Name = "wiz-done", Content = "Los geht's", Background = Accent, Padding = new Thickness(16, 6) }; done.Click += (_, _) => Show("start");
                sp.Children.Add(done);
                return Page("Ersteinrichtung", sp);
        }
        if (_wizMsg.Length > 0) sp.Children.Add(P(_wizMsg, _wizMsg.StartsWith('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), 12.5));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { back, next ?? Next() } });
        return Page("Ersteinrichtung", sp);
    }

    private static Control Detach(Control c)
    {
        switch (c.Parent)
        {
            case Panel p: p.Children.Remove(c); break;
            case ContentControl cc when ReferenceEquals(cc.Content, c): cc.Content = null; break;
            case Decorator d when ReferenceEquals(d.Child, c): d.Child = null; break;
        }
        return c;
    }

    private async Task RunSetupInstall(string model)
    {
        if (!_deps.HasConsent) _deps.GiveConsent(Principal.LocalOwner);
        var list = SetupPacks.ComponentsFor(_settings.StudioPacks).ToList();
        if (_settings.StudioPacks.Contains("ki") && model == DependencyCatalog.LargeModel) list.Add(DependencyCatalog.Get("model-3b"));
        var todo = list.Where(c => !_deps.IsInstalled(c) && c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Included).ToList();
        for (var i = 0; i < todo.Count; i++)
        {
            if (SetupCancelRequested) { _depLog.Text = "Einrichtung abgebrochen."; break; }
            _depLog.Text = $"{i + 1}/{todo.Count}: {todo[i].Name} …";
            SetupOverlayProgress(i, todo.Count, $"{todo[i].Name} wird eingerichtet …");
            await InstallOne(todo[i], refresh: false);
        }
        SetupOverlayProgress(todo.Count, todo.Count, "Fertig ✓");
        if (_deps.HasModel(model)) KiModel.Select(_core.DataRoot, model, _core.Audit);
        ReloadStudios();
    }

    private void FinishSetup()
    {
        if (_settings.SetupDone) return;
        Directory.CreateDirectory(_settings.ProjectsFolder);
        var tpl = Path.Combine(_settings.ProjectsFolder, "Vorlagen");
        Directory.CreateDirectory(tpl);
        foreach (var n in new[] { "DIN5008-Geschaeftsbrief-FormB.docx", "DIN5008-Privatbrief-FormB.docx" })
        {
            var dest = Path.Combine(tpl, n);
            if (File.Exists(dest)) continue;
            using var s = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/templates/{n}"));
            using var f = File.Create(dest); s.CopyTo(f);
        }
        // B3: passende Studios auf Desktop (und als Vorschlag aufs Handy)
        foreach (var id in Interests.StudiosFor(_settings.Interests)) if (!_settings.DesktopIcons.Contains(id)) _settings.DesktopIcons.Add(id);
        // B8: Lesezeichen aus Edge/Chrome, Windows-Ordner in der Suche
        if (_wizBookmarks && !_opt.Isolated)
            foreach (var f in BrowserImport.BookmarkFiles(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)))
                try { var n = BrowserImport.ImportInto(_core.DataRoot, BrowserImport.Parse(File.ReadAllText(f), f.Contains("Edge") ? "Edge" : "Chrome")); _core.Audit.Append(Principal.LocalOwner.Name, "import.bookmarks", n.ToString()); }
                catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { _core.Audit.Append("app", "import.bookmarks.failed", e.GetType().Name); }
        _settings.SearchWindowsFolders = _wizWinFolders;
        _settings.SetupDone = true; _settings.Save(_core.DataRoot);
        _core.Audit.Append(Principal.LocalOwner.Name, "setup.done", $"Pakete: {string.Join(",", _settings.StudioPacks)}");
        try { Backups.Create(DateTime.Now); } catch { /* Sicherung später erneut */ }
    }

    // ---------- Systemcheck: bereit / fehlt / Ein-Klick-Reparatur ----------
    private Control SystemCheckView() => Page("Systemcheck", new StackPanel { Spacing = 8, Children = {
        P("Was Trendo auf diesem PC braucht – und ob es funktioniert. Fehlt etwas, reicht ein Klick auf „Reparieren“.", Text, 13), SystemCheckPanel() } });

    private Control SystemCheckPanel()
    {
        var host = new StackPanel { Spacing = 6, Name = "syscheck-host", Children = { P("Prüfe …", Text) } };
        var comps = SetupPacks.ComponentsFor(_settings.StudioPacks.Count > 0 ? _settings.StudioPacks : _wizPacks).ToList();
        // Prüfen im Hintergrund (startet Programme mit --version) – nie auf dem UI-Thread blockieren
        _ = Task.Run(async () =>
        {
            var checks = (await Task.WhenAll(comps.Select(async c => (C: c, R: await _deps.CheckAsync(c))))).ToList();
            await Dispatcher.UIThread.InvokeAsync(() => { host.Children.Clear(); host.Children.Add(BuildCheckGrid(checks)); });
        });
        return host;
    }

    private string _repairMsg = "";
    private Control BuildCheckGrid(List<(Component C, ToolCheck R)> checks)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*,130"), Name = "syscheck" };
        var rows = new List<(string Name, string State, string Detail, Component? Fix)>();
        foreach (var (c, ch) in checks)
            rows.Add((c.Name, ch.State switch { ToolState.Working => "Bereit", ToolState.Broken => "Defekt", _ => "Fehlt" }, ch.Version ?? ch.Detail ?? "",
                ch.State == ToolState.Working || c.Mode is not (DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Included) ? null : c));
        rows.Add(("Benutzerdaten", Directory.Exists(_core.DataRoot) ? "Bereit" : "Fehlt", _core.DataRoot, null));
        rows.Add(("Projektordner", Directory.Exists(_settings.ProjectsFolder) ? "Bereit" : "Fehlt", _settings.ProjectsFolder, null));
        var last = Backups.List().FirstOrDefault();
        rows.Add(("Sicherung", last is null ? "Fehlt" : "Bereit", last is null ? "noch keine" : $"zuletzt {last.LastWriteTime:dd.MM.yyyy HH:mm}", null));
        rows.Add(("Updates", !_settings.AutoUpdate ? "Aus" : _updateInfo.StartsWith("Aktuell") || _updateInfo.StartsWith("Update") ? "Bereit" : "Hinweis", _updateInfo, null));
        rows.Add(("Protokoll", _core.Audit.Verify() ? "Bereit" : "Defekt", "unverändert und vollständig", null));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var color = r.State switch { "Bereit" => "#4ADE80", "Aus" => "#93A4C3", "Hinweis" => "#FACC15", _ => "#F87171" };
            Control fix = r.Fix is { } c ? MakeFix(c) : new TextBlock();
            var cells = new Control[] { P(r.Name, Text, 12), new TextBlock { Text = $"{Dot(r.State switch { "Bereit" or "Aus" => Trendo.Core.Sys.A11y.Level.Gut, "Hinweis" => Trendo.Core.Sys.A11y.Level.Hinweis, _ => Trendo.Core.Sys.A11y.Level.Problem })} {r.State}  {r.Detail}", Foreground = Brush.Parse(color), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, fix };
            for (var k = 0; k < 3; k++) { Grid.SetRow(cells[k], i); Grid.SetColumn(cells[k], k); cells[k].Margin = new Thickness(0, 2); cells[k].VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(cells[k]); }
        }
        var sum = rows.Count(r => r.State == "Bereit");
        var panel = new StackPanel { Spacing = 6, Children = { P($"{sum} von {rows.Count} Punkten bereit.", Text), grid } };
        if (_repairMsg.Length > 0) panel.Children.Insert(0, new TextBlock { Name = "repair-msg", Text = _repairMsg, Foreground = _repairMsg.Contains('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap });
        return panel;
        Control MakeFix(Component c)
        {
            var b = new Button { Name = "fix-" + c.Id, Content = "Reparieren", FontSize = 11, IsEnabled = !_depBusy };
            b.Click += async (_, _) =>
            {
                _repairMsg = $"{c.Name}: wird repariert …"; Show(CurrentView);
                bool ok; try { ok = InstallOverride is { } fake ? await fake(c) : await InstallOne(c, refresh: false); } catch (Exception ex) { ok = false; _depLog.Text = ex.Message; }
                _repairMsg = ok ? $"{c.Name}: repariert ✓" : $"{c.Name}: hat nicht geklappt{(_core.Offline ? " – keine Internetverbindung" : "")}. {_depLog.Text}".Trim();
                ReloadStudios(); Show(CurrentView);
            };
            return b;
        }
    }

    // ---------- Sicherung & Wiederherstellung ----------
    private string _backupMsg = "";
    private string? _backupConfirm;
    private Control BackupView()
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(P($"Trendo sichert deine Projekte und Einstellungen automatisch alle {_settings.BackupEveryHours} Stunden (die letzten 10 bleiben). Ablage: {Backups.Folder}", Text));
        var now = new Button { Name = "btn-backup-now", Content = "Jetzt sichern", Background = Accent };
        now.Click += (_, _) => { try { _backupMsg = "Gesichert: " + Path.GetFileName(Backups.Create(DateTime.Now)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _backupMsg = "Sichern hat nicht geklappt: " + ex.Message; } Show("sicherung"); };
        sp.Children.Add(now);
        if (_backupMsg.Length > 0) sp.Children.Add(P(_backupMsg, Brush.Parse("#4ADE80")));
        var bi = 0;
        foreach (var f in Backups.List())
        {
            var file = f.FullName; var idx = bi++;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { P($"{f.LastWriteTime:dd.MM.yyyy HH:mm} · {f.Length / 1024} KB", Text) } };
            if (_backupConfirm == file)
            {
                var yes = new Button { Name = $"backup-restore-yes-{idx}", Content = "Ja, wiederherstellen", FontSize = 11, Background = Accent };
                yes.Click += (_, _) =>
                {
                    _backupConfirm = null;
                    try { var n = Backups.Restore(file, DateTime.Now); _settings = UserSettings.Load(_core.DataRoot); _backupMsg = $"Wiederhergestellt: {n} Dateien. Der vorherige Stand wurde vorher gesichert."; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { _backupMsg = "Wiederherstellen hat nicht geklappt: " + ex.Message; }
                    Show("sicherung");
                };
                var no = new Button { Name = $"backup-restore-no-{idx}", Content = "Abbrechen", FontSize = 11 };
                no.Click += (_, _) => { _backupConfirm = null; _backupMsg = "Abgebrochen – nichts geändert."; Show("sicherung"); };
                string what;
                try
                {
                    var plan = Backups.Preview(file);
                    what = $"Ersetzt {plan.Replaced} Dateien, ergänzt {plan.Added}" + (plan.Examples.Count > 0 ? $" (z. B. {string.Join(", ", plan.Examples.Take(3))})" : "") + ". Der aktuelle Stand wird vorher gesichert. Sicher?";
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException) { what = "Sicherung nicht lesbar: " + ex.Message; }
                var info = P(what, Brush.Parse("#FACC15"), 11); info.Name = $"backup-restore-plan-{idx}";
                row.Children.Add(info); row.Children.Add(yes); row.Children.Add(no);
            }
            else
            {
                var rb = new Button { Name = $"backup-restore-{idx}", Content = "Wiederherstellen", FontSize = 11 };
                rb.Click += (_, _) => { _backupConfirm = file; _backupMsg = ""; Show("sicherung"); };
                row.Children.Add(rb);
            }
            sp.Children.Add(row);
        }
        sp.Children.Add(P("Beim Wiederherstellen wird der aktuelle Stand vorher gesichert. Das Protokoll, was Trendo getan hat, bleibt immer erhalten.", null, 11));
        return Page("Sicherung", sp);
    }

    // Auto-Updates über GitHub Releases (Velopack): siehe MainWindow.Updates.cs (U1–U6)
}

/// <summary>Liest RAM und Grafikkarte (nur lesend; Windows-Registry bzw. /proc).</summary>
internal static class HardwareProbe
{
    public static HardwareInfo Read()
    {
        var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; string? gpu = null; long vram = 0;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                foreach (var n in cls?.GetSubKeyNames().Where(n => n.All(char.IsDigit)) ?? [])
                {
                    using var k = cls!.OpenSubKey(n);
                    var mem = k?.GetValue("HardwareInformation.qwMemorySize") is long l ? l : k?.GetValue("HardwareInformation.MemorySize") is int i ? (uint)i : 0;
                    if (mem > vram) { vram = mem; gpu = k?.GetValue("DriverDesc") as string; }
                }
            }
            catch { /* ohne GPU-Infos weiter */ }
        }
        return new HardwareInfo(ram, gpu, vram);
    }
}
