using System.Text;
using Avalonia;
using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0735 (STUDIO-CITE-01): Quelle aus einer DOI füllen (öffentliche Crossref-Schnittstelle, ohne Konto, Herkunft an der Quelle),
/// RIS einlesen und RIS/CSL-JSON ausgeben. Der Abruf läuft über den vorhandenen Lese-Weg <see cref="ReadOnlyWeb"/>.</summary>
public partial class MainWindow
{
    /// <summary>Tests: ersetzt den Abruf bei Crossref. Standard ist der echte Lese-Weg.</summary>
    public Func<Uri, Task<(string Type, byte[] Bytes, Uri Final)>>? CiteFetchOverride { get; set; }
    string _ciDoi = "";

    /// <summary>Holt die Angaben zu einer DOI bei Crossref und legt die Quelle an – oder ergänzt die vorhandene Quelle mit derselben DOI.
    /// Ein Schritt im Rückgängig-Verlauf; die Herkunft (Adresse, Zeit, SHA-256) steht danach an der Quelle.</summary>
    public async Task<CiteKit.Source?> CiteFromDoiAsync(ProgramDocument d, string doi)
    {
        _ciDoi = doi ?? "";
        try
        {
            if (_core.Offline) throw new InvalidOperationException("Offline-Modus: Trendo geht nicht ins Internet. Die Angaben bitte von Hand eintragen.");
            var url = CiteKit.CrossrefUrl(_ciDoi);
            var (_, bytes, final) = await (CiteFetchOverride ?? (u => ReadOnlyWeb.GetAsync(u)))(url);
            var all = CiteSources(d);
            var same = all.FindIndex(s => s.Doi.Length > 0 && CiteKit.NormDoi(s.Doi) == CiteKit.NormDoi(_ciDoi));
            var got = CiteKit.FromCrossref(bytes, final, DateTimeOffset.Now, same >= 0 ? all[same].Id : NewId("q"));
            if (same >= 0)
            {
                var old = all[same]; // eigene Angaben (Adresse, Abrufdatum) bleiben, wo Crossref nichts sagt
                all[same] = got with { Url = old.Url.Length > 0 ? old.Url : got.Url, Accessed = old.Accessed };
            }
            else all.Add(got);
            CiSave(d, all, "Quelle aus DOI");
            _ciSel = got.Id; _ciDoi = "";
            _progMsg[CiteKey] = $"✓ Angaben aus Crossref übernommen: {CiteKit.Kinds.First(k => k.Id == got.Kind).Label} „{KreativSafe.Cut(got.Title, 60)}“. Herkunft steht an der Quelle.";
            _core.Audit.Append(Principal.LocalOwner.Name, "cite.doi", CiteKit.NormDoi(got.Doi));
            Show(CiteKey);
            return got;
        }
        catch (Exception ex)
        {
            _progMsg[CiteKey] = ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound } ? "Crossref kennt diese DOI nicht. Bitte prüfen."
                : ex is ArgumentException or InvalidDataException or InvalidOperationException ? ex.Message : "Abruf hat nicht geklappt: " + FriendlyError.Say(ex);
            Show(CiteKey);
            return null;
        }
    }

    /// <summary>Liest RIS (Zotero, Citavi, EndNote) aus einer Datei auf diesem Rechner und hängt die Quellen an.</summary>
    public int CiteImportRis(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, CiteKit.MaxRisChars)) throw new InvalidOperationException("Wähle eine RIS-Datei auf diesem Rechner (höchstens 5 MB).");
        var (got, warn) = CiteKit.ParseRis(File.ReadAllText(file));
        var all = CiteSources(d); all.AddRange(got.Select(s => s with { Id = NewId("q") }));
        CiSave(d, all, "RIS eingelesen");
        var dups = CiteKit.Duplicates(all).Count;
        _progMsg[CiteKey] = $"{got.Count} Quellen aus RIS eingelesen" + (warn.Count > 0 ? $", {warn.Count} Hinweis(e) ({warn[0]})" : "") + (dups > 0 ? $". ⚠ {dups}× doppelt – „Doppelte suchen“ zeigt sie." : ".");
        _ciSel = all.LastOrDefault()?.Id;
        return got.Count;
    }

    /// <summary>BibTeX oder RIS – nach Endung, sonst am Inhalt erkannt.</summary>
    public int CiteImportFile(ProgramDocument d, string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".ris") return CiteImportRis(d, file);
        if (ext is ".bib" or ".bibtex") return CiteImportBib(d, file);
        if (!KreativSafe.LocalFile(file, CiteKit.MaxBibChars)) throw new InvalidOperationException("Wähle eine Datei auf diesem Rechner (höchstens 5 MB).");
        using var r = new StreamReader(file);
        var head = new char[4096]; var n = r.Read(head, 0, head.Length);
        return System.Text.RegularExpressions.Regex.IsMatch(new string(head, 0, n), @"(?m)^TY  - ") ? CiteImportRis(d, file) : CiteImportBib(d, file);
    }

    async Task CiPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("cite-bib", "BibTeX- oder RIS-Datei", ["*.bib", "*.bibtex", "*.ris", "*.txt"]) is { } f) ProgramRun(CiteKey, () => CiteImportFile(d, f));
    }

    static string CiWriteText(string file, string text) { File.WriteAllText(file, text, new UTF8Encoding(false)); return file; }

    /// <summary>Feld „Quelle aus DOI“ in den Eigenschaften: DOI eingeben, Angaben holen.</summary>
    Control CiteDoiBox(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(H("Quelle aus DOI", 13));
        sp.Children.Add(P("DOI eingeben – Trendo holt Autor, Titel, Jahr und Zeitschrift bei Crossref (öffentlich, ohne Konto).", null, 11));
        var box = new TextBox { Name = "cite-p-doi", Text = _ciDoi, PlaceholderText = "z. B. 10.1038/nature14539" };
        var go = new Button { Name = "cite-doi-get", Content = "Angaben holen", Padding = new Thickness(10, 4), IsEnabled = !_core.Offline };
        async Task Run() { go.IsEnabled = false; await CiteFromDoiAsync(d, box.Text ?? ""); }
        go.Click += async (_, _) => await Run();
        box.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && go.IsEnabled) await Run(); };
        sp.Children.Add(box); sp.Children.Add(go);
        if (_core.Offline) sp.Children.Add(P("Offline-Modus: kein Abruf.", Muted, 11));
        return sp;
    }
}
