# Startbildschirm – Testergebnisse

Studio: Desktop einrichten (CORE-01) · Bereich: System & Sicherheit · Seite: `start` · Stand: 0.9.36 (Stand ccb1ad1)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 82 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 1

| Test | Ergebnis |
|---|---|
| 3D-Seite zeigt die Auftragsleiste, ohne die 3D-Arbeitsfläche zu verkleinern | ✓ bestanden |
| Abdeckung und StudioCatalog nennen dieselben Kennungen, Titel und Bereiche | ✓ bestanden |
| Alle Studios + Bereiche: jede Karte ist klickbar und führt aus der Liste heraus | ✓ bestanden |
| Anmeldung: „Beim Start nach PIN fragen“ lässt sich abschalten | ✓ bestanden |
| Apps: WhatsApp installieren → Desktop und Startmenü, öffnet als App-Fenster mit eigenem Speicher, Entfernen löscht die Anmeldung | ✓ bestanden |
| Ausnahmen bleiben eine kleine begründete Liste | ✓ bestanden |
| Baukasten übernimmt die neue Seite ins Formular | ✓ bestanden |
| Briefseite zeigt den echten Brieftext in der Vorschau | ✓ bestanden |
| Briefseite zeigt die Auftragsleiste und meldet ehrlich, wenn nichts eingebaut ist | ✓ bestanden |
| CORE-01: Dateisuche begrenzt Tiefe und Anzahl | ✓ bestanden |
| CORE-01: Suche findet mit Umlauten, Wortanfang und mehreren Wörtern; sortiert nach Treffergüte | ✓ bestanden |
| CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste | ✓ bestanden |
| Claude-Regeln 01.11.2026: Claude Code aus Trendo erlaubt (eigene Anmeldung je Nutzer) – kein Sperr-Hinweis; „Claude Code öffnen“ startet das echte Claude Code im Projektordner | ✓ bestanden |
| Claude-Regeln 01.11.2026: Trendo speichert keine Claude-Tokens – ein alter wird beim Start gelöscht, kein Token-Feld mehr | ✓ bestanden |
| Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt | ✓ bestanden |
| Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert | ✓ bestanden |
| Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert | ✓ bestanden |
| Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung | ✓ bestanden |
| Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml | ✓ bestanden |
| Desktop: anfangs keine Symbole; über Startmenü, Seite „Desktop-Symbole“ und Studio Manager hinzufügen; Rechtsklick entfernt; bleibt gespeichert | ✓ bestanden |
| Drei Studios gleichzeitig bei 1366×768: Briefe, Spiele, 3D – Taskleiste, Snap, Eingabe bleibt | ✓ bestanden |
| Ersteinrichtung: nach Neustart geht es beim letzten Schritt weiter, eine schon festgelegte PIN wird nicht erneut abgefragt | ✓ bestanden |
| Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück | ✓ bestanden |
| Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein | ✓ bestanden |
| Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter | ✓ bestanden |
| Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster | ✓ bestanden |
| Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen | ✓ bestanden |
| G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite | ✓ bestanden |
| G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe) | ✓ bestanden |
| G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint | ✓ bestanden |
| G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus | ✓ bestanden |
| Jedes Studio mit Status IMPLEMENTED hat einen eigenen Bereich (keine Sackgasse) | ✓ bestanden |
| Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert | ✓ bestanden |
| LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED | ✓ bestanden |
| LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte | ✓ bestanden |
| N11: Rechner – Tasten, wissenschaftlich, Programmierer, Grafik, Datum, Verlauf | ✓ bestanden |
| N18: Zettel anlegen, Farbe wählen, auf der Startseite anheften und lösen | ✓ bestanden |
| N20: Wecker stellen und klingeln (Benachrichtigung mit Schlummern), Timer läuft ab, Stoppuhr-Runden, Weltuhr | ✓ bestanden |
| N5: Notizbuch – Abschnitt und Seite anlegen, schreiben (speichert), Schlagworte, Suche, der KI als Wissen geben | ✓ bestanden |
| N9: Editor öffnet ANSI/CRLF, sucht, ersetzt, speichert in gleicher Kodierung; Kodierung umstellbar | ✓ bestanden |
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
| SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück | ✓ bestanden |
| Startbildschirm bekommt keine Auftragsleiste | ✓ bestanden |
| Startmenü: Anheften bleibt nach erneutem Öffnen, angeheftetes Studio sitzt in der Taskleiste | ✓ bestanden |
| Startmenü: Klick auf Startknopf öffnet, Klick daneben und Esc schließen, Windows-Taste schaltet um | ✓ bestanden |
| Startmenü: Studio aus A–Z anheften, angeheftet öffnen, lösen; Zuletzt benutzt | ✓ bestanden |
| Startmenü: Suche findet Studio/Seite, Ergebnis per Mausklick öffnet | ✓ bestanden |
| Startmenü: Suche und Enter starten das Studio, nicht die Übersicht | ✓ bestanden |
| Startmenü: jedes Katalog-Studio öffnet das eigene Fenster, Taskleiste, Minimieren, Wiederherstellen, Schließen | ✓ bestanden |
| Startseite, Alle Studios und jede Studio-Seite: keine sichtbare Taste ohne Wirkung | ✓ bestanden |
| Startseite: die neun bekannten Studios öffnen aus dem Startmenü per echtem Mausklick eine eigene Seite | ✓ bestanden |
| Studio-Abnahme: Seite öffnet, Überschrift ist der Katalogname, keine tote Taste, eine Hauptaktion ist bedienbar | nicht im Lauf |
| Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link | ✓ bestanden |
| Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts | ✓ bestanden |
| T-0107: Sparsam – keine Hintergrund-Unschärfe, keine Schatten und Effekte, keine Übergänge; Auswahl in den Einstellungen | ✓ bestanden |
| T-0212: Einfacher Modus vergrößert Knöpfe | ✓ bestanden |
| T-0212: Schalter zeigt Zustand; „Status mit Zeichen“ setzt ✓ ⚠ ✕ an echte Statuspunkte | ✓ bestanden |
| T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt | ✓ bestanden |
| T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“ | ✓ bestanden |
| T-0316: Studio – „✳ Claude“ immer in der Auftragsleiste; das Claude-Projekt des Studios trägt seine Rolle, kein Terminal | ✓ bestanden |
| T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Claude Code öffnen“ starten nie ein echtes Programm | ✓ bestanden |
| TV-Vollbild: kleines Fenster „Verkleinern“ beim Start und bei Mausbewegung, ein Klick verkleinert | ✓ bestanden |
| Viele Studiofenster: das erste bleibt, Hinweis statt stillem Schließen | ✓ bestanden |
| W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck | ✓ bestanden |
| W0B-02/03 (UI): Rechtsklick-Menü öffnet Einstellungen; Hintergrund wählen, Farbe passend zum Hintergrund | ✓ bestanden |
| W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md) | ✓ bestanden |
| W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert | ✓ bestanden |
| W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert | ✓ bestanden |
