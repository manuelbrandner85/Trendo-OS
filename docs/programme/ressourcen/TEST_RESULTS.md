# Speicher & Leistung – Testergebnisse

Studio: Leistung anzeigen (X-09) · Bereich: System & Sicherheit · Seite: `ressourcen` · Stand: 0.9.36 (Stand 2e53f9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 32 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+K fokussiert die Suche | ✓ bestanden |
| Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt | ✓ bestanden |
| Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert | ✓ bestanden |
| Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert | ✓ bestanden |
| Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung | ✓ bestanden |
| Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml | ✓ bestanden |
| Kachelkarte: Flächen proportional, lückenlos in der Fläche, Einlesen zählt Größen, Entfernen zieht Eltern nach | ✓ bestanden |
| Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert | ✓ bestanden |
| LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED | ✓ bestanden |
| LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte | ✓ bestanden |
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
| SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| Speicherplatz-Karte UI: Ordner einlesen, Kachel anklicken wählt, zweiter Klick geht hinein, Papierkorb erst nach Ja | ✓ bestanden |
| Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link | ✓ bestanden |
| Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts | ✓ bestanden |
| Task-Manager: Reiter Leistung mit Kurven, Prozesse sortierbar und durchsuchbar, Beenden nur für eigenes Trendo-Werkzeug nach Rückfrage | ✓ bestanden |
| W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck | ✓ bestanden |
| W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md) | ✓ bestanden |
| W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert | ✓ bestanden |
| W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert | ✓ bestanden |
| X-09: Prozesse werden nach Namen gruppiert, nach Speicher sortiert und Studios zugeordnet | ✓ bestanden |
