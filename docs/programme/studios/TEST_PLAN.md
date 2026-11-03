# Alle Studios – Testplan

Studio: Desktop einrichten (CORE-01) · Bereich: System & Sicherheit · Seite: `studios` · Stand: 0.9.36 (Stand a5ae41e)

44 Tests sprechen dieses Programm an.

- Alle Studios + Bereiche: jede Karte ist klickbar und führt aus der Liste heraus
- Alle Studios: Bereich und Suche filtern die Karten, Alle zeigt wieder alles
- Alle Studios: kurze deutsche Namen und ein Satz, nach Bereichen gruppiert, keine Kennungen oder Fachbegriffe sichtbar
- CORE-01: Dateisuche begrenzt Tiefe und Anzahl
- CORE-01: Suche findet mit Umlauten, Wortanfang und mehreren Wörtern; sortiert nach Treffergüte
- CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste
- Claude auch in der Studio-Übersicht, in der Suche und im Fokusmodus
- Claude im Menüband jedes Fachprogramms und Rechtsklick „✳ Claude fragen“ auf Elemente der Seite
- Claude kennt das Programm: offene Seite mit Feldern und Knöpfen geht mit; Claude bedient es erst nach „Ausführen“ – unbekannte Ziele fallen weg
- Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt
- Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert
- Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert
- Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung
- Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml
- Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar
- Jedes Studio mit Status IMPLEMENTED hat einen eigenen Bereich (keine Sackgasse)
- KI: die Seite lässt sich erneut öffnen, Status bleibt „Nicht verbunden“
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
- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe
- Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit
- Startseite: die neun bekannten Studios öffnen aus dem Startmenü per echtem Mausklick eine eigene Seite
- Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link
- Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts
- Symbole: alle Pfade sind gültige Avalonia-Geometrien mit Akzentfarben
- Symbole: jede Karte unter „Alle Studios“ und jeder Fach-Reiter zeigt sein Studio-Symbol
- Symbole: jedes Studio aus requirements.yaml hat ein eigenes, eindeutiges Symbol
- T-0107: Sparsam – keine Hintergrund-Unschärfe, keine Schatten und Effekte, keine Übergänge; Auswahl in den Einstellungen
- W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck
- W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md)
- W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert
- W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert
