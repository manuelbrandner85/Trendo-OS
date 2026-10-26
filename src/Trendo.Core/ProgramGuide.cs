namespace Trendo.Core;

/// <summary>Ausführliche, leicht verständliche Beschreibung zu jedem Programm (Vorgabe 2026-10-26): Was ist das? Wofür? Wie funktioniert es?
/// Was kann es alles? Woher kommt es? Wie entferne ich es? Für die mitgelieferten Programme fest geschrieben, für Programme von GitHub
/// aus den Angaben des Projekts zusammengesetzt (<see cref="ForGitHub"/>).</summary>
public static class ProgramGuide
{
    public sealed record Guide(string Id, string Name, string WhatIs, string UseFor, IReadOnlyList<string> HowItWorks, IReadOnlyList<string> Features, string Origin, string Remove)
    {
        /// <summary>Alles als lesbarer Text (für Hilfe, Vorlesen und die KI).</summary>
        public string ToText() =>
            $"{Name}\n\nWas ist das?\n{WhatIs}\n\nWofür nutze ich es?\n{UseFor}\n\nSo funktioniert es:\n" +
            string.Join("\n", HowItWorks.Select((s, i) => $"{i + 1}. {s}")) +
            "\n\nDas kann es alles:\n" + string.Join("\n", Features.Select(f => "• " + f)) +
            $"\n\nHerkunft und Lizenz:\n{Origin}\n\nEntfernen:\n{Remove}";
    }

    const string RemoveIncluded = "In Trendo unter „Programme“ auf „Entfernen“ klicken. Deine Dateien bleiben erhalten. Über das Vollpaket lässt sich das Programm jederzeit wieder einrichten. Wenn du Trendo OS deinstallierst, wird es automatisch mit entfernt.";

    static Guide G(string id, string name, string what, string use, string[] how, string[] can, string origin) => new(id, name, what, use, how, can, origin, RemoveIncluded);

    /// <summary>Beschreibungen der mitgelieferten Programme, Schlüssel = Paketname im Vollpaket.</summary>
    public static readonly IReadOnlyDictionary<string, Guide> Included = new[]
    {
        G("blender", "Blender (3D Studio)", "Ein vollwertiges 3D-Programm, mit dem auch Filmstudios arbeiten.",
            "Du baust damit 3D-Figuren, Räume, Produkte und Animationen und renderst daraus fotorealistische Bilder oder Videos.",
            ["Öffne in Trendo das 3D Studio und wähle „Neues Modell“ oder eine Vorlage.", "Forme das Objekt mit Werkzeugen wie Verschieben, Extrudieren oder Modellieren.", "Gib ihm Materialien wie Holz, Metall oder Glas und setze Licht und Kamera.", "Starte „Rendern“. Trendo speichert das Bild oder Video in deinem Projekt."],
            ["Modellieren, Sculpting (digitales Ton-Formen) und Texturen malen", "Fotorealistisches Rendern mit Cycles und schnelles Rendern mit Eevee", "Animation, Skelette und Physik (Stoff, Rauch, Flüssigkeiten)", "Videoschnitt und Compositing", "Export für Spiele (FBX, glTF) und für den 3D-Druck (STL)"],
            "Blender Foundation, Open Source (GNU GPL). Offizielle Quelle: blender.org."),
        G("libreoffice", "LibreOffice (Kern für Trendo Text, Tabelle und Präsentation)", "Ein komplettes Büropaket. Trendo nutzt es im Hintergrund für Spezialformate, die eigenen Trendo-Programme machen den Alltag.",
            "Du öffnest und speicherst damit Word-, Excel- und PowerPoint-Dateien originalgetreu und wandelst Dokumente in PDF um.",
            ["Öffne ein Dokument in Trendo Text, Trendo Tabelle oder Trendo Präsentation.", "Bei Spezialformaten fragt Trendo LibreOffice im Hintergrund, du siehst es nicht.", "Mit „Im Original öffnen“ startest du das volle LibreOffice, wenn du es brauchst."],
            ["Writer (Texte), Calc (Tabellen), Impress (Präsentationen), Draw (Zeichnungen)", "Liest und schreibt DOCX, XLSX, PPTX, ODT und viele weitere Formate", "PDF-Export mit Lesezeichen", "Deutsche Rechtschreibprüfung"],
            "The Document Foundation, Open Source (MPL 2.0). Offizielle Quelle: libreoffice.org."),
        G("krita", "Krita (Malen und Zeichnen)", "Ein Malprogramm für digitale Kunst, Comics und Konzeptbilder.",
            "Du malst mit Maus oder Grafiktablett wie auf Papier, bearbeitest Bilder in Ebenen und zeichnest Comics.",
            ["Öffne „Bild & Grafik“ und wähle „Neue Zeichnung“.", "Such dir einen Pinsel aus (Bleistift, Aquarell, Öl …) und male drauflos.", "Lege für jeden Teil eine eigene Ebene an, dann kannst du ihn später ändern.", "Speichere als Bild (PNG, JPG) oder als Krita-Datei zum Weitermalen."],
            ["Über 100 Pinsel, Druckempfindlichkeit für Grafiktabletts", "Ebenen, Masken und Filter", "Comic-Seiten mit Panels und Sprechblasen", "Einfache Animation Bild für Bild", "Öffnet Photoshop-Dateien (PSD)"],
            "KDE / Krita Foundation, Open Source (GNU GPL). Offizielle Quelle: krita.org."),
        G("inkscape", "Inkscape (Vektorgrafik)", "Ein Zeichenprogramm für Grafiken, die man beliebig groß machen kann, ohne dass sie unscharf werden.",
            "Du gestaltest Logos, Plakate, Flyer, Symbole und Schnittdateien für Plotter und Laser.",
            ["Öffne „Bild & Grafik“ und wähle „Neue Vektorgrafik“.", "Zeichne Formen wie Rechteck, Kreis und Stern oder freie Linien.", "Gib ihnen Farben, Verläufe und Text.", "Speichere als SVG oder exportiere als PNG oder PDF."],
            ["Formen, Pfade und Bezierkurven", "Text auf Pfaden und Schriftgestaltung", "Bilder nachzeichnen (aus Foto wird Vektor)", "Ausrichten, Verteilen und Raster", "Export als SVG, PDF, PNG und EPS"],
            "Inkscape-Projekt, Open Source (GNU GPL). Offizielle Quelle: inkscape.org."),
        G("kdenlive", "Kdenlive (Videoschnitt)", "Ein Videoschnittprogramm mit Zeitleiste, wie man es von Profi-Programmen kennt.",
            "Du schneidest Urlaubsvideos, YouTube-Clips oder Präsentationsfilme mit Musik, Titeln und Übergängen.",
            ["Öffne „Videos & Fotos“ und lege ein Videoprojekt an.", "Zieh deine Clips in die Zeitleiste und schneide sie mit der Schere zurecht.", "Füge Übergänge, Titel und Musik hinzu.", "Klick auf „Exportieren“. Trendo erstellt eine MP4-Datei."],
            ["Mehrere Video- und Tonspuren", "Über 100 Effekte und Übergänge", "Titel, Untertitel und Farbkorrektur", "Proxy-Schnitt für große 4K-Dateien", "Export in alle gängigen Formate"],
            "KDE-Projekt, Open Source (GNU GPL). Offizielle Quelle: kdenlive.org."),
        G("audacity", "Audacity (Audio bearbeiten)", "Ein Tonstudio für Aufnahmen und Bearbeitung von Sprache und Musik.",
            "Du nimmst Podcasts oder Sprachnachrichten auf, schneidest Musik, entfernst Rauschen und machst Klingeltöne.",
            ["Öffne „Musik & Audio“ und wähle „Aufnehmen“ oder eine Datei.", "Markiere einen Bereich mit der Maus und schneide, kopiere oder lösche ihn.", "Wende Effekte wie „Rauschen entfernen“ oder „Lautstärke angleichen“ an.", "Exportiere als MP3, WAV oder OGG."],
            ["Mehrspur-Aufnahme", "Rauschentfernung, Equalizer, Kompressor und Hall", "Tempo und Tonhöhe getrennt ändern", "Spektralansicht für feine Korrekturen", "Viele Formate über FFmpeg"],
            "Audacity-Team / Muse Group, Open Source (GNU GPL). Offizielle Quelle: audacityteam.org."),
        G("obs", "OBS Studio (Aufnahme und Streaming)", "Ein Programm, das deinen Bildschirm, deine Kamera und dein Mikrofon aufnimmt oder live überträgt.",
            "Du nimmst Anleitungen, Spiele oder Vorträge auf oder streamst live auf YouTube oder Twitch.",
            ["Öffne „Aufnahme & Stream“.", "Wähle, was zu sehen sein soll: Bildschirm, Fenster, Kamera oder Bild.", "Prüfe den Ton (Mikrofon, PC-Ton) in der Pegelanzeige.", "Klick auf „Aufnahme starten“ oder auf „Live gehen“."],
            ["Szenen mit beliebig vielen Quellen", "Aufnahme in hoher Qualität mit Grafikkarten-Beschleunigung", "Live-Streaming auf alle großen Plattformen", "Filter wie Greenscreen, Rauschunterdrückung und Farbkorrektur", "Virtuelle Kamera für Videokonferenzen"],
            "OBS Project, Open Source (GNU GPL). Offizielle Quelle: obsproject.com."),
        G("ffmpeg", "FFmpeg (Video- und Audio-Kern)", "Der unsichtbare Motor für Video und Ton. Fast jedes Medienprogramm der Welt nutzt ihn.",
            "Trendo nutzt ihn, um Videos und Musik umzuwandeln, zu verkleinern, zu schneiden und abzuspielen. Du bedienst ihn über die Trendo-Oberfläche.",
            ["Wähle in Trendo eine Datei und die Aktion, z. B. „In MP4 umwandeln“ oder „Verkleinern“.", "Trendo gibt FFmpeg den passenden Befehl.", "Das Ergebnis landet neben der Originaldatei, das Original bleibt erhalten."],
            ["Wandelt praktisch jedes Video- und Audioformat um", "Schneiden, Zusammenfügen und Verkleinern", "Ton aus Videos herauslösen", "Untertitel einbrennen", "Grafikkarten-Beschleunigung"],
            "FFmpeg-Projekt, Open Source (LGPL/GPL). Offizielle Quelle: ffmpeg.org."),
        G("mpv", "mpv (Medienspieler)", "Ein schlanker, sehr schneller Spieler für Videos und Musik.",
            "Du spielst damit jedes Video und jede Musikdatei ab, auch 4K und HDR, ohne Zusatz-Codecs.",
            ["Doppelklick auf ein Video in Trendo öffnet den Trendo Player.", "Leertaste = Pause, Pfeiltasten = vor und zurück, F = Vollbild.", "Untertitel und Tonspur wählst du im Menü des Players."],
            ["Spielt fast alle Formate", "Flüssige Wiedergabe dank Grafikkarte", "Untertitel, mehrere Tonspuren und Kapitel", "Geschwindigkeit ändern", "Bildschirmfoto aus dem Video"],
            "mpv-Projekt, Open Source (GPL/LGPL). Offizielle Quelle: mpv.io."),
        G("sumatra", "SumatraPDF (Lesen)", "Ein sehr schneller Betrachter für PDF, E-Books und Comics.",
            "Trendo PDF nutzt ihn als Rückfallebene. Du liest damit auch E-Books (EPUB, MOBI) und Comics (CBZ).",
            ["Öffne die Datei in Trendo PDF.", "Für Formate, die Trendo noch nicht selbst anzeigt, startet Trendo SumatraPDF."],
            ["PDF, EPUB, MOBI, XPS, DjVu, CBZ und CBR", "Startet in Sekundenbruchteilen", "Suche, Lesezeichen und Vollbild"],
            "Krzysztof Kowalczyk, Open Source (GNU GPL). Offizielle Quelle: sumatrapdfreader.org."),
        G("qpdf", "qpdf (PDF-Werkzeugkern)", "Ein Werkzeug, das PDF-Dateien neu ordnet, ohne ihren Inhalt zu verändern.",
            "Trendo PDF nutzt es für Zusammenfügen, Teilen, Drehen, Verschlüsseln und Reparieren.",
            ["Wähle in Trendo PDF die Aktion, z. B. „Zusammenfügen“.", "Trendo erledigt den Rest mit qpdf und legt eine neue Datei an."],
            ["Seiten zusammenfügen, teilen und drehen", "Passwortschutz setzen und entfernen (mit Passwort)", "Beschädigte PDFs reparieren", "Für das Web optimieren"],
            "qpdf-Projekt, Open Source (Apache 2.0). Offizielle Quelle: qpdf.sourceforge.io."),
        G("7zip", "7-Zip (Archive)", "Packt Dateien in Archive und entpackt sie wieder.",
            "Du verkleinerst Ordner zum Versenden, öffnest ZIP-, RAR- und 7z-Dateien und schützt Archive mit Passwort.",
            ["Rechtsklick auf Dateien in Trendo, dann „Packen“ oder „Entpacken“.", "Wähle bei Bedarf ein Passwort.", "Das Archiv entsteht im selben Ordner."],
            ["7z, ZIP, TAR, GZ packen", "Entpackt RAR, ISO, CAB und viele weitere", "Starke Verschlüsselung (AES-256)", "Sehr hohe Packrate"],
            "Igor Pavlov, Open Source (GNU LGPL, RAR-Teil mit Sonderlizenz). Offizielle Quelle: 7-zip.org."),
        G("tesseract", "Tesseract (Texterkennung)", "Erkennt Text in Bildern und gescannten Seiten, auch auf Deutsch.",
            "Aus Fotos von Briefen, Rechnungen oder Buchseiten wird durchsuchbarer, kopierbarer Text.",
            ["Wähle in Trendo ein Bild oder einen Scan und „Text erkennen“.", "Trendo zeigt den erkannten Text zum Prüfen und Kopieren.", "Für PDFs: „Durchsuchbar machen“ legt den Text unsichtbar über die Seite."],
            ["Über 100 Sprachen, Deutsch ist mitgeliefert", "Ganze Seiten, Spalten und Tabellenbereiche", "Durchsuchbare PDFs"],
            "Tesseract-Projekt (ursprünglich Google), Open Source (Apache 2.0). Offizielle Quelle: github.com/tesseract-ocr."),
        G("colmap", "COLMAP (Fotos zu 3D)", "Baut aus vielen Fotos eines Gegenstands ein 3D-Modell.",
            "Du fotografierst ein Objekt rundherum, und Trendo macht daraus ein 3D-Modell für Blender oder den 3D-Druck.",
            ["Mach 30 bis 100 Fotos rund um das Objekt, gut beleuchtet und ohne Bewegung.", "Wähle in Trendo „Scan zu 3D“ und den Fotoordner.", "Trendo berechnet die Kamerapositionen und die Punktwolke. Das dauert je nach PC Minuten.", "Öffne das Ergebnis im 3D Studio."],
            ["Kamerapositionen aus Fotos berechnen", "Dichte Punktwolke und Oberflächenmodell", "Export für Blender"],
            "ETH Zürich / UNC, Open Source (BSD). Offizielle Quelle: colmap.github.io."),
        G("git", "Git (Versionen)", "Speichert jeden Stand deiner Dateien, sodass du jederzeit zurückkannst.",
            "Trendo nutzt es für Code-Projekte, GitHub und das Sichern von Projektständen.",
            ["Öffne „GitHub & Code“ und wähle ein Projekt.", "„Stand sichern“ merkt sich alle Änderungen mit einer kurzen Notiz.", "„Zurück zu …“ holt einen früheren Stand wieder."],
            ["Versionen und Verlauf", "Zweige zum Ausprobieren", "Abgleich mit GitHub"],
            "Git-Projekt, Open Source (GNU GPL). Offizielle Quelle: git-scm.com."),
        G("python", "Python (Skripte)", "Eine einfache Programmiersprache. Viele KI- und Datenwerkzeuge brauchen sie.",
            "Trendo nutzt Python für Automatisierungen und KI-Werkzeuge. Du kannst eigene kleine Skripte ausführen.",
            ["Öffne „GitHub & Code“ und lege eine .py-Datei an.", "Schreib dein Skript oder lass es dir von der Trendo KI erstellen.", "Klick auf „Ausführen“. Die Ausgabe erscheint darunter."],
            ["Komplette Python-Laufzeit, eigene Kopie ohne Systemänderung", "Grundlage für KI- und Datenwerkzeuge"],
            "Python Software Foundation, Open Source (PSF-Lizenz). Offizielle Quelle: python.org."),
        G("vscode", "VSCodium (Code-Editor)", "Ein vollwertiger Programmier-Editor, die freie Fassung von Visual Studio Code.",
            "Du schreibst und bearbeitest Code in jeder Sprache, mit Farben, Vorschlägen und Fehlersuche.",
            ["Öffne „GitHub & Code“ und wähle „Im Editor öffnen“.", "Bearbeite die Dateien; Fehler werden rot markiert.", "Speichern genügt, Trendo sichert den Stand auf Wunsch mit Git."],
            ["Unterstützung für Hunderte Sprachen", "Suchen und Ersetzen im ganzen Projekt", "Git eingebaut", "Erweiterungen aus Open VSX"],
            "VSCodium-Projekt, Open Source (MIT). Offizielle Quelle: vscodium.com."),
        G("scrcpy", "scrcpy und adb (Handy am PC)", "Zeigt und steuert dein Android-Handy auf dem PC-Bildschirm.",
            "Du bedienst dein Handy mit Maus und Tastatur, überträgst Dateien und nimmst den Handybildschirm auf.",
            ["Schalte am Handy „USB-Debugging“ ein. Trendo zeigt dir, wie das geht.", "Verbinde das Handy per USB oder WLAN.", "Klick in Trendo auf „Handy anzeigen“."],
            ["Bildschirm spiegeln und steuern, ohne App auf dem Handy", "Dateien hin und her schieben", "Bildschirm aufnehmen", "Ton vom Handy am PC"],
            "Genymobile / Android-Projekt, Open Source (Apache 2.0). Offizielle Quelle: github.com/Genymobile/scrcpy."),
        G("ollama", "Ollama (Laufzeit für KI-Modelle)", "Führt KI-Sprachmodelle direkt auf deinem PC aus, ohne Internet und ohne Konto.",
            "Die Trendo KI nutzt es, damit deine Fragen und Dateien auf deinem PC bleiben.",
            ["Trendo startet Ollama automatisch im Hintergrund.", "Unter „KI-Modelle“ wählst du, welche Modelle geladen werden.", "Danach antwortet die Trendo KI mit dem gewählten Modell."],
            ["Viele freie Modelle (Qwen, Llama, Mistral, Gemma …)", "Nutzt die Grafikkarte, wenn vorhanden", "Komplett offline"],
            "Ollama, Open Source (MIT). Offizielle Quelle: ollama.com."),
        G("model", "Kleines KI-Modell (Qwen 2.5, 0,5 B)", "Ein kleines Sprachmodell, das Trendo sofort ohne Download nutzen kann.",
            "Es beantwortet einfache Fragen, fasst kurze Texte zusammen und hilft bei Formulierungen, auch auf schwachen PCs.",
            ["Funktioniert direkt nach der Einrichtung.", "Größere Modelle lädt Trendo automatisch nach, wenn dein PC genug Speicher hat."],
            ["Schnell und sparsam", "Deutsch und Englisch", "Komplett offline"],
            "Alibaba Qwen-Team, Apache 2.0. Offizielle Quelle: ollama.com/library/qwen2.5."),
    }.ToDictionary(g => g.Id);

    /// <summary>Beschreibung für ein Programm von GitHub aus den Angaben des Projekts. Ohne eigene Erfindungen: Unbekanntes wird so benannt.</summary>
    public static Guide ForGitHub(string repo, string? description, string licenseId, string? language, int stars, string? version, string? assetName, long? bytes, string? readmeIntro = null)
    {
        var what = string.IsNullOrWhiteSpace(description) ? "Das Projekt beschreibt sich auf GitHub nicht selbst. Schau auf der Projektseite nach, was es tut." : description.Trim();
        var use = string.IsNullOrWhiteSpace(readmeIntro) ? $"So beschreibt es das Projekt: {what}" : readmeIntro.Trim();
        var size = bytes is > 0 ? $", Download {(bytes.Value >= 1L << 20 ? $"{bytes.Value / (double)(1L << 20):0.#} MB" : $"{Math.Max(1, bytes.Value / 1024)} KB")}" : "";
        string[] how = assetName is null
            ? ["Für dieses Projekt gibt es kein fertiges Windows-Programm zum Installieren.", "Du kannst den Quellcode mit „Herunterladen“ in deinen Ordner „Open Source“ holen."]
            : [$"Klick auf „Installieren“. Trendo lädt „{assetName}“ von der offiziellen GitHub-Seite des Projekts.", "Trendo prüft die Datei (mit Prüfsumme, wenn GitHub eine angibt) und richtet sie in einem eigenen Ordner ein, ohne Admin und ohne Systemänderung.", "Danach findest du das Programm unter „Programme“ und im Startmenü und startest es mit einem Klick."];
        var can = new List<string> { $"Beliebtheit: {stars:N0} Sterne auf GitHub" };
        if (!string.IsNullOrWhiteSpace(language)) can.Add("Programmiersprache: " + language);
        if (!string.IsNullOrWhiteSpace(version)) can.Add("Neueste Version: " + version);
        can.Add("Alle Funktionen stehen auf der Projektseite. Trendo zeigt nur, was das Projekt selbst angibt.");
        return new Guide(Trendo.Core.GitHubApps.IdFor(repo), repo, what, use, how, can,
            $"GitHub-Projekt {repo}{size}. {Trendo.Core.OpenSource.Explain(licenseId)}",
            "In Trendo unter „Programme“ auf „Entfernen“ klicken. Trendo löscht genau den Ordner dieses Programms. Bei der Deinstallation von Trendo OS wird es automatisch mit entfernt.");
    }
}
