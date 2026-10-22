using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-06: Spiele-Studio. Vorlagen, eigenes Spiel mit Regeln und Level, Unreal nur wenn die Engine da ist.</summary>
public partial class MainWindow
{
    public string GameFolder => WorkFolder("Spiele");
    public Func<IReadOnlyList<GameStudio.Engine>> EngineFinder { get; set; } = GameStudio.FindEngines;
    private SpielEntwurf _spiel = new();
    private string? _spielOrdner;
    private Canvas? _spielLeinwand;
    private TextBlock? _spielInfo;

    private void InitW4Game() => RegisterTab(KreativTabs, "spiele", "Spiele", "CORE-06", GameView, "spiel", "game", "unreal", "snake");

    private Control GameView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Stell Regeln ein, zeichne ein Level und spiel es im Browser. Unreal startet nur, wenn es auf diesem PC installiert ist.") } };
        var name = new TextBox { Name = "game-name", Text = "Mein Spiel", Width = 200 };
        var tpl = new ComboBox { Name = "game-template", ItemsSource = GameStudio.Templates.Select(t => t.Label).ToList(), SelectedIndex = 0, Width = 170 };
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Fertige Vorlage", 14), new WrapPanel { Children = { name, tpl } },
            Act("game-create", "Anlegen und spielen", () =>
            {
                try
                {
                    var dir = GameStudio.CreateBrowserGame(GameFolder, GameStudio.Templates[Math.Max(0, tpl.SelectedIndex)].Id, name.Text ?? "Spiel");
                    _core.Audit.Append(Principal.LocalOwner.Name, "game.create", Path.GetFileName(dir));
                    Launch(Path.Combine(dir, "index.html")); Say($"„{Path.GetFileName(dir)}“ angelegt und im Browser geöffnet.", "spiele");
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }, true) } }));

        sp.Children.Add(Card(EigenesSpiel()));
        sp.Children.Add(Card(UnrealKarte()));

        sp.Children.Add(H("Deine Spiele", 14));
        var i = 0;
        foreach (var d in Directory.GetDirectories(GameFolder).OrderByDescending(Directory.GetLastWriteTime).Take(15))
        {
            var dir = d; var html = Path.Combine(dir, "index.html"); var up = Directory.GetFiles(dir, "*.uproject").FirstOrDefault();
            sp.Children.Add(Card(Row(P(Path.GetFileName(dir) + (up is not null ? " · Unreal" : " · Browser"), Text), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Act($"game-play-{i}", up is not null ? "Öffnen" : "Spielen", () => Launch(up ?? html)), Act($"game-folder-{i}", "Ordner", () => Launch(dir)), up is not null ? OpenInButton("unreal", () => up, "spiele", $"-{i}") : new Panel() } })));
            i++;
        }
        return KreativPage("Spiele-Studio", sp);
    }

    private Control EigenesSpiel()
    {
        var e = _spiel;
        var art = new ComboBox { Name = "game-art", ItemsSource = SpielBaukasten.Arten.Select(a => a.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(SpielBaukasten.Arten, a => a.Id == e.Art)), Width = 160 };
        var tempo = Zahl("game-tempo", e.Tempo, 1, 10);
        var leben = Zahl("game-leben", e.Leben, 1, 9);
        var sieg = Zahl("game-sieg", e.Sieg, 1, 999);
        var figur = new TextBox { Name = "game-figur", Text = e.Figur, Width = 110, Watermark = "Figurfarbe" };
        var hintergrund = new TextBox { Name = "game-hintergrund", Text = e.Hintergrund, Width = 110, Watermark = "Hintergrund" };
        var figurDatei = new TextBox { Name = "game-figur-file", Text = e.FigurDatei, Width = 220, PlaceholderText = "Bilddatei für die Figur, png oder jpg" };
        var tonDatei = new TextBox { Name = "game-ton-file", Text = e.TonDatei, Width = 220, PlaceholderText = "Audiodatei wav oder mp3" };
        var ton = new CheckBox { Name = "game-ton", Content = e.Ton ? "Ton · Ein" : "Ton · Aus", IsChecked = e.Ton, Foreground = Text };
        ton.IsCheckedChanged += (_, _) => { e.Ton = ton.IsChecked == true; ton.Content = e.Ton ? "Ton · Ein" : "Ton · Aus"; ZeichneSpiel(); };
        var breite = Zahl("game-breite", e.Breite, 8, 24);
        var hoehe = Zahl("game-hoehe", e.Hoehe, 6, 16);
        var eigenName = new TextBox { Name = "game-custom-name", Text = e.Name, Width = 200 };
        void Lesen()
        {
            e.Art = SpielBaukasten.Arten[Math.Max(0, art.SelectedIndex)].Id;
            e.Tempo = (int)(tempo.Value ?? e.Tempo);
            e.Leben = (int)(leben.Value ?? e.Leben);
            e.Sieg = (int)(sieg.Value ?? e.Sieg);
            e.Figur = figur.Text ?? e.Figur;
            e.Hintergrund = hintergrund.Text ?? e.Hintergrund;
            e.FigurDatei = figurDatei.Text ?? "";
            e.TonDatei = tonDatei.Text ?? "";
            e.Breite = (int)(breite.Value ?? e.Breite);
            e.Hoehe = (int)(hoehe.Value ?? e.Hoehe);
            e.Name = string.IsNullOrWhiteSpace(eigenName.Text) ? "Mein Level" : eigenName.Text.Trim();
            RasterSichern();
            ZeichneSpiel();
        }
        foreach (var n in new[] { tempo, leben, sieg, breite, hoehe }) n.ValueChanged += (_, _) => Lesen();
        art.SelectionChanged += (_, _) => Lesen();
        figur.TextChanged += (_, _) => Lesen();
        hintergrund.TextChanged += (_, _) => Lesen();
        var info = new TextBlock { Name = "game-preview-text", Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var stand = new TextBlock { Name = "game-zelle-stand", Foreground = Text, FontSize = 12 };
        var canvas = new Canvas { Name = "game-preview", Width = 480, Height = 220, Background = Brush.Parse("#10203f") };
        canvas.PointerPressed += (_, ev) =>
        {
            Lesen();
            var p = ev.GetPosition(canvas);
            var zelle = Zelle(p.X, p.Y, canvas.Width, canvas.Height);
            if (zelle is { } z) SchalteFeld(z.X, z.Y);
        };
        _spielLeinwand = canvas; _spielInfo = info;
        var box = new StackPanel { Spacing = 6 };
        box.Children.Add(H("Eigenes Spiel", 14));
        box.Children.Add(P("Tempo, Leben, Punkte zum Sieg, Farben und ein eigenes Level. Die Vorschau ändert sich sofort."));
        box.Children.Add(new WrapPanel { Children = { art, P("Tempo"), tempo, P("Leben"), leben, P("Sieg"), sieg } });
        box.Children.Add(new WrapPanel { Children = { P("Figur"), figur, P("Hintergrund"), hintergrund, ton } });
        box.Children.Add(new WrapPanel { Children = { figurDatei, tonDatei } });
        box.Children.Add(new WrapPanel { Children = { P("Felder breit"), breite, P("hoch"), hoehe,
            Act("game-zelle", "Feld oben links umschalten", () => { Lesen(); SchalteFeld(0, 0); }) } });
        box.Children.Add(stand);
        box.Children.Add(canvas);
        box.Children.Add(info);
        box.Children.Add(new WrapPanel { Children = { eigenName,
            Act("game-build", "Spiel bauen", () =>
            {
                Lesen();
                try
                {
                    _spielOrdner = SpielBaukasten.Bauen(GameFolder, e);
                    Launch(Path.Combine(_spielOrdner, "index.html"));
                    Say($"„{Path.GetFileName(_spielOrdner)}“ gebaut und im Browser geöffnet.", "spiele");
                }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }, true),
            Act("game-zip", "Als ZIP speichern", () =>
            {
                if (_spielOrdner is null || !Directory.Exists(_spielOrdner)) { Say("Bitte zuerst das Spiel bauen.", "spiele"); return; }
                try { var z = SpielBaukasten.Zip(_spielOrdner); Say("ZIP gespeichert: " + Path.GetFileName(z), "spiele"); }
                catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
            }),
            Act("game-open-custom", "Im Browser öffnen", () =>
            {
                if (_spielOrdner is null || !File.Exists(Path.Combine(_spielOrdner, "index.html"))) { Say("Bitte zuerst das Spiel bauen.", "spiele"); return; }
                Launch(Path.Combine(_spielOrdner, "index.html")); Say("Spiel im Browser geöffnet.", "spiele");
            }) } });
        ZeichneSpiel();
        return box;

        void SchalteFeld(int x, int y)
        {
            RasterSichern();
            var i = y * e.Breite + x;
            if (i < 0 || i >= e.Raster.Count) return;
            e.Raster[i] = !e.Raster[i];
            stand.Text = e.Raster[i] ? "Feld oben links: an" : "Feld oben links: aus";
            if (x != 0 || y != 0) stand.Text = e.Raster[i] ? $"Feld {x + 1},{y + 1}: an" : $"Feld {x + 1},{y + 1}: aus";
            ZeichneSpiel();
        }
    }

    private static NumericUpDown Zahl(string name, int wert, int min, int max) => new()
    {
        Name = name, Minimum = min, Maximum = max, Value = wert, Increment = 1, Width = 80, FormatString = "0",
    };

    private void RasterSichern()
    {
        var e = _spiel;
        e.Breite = SpielBaukasten.ClampBreite(e.Breite);
        e.Hoehe = SpielBaukasten.ClampHoehe(e.Hoehe);
        var n = e.Breite * e.Hoehe;
        if (e.Raster.Count == n) return;
        var alt = e.Raster;
        e.Raster = Enumerable.Range(0, n).Select(i => i < alt.Count && alt[i]).ToList();
    }

    private (int X, int Y)? Zelle(double px, double py, double w, double h)
    {
        var e = _spiel;
        if (w <= 0 || h <= 0 || e.Breite <= 0 || e.Hoehe <= 0) return null;
        var x = (int)(px / (w / e.Breite));
        var y = (int)(py / (h / e.Hoehe));
        if (x < 0 || y < 0 || x >= e.Breite || y >= e.Hoehe) return null;
        return (x, y);
    }

    private void ZeichneSpiel()
    {
        if (_spielLeinwand is not { } canvas || _spielInfo is not { } info) return;
        RasterSichern();
        var e = _spiel;
        var art = SpielBaukasten.Arten.FirstOrDefault(a => a.Id == e.Art).Label ?? e.Art;
        info.Text = $"{art} · Tempo {e.Tempo} · {e.Leben} Leben · Sieg bei {e.Sieg} Punkten" + (e.Ton ? " · mit Ton" : "");
        try { canvas.Background = Brush.Parse(SpielBaukasten.SicherFarbe(e.Hintergrund, "#10203f")); } catch (FormatException) { canvas.Background = Brush.Parse("#10203f"); }
        canvas.Children.Clear();
        IBrush farbe;
        try { farbe = Brush.Parse(SpielBaukasten.SicherFarbe(e.Figur, "#4FB3FF")); } catch (FormatException) { farbe = Brush.Parse("#4FB3FF"); }
        var cw = canvas.Width / e.Breite; var ch = canvas.Height / e.Hoehe;
        for (var y = 0; y < e.Hoehe; y++)
            for (var x = 0; x < e.Breite; x++)
            {
                if (!e.Raster[y * e.Breite + x]) continue;
                var rect = new Border { Width = Math.Max(2, cw - 2), Height = Math.Max(2, ch - 2), Background = farbe, CornerRadius = new CornerRadius(2) };
                Canvas.SetLeft(rect, x * cw + 1); Canvas.SetTop(rect, y * ch + 1);
                canvas.Children.Add(rect);
            }
        if (e.Art == "snake")
        {
            var kopf = new Border { Width = Math.Max(2, cw - 2), Height = Math.Max(2, ch - 2), Background = farbe };
            Canvas.SetLeft(kopf, 2 * cw); Canvas.SetTop(kopf, 2 * ch); canvas.Children.Add(kopf);
        }
    }

    private Control UnrealKarte()
    {
        var engines = EngineFinder();
        var ue = new StackPanel { Spacing = 6, Children = { H("Unreal Engine", 14) } };
        if (engines.Count == 0)
        {
            ue.Children.Add(P("Unreal ist auf diesem PC nicht installiert. Ein Projekt kannst du trotzdem anlegen. Der Editor startet nicht."));
            ue.Children.Add(Act("game-get-unreal", "Epic Launcher holen", () => Launch("https://store.epicgames.com/download")));
        }
        else ue.Children.Add(P($"Gefunden: Unreal {string.Join(", ", engines.Select(e => e.Version))}"));
        var uname = new TextBox { Name = "game-ue-name", Text = "MeinUnrealSpiel", Width = 200 };
        ue.Children.Add(new WrapPanel { Children = { uname, Act("game-ue-create", "Unreal-Projekt anlegen", () =>
        {
            try
            {
                var list = EngineFinder();
                var version = list.Count > 0 ? list[0].Version : "5.5";
                var dir = GameStudio.CreateUnrealProject(GameFolder, uname.Text ?? "Spiel", version);
                var up = Directory.GetFiles(dir, "*.uproject")[0];
                _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal_project", version);
                if (list.Count == 0) { Say($"Projekt „{Path.GetFileName(dir)}“ angelegt. Unreal ist nicht installiert, der Editor startet nicht.", "spiele"); return; }
                var exe = GameStudio.EditorExe(list[0]);
                if (File.Exists(exe)) _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, up);
                Say($"Projekt „{Path.GetFileName(dir)}“ für Unreal {version} angelegt." + (File.Exists(exe) ? " Der Editor startet." : " Der Editor wurde nicht gefunden und startet nicht."), "spiele");
            }
            catch (Exception ex) { Say("Hat nicht geklappt: " + ex.Message, "spiele"); }
        }), Act("game-ue-open", "Vorhandenes Projekt öffnen", () => _ = UnrealOeffnen()) } });
        return ue;
    }

    private async Task UnrealOeffnen()
    {
        string? path = null;
        try
        {
            if (PickOverride is { } fake)
            {
                var got = await fake(new PickRequest("game-ue-open", false, false, ["uproject"]));
                path = got?.FirstOrDefault();
            }
            else
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Unreal-Projekt",
                    FileTypeFilter = [new FilePickerFileType("Unreal-Projekt") { Patterns = ["*.uproject"] }, new FilePickerFileType("Alle Dateien") { Patterns = ["*"] }],
                });
                path = files.FirstOrDefault()?.TryGetLocalPath();
            }
        }
        catch (Exception ex) { Say("Die Dateiauswahl geht hier nicht: " + ex.Message, "spiele"); return; }
        if (string.IsNullOrWhiteSpace(path)) { Say("Bitte zuerst eine Unreal-Projektdatei wählen.", "spiele"); return; }
        var list = EngineFinder();
        if (list.Count == 0) { Say("Unreal ist auf diesem PC nicht installiert. Die Projektdatei wurde nicht geöffnet.", "spiele"); return; }
        var exe = GameStudio.EditorExe(list[0]);
        if (!File.Exists(exe)) { Say("Der Unreal-Editor wurde nicht gefunden. Die Projektdatei bleibt geschlossen.", "spiele"); return; }
        _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, path);
        Say("Unreal öffnet das Projekt.", "spiele");
    }
}
