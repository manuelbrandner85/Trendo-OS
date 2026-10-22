using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-10: Sauberer Nachbau.</summary>
public partial class MainWindow
{
    public string CleanFolder => WorkFolder("Sauberer Nachbau");
    private string? _cleanOpen;
    private IReadOnlyList<Similar>? _cleanSims;

    private void InitW5CleanRoom()
    {
        RegisterTab(BusinessTabs, "nachbau", "Sauberer Nachbau", "X-10", CleanView, "clean room", "nachbau", "nachbauen", "urheberrecht", "aehnlichkeit", "plagiat");
        EnsureMyPrograms();
    }

    public void CleanCheck(string original)
    {
        if (_cleanOpen is null || !Directory.Exists(original)) { Say("Wähle einen Original-Ordner, der existiert.", "nachbau"); return; }
        _cleanSims = CleanRoom.Compare(Path.Combine(_cleanOpen, "2-Umsetzung"), original);
        var rep = CleanRoom.Report(_cleanOpen, _cleanSims, original);
        CleanRoom.Log(_cleanOpen, "Prüfen", $"Ähnlichkeitsprüfung: {_cleanSims.Count} auffällige Dateien");
        Say(_cleanSims.Count == 0 ? "Keine auffällige Ähnlichkeit gefunden. Bericht gespeichert." : $"{_cleanSims.Count} Dateien sind dem Original sehr ähnlich. Bitte prüfen.", "nachbau");
    }

    private Control CleanView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Du kannst ein Programm benennen oder beschreiben, zum Beispiel „ein Notizprogramm wie Notepad++“ oder „so etwas wie Paint“. Trendo baut daraus eine eigene App. Ein Original-Ordner bleibt freiwillig und dient nur dem späteren Vergleich, nie als Code-Vorlage. Keine Rechtsberatung.") } };
        RebuildSection(sp);
        var rules = new StackPanel { Spacing = 2, Children = { H("Die Regeln", 14) } };
        foreach (var r in CleanRoom.Rules) rules.Children.Add(P("• " + r, null, 11));
        sp.Children.Add(Card(rules));
        var name = new TextBox { Name = "clean-name", PlaceholderText = "Name deines Projekts", Width = 220 }; var orig = new TextBox { Name = "clean-original", PlaceholderText = "Vorbild, z. B. ein bekannter Notizblock", Width = 260 };
        sp.Children.Add(Card(new WrapPanel { Children = { name, orig, Act("clean-create", "Anlegen", () =>
        {
            try { _cleanOpen = CleanRoom.Create(CleanFolder, string.IsNullOrWhiteSpace(name.Text) ? "Nachbau" : name.Text, orig.Text ?? ""); _cleanSims = null; Say("Angelegt. Schritt 1: Beschreibung schreiben.", "nachbau"); }
            catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "nachbau"); }
        }, true) } }));
        var dirs = Directory.GetDirectories(CleanFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        _cleanOpen ??= dirs.FirstOrDefault();
        var pick = new WrapPanel(); var n = 0;
        foreach (var d in dirs.Take(10)) { var dir = d; pick.Children.Add(Act($"clean-open-{n++}", (dir == _cleanOpen ? "● " : "") + Path.GetFileName(dir), () => { _cleanOpen = dir; _cleanSims = null; Show("nachbau"); })); }
        if (n > 0) sp.Children.Add(pick);
        if (_cleanOpen is { } open && Directory.Exists(open))
        {
            var role = new ComboBox { Name = "clean-role", ItemsSource = CleanRoom.Roles, SelectedIndex = 0, Width = 140 };
            var note = new TextBox { Name = "clean-note", PlaceholderText = "Was hast du gemacht?", Width = 320 };
            var j = new StackPanel { Spacing = 4, Children = { Row(H(Path.GetFileName(open), 14), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Act("clean-spec", "Beschreibung öffnen", () => Launch(Path.Combine(open, "1-Beschreibung", "BESCHREIBUNG.md"))), Act("clean-folder", "Ordner", () => Launch(open)) } }),
                new WrapPanel { Children = { role, note, Act("clean-log", "Ins Tagebuch", () => { CleanRoom.Log(open, CleanRoom.Roles[Math.Max(0, role.SelectedIndex)], note.Text ?? ""); Show("nachbau"); }) } } } };
            foreach (var e in CleanRoom.Journal(open).TakeLast(8).Reverse()) j.Children.Add(P($"{e.At:dd.MM. HH:mm} · {e.Role} · {e.Text}", null, 11));
            sp.Children.Add(Card(j));
            var theirs = new TextBox { Name = "clean-theirs", PlaceholderText = "Ordner mit dem Original-Code (nur zum Vergleich)", Width = 360 };
            var chk = new StackPanel { Spacing = 4, Children = { H("Ähnlichkeit prüfen", 14), P("Vergleicht deinen Code in „2-Umsetzung“ mit dem Original. Umbenennen von Namen hilft dabei nicht.", null, 11),
                new WrapPanel { Children = { theirs, Act("clean-check", "Prüfen", () => CleanCheck(theirs.Text?.Trim('"', ' ') ?? ""), true) } } } };
            if (_cleanSims is { } sims) foreach (var s in sims.Take(10)) chk.Children.Add(P($"⚠ {s.Ours} ↔ {s.Theirs}: {s.Score:P0} gleich", Brush.Parse("#F97316"), 11));
            sp.Children.Add(Card(chk));
        }
        return BusinessPage("Sauberer Nachbau", sp);
    }
}
