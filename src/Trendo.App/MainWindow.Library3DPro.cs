using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>Freie 3D-Modelle aus weiteren Quellen (Freigabe 05.11.2026, Q1–Q4): Thingiverse, Smithsonian, NASA, Sketchfab mit eigenem Schlüssel,
/// Seiten ohne Schnittstelle über den Browser und den Download-Ordner, Lizenz-Filter, Übernahme ins 3D-Studio oder in den 3D-Druck.</summary>
public partial class MainWindow
{
    public const string BibOtherSites = "Andere Seiten";
    public string BibSource { get; set; } = AssetLibrary.PolyHaven;
    public bool BibCommercialOnly { get; set; }
    /// <summary>Für Tests: Download-Ordner.</summary>
    public string? DownloadsOverride { get; set; }
    DateTime _bibPickupSince = DateTime.Now.AddHours(-1);
    string _bibPickupSite = DownloadPickup.Sites[0].Name;
    string BibDownloads => DownloadsOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    FreeModels BibFree => new(new HttpFetch(BibHttpOverride is { } h ? MakeBibHttp(h) : BibShared), Path.Combine(_core.DataRoot, "bibliothek3d"));

    string? BibKey(string source) => FreeModels.Sources.FirstOrDefault(s => s.Name == source)?.KeyName is { } k ? _core.Secrets.Get(k) : null;
    /// <summary>Schlüssel des Nutzers für eine Quelle speichern (nur im Geheimnisspeicher).</summary>
    public void BibSetKey(string source, string key)
    {
        if (FreeModels.Sources.FirstOrDefault(s => s.Name == source)?.KeyName is not { } k) return;
        if (string.IsNullOrWhiteSpace(key)) { _core.Secrets.Delete(k); return; }
        _core.Secrets.Set(k, key.Trim());
        _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek3d.schluessel", source);
    }

    static bool BibTo3D(string file) => Studio3D.Imports.Contains(Path.GetExtension(file).ToLowerInvariant());
    static bool BibToPrint(string file) => Path.GetExtension(file).ToLowerInvariant() is ".stl" or ".3mf" or ".obj" or ".step" or ".stp";
    /// <summary>Q4: Modell ins 3D-Studio übernehmen (Maßeinheit erkennt der Import selbst: mm oder cm).</summary>
    public void BibUseIn3D(LibraryItem item) { Show("3d"); Import3DFile(item.File); _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek3d.ins3d", item.Name); }
    /// <summary>Q4: Modell in den 3D-Druck legen (danach dort vorbereiten und drucken).</summary>
    public void BibUseInPrint(LibraryItem item) { Open3dPrint(item.File); _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek3d.indruck", item.Name); }

    /// <summary>Zeile mit den Quellen für Modelle; dazu bei Bedarf das Feld für den eigenen Schlüssel.</summary>
    private Control BibSourceBar(Func<string, string, Action, bool, Button> btn)
    {
        var sp = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 6) };
        var row = new WrapPanel { Name = "bib-sources" };
        foreach (var name in new[] { AssetLibrary.PolyHaven }.Concat(FreeModels.Sources.Select(s => s.Name)).Append(BibOtherSites))
        {
            var n = name; var b = btn("bib-src-" + Scene3D.Slug(name), name, () => { BibSource = n; BibHits = []; _bibMsg = ""; Show(BIB); }, false);
            if (BibSource == name) { b.Background = Brush.Parse("#C9A227"); b.Foreground = Brushes.White; }
            ToolTip.SetTip(b, FreeModels.Sources.FirstOrDefault(s => s.Name == name)?.Hint ?? (name == BibOtherSites ? "Printables, MakerWorld, Cults3D, MyMiniFactory, Thangs: im Browser laden, Trendo übernimmt die Datei" : "Fotorealistische Modelle, frei nutzbar (CC0)"));
            row.Children.Add(b);
        }
        sp.Children.Add(row);
        if (FreeModels.Sources.FirstOrDefault(s => s.Name == BibSource) is { KeyName: not null } src)
        {
            var have = BibKey(src.Name) is not null;
            var key = new TextBox { Name = "bib-key", PasswordChar = '•', PlaceholderText = have ? "Schlüssel gespeichert – zum Ändern neu eingeben" : "Dein kostenloser Schlüssel", Width = 260 };
            var save = btn("bib-key-save", "Speichern", () => { BibSetKey(src.Name, key.Text ?? ""); _bibMsg = string.IsNullOrWhiteSpace(key.Text) ? "Schlüssel entfernt." : "Schlüssel geschützt gespeichert."; Show(BIB); }, false);
            var get = btn("bib-key-get", "Schlüssel holen", () => Launch(src.KeyHelpUrl!), false);
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = $"{src.Name}-Schlüssel", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 4) }, key, new Border { Width = 4 }, save, get,
                new TextBlock { Text = have ? "eingerichtet" : src.Name == FreeModels.Sketchfab ? "Suchen geht ohne – Laden braucht ihn" : "nötig zum Suchen und Laden", Foreground = have ? Brush.Parse("#4ADE80") : Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4) } } });
        }
        return sp;
    }

    /// <summary>Q2: Seiten ohne Schnittstelle – im Browser suchen und selbst laden, Trendo übernimmt die Datei aus dem Download-Ordner.</summary>
    private Control BibPickupView(Func<string, string, Action, bool, Button> btn)
    {
        var sp = new StackPanel { Name = "bib-pickup", Spacing = 8 };
        sp.Children.Add(H("Andere Seiten – selbst laden, Trendo übernimmt", 14));
        sp.Children.Add(P("Diese Seiten haben keine offene Schnittstelle. Trendo öffnet die Suche im Browser, du lädst ein kostenloses Modell selbst herunter (mit deiner Anmeldung, falls nötig) – danach holt Trendo die neue Datei aus deinem Download-Ordner. Kein Abgreifen der Seite, kein Passwort in Trendo.", null, 12));
        var sites = new WrapPanel { Name = "bib-sites" };
        foreach (var (s, i) in DownloadPickup.Sites.Where(s => s.Kind == "3D-Druck").Select((s, i) => (s, i)))
        {
            var ss = s;
            sites.Children.Add(btn($"bib-site-{i}", s.Name + " öffnen", () => { _bibPickupSince = DateTime.Now; _bibPickupSite = ss.Name; Launch(DownloadPickup.SearchUrl(ss, BibQuery)); _bibMsg = $"{ss.Name} geöffnet – Modell laden, dann hier „Neue Downloads“."; Show(BIB); }, false));
        }
        sp.Children.Add(sites);
        var files = DownloadPickup.NewFiles(BibDownloads, _bibPickupSince, [".stl", ".3mf", ".obj", ".glb", ".gltf", ".zip", ".step", ".stp"]);
        sp.Children.Add(H($"Neue Downloads ({files.Count})", 13));
        if (files.Count == 0) sp.Children.Add(P("Noch nichts Neues im Download-Ordner seit dem Öffnen der Seite.", Muted, 12));
        var licenses = new[] { "CC0 1.0", "CC BY 4.0", "CC BY-SA 4.0", "CC BY-NC 4.0", "Standard-Lizenz der Seite (nur privat)" };
        foreach (var (f, i) in files.Select((f, i) => (f, i)))
        {
            var ff = f;
            var lic = new ComboBox { Name = $"bib-pick-lic-{i}", ItemsSource = licenses, SelectedIndex = 1, Width = 220 };
            var author = new TextBox { Name = $"bib-pick-author-{i}", PlaceholderText = "Urheber (von der Seite)", Width = 180 };
            var take = btn($"bib-pick-{i}", "Übernehmen", () =>
            {
                try
                {
                    var item = BibFree.Pickup(Bib, ff, _bibPickupSite, licenses[Math.Max(0, lic.SelectedIndex)], (author.Text ?? "").Trim(), DownloadPickup.Sites.First(s => s.Name == _bibPickupSite).Url);
                    _core.Audit.Append(Principal.LocalOwner.Name, "bibliothek3d.download", $"{_bibPickupSite}/{Path.GetFileName(ff)} ({item.License})");
                    _bibMsg = $"„{item.Name}“ liegt in deiner Bibliothek ({item.License}).";
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { _bibMsg = "" + ex.Message; }
                Show(BIB);
            }, true);
            sp.Children.Add(new Border { Background = Brush.Parse("#2C2C2C"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = Path.GetFileName(f), Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 },
                new WrapPanel { Children = { lic, new Border { Width = 4 }, author, new Border { Width = 4 }, take } } } } });
        }
        sp.Children.Add(btn("bib-pick-refresh", "Neue Downloads prüfen", () => Show(BIB), false));
        return sp;
    }
}
