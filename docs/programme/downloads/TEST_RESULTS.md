# Downloads – Testergebnisse

Studio: Surfen & Herunterladen (SYS-03) · Bereich: System & Sicherheit · Seite: `downloads` · Stand: 0.9.36 (Stand d04e032)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 44 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste | ✓ bestanden |
| Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt | ✓ bestanden |
| Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert | ✓ bestanden |
| Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert | ✓ bestanden |
| Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung | ✓ bestanden |
| Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml | ✓ bestanden |
| Download-Manager: mehrere Links auf einmal, höchstens N gleichzeitig, Fortschritt, Anhalten/Fortsetzen, Prüfsumme falsch → Fehler; ohne Netz (nachgebildet) | ✓ bestanden |
| Fahrtenbuch (T-0311): Fahrt korrigieren mit Grund, Fahrt stornieren mit Grund, Monat festschreiben – danach keine Änderung mehr möglich | ✓ bestanden |
| Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert | ✓ bestanden |
| LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED | ✓ bestanden |
| LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte | ✓ bestanden |
| Lernplan: Plan mit Prüfungstermin anlegen → Tagesplan mit Ziel, Kalender mit Prüfungstag; „Jetzt lernen“ lernt in Plan-Reihenfolge und hält das Tempo | ✓ bestanden |
| QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet | ✓ bestanden |
| QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet | ✓ bestanden |
| QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben | ✓ bestanden |
| QA-017: Navigationsschaltflächen haben zugängliche Namen | ✓ bestanden |
| QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur | ✓ bestanden |
| QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert | ✓ bestanden |
| QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen | ✓ bestanden |
| STUDIO-LETTER-01: Schnellzugriff „Neuer Brief“ → Kündigung → ausfüllen → prüfen → speichern; Absender gemerkt | ✓ bestanden |
| SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter | ✓ bestanden |
| SYS-03 (UI): Tabs; Prüfsumme rot/grün; Link-Warnung; Suchmaschine wirkt in Strg+K; Webnotiz in der Suche | ✓ bestanden |
| SYS-03-P06: Prüfsumme – Algorithmus erkannt, Abweichung gemeldet | ✓ bestanden |
| SYS-03-P07: Download abbrechen und fortsetzen ergibt dieselbe Prüfsumme (lokaler Testserver) | ✓ bestanden |
| SYS-03-P07: Fortsetzen-Zustand nur für dieselbe Adresse | ✓ bestanden |
| SYS-03-P08: Lesezeichen – Chrome-HTML-Import mit Ordnern, Export/Import verlustfrei | ✓ bestanden |
| SYS-03-P09/P22: Lesemodus ohne Skripte; Offline-Kopie ohne Skripte, mit Basis-Adresse | ✓ bestanden |
| SYS-03-P10/P11/P12/P25: Tor nur offiziell, Privat-Parameter je Browser, Standardbrowser lesen, Verlauf-Anleitung | ✓ bestanden |
| SYS-03-P13: Suchmaschine gespeichert, Standard DuckDuckGo, wirkt auf die Suchadresse | ✓ bestanden |
| SYS-03-P14: PDF-Befehl für Chrome/Edge (A4-Prüfung über pdfinfo) | ✓ bestanden |
| SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da) | übersprungen |
| SYS-03-P15/P16: Echte Adressen bleiben ohne Warnung; Ziel wird erklärt | ✓ bestanden |
| SYS-03-P15/P16: Warnzeichen bei Phishing-Adressen | nicht im Lauf |
| SYS-03-P17: Formular-Hinweise (IBAN, Karte, Passwort, http) – nie Absenden | ✓ bestanden |
| SYS-03-P18/P19/P20/P21/P24: Downloads sortieren + rückgängig, Quarantäne, Defender-Auswertung, Webnotiz, Proxy | ✓ bestanden |
| SYS-03: Adressen werden sicher normalisiert (nur http/https, sonst Suche oder Ablehnung) | nicht im Lauf |
| SYS-03: Browser werden über bekannte Pfade gefunden; riskante und unfertige Downloads erkannt | ✓ bestanden |
| SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt | ✓ bestanden |
| SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen | ✓ bestanden |
| SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link | ✓ bestanden |
| Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts | ✓ bestanden |
| W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck | ✓ bestanden |
| W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md) | ✓ bestanden |
| W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert | ✓ bestanden |
| W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert | ✓ bestanden |
