# Zusatzprogramme – Testergebnisse

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `components` · Stand: 0.9.36 (Stand 6cf88c3)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 53 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste | ✓ bestanden |
| Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt | ✓ bestanden |
| Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert | ✓ bestanden |
| Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert | ✓ bestanden |
| Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung | ✓ bestanden |
| Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml | ✓ bestanden |
| Einrichtung: Ergebnis bleibt stehen (mit fehlgeschlagenen Paketen), schließt erst nach „Los geht's“ | ✓ bestanden |
| Einrichtung: „Abbrechen“ beendet die Installation und schließt den Bildschirm; danach kein Ergebnisbildschirm | ✓ bestanden |
| GitHub (UI): Treffer mit Beschreibung, „Zustimmen & installieren“ richtet ein, Programmseite zeigt Starten/Entfernen | ✓ bestanden |
| KI-Fortschritt: laufende KI-Aufgabe erscheint oben mit Bezeichnung, Phase und Abbrechen; danach verschwindet sie | ✓ bestanden |
| KI-Quellen und KI-Bibliothek: ohne laufendes Ollama ein Hinweis mit nächstem Schritt, gesperrte Knöpfe sagen warum | ✓ bestanden |
| Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert | ✓ bestanden |
| LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED | ✓ bestanden |
| LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte | ✓ bestanden |
| Programme (UI): mitgelieferte Programme haben eine Beschreibung mit Funktionsweise | ✓ bestanden |
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
| SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen | ✓ bestanden |
| SYS-11-P07: Portable-ZIP nur mit richtiger Prüfsumme entpacken | ✓ bestanden |
| SYS-11-P10: Verknüpfungen mit Parametern (Anführungszeichen) speichern und entfernen | ✓ bestanden |
| SYS-11-P11: Autostart lesen; ein/aus nur mit Bestätigung, nichts gelöscht | ✓ bestanden |
| SYS-11-P14: Offline-Installer – Prüfsumme aus .sha256 und SHA256SUMS, falsche/fehlende erkannt | ✓ bestanden |
| SYS-11-P15/P20: Systemvoraussetzungen blockieren mit Grund; Kompatibilitätshinweise | ✓ bestanden |
| SYS-11-P18: Zurücksetzen legt Sicherung an und lässt sich wiederherstellen | ✓ bestanden |
| SYS-11-P19/P06: Programmnamen zu Zuordnungen; Update-Text | ✓ bestanden |
| SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler | ✓ bestanden |
| SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise) | ✓ bestanden |
| SYS-11-P24/P25: Platz je Werkzeug; Selbsttest-Bericht ohne private Pfade | ✓ bestanden |
| SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen | ✓ bestanden |
| SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link | ✓ bestanden |
| Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts | ✓ bestanden |
| T-0313: Abgebrochener Download – angefangene Datei bleibt, nächster Versuch lädt mit Range ab der Stelle weiter (206) und richtet ein | ✓ bestanden |
| T-0313: GitHub-Anmeldung – Schlüssel wird geprüft und im sicheren Speicher abgelegt, geht nur an api.github.com, Abmelden löscht ihn; falscher Schlüssel abgewiesen | ✓ bestanden |
| T-0313: Trefferliste zeigt „✓ Windows-Programm · Version · Größe“ bzw. „Nur Quellcode“; Installieren nur, wo es ein Windows-Programm gibt | ✓ bestanden |
| T-0313: Update – neue Version wird erkannt (Versionsvergleich), „Aktualisieren“ richtet sie ein, danach kein Update mehr | ✓ bestanden |
| T-0314: Fehlgeschlagener Schritt → klare Meldung mit letzter Ausgabe, nichts eingetragen, alter Stand bleibt; Rust-Projekt zeigt Grund und findet Fork mit Windows-Programm und PyPI-Paket | ✓ bestanden |
| T-0314: Python-Projekt ohne Windows-Programm → Abzeichen „läuft in Trendo“, Zustimmung, Einrichtung mit uv (sichtbare Schritte), Start im Fenster, Entfernen | ✓ bestanden |
| T-0314: Reine Web-Seite läuft ohne Laufzeit über Trendos lokalen Server (nur 127.0.0.1, nur der Projektordner) und lässt sich beenden | ✓ bestanden |
| W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck | ✓ bestanden |
| W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md) | ✓ bestanden |
| W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert | ✓ bestanden |
| W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert | ✓ bestanden |
| Zusatzprogramme: Vollpaket-Karte mit Ordnerwahl; falscher Ordner wird erklärt | ✓ bestanden |
