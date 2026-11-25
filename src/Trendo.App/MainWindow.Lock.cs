using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 27.10.2026 (B2, B7): Sperrbildschirm wie bei Windows – Profilbild, Name, Uhr, PIN oder Passwort. „PIN vergessen?“ hilft
/// mit Wiederherstellungs-Code oder der Windows-Anmeldung. Mehrere Personen wählen sich hier selbst aus.</summary>
public partial class MainWindow
{
    Grid? _lockLayer;
    string _lockMode = "pin", _lockMsg = "", _lockNew1 = "", _lockNew2 = "";
    string? _lockNewCode;
    /// <summary>Für Tests: Windows-Bestätigung ohne Dialog.</summary>
    public Func<bool>? WindowsConfirmOverride { get; set; }
    /// <summary>Für Tests: Personenwechsel ohne Neustart (Datenordner der Person).</summary>
    public Action<string>? SwitchPersonOverride { get; set; }
    public bool IsLocked => _lockLayer is { IsVisible: true };
    PersonStore Persons => new(PersonStore.BaseOf(_core.DataRoot));

    /// <summary>Beim Start: sperren, wenn eine Anmeldung eingerichtet ist oder mehrere Personen den PC nutzen.</summary>
    public void LockIfNeeded() { if ((Lock.Enabled && _settings.LockOnStart) || Persons.All().Count > 0) LockNow(); }

    public void LockNow()
    {
        if (Content is not LayoutTransformControl { Child: Grid root }) return;
        if (_lockLayer is null)
        {
            _lockLayer = new Grid { Name = "lock-screen", ZIndex = 10_000, Background = Brush.Parse("#F2050B18") };
            Grid.SetRowSpan(_lockLayer, Math.Max(1, root.RowDefinitions.Count)); Grid.SetColumnSpan(_lockLayer, Math.Max(1, root.ColumnDefinitions.Count));
            root.Children.Add(_lockLayer);
        }
        _lockMode = "pin"; _lockMsg = ""; _lockNewCode = null;
        _lockLayer.IsVisible = true;
        RenderLock();
        _core.Audit.Append("app", "profile.locked", "");
    }

    void Unlock()
    {
        if (_lockLayer is null) return;
        _lockLayer.IsVisible = false; _lockLayer.Children.Clear();
        _core.Audit.Append(Principal.LocalOwner.Name, "profile.unlocked", "");
    }

    /// <summary>Entsperren mit PIN/Passwort (öffentlich für Tests und die Bedienhilfe).</summary>
    public bool TryUnlock(string secret)
    {
        if (!Lock.Enabled || Lock.Verify(secret)) { Unlock(); return true; }
        var wait = Lock.WaitLeft();
        _lockMsg = wait > TimeSpan.Zero ? $"Zu viele Versuche – bitte {Math.Ceiling(wait.TotalSeconds)} Sekunden warten." : (Lock.Current == ProfileLock.Kind.Pin ? "Die PIN stimmt nicht." : "Das Passwort stimmt nicht.");
        _core.Audit.Append("app", "profile.unlock.failed", "");
        RenderLock();
        return false;
    }

    /// <summary>PIN vergessen: mit Wiederherstellungs-Code oder Windows-Anmeldung freischalten, danach neue PIN wählen.</summary>
    public bool RecoverWithCode(string code)
    {
        if (!Lock.VerifyRecovery(code)) { _lockMsg = "Der Code stimmt nicht. Groß-/Kleinschreibung und Striche sind egal."; RenderLock(); return false; }
        _core.Audit.Append(Principal.LocalOwner.Name, "profile.recovery.code", "");
        _lockMode = "neu"; _lockMsg = ""; RenderLock(); return true;
    }

    public bool RecoverWithWindows()
    {
        var ok = WindowsConfirmOverride?.Invoke() ?? WindowsLogon.Confirm(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, "Bestätige mit deiner Windows-Anmeldung, dann kannst du eine neue PIN für Trendo wählen.");
        _core.Audit.Append(Principal.LocalOwner.Name, ok ? "profile.recovery.windows" : "profile.recovery.windows.failed", "");
        if (!ok) { _lockMsg = "Windows hat die Anmeldung nicht bestätigt."; RenderLock(); return false; }
        _lockMode = "neu"; _lockMsg = ""; RenderLock(); return true;
    }

    /// <summary>Neue PIN nach dem Freischalten. Liefert den neuen Wiederherstellungs-Code.</summary>
    public string? SetNewSecret(string secret, string repeat)
    {
        var kind = Lock.Current == ProfileLock.Kind.None ? ProfileLock.Kind.Pin : Lock.Current;
        if (ProfileLock.Problem(kind, secret) is { } p) { _lockMsg = p; RenderLock(); return null; }
        if (secret != repeat) { _lockMsg = "Die beiden Eingaben sind nicht gleich."; RenderLock(); return null; }
        _lockNewCode = Lock.Set(kind, secret);
        _core.Audit.Append(Principal.LocalOwner.Name, "profile.lock.reset", kind.ToString());
        _lockMode = "code"; _lockMsg = ""; RenderLock();
        return _lockNewCode;
    }

    /// <summary>Andere Person: Trendo startet mit deren Datenordner neu (eigene Einstellungen, Projekte, Anmeldung).</summary>
    public void SwitchPerson(string id)
    {
        var root = Persons.RootOf(id);
        _core.Audit.Append("app", "person.switch", id);
        if (SwitchPersonOverride is { } o) { o(root); return; }
        if (Environment.ProcessPath is { } exe)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
            psi.ArgumentList.Add("--data-root"); psi.ArgumentList.Add(root);
            System.Diagnostics.Process.Start(psi);
            Close();
        }
    }

    void RenderLock()
    {
        if (_lockLayer is null) return;
        _lockLayer.Children.Clear();
        if (LoadWallpaper(_settings.CurrentWallpaper(DateTime.Now)) is { } wp) _lockLayer.Children.Add(new Image { Source = wp, Stretch = Stretch.UniformToFill, Opacity = 0.35 });
        var col = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420 };
        col.Children.Add(new TextBlock { Name = "lock-clock", Text = DateTime.Now.ToString("HH:mm"), FontSize = 64, FontWeight = FontWeight.Light, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center });
        col.Children.Add(new TextBlock { Text = DateTime.Now.ToString("dddd, d. MMMM", System.Globalization.CultureInfo.GetCultureInfo("de-DE")), Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -10, 0, 18) });
        var av = AvatarView(110); av.HorizontalAlignment = HorizontalAlignment.Center; col.Children.Add(av);
        col.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(_settings.Name) ? "Willkommen" : _settings.Name, Foreground = Text, FontSize = 24, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        var pin = Lock.Current == ProfileLock.Kind.Pin;
        switch (_lockMode)
        {
            case "pin" when !Lock.Enabled:
            {
                var go = new Button { Name = "lock-enter", Content = "Anmelden", Background = Accent, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(28, 8) };
                go.Click += (_, _) => Unlock();
                col.Children.Add(go);
                break;
            }
            case "pin":
            {
                var box = new TextBox { Name = "lock-secret", PasswordChar = '•', PlaceholderText = pin ? "PIN" : "Passwort", Width = 240, HorizontalAlignment = HorizontalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center, FontSize = 18 };
                box.KeyDown += (_, e) => { if (e.Key == Key.Enter) TryUnlock(box.Text ?? ""); };
                var go = new Button { Name = "lock-unlock", Content = "Entsperren", Background = Accent, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(24, 8) };
                go.Click += (_, _) => TryUnlock(box.Text ?? "");
                var forgot = new Button { Name = "lock-forgot", Content = pin ? "PIN vergessen?" : "Passwort vergessen?", Background = Brushes.Transparent, Foreground = Accent, HorizontalAlignment = HorizontalAlignment.Center };
                forgot.Click += (_, _) => { _lockMode = "vergessen"; _lockMsg = ""; RenderLock(); };
                col.Children.Add(box); col.Children.Add(go); col.Children.Add(forgot);
                _ui.Post(() => box.Focus(), DispatcherPriority.Background);
                break;
            }
            case "vergessen":
            {
                col.Children.Add(P("Kein Problem – deine Daten bleiben erhalten. So setzt du die " + (pin ? "PIN" : "das Passwort") + " neu:", Text, 13));
                var code = new TextBox { Name = "lock-recovery", PlaceholderText = "Wiederherstellungs-Code (z. B. ABCD-EFGH-…)", Width = 300, HorizontalAlignment = HorizontalAlignment.Center };
                var useCode = new Button { Name = "lock-recovery-go", Content = "Mit Code freischalten", Background = Accent, HorizontalAlignment = HorizontalAlignment.Center };
                useCode.Click += (_, _) => RecoverWithCode(code.Text ?? "");
                col.Children.Add(code); col.Children.Add(useCode);
                if (OperatingSystem.IsWindows() || WindowsConfirmOverride is not null)
                {
                    var win = new Button { Name = "lock-windows", Content = "Mit meiner Windows-Anmeldung bestätigen", HorizontalAlignment = HorizontalAlignment.Center };
                    win.Click += (_, _) => RecoverWithWindows();
                    col.Children.Add(win);
                }
                if (Persons.All().Any(p => !p.Child)) col.Children.Add(P("Oder: Eine erwachsene Person meldet sich an und setzt die Anmeldung unter „Personen“ zurück.", null, 11.5));
                var back = new Button { Name = "lock-back", Content = "Zurück", Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center };
                back.Click += (_, _) => { _lockMode = "pin"; _lockMsg = ""; RenderLock(); };
                col.Children.Add(back);
                break;
            }
            case "neu":
            {
                col.Children.Add(P("✓ Bestätigt. Wähle jetzt " + (pin ? "eine neue PIN." : "ein neues Passwort."), Brush.Parse("#4ADE80"), 13));
                var a = new TextBox { Name = "lock-new1", PasswordChar = '•', PlaceholderText = pin ? "Neue PIN" : "Neues Passwort", Width = 240, HorizontalAlignment = HorizontalAlignment.Center, Text = _lockNew1 };
                var b = new TextBox { Name = "lock-new2", PasswordChar = '•', PlaceholderText = "Wiederholen", Width = 240, HorizontalAlignment = HorizontalAlignment.Center, Text = _lockNew2 };
                a.TextChanged += (_, _) => _lockNew1 = a.Text ?? ""; b.TextChanged += (_, _) => _lockNew2 = b.Text ?? "";
                var save = new Button { Name = "lock-new-save", Content = "Speichern", Background = Accent, HorizontalAlignment = HorizontalAlignment.Center };
                save.Click += (_, _) => { if (SetNewSecret(a.Text ?? "", b.Text ?? "") is not null) { _lockNew1 = _lockNew2 = ""; } };
                col.Children.Add(a); col.Children.Add(b); col.Children.Add(save);
                break;
            }
            case "code":
            {
                col.Children.Add(P("Fertig! Dein neuer Wiederherstellungs-Code – bitte aufschreiben:", Text, 13));
                col.Children.Add(new SelectableTextBlock { Name = "lock-new-code", Text = _lockNewCode, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 22, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center });
                var go = new Button { Name = "lock-done", Content = "Weiter zu Trendo", Background = Accent, HorizontalAlignment = HorizontalAlignment.Center };
                go.Click += (_, _) => Unlock();
                col.Children.Add(go);
                break;
            }
        }
        if (_lockMsg.Length > 0) col.Children.Add(new TextBlock { Name = "lock-msg", Text = _lockMsg, Foreground = Brush.Parse("#FACC15"), FontSize = 13, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        _lockLayer.Children.Add(col);
        // B7: andere Personen unten links wie bei Windows
        var persons = Persons.All();
        if (persons.Count > 0)
        {
            var list = new StackPanel { Name = "lock-persons", Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24) };
            var store = Persons;
            var me = PersonStore.BaseOf(_core.DataRoot) == _core.DataRoot ? "haupt" : Path.GetFileName(_core.DataRoot);
            var main = UserSettings.Load(store.RootOf("haupt"));
            foreach (var (id, name, avatar) in new[] { ("haupt", main.Name.Length > 0 ? main.Name : "Hauptkonto", main.Avatar) }.Concat(persons.Select(p => (p.Id, p.Name, p.Avatar))))
            {
                if (id == me) continue;
                var b = new Button { Name = "lock-person-" + id, Background = Brush.Parse("#33000000"), Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(10),
                    Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { AvatarView(30, avatar, store.RootOf(id)), new TextBlock { Text = name, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } };
                var pid = id; b.Click += (_, _) => SwitchPerson(pid);
                list.Children.Add(b);
            }
            _lockLayer.Children.Add(list);
        }
    }

    // ---------- Personen verwalten (B7) ----------
    void InitPersons()
    {
        _showMap["personen"] = PersonsView;
        if (!ExtraPages.Any(p => p.Key == "personen")) ExtraPages.Add(("Personen", "personen", ["person", "familie", "kind", "konto", "benutzer", "pin", "anmeldung", "sperren"]));
    }

    string _personMsg = "", _personName = "";
    bool _personChild;

    /// <summary>T-0764 (4. Prüfrunde M1): ehrlicher Hinweis in Alltagssprache, sobald es ein Kinderprofil gibt (oder man selbst eines ist):
    /// Trendos Kinderschutz ersetzt kein eigenes Windows-Konto.</summary>
    internal Control? KinderschutzHinweis()
    {
        var hasChild = _roleChild || Persons.All().Any(p => p.Child) || ProfileRole.IsChild(PersonStore.BaseOf(_core.DataRoot)); // T-0764 (14.): geschützte Quelle
        if (!hasChild) return null;
        return WCard(new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = "Gut zu wissen: Grenzen des Kinderschutzes", Foreground = Text, FontWeight = FontWeight.SemiBold },
            new TextBlock { Name = "kinderschutz-hinweis", TextWrapping = TextWrapping.Wrap, Foreground = Muted, FontSize = 12,
                Text = "Im Kinderprofil installiert Trendo nichts, startet keine fremden Programme und führt keine Skripte aus. Trendo läuft aber im selben Windows-Konto wie die Erwachsenen. Wer sich gut auskennt, kann Trendos Sperren dort umgehen. Wirklich sicher ist es nur, wenn das Kind ein eigenes Windows-Konto ohne Administratorrechte bekommt (Windows-Einstellungen → Konten → Familie)." } } });
    }

    /// <summary>T-0713: Hinweis-Karte, wenn neben einem Kinderprofil ein Erwachsenen-Profil ohne PIN besteht (dann kann ein Kind über „Wechseln“
    /// dort freigeben). Nur Hinweis mit „PIN festlegen“, nichts wird erzwungen. Im Kinderprofil nicht sichtbar. null = kein Hinweis nötig.</summary>
    internal Control? PinHinweisKarte()
    {
        if (_settings.Child) return null;
        var open = Persons.AdultsWithoutLockBesideChild();
        if (open.Count == 0) return null;
        var selfId = PersonStore.IdOf(_core.DataRoot) ?? "haupt";
        var self = open.Any(p => p.Id == selfId);
        string Label((string Id, string Name) p) => p.Id == selfId ? "Dein Profil" : $"Das Profil „{(p.Name.Length > 0 ? p.Name : "Hauptprofil")}“";
        var who = open.Count == 1 ? Label(open[0]) + " hat" : string.Join(", ", open.Select(Label)) + " haben";
        var set = new Button { Name = "pin-hinweis-festlegen", Content = "PIN festlegen" };
        ToolTip.SetTip(set, self ? "Für dein Profil eine PIN wählen" : "Zu den Personen – dort in das Profil wechseln und eine PIN festlegen");
        set.Click += (_, _) =>
        {
            if (self) { _wizStep = 1; _wizRecovery = null; _wizChangeLock = true; Show("einrichtung"); return; }
            _personMsg = "Wechsle in das Profil ohne PIN und lege dort unter „Anmeldung ändern“ eine PIN fest.";
            Show("personen");
        };
        return new Border { Name = "pin-hinweis", Background = Brush.Parse("#33F59E0B"), BorderBrush = Brush.Parse("#66F59E0B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Kinderschutz: eine PIN fehlt", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                new TextBlock { Name = "pin-hinweis-text", Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
                    Text = $"{who} keine PIN. Ein Kind kann mit „Wechseln“ in dieses Profil gehen und dort Programme einrichten oder andere Dinge freigeben. Mit einer PIN geht das nur mit dir." },
                new WrapPanel { Children = { set } } } } };
    }

    Control PersonsView()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(P("Jede Person am PC hat ihr eigenes Trendo: eigene Projekte, Einstellungen und Anmeldung. Beim Start wählt sich jede Person selbst.", Text, 13));
        if (_personMsg.Length > 0) sp.Children.Add(P(_personMsg, _personMsg.StartsWith('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), 12.5));
        if (PinHinweisKarte() is { } pinHint) sp.Children.Add(pinHint); // T-0713
        if (KinderschutzHinweis() is { } kh) sp.Children.Add(kh); // T-0764 (4. Prüfrunde M1)
        // eigene Anmeldung
        var mine = new StackPanel { Spacing = 6 };
        mine.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { AvatarView(48), new TextBlock { Text = (string.IsNullOrEmpty(_settings.Name) ? "Du" : _settings.Name) + (_settings.Child ? " (Kind)" : ""), Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } });
        mine.Children.Add(P("Anmeldung: " + Lock.Current switch { ProfileLock.Kind.Pin => "PIN", ProfileLock.Kind.Password => "Passwort", _ => "ohne" }, null, 12));
        var lockNow = new Button { Name = "person-lock-now", Content = "Jetzt sperren", IsEnabled = Lock.Enabled || Persons.All().Count > 0 };
        lockNow.Click += (_, _) => LockNow();
        var change = new Button { Name = "person-change-lock", Content = "Anmeldung ändern" };
        change.Click += (_, _) => { _wizStep = 1; _wizRecovery = null; _wizChangeLock = true; Show("einrichtung"); };
        mine.Children.Add(new WrapPanel { Children = { Sp(lockNow), Sp(change) } });
        var atStart = new CheckBox { Name = "person-lock-at-start", Content = "Beim Start von Trendo nach PIN/Passwort fragen", IsChecked = _settings.LockOnStart, IsEnabled = Lock.Enabled, Foreground = Text };
        atStart.IsCheckedChanged += (_, _) => { _settings.LockOnStart = atStart.IsChecked == true; SaveSettings(); };
        mine.Children.Add(atStart);
        sp.Children.Add(WCard(mine));

        var store = Persons;
        foreach (var p in store.All())
        {
            var id = p.Id; var nm = p.Name;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            row.Children.Add(AvatarView(40, p.Avatar, store.RootOf(id)));
            row.Children.Add(Pos(new StackPanel { Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = nm + (p.Child ? " (Kind)" : ""), Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                P("Anmeldung: " + new ProfileLock(store.RootOf(id)).Current switch { ProfileLock.Kind.Pin => "PIN", ProfileLock.Kind.Password => "Passwort", _ => "ohne" }, null, 11) } }, 1));
            var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!_settings.Child)
            {
                var reset = new Button { Name = "person-reset-" + id, Content = "Anmeldung zurücksetzen" };
                ToolTip.SetTip(reset, "Für vergessene PINs: Die Person meldet sich danach ohne PIN an und wählt eine neue.");
                reset.Click += (_, _) => { new ProfileLock(store.RootOf(id)).Set(ProfileLock.Kind.None); _core.Audit.Append(Principal.LocalOwner.Name, "person.lock.reset", id); _personMsg = $"✓ Die Anmeldung von {nm} ist zurückgesetzt."; Show("personen"); };
                actions.Children.Add(Sp(reset));
                actions.Children.Add(Sp(ConfirmRemoveButton("person:" + id, "person-remove-" + id, "personen", () => { store.Remove(id, _core.Audit); _personMsg = $"{nm} ist nicht mehr in der Liste. Die Daten bleiben im Ordner erhalten."; return Task.CompletedTask; })));
            }
            var sw = new Button { Name = "person-switch-" + id, Content = "Wechseln" }; sw.Click += (_, _) => SwitchPerson(id);
            actions.Children.Add(Sp(sw));
            row.Children.Add(Pos(actions, 2));
            sp.Children.Add(WCard(row));
        }
        if (!_settings.Child)
        {
            var add = new StackPanel { Spacing = 6 };
            add.Children.Add(H("Person hinzufügen", 14));
            var name = new TextBox { Name = "person-name", Text = _personName, PlaceholderText = "Name", Width = 220 };
            name.TextChanged += (_, _) => _personName = name.Text ?? "";
            var child = new CheckBox { Name = "person-child", Content = "Kind (einfache Bedienung, Installieren nur durch Erwachsene)", IsChecked = _personChild, Foreground = Text };
            child.IsCheckedChanged += (_, _) => _personChild = child.IsChecked == true;
            var go = new Button { Name = "person-add", Content = "Hinzufügen", Background = Accent };
            go.Click += (_, _) =>
            {
                _personName = name.Text ?? ""; _personChild = child.IsChecked == true;
                try { var p = store.Add(_personName, "emoji:" + AvatarEmojis[(store.All().Count + 1) % AvatarEmojis.Length], _personChild, _core.Audit); _personMsg = $"✓ {p.Name} ist angelegt."; _personName = ""; _personChild = false; }
                catch (ArgumentException e) { _personMsg = e.Message; }
                Show("personen");
            };
            add.Children.Add(new WrapPanel { Children = { Sp(name), Sp(go) } }); add.Children.Add(child);
            sp.Children.Add(WCard(add));
        }
        return Page("Personen", sp);
    }
}
