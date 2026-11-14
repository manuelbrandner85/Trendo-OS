using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Trendo.App;

/// <summary>Modernisierung G3 (freigegeben 07.11.2026): Vorschlags-Chips unter der KI-Zeile jedes Studios – ein Klick setzt einen typischen Auftrag ein
/// (nichts startet von selbst, „Los“ bleibt beim Nutzer). Beispiele sind bewusst alltagsnah und ohne echte Namen.</summary>
public partial class MainWindow
{
    public static readonly IReadOnlyDictionary<string, string[]> PromptChips = new Dictionary<string, string[]>
    {
        ["STUDIO-LETTER-01"] = ["Kündigung meines Fitnessstudios", "Widerspruch gegen einen Bescheid", "Mietminderung wegen Heizungsausfall"],
        ["SYS-10"] = ["Haushaltsplan als Tabelle", "Präsentation über unser Projekt, 8 Folien", "Lebenslauf modern gestalten"],
        ["SYS-09"] = ["Antwort auf die letzte E-Mail entwerfen", "Termin nächste Woche Dienstag 10 Uhr", "Ungelesene Mails zusammenfassen"],
        ["SPEC-11"] = ["Monatsausgaben nach Kategorie", "Rechnung für Erika Muster schreiben", "Sparplan für 5.000 €"],
        ["STUDIO-ONLINE-BUSINESS-01"] = ["Produktbeschreibung für handgemachte Kerzen", "Preise kalkulieren", "Startplan für einen Online-Shop"],
        ["CORE-08"] = ["Urlaubsvideo mit Musik schneiden", "Foto aufhellen und zuschneiden", "Untertitel erzeugen"],
        ["STUDIO-MUSIK-01"] = ["Sommerlied über einen Roadtrip", "Ruhige Klavierballade, instrumental", "Rap über Montagmorgen"],
        ["STUDIO-FILM-01"] = ["Drehbuch für einen Kurzfilm über einen Neuanfang", "Trailer von einer Minute", "Dialog spannender machen"],
        ["STUDIO-POD-01"] = ["Motivideen für Wanderfreunde", "Verkaufstext für dieses T-Shirt", "Sinnvollen Preis ausrechnen"],
        ["STUDIO-CONSOLE-01"] = ["Erkläre mir den nächsten Schritt", "Was fehlt auf diesem PC?", "Bring es auf mein Steam Deck"],
        ["STUDIO-SUMMARY-01"] = ["Fasse diesen Text in fünf Punkten zusammen", "Erkläre ihn in einfacher Sprache", "Welche Termine stehen drin?"],
        ["STUDIO-LOGO-01"] = ["Logo für mein Café", "Ruhigere Farben vorschlagen", "Slogan dazu finden"],
        ["STUDIO-PODCAST-01"] = ["Folge über Balkongärten planen", "Shownotes schreiben", "Interviewfragen vorschlagen"],
        ["STUDIO-COMIC-01"] = ["Vier-Bilder-Comic über einen Kater", "Storyboard für einen Werbeclip", "Pointe für das letzte Bild"],
        ["STUDIO-BOOK-01"] = ["Gliedere einen Krimi in zwölf Kapitel", "Schreib den Anfang von Kapitel 1", "Lektorat für dieses Kapitel"],
        ["STUDIO-ANIM-01"] = ["Titel soll von links einfliegen", "Logo sanft einblenden", "Ein Ball springt dreimal"],
        ["STUDIO-PHOTOS-01"] = ["Urlaubsfotos nach Monat sortieren", "Doppelte Fotos finden", "Nach Datum umbenennen"],
        ["STUDIO-IMAGEAI-01"] = ["Titelbild für meinen Blog", "Leuchtturm als Aquarell", "Vier Varianten für ein Poster"],
        ["STUDIO-VIDEOAI-01"] = ["Video über einen Morgen am Meer", "Fünf Szenen für eine Produktvorstellung", "Kürze das Video auf 20 Sekunden"],
        ["STUDIO-AGENTS-01"] = ["Helfer, der Briefe auf Fehler prüft", "Anweisungen verbessern", "Welche Werkzeuge braucht er?"],
        ["STUDIO-MODELS-01"] = ["Zehn Testfragen für den Alltag", "Woran erkenne ich eine gute Antwort?", "Erkläre die Ergebnistabelle"],
        ["STUDIO-TRAIN-01"] = ["Beispiele für E-Mail-Kategorien", "Mehr Beispiele für eine Kategorie", "Was bedeutet Trefferquote?"],
        ["SPEC-15"] = ["Stream-Szene mit Kamera und Bildschirm", "Ablauf für eine 30-Minuten-Sendung"],
        ["SPEC-01"] = ["Wohnzimmer 4 × 5 m einrichten", "Wandfläche zum Streichen berechnen"],
        ["SPEC-02"] = ["Vorwiderstand für eine LED an 12 V", "Spannungsteiler 5 V → 3,3 V"],
        ["SPEC-13"] = ["Radtour 40 km mit Höhenprofil", "GPX-Datei ansehen"],
        ["SPEC-06"] = ["Vokabeln Englisch Kapitel 3", "Prüfungsfragen zur Ersten Hilfe"],
        ["CORE-02"] = ["Erkläre mir Zinseszins", "Plane meine Woche", "Fasse diesen Text zusammen"],
        ["SYS-05"] = ["Jeden Morgen Wetter und Termine zeigen", "Downloads-Ordner wöchentlich aufräumen"],
        ["CORE-07"] = ["Webseite für ein Café", "Kontaktformular einbauen"],
        ["X-03"] = ["App für Einkaufslisten", "Vereins-App mit Terminen"],
        ["SYS-08"] = ["Recherche: Wärmepumpe Kosten 2026", "Quellen zu gesunder Ernährung"],
        ["STUDIO-CODEAI-01"] = ["Erkläre mir diese Datei", "Schreib einen Test dafür", "Warum schlägt der Test fehl?"],
        ["STUDIO-VISION-01"] = ["Beschreibe dieses Bild", "Finde unscharfe Fotos", "Lies den Text im Bild"],
        ["STUDIO-CITE-01"] = ["Zitiere nach APA", "Belegt die Quelle die Aussage?", "Welche Angaben fehlen?"],
        ["STUDIO-DAYPLAN-01"] = ["Plane meinen Tag", "Was schaffe ich bis Mittag?", "Rest auf morgen schieben"],
    };

    /// <summary>Chips für ein Studio (leer = keine Vorschläge hinterlegt).</summary>
    Control PromptChipRow(string studio, TextBox box)
    {
        var row = new WrapPanel { Name = "auftrag-chips", Margin = new Thickness(30, 0, 0, 0) };
        if (!PromptChips.TryGetValue(studio, out var chips)) { row.IsVisible = false; return row; }
        var i = 0;
        foreach (var c in chips)
        {
            var b = new Button { Name = $"auftrag-chip-{i++}", Content = c, FontSize = 12, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(12),
                Background = Brush.Parse("#1AFFFFFF"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Foreground = Text };
            ToolTip.SetTip(b, "Als Auftrag einsetzen – mit „Los“ startest du ihn");
            var text = c; b.Click += (_, _) => { box.Text = text; box.Focus(); box.CaretIndex = text.Length; };
            row.Children.Add(b);
        }
        // Chips nur, solange nichts getippt ist
        void Sync() => row.IsVisible = string.IsNullOrWhiteSpace(box.Text);
        box.TextChanged += (_, _) => Sync(); Sync();
        return row;
    }
}
