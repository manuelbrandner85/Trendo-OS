# Apps – Testplan

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `apps` · Stand: 0.9.36 (Stand 3c05eb0)

41 Tests sprechen dieses Programm an.

- CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste
- Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt
- Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert
- Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert
- Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung
- Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml
- Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert
- LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED
- LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte
- QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet
- QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet
- QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben
- QA-017: Navigationsschaltflächen haben zugängliche Namen
- QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur
- QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert
- QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen
- STUDIO-LETTER-01: Schnellzugriff „Neuer Brief“ → Kündigung → ausfüllen → prüfen → speichern; Absender gemerkt
- SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter
- SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt
- SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen
- SYS-11-P07: Portable-ZIP nur mit richtiger Prüfsumme entpacken
- SYS-11-P10: Verknüpfungen mit Parametern (Anführungszeichen) speichern und entfernen
- SYS-11-P11: Autostart lesen; ein/aus nur mit Bestätigung, nichts gelöscht
- SYS-11-P14: Offline-Installer – Prüfsumme aus .sha256 und SHA256SUMS, falsche/fehlende erkannt
- SYS-11-P15/P20: Systemvoraussetzungen blockieren mit Grund; Kompatibilitätshinweise
- SYS-11-P18: Zurücksetzen legt Sicherung an und lässt sich wiederherstellen
- SYS-11-P19/P06: Programmnamen zu Zuordnungen; Update-Text
- SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler
- SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise)
- SYS-11-P24/P25: Platz je Werkzeug; Selbsttest-Bericht ohne private Pfade
- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe
- Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit
- Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link
- Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts
- Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog
- StudioCatalog.NameOf: Katalogname, sonst der bisherige Text
- W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck
- W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md)
- W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert
- W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert
