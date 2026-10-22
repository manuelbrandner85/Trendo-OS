using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

public partial class MainWindow
{
    private readonly List<DateiFeld> _dateiFelder = [];
    private bool _dateiDrop;
    private RecentPaths? _zuletzt;
    private string? _programmFrage;

    /// <summary>Test: liefert einen Pfad statt den Systemdialog zu öffnen. Argumente: Ordner, Titel, Muster.</summary>
    public Func<bool, string, IReadOnlyList<string>?, Task<string?>>? DateiWahlOverride { get; set; }

    public IBrush MutedBrush => Muted;

    private RecentPaths Zuletzt => _zuletzt ??= new RecentPaths(Path.Combine(_core.DataRoot, "zuletzt-dateien.json"));

    internal void RegisterDateiFeld(DateiFeld feld)
    {
        _dateiFelder.Add(feld);
        if (_dateiDrop) return;
        _dateiDrop = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, StudioZiehen);
        AddHandler(DragDrop.DropEvent, StudioAblegen);
    }

    public void ZuletztMerken(string studio, string pfad) => Zuletzt.Remember(studio, pfad);
    public IReadOnlyList<string> ZuletztListe(string studio) => Zuletzt.List(studio);

    /// <summary>Legt eine Datei auf das erste leere Dateifeld dieser Seite, sonst auf das erste Dateifeld. Dasselbe passiert beim Ziehen auf das Studio.</summary>
    public bool DateiAblegen(string pfad)
    {
        var ziel = _dateiFelder.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.Eingabe.Text)) ?? _dateiFelder.FirstOrDefault();
        if (ziel is null) return false;
        if (ziel.Ordner && File.Exists(pfad)) pfad = Path.GetDirectoryName(pfad) ?? pfad;
        ziel.Uebernehmen(pfad);
        return true;
    }

    private void StudioZiehen(object? sender, DragEventArgs e)
    {
        if (e.Handled || !DateiFeld.HatDateien(e.DataTransfer)) return;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void StudioAblegen(object? sender, DragEventArgs e)
    {
        if (e.Handled) return;
        var pfad = DateiFeld.ErsterPfad(e.DataTransfer);
        if (pfad is null) return;
        if (DateiAblegen(pfad)) e.Handled = true;
    }

    public async Task<IReadOnlyList<string>> DateienWaehlen(bool ordner, string titel, bool mehrere, IReadOnlyList<string>? muster)
    {
        if (DateiWahlOverride is { } over)
        {
            var one = await over(ordner, titel, muster);
            return string.IsNullOrWhiteSpace(one) ? [] : [one];
        }
        if (ordner)
        {
            var ordnerListe = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = titel, AllowMultiple = mehrere });
            return ordnerListe.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
        }
        var opt = new FilePickerOpenOptions { Title = titel, AllowMultiple = mehrere };
        if (muster is { Count: > 0 } && !(muster.Count == 1 && muster[0] == "*.*"))
            opt.FileTypeFilter = [new FilePickerFileType(titel) { Patterns = muster.ToList() }, new FilePickerFileType("Alle Dateien") { Patterns = ["*.*"] }];
        var dateien = await StorageProvider.OpenFilePickerAsync(opt);
        return dateien.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
    }
}
