using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>T-0682: Fremde Office-Dateien schützen. Enthält eine geöffnete Datei Dinge, die Trendo Text, Tabelle oder Präsentation nicht hält
/// (Importbericht aus <see cref="ImportCheck"/>), dann überschreibt „Speichern“ das Original nicht mehr still: Eine Hinweisleiste nennt, was fehlt,
/// und bietet „Als Kopie speichern“ (Standard) oder – nach ausdrücklicher Bestätigung – „Trotzdem überschreiben“; vor dem Überschreiben wird das
/// Original daneben gesichert. Ohne erkannten Verlust bleibt alles wie bisher.</summary>
public partial class MainWindow
{
    sealed class ImportGuard(string path, ImportReport report)
    {
        public string Path { get; } = path;
        public ImportReport Report { get; } = report;
        /// <summary>Der Nutzer hat das Überschreiben ausdrücklich bestätigt.</summary>
        public bool Confirmed;
        /// <summary>Die Leiste fragt gerade „Wirklich überschreiben?“.</summary>
        public bool Asking;
        public bool Details;
        /// <summary>Ein Speichern wurde aufgehalten – die Leiste hebt sich hervor.</summary>
        public bool Stopped;
    }

    readonly Dictionary<string, ImportGuard> _importGuards = [];

    /// <summary>Importbericht der gerade geschützten Datei (null = kein Schutz nötig oder schon entschieden). Für Tests und die Oberfläche.</summary>
    public ImportReport? OfficeImportReport(string kind) => _importGuards.GetValueOrDefault(kind)?.Report;

    /// <summary>Nach dem Öffnen: Schutz einschalten, wenn der Leser etwas nicht übernommen hat.</summary>
    void ImportGuardSet(string kind, string? path, ImportReport? report)
    {
        if (path is null || report is null || !report.HasLoss) { _importGuards.Remove(kind); return; }
        _importGuards[kind] = new ImportGuard(Path.GetFullPath(path), report);
        _core.Audit.Append(Principal.LocalOwner.Name, "office.import.verlust", $"{Path.GetFileName(path)}: {string.Join(", ", report.Verluste.Select(v => $"{v.Id}×{v.Anzahl}"))}");
    }

    void ImportGuardClear(string kind) => _importGuards.Remove(kind);

    static bool SameFile(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>Hält ein Speichern in die Originaldatei auf, solange es nicht ausdrücklich bestätigt ist.</summary>
    bool ImportGuardStops(string kind, string target)
    {
        if (!_importGuards.TryGetValue(kind, out var g) || g.Confirmed || !SameFile(g.Path, target)) return false;
        g.Stopped = true;
        return true;
    }

    string ImportStopMessage(string kind) => _importGuards.TryGetValue(kind, out var g)
        ? $"Nicht gespeichert: „{Path.GetFileName(g.Path)}“ enthält Dinge, die Trendo nicht darstellt. Oben „Als Kopie speichern“ wählen – das Original bleibt, wie es ist."
        : "";

    /// <summary>Welche Endung Trendo für dieses Programm schreiben kann, wenn das Original eine andere hat (ODS, ODP).</summary>
    static string ImportCopyExt(string kind, string original)
    {
        var ext = Path.GetExtension(original).ToLowerInvariant();
        return kind switch { "text" => ext is ".docx" or ".odt" ? ext : ".docx", "tabelle" => ext == ".xlsx" ? ext : ".xlsx", _ => ".pptx" };
    }

    static bool ImportCanOverwrite(string kind, string original) => string.Equals(ImportCopyExt(kind, original), Path.GetExtension(original), StringComparison.OrdinalIgnoreCase);

    /// <summary>Vorschlag für die Kopie: „Name (Trendo).docx“ neben dem Original, bei Bedarf mit Nummer.</summary>
    public static string ImportCopyName(string kind, string original, string? dir = null)
    {
        dir ??= Path.GetDirectoryName(Path.GetFullPath(original))!;
        var name = Path.GetFileNameWithoutExtension(original); var ext = ImportCopyExt(kind, original);
        var f = Path.Combine(dir, $"{name} (Trendo){ext}");
        for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{name} (Trendo {i}){ext}");
        return f;
    }

    bool ImportSave(string kind, string path) => kind switch { "text" => TextSaveAs(path), "tabelle" => SheetSaveAs(path), _ => DeckSaveAs(path) };
    void ImportSay(string kind, string msg) { switch (kind) { case "text": _txMsg = msg; break; case "tabelle": _shMsg = msg; break; default: _dkMsg = msg; break; } }

    /// <summary>„Als Kopie speichern“: schreibt neben das Original (oder, wenn dort nicht geschrieben werden darf, in den Dokumente-Ordner) und arbeitet in der Kopie weiter. Das Original bleibt unberührt.</summary>
    public string? OfficeSaveCopy(string kind)
    {
        if (!_importGuards.TryGetValue(kind, out var g)) return null;
        var original = g.Path; string copy;
        try { copy = ImportCopyName(kind, original); if (!ImportSave(kind, copy)) return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var dir = Path.Combine(ProjectsRoot, "Dokumente"); Directory.CreateDirectory(dir);
            copy = ImportCopyName(kind, original, dir); if (!ImportSave(kind, copy)) return null;
        }
        _importGuards.Remove(kind);
        ImportSay(kind, $"Als Kopie gespeichert: {Path.GetFileName(copy)} – „{Path.GetFileName(original)}“ ist unverändert.");
        _core.Audit.Append(Principal.LocalOwner.Name, "office.import.kopie", $"{Path.GetFileName(original)} → {Path.GetFileName(copy)}");
        return copy;
    }

    /// <summary>Name der Sicherung des Originals: „Name.original.docx“; liegt dort schon eine andere Datei, „Name.original 2.docx“ usw.</summary>
    static string ImportBackupName(string original)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(original))!; var name = Path.GetFileNameWithoutExtension(original); var ext = Path.GetExtension(original);
        var f = Path.Combine(dir, $"{name}.original{ext}");
        for (var i = 2; File.Exists(f) && !SameContent(f, original); i++) f = Path.Combine(dir, $"{name}.original {i}{ext}");
        return f;
    }

    static bool SameContent(string a, string b)
    {
        var fa = new FileInfo(a); var fb = new FileInfo(b);
        return fa.Exists && fb.Exists && fa.Length == fb.Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    /// <summary>„Trotzdem überschreiben“ nach ausdrücklicher Bestätigung: sichert zuerst das Original als „Name.original.Endung“ daneben, dann wird gespeichert.
    /// Gelingt die Sicherung nicht, wird nicht überschrieben. Liefert den Pfad der Sicherung.</summary>
    public string? OfficeOverwriteOriginal(string kind)
    {
        if (!_importGuards.TryGetValue(kind, out var g) || !ImportCanOverwrite(kind, g.Path)) return null;
        var backup = ImportBackupName(g.Path);
        if (File.Exists(g.Path) && !File.Exists(backup)) File.Copy(g.Path, backup);
        g.Confirmed = true;
        try { if (!ImportSave(kind, g.Path)) { g.Confirmed = false; return null; } }
        catch { g.Confirmed = false; throw; }
        _importGuards.Remove(kind);
        ImportSay(kind, $"Überschrieben: {Path.GetFileName(g.Path)} – das Original liegt als „{Path.GetFileName(backup)}“ daneben.");
        _core.Audit.Append(Principal.LocalOwner.Name, "office.import.ueberschrieben", $"{Path.GetFileName(g.Path)} (Sicherung {Path.GetFileName(backup)})");
        return backup;
    }

    /// <summary>Hinweisleiste: was die Datei enthält, das Trendo nicht darstellt – mit „Als Kopie speichern“ und „Trotzdem überschreiben“ (zweistufig).</summary>
    Control? ImportBanner(string kind)
    {
        if (!_importGuards.TryGetValue(kind, out var g)) return null;
        var file = Path.GetFileName(g.Path); var canOverwrite = ImportCanOverwrite(kind, g.Path);
        void Run(Action a)
        {
            try { a(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException) { ImportSay(kind, "Speichern ging nicht: " + FriendlyError.Say(e)); }
            Show(kind);
        }
        Button B(string name, string label, Action go, bool accent = false)
        {
            var b = new Button { Name = $"office-import-{name}-{kind}", Content = label, Padding = new Thickness(12, 4), Margin = new Thickness(6, 0, 0, 0), Focusable = false };
            if (accent) b.Background = Accent;
            b.Click += (_, _) => Run(go);
            return b;
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right };
        string text;
        if (g.Asking && canOverwrite)
        {
            text = $"Wirklich überschreiben? In „{file}“ gehen verloren: {g.Report.Kurz(6)}. Das Original wird vorher als „{Path.GetFileName(ImportBackupName(g.Path))}“ daneben gesichert.";
            buttons.Children.Add(B("no", "Abbrechen", () => g.Asking = false, accent: true));
            buttons.Children.Add(B("yes", "Ja, überschreiben", () => OfficeOverwriteOriginal(kind)));
        }
        else
        {
            text = $"Diese Datei enthält Dinge, die Trendo nicht darstellt: {g.Report.Kurz()}. "
                + (canOverwrite ? "Beim Speichern in diese Datei gingen sie verloren." : $"Trendo kann dieses Format nicht speichern – die Kopie wird eine {ImportCopyExt(kind, g.Path).TrimStart('.').ToUpperInvariant()}-Datei, das Original bleibt.");
            var copy = B("copy", "Als Kopie speichern", () => OfficeSaveCopy(kind), accent: true);
            ToolTip.SetTip(copy, $"Speichert als „{Path.GetFileName(ImportCopyName(kind, g.Path))}“ – das Original bleibt, wie es ist.");
            buttons.Children.Add(copy);
            if (canOverwrite) buttons.Children.Add(B("overwrite", "Trotzdem überschreiben", () => g.Asking = true));
            buttons.Children.Add(B("details", g.Details ? "Weniger" : "Alles zeigen", () => g.Details = !g.Details));
        }
        var body = new StackPanel { Children = { new TextBlock { Name = $"office-import-text-{kind}", Text = text, Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } };
        if (g.Details && !g.Asking)
            body.Children.Add(new TextBlock { Name = $"office-import-list-{kind}", Text = g.Report.Lang() + "\nDie Liste nennt nur, was Trendo beim Öffnen erkannt hat.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
        var strong = g.Stopped || g.Asking;
        return new Border { Name = $"office-import-{kind}", Background = Brush.Parse(strong ? "#33F5A524" : "#1AF5A524"), BorderBrush = Brush.Parse(strong ? "#FFF5A524" : "#66F5A524"), BorderThickness = new Thickness(strong ? 2 : 1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 8),
            Child = new DockPanel { Children = { buttons, body } } };
    }
}
