using System.Text;

namespace Trendo.Core.PromptAction;

public sealed record StudioProfile(
    string Id,
    string Title,
    string Area,
    string Existing,
    string PlannedAction,
    string Inputs,
    string Outputs,
    string Blocker);

/// <summary>Alle Einträge aus StudioCatalog. Reife kommt nur aus registrierten Adaptern, nie aus der Wunschliste.</summary>
public static class CapabilityCatalog
{
    private static readonly StudioProfile[] All =
    [
        P("STUDIO-LETTER-01", "Briefe schreiben", "Arbeiten & Büro", "Letters, LetterArchive, LetterCase, PdfExport", "Brief als DOCX entwerfen", "Absender, Empfänger, Betreff, Text", "DOCX, optional PDF", "Über den KI-Auftrag entsteht das PDF nur, wenn LibreOffice (soffice) vorhanden ist. Im Programm „Briefe“ schreibt Trendo das PDF selbst, ohne LibreOffice."),
        P("SYS-10", "Texte & Tabellen", "Arbeiten & Büro", "Ooxml, Csv, Pdf, Writing", "Dokument oder Tabelle mit geprüfter Formel", "Text oder Zellen", "DOCX, XLSX", "Über den KI-Auftrag nur Grundrechenarten und SUMME; Trendo liest das Ergebnis selbst zurück, kein fremdes Programm rechnet nach. Text, Tabelle, Präsentation und PDF arbeiten ohne LibreOffice."),
        P("SYS-09", "E-Mails & Termine", "Arbeiten & Büro", "MailCenter", "Entwurf, Versand nur mit Freigabe", "Empfänger, Betreff, Text", "EML-Entwurf", "Über den KI-Auftrag entsteht nur der Entwurf; von dort wird nichts verschickt. Im Programm „E-Mail“ sendest du selbst über dein eingerichtetes Postfach (SMTP)."),
        P("X-11", "Neues Projekt starten", "Arbeiten & Büro", "ProjectStore", "Projekt anlegen", "Projekt-ID", "Projektordner, Text", "Es entsteht ein Projektordner. Es öffnet sich kein Fenster."),
        P("SYS-02", "Kleine Helfer & Musik", "Arbeiten & Büro", "Calc, NoteStore, FileShelf", "Notiz oder Rechnung", "Text oder Ausdruck", "Notiz, Zahl", "Musik abspielen und Dateien verschieben sind nicht angeschlossen."),
        P("STUDIO-DECKDESIGN-01", "Präsentationen gestalten", "Arbeiten & Büro", "DeckDesign, SlideDoc, SlideFiles, SlidePdf", "Präsentation gestalten", "Gliederung, Design, Redezeit", "Präsentation (PPTX), Folienplan (TXT); im Studio auch PDF", "Gestaltet nur nach Regeln und Vorlagen; Bilder musst du selbst einfügen. Die KI erfindet keine Zahlen."),
        P("STUDIO-DOCSCAN-01", "Dokumente scannen", "Arbeiten & Büro", "DocScan, VisionPlus.Homography, PdfTools, Ocr", "Scan-Auftrag anlegen", "Titel, Datum, Modus", "Scan-Auftrag (TXT); im Studio mehrseitige PDF", "Scanner (WIA/TWAIN) und Kamera sind nicht angeschlossen – es werden Fotos und Bilder eingelesen. Texterkennung braucht Tesseract."),
        P("STUDIO-DAYPLAN-01", "Tagesplaner", "KI & Automatik", "DayPlanner, Calendar", "Tag planen", "Aufgaben, Termine, Arbeitszeit", "Tagesplan (TXT), im Studio auch Kalenderdatei (ICS)", "Trägt nichts in den Kalender ein; die Kalenderdatei importierst du selbst."),
        P("STUDIO-CV-01", "Bewerbung & Lebenslauf", "Arbeiten & Büro", "CvCheck, CvText, CvPdf, Anschreiben, Mappe, LetterPdf, PdfTools", "Lebenslauf prüfen", "Name, Stationen, Ausbildung, Kenntnisse, Sprachen, Anzeige", "Prüfbericht und ATS-Fassung (TXT), im Studio PDF/DOCX, Anschreiben, Mappe", "Versendet nichts; Bewerbungen schickst du selbst ab."),
        P("STUDIO-CARDS-01", "Karten & Einladungen", "Arbeiten & Büro", "Karten, KartenVorlagen, KartenPdf, Gaeste", "Karte anlegen", "Anlass, Format, Datum, Ort, Gäste", "Kartenplan mit Gästeliste (TXT), im Studio Druck-PDF und CSV", "Druckt und versendet nichts."),
        P("STUDIO-CONTRACTS-01", "Verträge-Ordner", "Arbeiten & Büro", "Vertraege, Ics, LetterKit, LetterPdf", "Kündigungsfrist berechnen", "Beginn, Laufzeit, Verlängerung, Frist, Kosten", "Fristbericht (TXT), im Studio Kalenderdatei und Kündigungsschreiben", "Kündigt nichts und versendet nichts – der Brief bleibt ein Entwurf."),
        P("STUDIO-PDFTOOLS-01", "PDF-Werkstatt", "Arbeiten & Büro", "PdfTools (Trendo PDF), PdfView", "PDF-Auftrag vorbereiten", "Aktion, Seiten, Seitenzahl", "Arbeitsauftrag (TXT), im Studio neue PDF-Dateien", "Die KI bearbeitet keine PDFs selbst; ausgeführt wird in der Werkstatt, das Original bleibt."),
        P("STUDIO-FORMS-01", "Formulare ausfüllen", "Arbeiten & Büro", "PdfFormular, FormProfile, PdfTools", "Formularfelder zuordnen", "Feldnamen des Formulars", "Zuordnung (TXT), im Studio ausgefüllte PDF", "Ausgefüllt und abgeschickt wird nur von dir; „Meine Angaben“ bleiben lokal."),
        P("STUDIO-ONLINE-BUSINESS-01", "Online verkaufen", "Business & Geld", "BusinessStudio", "Geschäftsplan rechnen", "Preis, Kosten, Menge", "Markdown", "Nur eine Schätzung aus deinen Zahlen. Kein Shop, keine Zahlung, keine Rechtsberatung."),
        P("STUDIO-POD-01", "Print on Demand", "Business & Geld", "PodCheck, PodPrice, PodLegal", "Druckdatei prüfen und Preis rechnen", "Motiv, Produkt, Kosten", "Prüfbericht, Verkaufstext", "Über den KI-Auftrag nur Prüfung und Preis; von dort wird nichts online gestellt und nichts bestellt. Im Studio lassen sich Printful, Printify, Stripe und Netlify mit dem eigenen Konto verbinden; veröffentlicht und bestellt wird nur nach Freigabe."),
        P("SPEC-11", "Geld & Rechnungen", "Business & Geld", "Finance, Ledger", "Rechnung mit Decimal rechnen, nicht buchen", "Verkäufer, Käufer, Positionen", "HTML-Entwurf", "Über den KI-Auftrag wird nichts gebucht und nichts bezahlt. Im Programm trägt „Rechnung erstellen“ die Einnahme ein. Keine Steuerberatung."),
        P("CORE-08", "Videos & Fotos bearbeiten", "Kreativ & Medien", "MediaStudio, ffmpeg", "Datei wirklich umwandeln", "Quelldatei im Projekt", "Mediendatei", "Hochladen ist nicht angeschlossen. Umwandeln braucht ffmpeg."),
        P("STUDIO-MUSIK-01", "Trendo Musik", "Kreativ & Medien", "SongLibrary, AceStepClient (ACE-Step 1.5)", "Liedplan schreiben", "Thema, Stil", "Liedplan (TXT); im Studio MP3 (WAV/FLAC über ffmpeg)", "Über den KI-Auftrag entsteht nur der Liedplan. Das Lied mit Gesang erzeugst du im Studio; dafür muss ACE-Step in der KI-Bibliothek eingerichtet sein. Verlängern und Spuren trennen gehen mit dem Turbo-Modell nicht."),
        P("STUDIO-FILM-01", "Filme machen", "Kreativ & Medien", "Fountain, FilmBreakdowns, ShotPlan, FilmCut, VideoEdit (FFmpeg)", "Drehbuch, Shotliste und fertiger Film aus eigenen Clips und Fotos", "Drehbuch als Text, eigene Videos, Fotos, Musik", "Film (MP4) mit Untertiteln (SRT), Fountain, Shotliste (CSV)", "Der Film entsteht aus deinen eigenen Clips und Fotos; wo Material fehlt, steht eine Texttafel. Trendo dreht keine Szenen und erzeugt hier keine Videoclips. Einzelne Storyboard-Bilder malt die lokale Bild-KI, wenn sie eingerichtet ist. Gerendert wird nur auf deinen Klick, mit FFmpeg."),
        P("STUDIO-LOGO-01", "Logos & Marken", "Kreativ & Medien", "LogoKit", "Logo als SVG entwerfen", "Name, Farben, Form", "SVG, Markenpaket (MD)", "Keine Markenrecherche beim Amt. Bilder-KI ist hier nicht angeschlossen."),
        P("STUDIO-PODCAST-01", "Podcast aufnehmen", "Kreativ & Medien", "PodcastKit, ffmpeg", "Shownotes und Kapitel schreiben", "Titel, Kapitel, Aufnahme", "Shownotes (MD), Kapiteldatei, RSS, MP3", "Lautheit braucht ffmpeg. Veröffentlichen macht die Person selbst."),
        P("STUDIO-COMIC-01", "Comics & Storyboards", "Kreativ & Medien", "ComicKit", "Comicseite anlegen", "Geschichte, Bilder", "SVG-Seite, Storyboard (MD)", "Bilder entstehen nicht hier, sie werden eingesetzt."),
        P("STUDIO-BOOK-01", "Bücher schreiben", "Kreativ & Medien", "BookKit", "Buch gliedern", "Titel, Kapitelzahl, Idee", "Manuskript (MD), E-Book (EPUB)", "Ohne Claude nur Gerüst und Statistik, kein Text."),
        P("STUDIO-ANIM-01", "Animation & Motion", "Kreativ & Medien", "AnimKit, ffmpeg", "Titel animieren", "Text, Bewegung", "Animiertes SVG, Video (MP4)", "Video braucht ffmpeg. Keine Figurenanimation."),
        P("STUDIO-ART-01", "Künstlerstudio", "Kreativ & Medien", "StrokeEngine, PaintDoc, PaintFiles, Palettes, Mannequin, Sketch", "Malvorlage anlegen", "Motiv, Format, Farben, Spiegeln", "Malvorlage (ORA mit Ebenen), Palette (GPL)", "Malen selbst geschieht mit Stift oder Maus im Studio; die KI malt keine fertigen Bilder."),
        P("STUDIO-PHOTOS-01", "Fotos verwalten", "Kreativ & Medien", "PhotoOrganizer, PhotoAlbum, Renamer", "Fotos ordnen (Probelauf)", "Fotoordner, Zielordner", "Plan, Kopien oder verschobene Fotos", "Über die KI nur der Probelauf; einsortiert wird im Studio nach Bestätigung."),
        P("STUDIO-TV-01", "TV & Live", "Kreativ & Medien", "TvLibrary, M3u, Xmltv, mpv, ffmpeg", "Senderliste und Programmführer zeigen", "Senderliste (M3U/Xtream), EPG", "Wiedergabe, Aufnahme (TS)", "Trendo bringt keine Sender mit. Aufnehmen braucht ffmpeg, Abspielen mpv."),
        P("SPEC-15", "Live streamen", "Kreativ & Medien", "Live-Studio der App", "Sendungsplan", "Titel, Ablauf", "Plan", "Über den KI-Auftrag entsteht nur der Plan; gesendet wird von dort nichts. Im Studio startest du den Stream selbst, nach Zustimmung."),
        P("SPEC-08", "Kleidung & Produkte entwerfen", "Kreativ & Medien", "Größen- und Schnittlogik der App", "Maße prüfen", "Maße", "Größentext", "Nur die Größentabelle. Es wird nichts bestellt."),
        P("X-07", "Freie Bilder & Klänge finden", "Kreativ & Medien", "OpenSource, FreeAssets", "Quelle mit Lizenz merken; freies Stück von Poly Haven laden", "Name und Lizenz; zum Laden „Holen: ja“ und „Asset: Kennung“", "Hinweis; geladene Dateien mit Lizenz- und Quellenbeleg (HERKUNFT.txt)", "Geladen wird nur nach deiner Freigabe und nur CC0-Stücke von Poly Haven, höchstens 2 MB je Datei."),
        P("CORE-12", "KI-Anweisungen schreiben", "KI & Automatik", "PromptLibrary", "Vorlage füllen", "Vorlage, Platzhalter", "Text", "Die Vorlage wird gefüllt. Die KI wird nicht gefragt."),
        P("CORE-05", "3D-Modelle erstellen", "3D & Spiele", "Scene3D, Mesh3D, Studio3D", "Modelldatei, Render nur mit Blender", "Form und Maße", "OBJ, optional Blend", "Blender wird aus dem Auftrag nicht gestartet. OBJ und die eingebaute Vorschau brauchen es nicht."),
        P("STUDIO-CONSOLE-01", "Konsolenspiele portieren", "3D & Spiele", "ConsolePortKit, console-skill, PS2Recomp", "Portierung planen", "Spiel, eigenes Abbild", "Plan, später ein Programm", "Nur eigene PS2-Spiele. Trendo liefert keine Spiele und kein BIOS. Portieren macht Claude mit den Skills und ist experimentell."),
        P("STUDIO-SUMMARY-01", "Texte zusammenfassen", "KI & Automatik", "SummaryKit", "Text zusammenfassen", "Text, Länge", "Zusammenfassung (MD)", "Ohne Claude nur die wichtigsten Sätze, keine eigene Formulierung."),
        P("STUDIO-IMAGEAI-01", "Bilder mit KI", "KI & Automatik", "ImageAiKit, stable-diffusion.cpp", "Bildauftrag vorbereiten", "Beschreibung, Stil, Format", "PNG mit Herkunftsnachweis", "Malen braucht SD Turbo oder SDXL Turbo aus der KI-Bibliothek."),
        P("STUDIO-VIDEOAI-01", "Videos mit KI", "KI & Automatik", "VideoAiKit, ImageAiKit, stable-diffusion.cpp, ffmpeg", "Szenenplan für ein KI-Video", "Szenen, Titel", "Szenenplan, Standbilder, Video (MP4) mit Herkunftsnachweis", "Bilder braucht die lokale Bild-KI, das Video ffmpeg; höchstens 60 Sekunden. Echte KI-Videoclips nur über das eigene kie.ai-Konto."),
        P("STUDIO-AGENTS-01", "KI-Agenten bauen", "KI & Automatik", "AgentBlueprint, AgentCenter, StudioAgentsExport", "Eigenen KI-Helfer entwerfen", "Name, Aufgabe, Anweisungen, Werkzeuge", "Claude-Code-Subagent (MD), Trendo-Rolle", "Gespeichert wird erst nach Ordnerwahl, nie überschrieben; keine Schlüssel im Text."),
        P("STUDIO-MODELS-01", "KI-Modelle vergleichen", "KI & Automatik", "ModelCompare, IntelligenceCore", "Vergleichsfragen vorbereiten", "Fragen", "Ergebnistabelle (MD, CSV)", "Gefragt wird nur bei eingerichteten KI-Quellen; ohne Quelle ein Hinweis."),
        P("STUDIO-TRAIN-01", "Eigene KI trainieren", "KI & Automatik", "TextClassifier (Naive Bayes)", "Textklassifikator trainieren", "Beispiele: Text = Kategorie", "Modell (JSON), Auswertung (MD)", "Nur Textkategorien, kein Sprachmodell; gut ab etwa 20 Beispielen je Kategorie."),
        P("CORE-06", "Spiele bauen", "3D & Spiele", "GameStudio", "Browser-Spiel, Unreal nur wenn installiert", "Vorlage, Name", "HTML oder uproject", "Der Unreal-Editor wird nicht gestartet. Browser-Spiel und Projektdatei gehen ohne ihn."),
        P("SPEC-04", "3D-Modelle ansehen", "3D & Spiele", "XrInspector, XrPlus, Mesh3D", "3D-Modell prüfen", "3D-Datei im Projekt (GLB, glTF, OBJ, STL)", "Prüfbericht (MD)", "Liest das Modell nur, verändert es nicht. Die Seitengruppe steht nicht in der Übersicht; Modell-Prüfer und 3D-Bibliothek erreichst du aus 3D-Druck, Architektur und über die Suche."),
        P("SPEC-09", "Gegenstände einscannen", "3D & Spiele", "PhotoScan, PointCloud", "Scan aus echten Bildern", "Bilder", "keine", "COLMAP und Meshroom werden aus dem Auftrag nicht gestartet. Punktwolken (PLY, XYZ) bearbeitest du im Programm „Punktwolke“."),
        P("SPEC-01", "Wohnung planen", "Technik & Fachgebiete", "Flächenrechnung der App", "Fläche aus Maßen", "Länge, Breite", "SVG, Fläche", "Nur Fläche, Bedarf und Grundriss. Es wird nichts bestellt."),
        P("SPEC-02", "Schaltungen berechnen", "Technik & Fachgebiete", "Elektronik-Rechner der App", "Widerstand und LED prüfen", "Spannung, Strom", "Zahl mit Einheit", "Nur die Rechnung. Es wird keine Schaltung aufgebaut."),
        P("SPEC-03", "Statik nachrechnen", "Technik & Fachgebiete", "Balkenrechnung der App", "Balken mit Annahmen prüfen", "Last, Länge, Profil", "Prüftext", "Nur die eingebaute Balkenrechnung. Keine Prüfung durch eine Fachperson."),
        P("SPEC-10", "Geräte überwachen", "Technik & Fachgebiete", "Messwert-Ansichten der App", "Messreihe zeigen", "Werte", "Tabelle", "Über den KI-Auftrag nur die eingetippten Werte. Im Studio liest Trendo Live-Messwerte über MQTT mit; geschaltet wird nichts."),
        P("SPEC-13", "Routen & Karten", "Technik & Fachgebiete", "Routenlogik der App", "Strecke aus Punkten", "Punkte", "Länge", "Über den KI-Auftrag nur die Strecke. Im Programm „Karten & Touren“ lädt Trendo mit Internet OpenStreetMap-Kacheln."),
        P("SPEC-14", "Fräsen & Fertigen", "Technik & Fachgebiete", "Drehzahlrechnung der App", "Nur rechnen, keine Maschine starten", "Werkzeug, Material", "Drehzahl", "Keine Fräs- oder Maschinenbefehle aus einem Prompt."),
        P("SPEC-05", "Messwerte auswerten", "Wissen & Lernen", "Science", "Messreihe auswerten", "Werte", "Kennzahl", "Nur die eingebaute Statistik. Keine wissenschaftliche Beratung."),
        P("SPEC-06", "Lernen mit Karteikarten", "Wissen & Lernen", "Karteikarten der App", "Karte anlegen", "Vorderseite, Rückseite", "CSV", "Nur die Karte auf diesem PC. Es gibt keinen Kurs."),
        P("SPEC-07", "Texte übersetzen", "Wissen & Lernen", "bestehendes Basisstudio, kein 2.0-Ausbau", "keine neue Übersetzung über diesen Auftrag", "keine", "keine", "Language & Localization Studio 2.0 bleibt abgelehnt. Kein neuer Übersetzungsadapter."),
        P("CORE-10", "Wissen merken", "Wissen & Lernen", "Knowledge", "Notiz ins eigene Wissen", "Text", "Eintrag", "Nur der eigene Eintrag auf diesem PC. Es wird nichts hochgeladen."),
        P("SYS-08", "Sicher recherchieren", "Wissen & Lernen", "ResearchDesk", "Seite nur lesen", "Adresse", "Lesetext", "Nur der Lesemodus. .onion, das eigene Netz und Downloads bleiben draußen. Offline wird nichts geholt."),
        P("STUDIO-CITE-01", "Recherche mit Quellen", "KI & Automatik", "CiteKit, ResearchDesk", "Quelle zitieren", "Titel, Autorin, Jahr, Verlag oder URL, Stil", "Quellenangabe (MD) mit BibTeX", "Nur Formatieren und Prüfen der Angaben. Ob die Quelle die Aussage belegt, prüft Claude im Studio."),
        P("CORE-02", "KI fragen", "KI & Automatik", "IntelligenceCore.AskAsync", "Frage an die lokale KI", "Frage", "Antwort", "Nur die angeschlossene lokale KI. Ohne erreichbare Quelle gibt es keine Antwort."),
        P("CORE-03", "KI-Team einsetzen", "KI & Automatik", "AgentCenter", "Team um Rat fragen", "Aufgabe, Team", "Text", "Nur die vorhandenen Rollen. Ohne lokale KI gibt es keinen Rat."),
        P("X-05", "Kostenlose KI finden", "KI & Automatik", "KiCatalog, nur Loopback", "Lokale Quelle prüfen", "keine", "Status", "Nur Loopback aus dem Katalog. Kostenpflichtige und Online-Konten werden nicht gefragt."),
        P("SYS-05", "Aufgaben automatisieren", "KI & Automatik", "WorkflowEngine", "Ablauf planen", "Schritte", "Ablauf", "Nur der Plan. Der Ablauf verschiebt und kopiert nichts."),
        P("X-01", "Großprojekte umsetzen", "KI & Automatik", "bestehende Studios", "Studios nur nennen", "Ziel", "Plan", "Nur die Namen der Studios. Es wird nichts ausgeführt."),
        P("STUDIO-CODEAI-01", "Code mit KI", "KI & Automatik", "CodeAiKit, CodeProject, DevStudio", "Projekt untersuchen", "Projektordner", "Bericht (MD) mit Projektart und Testbefehl", "Über die KI wird nichts ausgeführt; Tests startet das Studio nach Bestätigung."),
        P("STUDIO-VISION-01", "Bilder erkennen", "KI & Automatik", "VisionKit, Tesseract (optional)", "Bildfakten auflisten", "Bildordner", "Liste (MD) mit Format, Größe und EXIF", "Schärfe, Helligkeit, Farben und Texterkennung nur im Studio; Texterkennung braucht Tesseract."),
        P("CORE-07", "Webseite bauen", "Entwickeln", "DevStudio", "Projektvorlage anlegen, nicht veröffentlichen", "Vorlage, Name", "Quelltext", "Über den KI-Auftrag wird nichts veröffentlicht und nichts hochgeladen. Im Studio lädst du die Seite selbst zu Netlify hoch, mit eigenem Konto und Zustimmung."),
        P("X-03", "Apps zusammenklicken", "Entwickeln", "PageBuilder", "Seitenentwurf", "Inhalt", "HTML, seite.json", "HTML und Formulardatei im Projekt. Kein Shop, kein 3D, kein Pixel-Streaming. Veröffentlichen ist nicht angeschlossen."),
        P("X-06", "Projekte von GitHub holen", "Entwickeln", "OpenSource", "Nur anzeigen, nicht mergen", "Name", "Hinweis", "Der KI-Auftrag erklärt nur die Lizenz und klont selbst nichts. Im Studio holt „Quellcode holen“ eine echte Kopie (git clone, neuester Stand, mit Herkunftsdatei) in den Ordner „Open Source“; „Im Code-Editor“ öffnet sie mit erkannter Bauanleitung. Fertige Windows-Programme richtet „Installieren & starten“ mit Prüfsumme ein, Installer erst nach Signaturprüfung und eigener Bestätigung."),
        P("X-04", "Trendo erweitern", "Entwickeln", "ExtensionCatalog", "Erweiterung entwerfen", "Name", "Entwurf", "Nur der Entwurf. Es wird nichts installiert."),
        P("X-10", "Programme nachbauen", "Entwickeln", "CleanRoom", "Nur eigene Beschreibung", "Verhalten", "Entwurf", "Nur die Beschreibung im Projekt. Keine Rechtsberatung und kein Originalcode."),
        P("CORE-01", "Desktop einrichten", "System & Sicherheit", "UserSettings, Startseite T-0218", "Desktop nur zeigen", "keine", "Text", "Nur Anzeige. Desktop, Startmenü und Fenster richtest du unter Einstellungen → Startbildschirm ein."),
        P("SYS-01", "Startmodus wählen", "System & Sicherheit", "OsMode", "Modus nur anzeigen", "keine", "Hinweis", "Nur der erkannte Modus. Es wird nichts umgestellt."),
        P("CORE-11", "Daten schützen", "System & Sicherheit", "ISecretStore", "Speicher nur nennen", "keine", "Text", "Nur der Name des Speichers. Werte werden nicht gelesen, gesetzt oder gelöscht."),
        P("SPEC-12", "System auf Gefahren prüfen", "System & Sicherheit", "ForensicsLab, 120 Stunden", "Forensik unverändert lassen", "keine", "keine", "Verbesserung 34 bleibt abgelehnt. Die 120-Stunden-Regel wird nicht angetastet."),
        P("SYS-03", "Downloads & Links", "System & Sicherheit", "ResearchDesk", "Datei nach Freigabe laden", "Adresse", "Datei", "Erst nach der Freigabe, höchstens 2 MB, nur http(s) im Lesemodus. Ohne „Laden: ja“ passiert nichts."),
        P("SYS-04", "Programme installieren", "System & Sicherheit", "DependencyManager", "Katalogprogramm nach Zustimmung und Freigabe", "Programm", "Plan oder Prüfergebnis", "Nur Katalogprogramme, nur nach Lizenzdatei und Freigabe. winget läuft nicht von allein."),
        P("SYS-06", "Dateien teilen", "System & Sicherheit", "FolderShare, ShareServer", "Ordner im Projekt nach Freigabe", "Ordner", "Hinweis", "Nur ein Ordner im Projekt, nur nach Freigabe, höchstens 24 Stunden. Ohne „Teilen: ja“ passiert nichts."),
        P("SYS-07", "Geräte erkennen", "System & Sicherheit", "DeviceScanner", "Geräte nur nennen", "keine", "Liste", "Nur die gelesene Liste. Es wird kein Gerät gestartet."),
        P("SYS-11", "Programme starten", "System & Sicherheit", "ExternalApps", "Programm aus der Liste nach Freigabe", "Programm, Datei", "Plan", "Nur die feste Liste und eine Datei im Projekt, nur nach Freigabe."),
        P("SYS-12", "Schrift & Farben anpassen", "System & Sicherheit", "UserSettings, Design 41", "Darstellung nur zeigen", "keine", "Text", "Nur Anzeige. Schrift und Farben bleiben beim laufenden Design."),
        P("SYS-13", "Qualität prüfen", "System & Sicherheit", "AuditLog, SelfHealer", "Protokoll schreiben", "keine", "Protokoll", "Protokoll und Prüfung. Der Explorer wird nicht geöffnet."),
        P("X-02", "Einrichten & Reparieren", "System & Sicherheit", "SelfHealer", "Nur prüfen", "keine", "Protokoll", "Über den KI-Auftrag nur die Prüfung. Im Studio repariert „Jetzt prüfen und reparieren“ die sicheren Fälle. Verbesserung 35 bleibt offen."),
        P("X-08", "Handy verbinden", "System & Sicherheit", "MobileBridge", "adb-Liste, wenn adb schon da ist", "keine", "Liste", "Nur „adb devices -l“. Kein Spiegel, kein Foto, keine Installation."),
        P("CORE-04", "PC, Pi & Handy verbinden", "System & Sicherheit", "FusionStore", "Verbund nur zeigen", "keine", "Liste", "Nur die gespeicherten Geräte. Es wird nichts gekoppelt."),
        P("X-09", "Leistung anzeigen", "System & Sicherheit", "ResourceMonitor, SystemMetrics", "Messwerte lesen", "keine", "Messwert", "Nur die gelesenen Messwerte. Es wird kein Programm beendet."),
        // T-0780 (Etappe 0.9.113 „Wissen & Lernen“)
        P("STUDIO-REFERAT-01", "Referate & Hausarbeiten", "Wissen & Lernen", "ReferatKit, CiteKit, TextDoc (DOCX/ODT/PDF), SlideDoc (PPTX/ODP)", "Gliederung anlegen", "Thema, Punkte des Hauptteils", "Gliederung (MD), im Studio Arbeit als DOCX/ODT/PDF und Folien als PPTX/ODP", "Über den Auftrag nur das Gliederungs-Gerüst. Text und Zitate entstehen im Studio; zitiert wird nur aus den eigenen Quellen, nichts wird abgegeben."),
        P("STUDIO-TIMELINE-01", "Zeitleisten", "Wissen & Lernen", "TimelineKit, HistDate", "Ereignisse als Tabelle", "Zeilen mit Datum, Titel und Spur", "Tabelle (CSV), im Studio Zeitleiste als SVG, PNG und PDF", "Über den Auftrag nur die geprüfte Tabelle. Daten werden nicht geraten; unlesbare Zeilen werden genannt."),
        // T-0781 Wissen & Lernen (Freigabe 26.11.2026)
        P("STUDIO-MATH-01", "Mathe-Helfer", "Wissen & Lernen", "MatheKern, Bruch, Polynom, Rechenblatt, GraphView", "Aufgabe mit Lösungsweg rechnen", "Aufgabe, Art (Gleichung, System, Bruch, Ableiten …)", "Lösungsweg (MD); im Studio Rechenblatt als PDF, LaTeX, Markdown", "Gerechnet wird nur im eigenen Rechenkern; die KI übersetzt höchstens eine Textaufgabe. Integrale nur für Grundfunktionen und Polynom mal e/sin/cos; Gleichungen höheren Grades ohne rationale Nullstelle nur als Näherung."),
        P("STUDIO-SCIENCE-01", "Naturwissenschaft", "Wissen & Lernen", "Elemente, Chemie, Einheiten, Formelsammlung, StatsPlus.Propagate", "Molmasse, Reaktion oder Einheiten rechnen", "Summenformel, Reaktionsgleichung oder Rechnung mit Einheiten", "Rechnung (MD); im Studio Laborheft als PDF, CSV, Markdown und Element-Tabelle", "Physik nur mit den Formeln der eingebauten Formelsammlung; keine Strukturformeln, keine Simulation. Messreihen wertet „Messdaten & Auswertung“ aus."),
    ];

    private static StudioProfile P(string id, string title, string area, string existing, string action, string inputs, string outputs, string blocker) =>
        new(id, title, area, existing, action, inputs, outputs, blocker);

    public static IReadOnlyList<StudioProfile> Profiles => All;
    public static IReadOnlyList<string> Ids => All.Select(p => p.Id).ToList();
    public static StudioProfile? Find(string? id) => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    public static string Title(string id) => Find(id)?.Title ?? id;
    public static string Area(string id) => Find(id)?.Area ?? "";

    public static CapabilityLevel Level(string id, IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var mine = adapters.Where(a => string.Equals(a.StudioId, id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0) return CapabilityLevel.NotImplemented;
        if (mine.All(a => a.DeclaredLevel == CapabilityLevel.Blocked)) return CapabilityLevel.Blocked;
        if (mine.All(a => a.DeclaredLevel == CapabilityLevel.Ready)) return CapabilityLevel.Ready;
        if (mine.Any(a => a.DeclaredLevel is CapabilityLevel.Ready or CapabilityLevel.Partial)) return CapabilityLevel.Partial;
        return mine[0].DeclaredLevel;
    }

    public static string UiLine(string id, IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var profile = Find(id);
        var title = profile?.Title ?? id;
        var mine = adapters.Where(a => string.Equals(a.StudioId, id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count == 0) return title + ": " + StatusText.German(CapabilityLevel.NotImplemented) + ". " + (profile?.Blocker ?? "Kein Fachadapter registriert.");
        return title + ": " + StatusText.German(Level(id, adapters)) + ". " + string.Join(", ", mine.Select(a => a.Label));
    }

    public static string Markdown(IReadOnlyList<IStudioActionAdapter> adapters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Studio-Abdeckung – Verbesserung 42");
        sb.AppendLine();
        sb.AppendLine("Erzeugt aus `CapabilityCatalog` und den wirklich registrierten Adaptern. `ready` steht nur an einem Adapter, dessen Beleg ein vorhandener Test ist. Einträge ohne Adapter sind `not_implemented`.");
        sb.AppendLine();
        sb.AppendLine("| Studio-ID | Titel | Bereich | Vorhandene Bausteine | Vorgesehene Aktion | Eingaben | Adapter | Ausgabe | Reife | Beleg | Blocker |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var p in All)
        {
            var mine = adapters.Where(a => string.Equals(a.StudioId, p.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            var adapter = mine.Count == 0 ? "keiner" : string.Join(", ", mine.Select(a => a.ActionId));
            var evidence = mine.Count == 0 ? "—" : string.Join(", ", mine.Select(a => string.IsNullOrWhiteSpace(a.Evidence) ? "—" : a.Evidence));
            var blocker = mine.Count == 0 ? p.Blocker : mine.Any(a => a.DeclaredLevel != CapabilityLevel.Ready) ? p.Blocker : "—";
            sb.Append("| ").Append(p.Id).Append(" | ").Append(p.Title).Append(" | ").Append(p.Area).Append(" | ")
                .Append(p.Existing).Append(" | ").Append(p.PlannedAction).Append(" | ").Append(p.Inputs).Append(" | ")
                .Append(adapter).Append(" | ").Append(p.Outputs).Append(" | ").Append(LevelName(Level(p.Id, adapters))).Append(" | ")
                .Append(evidence).Append(" | ").Append(blocker).AppendLine(" |");
        }
        sb.AppendLine();
        sb.AppendLine($"Anzahl: {All.Length} Katalogeinträge, {adapters.Count} registrierte Aktionen.");
        return sb.ToString();
    }

    public static string LevelName(CapabilityLevel level) => level switch
    {
        CapabilityLevel.Ready => "ready",
        CapabilityLevel.Partial => "partial",
        CapabilityLevel.Blocked => "blocked",
        _ => "not_implemented"
    };
}
