# Testübersicht: Schrift & Farben anpassen

Studio SYS-12 · Schrift & Farben anpassen · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 105. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Absturzschutz: abgefangener Oberflächenfehler zeigt einen Hinweis „Trendo läuft weiter“ – nur einmal bei Folgefehlern, Fenster bleibt bedienbar
- Alle Studios: Bereich und Suche filtern die Karten, Alle zeigt wieder alles
- Anmeldung: „Beim Start nach PIN fragen“ lässt sich abschalten
- Apps: WhatsApp installieren → Desktop und Startmenü, öffnet als App-Fenster mit eigenem Speicher, Entfernen löscht die Anmeldung
- Bedienstil: in den Einstellungen wählbar; Mac setzt runde Ampel-Knöpfe links (Schließen zuerst), Linux runde Knöpfe rechts – Knöpfe funktionieren weiter
- Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert
- CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste
- Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen
- Dateimanager-Verhalten je System: Ziehen verschiebt (Strg kopiert), Kopiename je System, Finder-Zeilenschalter benennt um, Entf/⌘⌫ legt in den Papierkorb, Leertaste zeigt Vorschau
- Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt
- Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert
- Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert
- Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung
- Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml
- Einstellungen-App wie Windows 11: Bereiche links, Suche über alle Einstellungen, Betriebsmodus öffnet den Stick-Schreiber ohne PC-Einrichtung und ohne „vom PC entfernen“
- Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar
- Ersteinrichtung im Vollbild: Claude freiwillig zuerst; Stick/SSD zeigen echte Laufwerke mit GB, zu kleine gesperrt, Windows-Laufwerk nie; ehrlicher Hinweis solange das Abbild fehlt
- Ersteinrichtung: nach Neustart geht es beim letzten Schritt weiter, eine schon festgelegte PIN wird nicht erneut abgefragt
- Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück
- Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein
- Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet
- Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter
- Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster
- Fenster: maximiert füllt den ganzen Bildschirm – über Kopfleiste und Taskleiste, ohne Rand; Wiederherstellen bringt die Leisten zurück
- Fenster: maximiert und daneben geöffnet bleiben im AutomationPeer-Baum, mit Namen und endlichem Rechteck
- Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen
- Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe
- KI: die Seite lässt sich erneut öffnen, Status bleibt „Nicht verbunden“
- Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert
- LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED
- LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte
- Lesbare Schrift: Knöpfe in „Sehen“; Atkinson wird gespeichert und aufs Fenster angewandt; zurück zum Standard; unbekannte Wahl ignoriert; Schriftdateien und Lizenz liegen bei
- Mehrfachauswahl wie im System: Umschalt-Klick Bereich, Strg-Klick dazu/weg, Rahmen mit der Maus aufziehen, Alles auswählen, mehrere kopieren/einfügen und in den Papierkorb, Klick ins Leere hebt auf
- Neuerungen im Vollbild: nicht wegklickbar (Esc, Klick daneben), läuft von selbst weiter, Pfeile blättern, „Los geht's“ erst auf der letzten Folie
- Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt
- Profi-Stufe: Befehlssuche führt Befehle aus, Verlauf springt zurück, Projektdaten – falsches JSON wird abgelehnt, richtiges übernommen und rückgängig machbar
- Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü ☰ – Menüeinträge lösen dieselben Befehle aus
- QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet
- QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet
- QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben
- QA-017: Navigationsschaltflächen haben zugängliche Namen
- QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur
- QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert
- QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen
- Registerkarten wie Explorer/Finder/Dateien: Strg/⌘+T neu, Mittelklick-Ordner in neuer Karte, Wechsel per Klick und Strg+Bild↓, eigener Verlauf je Karte, Strg/⌘+W schließt erst die Karte, dann das Fenster
- STUDIO-LETTER-01: Schnellzugriff „Neuer Brief“ → Kündigung → ausfüllen → prüfen → speichern; Absender gemerkt
- SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter
- SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt
- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- SYS-12 (UI): Bericht über alle Seiten; keine Seite tiefer als 2 Ebenen; Prüfseite hat keine unbenannten Knöpfe
- SYS-12 (UI): Leise Benachrichtigung; Einstellung bleibt gespeichert; einfacher Modus zeigt weniger Kacheln
- SYS-12 (UI): Mindestschrift greift; Knöpfe haben Namen und sind per Tab erreichbar; Bewegung aus = keine Übergänge
- SYS-12-P03/P09/P18: Mindestschrift, Status mit Zeichen, leise Meldungen
- SYS-12-P07: WCAG-Kontrast (Schwarz/Weiß 21:1, AA-Grenzen)
- SYS-12-P10: Leichte Sprache – höchstens 2 kurze Sätze, einfache Wörter
- SYS-12-P14: Jede Akzentfarbe wird für Text auf jedem Design AA-tauglich
- SYS-12-P15/P16: Navigation >2 Ebenen und englische Beschriftungen werden gefunden
- SYS-12-P20: Vorlesen – Windows-SAPI-Befehl, Linux espeak oder nichts
- SYS-12-P22: Einstellungen exportieren/importieren verlustfrei, Unsinn abgelehnt
- SYS-12-P23: Fensterposition nur auf vorhandenem Bildschirm wiederherstellen

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
