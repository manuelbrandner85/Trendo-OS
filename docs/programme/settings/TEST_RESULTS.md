# Einstellungen – Testergebnisse

Studio: Schrift & Farben anpassen (SYS-12) · Bereich: System & Sicherheit · Seite: `settings` · Stand: 0.9.36 (Stand e6f79fe)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 66 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Absturzschutz: abgefangener Oberflächenfehler zeigt einen Hinweis „Trendo läuft weiter“ – nur einmal bei Folgefehlern, Fenster bleibt bedienbar | ✓ bestanden |
| Alle Studios: Bereich und Suche filtern die Karten, Alle zeigt wieder alles | ✓ bestanden |
| Bedienstil: in den Einstellungen wählbar; Mac setzt runde Ampel-Knöpfe links (Schließen zuerst), Linux runde Knöpfe rechts – Knöpfe funktionieren weiter | ✓ bestanden |
| CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste | ✓ bestanden |
| Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen | ✓ bestanden |
| Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt | ✓ bestanden |
| Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert | ✓ bestanden |
| Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert | ✓ bestanden |
| Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung | ✓ bestanden |
| Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml | ✓ bestanden |
| Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar | ✓ bestanden |
| Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück | ✓ bestanden |
| Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein | ✓ bestanden |
| Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet | ✓ bestanden |
| Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter | ✓ bestanden |
| Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster | ✓ bestanden |
| Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück | ✓ bestanden |
| Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck | ✓ bestanden |
| Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen | ✓ bestanden |
| Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe | ✓ bestanden |
| KI: die Seite lässt sich erneut öffnen, Status bleibt „Nicht verbunden“ | ✓ bestanden |
| Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert | ✓ bestanden |
| LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED | ✓ bestanden |
| LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte | ✓ bestanden |
| Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt | ✓ bestanden |
| QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet | ✓ bestanden |
| QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet | ✓ bestanden |
| QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben | ✓ bestanden |
| QA-017: Navigationsschaltflächen haben zugängliche Namen | ✓ bestanden |
| QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur | ✓ bestanden |
| QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert | ✓ bestanden |
| QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen | ✓ bestanden |
| STUDIO-LETTER-01: Schnellzugriff „Neuer Brief“ → Kündigung → ausfüllen → prüfen → speichern; Absender gemerkt | ✓ bestanden |
| SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter | ✓ bestanden |
| SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt | ✓ bestanden |
| SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen | ✓ bestanden |
| SYS-12 (UI): Bericht über alle Seiten; keine Seite tiefer als 2 Ebenen; Prüfseite hat keine unbenannten Knöpfe | ✓ bestanden |
| SYS-12 (UI): Leise Benachrichtigung; Einstellung bleibt gespeichert; einfacher Modus zeigt weniger Kacheln | ✓ bestanden |
| SYS-12 (UI): Mindestschrift greift; Knöpfe haben Namen und sind per Tab erreichbar; Bewegung aus = keine Übergänge | ✓ bestanden |
| SYS-12-P03/P09/P18: Mindestschrift, Status mit Zeichen, leise Meldungen | ✓ bestanden |
| SYS-12-P07: WCAG-Kontrast (Schwarz/Weiß 21:1, AA-Grenzen) | ✓ bestanden |
| SYS-12-P10: Leichte Sprache – höchstens 2 kurze Sätze, einfache Wörter | ✓ bestanden |
| SYS-12-P14: Jede Akzentfarbe wird für Text auf jedem Design AA-tauglich | ✓ bestanden |
| SYS-12-P15/P16: Navigation >2 Ebenen und englische Beschriftungen werden gefunden | ✓ bestanden |
| SYS-12-P20: Vorlesen – Windows-SAPI-Befehl, Linux espeak oder nichts | ✓ bestanden |
| SYS-12-P22: Einstellungen exportieren/importieren verlustfrei, Unsinn abgelehnt | ✓ bestanden |
| SYS-12-P23: Fensterposition nur auf vorhandenem Bildschirm wiederherstellen | ✓ bestanden |
| SYS-12-P25: Bericht listet Befunde je Seite | ✓ bestanden |
| SYS-12: Anzeigegröße wird auf erlaubte Stufen begrenzt | ✓ bestanden |
| SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück | ✓ bestanden |
| Startmenü: Klick auf Startknopf öffnet, Klick daneben und Esc schließen, Windows-Taste schaltet um | ✓ bestanden |
| Startmenü: Studio aus A–Z anheften, angeheftet öffnen, lösen; Zuletzt benutzt | ✓ bestanden |
| Startmenü: Suche findet Studio/Seite, Ergebnis per Mausklick öffnet | ✓ bestanden |
| Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link | ✓ bestanden |
| Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts | ✓ bestanden |
| Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog | ✓ bestanden |
| StudioCatalog.NameOf: Katalogname, sonst der bisherige Text | ✓ bestanden |
| T-0107: Sparsam – keine Hintergrund-Unschärfe, keine Schatten und Effekte, keine Übergänge; Auswahl in den Einstellungen | ✓ bestanden |
| Trendo erweitern: Erweiterungen dürfen alles außer Name und Logo – Produktname, Marke oder Logo-Datei werden abgelehnt | ✓ bestanden |
| W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck | ✓ bestanden |
| W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md) | ✓ bestanden |
| W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert | ✓ bestanden |
| W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert | ✓ bestanden |
| Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar | ✓ bestanden |
