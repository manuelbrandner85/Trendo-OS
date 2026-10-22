using System.Text.RegularExpressions;
using Avalonia.Automation;
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
    public Button? OrdnerWahl { get; }
    public ComboBox Zuletzt { get; }
    public bool Ordner { get; }
    public bool Mehrere { get; }
    public bool AuchOrdner { get; }
    public IReadOnlyList<string> Muster { get; }
    public string Beschriftung { get; }

    /// <summary>Wird aufgerufen, wenn ein Pfad über den Dialog, die Liste oder Ziehen gesetzt wurde.</summary>
    public Action<string>? Gewaehlt { get; set; }

    public DateiFeld(MainWindow fenster, string name, string beschriftung, string text, bool ordner, string studio, IReadOnlyList<string>? muster = null, bool zeigeBeschriftung = true, bool mehrere = false, bool auchOrdner = false)
    {
        _fenster = fenster;
        _studio = studio;
        Ordner = ordner;
        Mehrere = mehrere;
        AuchOrdner = auchOrdner;
        Beschriftung = beschriftung;
        Muster = muster is { Count: > 0 } ? muster : MusterAusText(beschriftung);
        Name = name + "-feld";
        Spacing = 4;
        Orientation = Orientation.Vertical;

        var hinweis = ordner ? "Ordner hierher ziehen oder durchsuchen" : "Datei hierher ziehen oder durchsuchen";
        Eingabe = new TextBox
        {
            Name = name,
            Text = text,
            PlaceholderText = !zeigeBeschriftung && beschriftung.Length > 0 ? beschriftung : hinweis,
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            AcceptsReturn = mehrere,
            TextWrapping = mehrere ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = mehrere ? 60 : 0,
        };
        Durchsuchen = new Button { Name = name + "-browse", Content = "Durchsuchen …", FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), VerticalAlignment = VerticalAlignment.Center };
        Durchsuchen.Click += async (_, _) =>
        {
            try
            {
                var titel = string.IsNullOrWhiteSpace(beschriftung) ? (Ordner ? "Ordner wählen" : "Datei wählen") : beschriftung;
                var pfade = await fenster.DateienWaehlen(Ordner, titel, Mehrere, Muster);
                if (Mehrere) { foreach (var p in pfade) Uebernehmen(p); }
                else if (pfade.Count > 0) Uebernehmen(pfade[0]);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* Dialog auf diesem System nicht verfügbar */ }
        };
        if (auchOrdner)
        {
            OrdnerWahl = new Button { Name = name + "-browse-ordner", Content = "Ordner …", FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(OrdnerWahl, "Ordner …");
            OrdnerWahl.Click += async (_, _) =>
            {
                try
                {
                    var pfade = await fenster.DateienWaehlen(true, "Ordner wählen", false, null);
                    if (pfade.Count > 0) Uebernehmen(pfade[0]);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException) { /* Dialog auf diesem System nicht verfügbar */ }
            };
        }
        Zuletzt = new ComboBox { Name = name + "-recent", MinWidth = 180, IsVisible = false, PlaceholderText = "Zuletzt benutzt" };
        ToolTip.SetTip(Zuletzt, "Zuletzt benutzt");
        AutomationProperties.SetName(Durchsuchen, "Durchsuchen …");
        AutomationProperties.SetName(Eingabe, Eingabe.PlaceholderText ?? beschriftung);
        AutomationProperties.SetName(Zuletzt, "Zuletzt benutzt");
        Zuletzt.SelectionChanged += (_, _) =>
        {
            if (_ruhe) return;
            if (Zuletzt.SelectedItem is ZuletztEintrag e) Uebernehmen(e.Pfad, merken: false);
        };

        if (zeigeBeschriftung && beschriftung.Length > 0)
            Children.Add(new TextBlock { Text = beschriftung, Foreground = fenster.MutedBrush, FontSize = 11 });
        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        zeile.Children.Add(Eingabe);
        zeile.Children.Add(Durchsuchen);
        if (OrdnerWahl is not null) zeile.Children.Add(OrdnerWahl);
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
        if (Mehrere)
        {
            var zeilen = (Eingabe.Text ?? "").Replace("\r", "").Split('\n').Select(z => z.Trim()).Where(z => z.Length > 0).ToList();
            if (!zeilen.Any(z => string.Equals(z, pfad, StringComparison.OrdinalIgnoreCase))) zeilen.Add(pfad);
            Eingabe.Text = string.Join("\n", zeilen);
        }
        else Eingabe.Text = pfad;
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

    /// <summary>True, wenn dieses Textfeld einen Pfad oder Ordner erwartet und deshalb die gemeinsame Auswahl braucht.</summary>
    public static bool BrauchtAuswahl(string? name, string? hint)
    {
        var n = (name ?? "").Trim().ToLowerInvariant();
        var l = (hint ?? "").Trim().ToLowerInvariant();
        if (n.Length == 0 && l.Length == 0) return false;
        if (n is "bm-folder" or "sc-args" or "wfe-pattern" or "ren-pattern" or "attachments" or "letter-attachments" or "wfe-text") return false;
        if (n.Contains("attachment")) return false;
        if (l.Contains("prüfsumme") || l.Contains("pruefsumme") || l.Contains("muster") || l.Contains("parameter") || l.Contains("ordner (optional)")) return false;
        var bilddatei = l.Contains("bilddatei");
        if (!bilddatei && (l.Contains("adresse") || l.Contains("https://") || l.Contains("http://"))) return false;
        if (n is "pdf-merge-files" or "pkg-att" or "pkg-main" or "cmp-a" or "cmp-b" or "merge-template" or "merge-csv" or "bm-import" or "clean-theirs" or "portable-zip" or "sc-exe" or "wfe-from" or "wfe-to" or "wfe-at") return true;
        if (n.EndsWith("-file") || n.EndsWith("-folder") || n.EndsWith("-path") || n.EndsWith("-exe") || n.EndsWith("-zip") || n.EndsWith("-img") || n.EndsWith("-doc") || n.EndsWith("-files")) return true;
        if (n.Contains("-file-") || n.Contains("-folder-")) return true;
        if (Wort(l, "ordner") || Wort(l, "datei") || Wort(l, "dokument") || l.Contains("hauptdokument") || Wort(l, "fassung") || l.Contains("programmdatei") || l.Contains("bilddatei")) return true;
        if (l.Contains(".json") || l.Contains(".vcf") || l.Contains(".eml") || l.Contains(".png") || l.Contains(".docx") || l.Contains(".csv") || l.Contains(".zip") || l.Contains(".pdf") || l.Contains(".txt") || l.Contains(".md") || l.Contains(".ics")) return true;
        if (l.Contains("bild für") || l.Contains("bild der") || l.Contains("bild zum") || l.Contains("oder bild") || l.Contains("vorhandenes bild")) return true;
        if (l.Contains("vorlage") && (l.Contains("word") || l.Contains(".docx") || l.Contains("platzhalter"))) return true;
        return false;
    }

    /// <summary>Datei und Ordner sind beide gültig: ein Knopf für Dateien, einer für den Ordner.</summary>
    public static bool AuchOrdnerFeld(string? hint)
    {
        var l = (hint ?? "").ToLowerInvariant();
        return l.Contains("oder datei") || l.Contains("oder zip") || l.Contains("ordner/datei");
    }

    /// <summary>Nur ein Ordner. Mischfelder (Datei oder Ordner) zählen hier nicht.</summary>
    public static bool IstOrdnerFeld(string? name, string? hint)
    {
        var n = (name ?? "").ToLowerInvariant();
        var l = (hint ?? "").ToLowerInvariant();
        if (AuchOrdnerFeld(l)) return false;
        if (l.Contains("bilddatei") || l.Contains("https://") || l.Contains("http://")) return false;
        if (n.Contains("folder")) return true;
        return Wort(l, "ordner");
    }

    /// <summary>Mehrere Pfade, einer pro Zeile. Durchsuchen hängt an.</summary>
    public static bool MehrereFelder(string? name, string? hint)
    {
        var n = (name ?? "").ToLowerInvariant();
        var l = (hint ?? "").ToLowerInvariant();
        if (n is "pdf-merge-files" or "pkg-att") return true;
        if (!BrauchtAuswahl(name, hint)) return false;
        return l.Contains("pro zeile");
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
        var m = new List<string>();
        void Add(params string[] p)
        {
            foreach (var x in p)
                if (!m.Contains(x)) m.Add(x);
        }
        if (l.Contains("csv")) Add("*.csv");
        if (l.Contains("gpx")) Add("*.gpx");
        if (l.Contains("srt")) Add("*.srt");
        if (l.Contains("glb") || l.Contains("gltf")) Add("*.glb", "*.gltf");
        if (l.Contains("vcf") || l.Contains(".eml")) Add("*.vcf", "*.eml");
        if (l.Contains(".ics") || l.Contains("ics-datei") || l.Contains("ics ")) Add("*.ics");
        if (l.Contains("json")) Add("*.json");
        if (l.Contains("programmdatei") || l.Contains(".exe") || l.Contains(".msi")) Add("*.exe", "*.msi");
        if (l.Contains("lesezeichen")) Add("*.html", "*.htm");
        if (l.Contains("bild") || l.Contains("foto") || (l.Contains("scan") && l.Contains("foto")))
            Add("*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp", "*.tif", "*.tiff");
        else if (l.Contains("png")) Add("*.png");
        if (l.Contains("video") || l.Contains("film")) Add("*.mp4", "*.mkv", "*.mov", "*.webm", "*.avi");
        if (l.Contains("audio") || l.Contains("ton")) Add("*.mp3", "*.wav", "*.flac", "*.m4a", "*.ogg");
        if (l.Contains("pdf")) Add("*.pdf");
        if (l.Contains("punkt") || l.Contains("ply") || l.Contains("xyz") || (l.Contains("scan") && !l.Contains("foto") && !l.Contains("bild")))
            Add("*.ply", "*.xyz", "*.pcd", "*.las", "*.e57");
        if (l.Contains("gcode") || l.Contains(".nc")) Add("*.nc", "*.gcode", "*.tap");
        if (l.Contains("zip")) Add("*.zip");
        if (l.Contains("word") || l.Contains("docx") || l.Contains("odt")) Add("*.docx", "*.odt", "*.doc");
        if (l.Contains("xlsx")) Add("*.xlsx");
        if (l.Contains("pptx")) Add("*.pptx");
        if (l.Contains(".txt")) Add("*.txt");
        if (l.Contains(".md")) Add("*.md");
        return m.Count == 0 ? ["*.*"] : m;
    }

    private static bool Wort(string text, string wort) => Regex.IsMatch(text, $@"\b{Regex.Escape(wort)}\b", RegexOptions.CultureInvariant);

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
