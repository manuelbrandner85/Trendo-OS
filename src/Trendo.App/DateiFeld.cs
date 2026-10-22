using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Trendo.App;

/// <summary>Eintrag in der Liste „Zuletzt benutzt“. Der angezeigte Text ist der Dateiname.</summary>
public sealed record ZuletztEintrag(string Pfad)
{
    public override string ToString()
    {
        var name = Path.GetFileName(Pfad);
        return string.IsNullOrEmpty(name) ? Pfad : name;
    }
}

/// <summary>Datei- oder Ordnerfeld: Durchsuchen, Ziehen und Ablegen, zuletzt benutzte Dateien. Der Pfad muss nicht abgetippt werden.</summary>
public sealed class DateiFeld : StackPanel
{
    private readonly MainWindow _fenster;
    private readonly string _studio;
    private bool _ruhe;

    public TextBox Eingabe { get; }
    public Button Durchsuchen { get; }
    public ComboBox Zuletzt { get; }
    public bool Ordner { get; }
    public IReadOnlyList<string> Muster { get; }
    public string Beschriftung { get; }

    /// <summary>Wird aufgerufen, wenn ein Pfad über den Dialog, die Liste oder Ziehen gesetzt wurde.</summary>
    public Action<string>? Gewaehlt { get; set; }

    public DateiFeld(MainWindow fenster, string name, string beschriftung, string text, bool ordner, string studio, IReadOnlyList<string>? muster = null, bool zeigeBeschriftung = true)
    {
        _fenster = fenster;
        _studio = studio;
        Ordner = ordner;
        Beschriftung = beschriftung;
        Muster = muster is { Count: > 0 } ? muster : MusterAusText(beschriftung);
        Name = name + "-feld";
        Spacing = 4;
        Orientation = Orientation.Vertical;

        Eingabe = new TextBox
        {
            Name = name,
            Text = text,
            PlaceholderText = ordner ? "Ordner hierher ziehen oder durchsuchen" : "Datei hierher ziehen oder durchsuchen",
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Durchsuchen = new Button { Name = name + "-browse", Content = "Durchsuchen …", FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), VerticalAlignment = VerticalAlignment.Center };
        Durchsuchen.Click += async (_, _) =>
        {
            try
            {
                var pfade = await fenster.DateienWaehlen(Ordner, string.IsNullOrWhiteSpace(beschriftung) ? (Ordner ? "Ordner wählen" : "Datei wählen") : beschriftung, false, Muster);
                if (pfade.Count > 0) Uebernehmen(pfade[0]);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* Dialog auf diesem System nicht verfügbar */ }
        };
        Zuletzt = new ComboBox { Name = name + "-recent", MinWidth = 180, IsVisible = false, PlaceholderText = "Zuletzt benutzt" };
        Zuletzt.SelectionChanged += (_, _) =>
        {
            if (_ruhe) return;
            if (Zuletzt.SelectedItem is ZuletztEintrag e) Uebernehmen(e.Pfad, merken: false);
        };

        if (zeigeBeschriftung && beschriftung.Length > 0)
            Children.Add(new TextBlock { Text = beschriftung, Foreground = fenster.MutedBrush, FontSize = 11 });
        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Eingabe, Durchsuchen } };
        Children.Add(zeile);
        Children.Add(Zuletzt);

        DragDrop.SetAllowDrop(this, true);
        DragDrop.SetAllowDrop(Eingabe, true);
        AddHandler(DragDrop.DragOverEvent, BeimZiehen);
        AddHandler(DragDrop.DropEvent, BeimAblegen);
        Eingabe.AddHandler(DragDrop.DragOverEvent, BeimZiehen);
        Eingabe.AddHandler(DragDrop.DropEvent, BeimAblegen);
        LadeZuletzt(null);
        fenster.RegisterDateiFeld(this);
    }

    public void Uebernehmen(string pfad, bool merken = true)
    {
        pfad = pfad.Trim().Trim('"');
        if (pfad.Length == 0) return;
        Eingabe.Text = pfad;
        if (merken) _fenster.ZuletztMerken(_studio, pfad);
        LadeZuletzt(pfad);
        Gewaehlt?.Invoke(pfad);
    }

    private void LadeZuletzt(string? waehlen)
    {
        var list = _fenster.ZuletztListe(_studio).Select(p => new ZuletztEintrag(p)).ToList();
        _ruhe = true;
        Zuletzt.ItemsSource = list;
        Zuletzt.IsVisible = list.Count > 0;
        Zuletzt.SelectedItem = waehlen is null ? null : list.FirstOrDefault(e => string.Equals(e.Pfad, waehlen, StringComparison.OrdinalIgnoreCase));
        _ruhe = false;
    }

    private void BeimZiehen(object? sender, DragEventArgs e)
    {
        if (!HatDateien(e.DataTransfer)) return;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void BeimAblegen(object? sender, DragEventArgs e)
    {
        var pfad = ErsterPfad(e.DataTransfer);
        if (pfad is null) return;
        if (Ordner && File.Exists(pfad)) pfad = Path.GetDirectoryName(pfad) ?? pfad;
        Uebernehmen(pfad);
        e.Handled = true;
    }

    public static bool IstPfadSchluessel(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return key.Equals("file", StringComparison.OrdinalIgnoreCase)
            || key.Equals("folder", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("file", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("folder", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IstOrdner(string key, string label)
    {
        if (key.StartsWith("folder", StringComparison.OrdinalIgnoreCase)) return true;
        var l = label.ToLowerInvariant();
        return l.Contains("ordner") && !l.Contains("datei") && !l.Contains("zip") && !l.Contains("csv");
    }

    public static IReadOnlyList<string> MusterAusText(string label)
    {
        var l = label.ToLowerInvariant();
        if (l.Contains("csv")) return ["*.csv"];
        if (l.Contains("gpx")) return ["*.gpx"];
        if (l.Contains("srt")) return ["*.srt"];
        if (l.Contains("glb") || l.Contains("gltf")) return ["*.glb", "*.gltf"];
        if (l.Contains("bild") || l.Contains("foto") || l.Contains("scan") && l.Contains("foto")) return ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp", "*.tif", "*.tiff"];
        if (l.Contains("video") || l.Contains("film")) return ["*.mp4", "*.mkv", "*.mov", "*.webm", "*.avi"];
        if (l.Contains("audio") || l.Contains("ton")) return ["*.mp3", "*.wav", "*.flac", "*.m4a", "*.ogg"];
        if (l.Contains("pdf")) return ["*.pdf"];
        if (l.Contains("punkt") || l.Contains("ply") || l.Contains("xyz") || (l.Contains("scan") && !l.Contains("foto"))) return ["*.ply", "*.xyz", "*.pcd", "*.las", "*.e57"];
        if (l.Contains("gcode") || l.Contains(".nc")) return ["*.nc", "*.gcode", "*.tap"];
        if (l.Contains("zip")) return ["*.zip"];
        if (l.Contains("word") || l.Contains("docx")) return ["*.docx", "*.odt", "*.doc"];
        return ["*.*"];
    }

    public static bool HatDateien(IDataTransfer? data) => data?.Contains(DataFormat.File) == true;

    public static string? ErsterPfad(IDataTransfer? data)
    {
        var items = data?.TryGetFiles();
        if (items is null) return null;
        foreach (var item in items)
            if (item.TryGetLocalPath() is { Length: > 0 } p) return p;
        return null;
    }
}
