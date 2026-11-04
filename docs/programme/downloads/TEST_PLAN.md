# Downloads – Testplan

Studio: Surfen & Herunterladen (SYS-03) · Bereich: System & Sicherheit · Seite: `downloads` · Stand: 0.9.36 (Stand ab225a6)

47 Tests sprechen dieses Programm an.

- CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste
- Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt
- Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert
- Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert
- Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung
- Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml
- Download-Manager: mehrere Links auf einmal, höchstens N gleichzeitig, Fortschritt, Anhalten/Fortsetzen, Prüfsumme falsch → Fehler; ohne Netz (nachgebildet)
- Fahrtenbuch (T-0311): Fahrt korrigieren mit Grund, Fahrt stornieren mit Grund, Monat festschreiben – danach keine Änderung mehr möglich
- Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert
- LETTER-UI: Briefkopf → Akte (Behörde) → Fakt, Chronik, Eingang → Frist UNGEPRÜFT → Quelle fehlt → Export gesperrt → beheben → DOCX → Freigabe → Versand BLOCKED
- LETTER-UI: Briefstudio hat genau eine Unterebene; Akte/Ausgabe erst mit offener Akte
- Lernplan: Plan mit Prüfungstermin anlegen → Tagesplan mit Ziel, Kalender mit Prüfungstag; „Jetzt lernen“ lernt in Plan-Reihenfolge und hält das Tempo
- QA-004 UI: Browser-Aktion abgelehnt → nichts geöffnet
- QA-004 UI: Browser-Aktion freigegeben → genau einmal geöffnet
- QA-004 UI: Explorer-Aktion → Freigabe-Ansicht; Ausführung erst nach Klick auf Freigeben
- QA-017: Navigationsschaltflächen haben zugängliche Namen
- QA-017: Strg+1..5 schaltet alle Hauptbereiche per Tastatur
- QA-017: Tab bewegt Fokus durch die Navigation, Enter aktiviert
- QA-019 (simuliert): Offline-Modus – Browser-Aktion deaktiviert, lokale Funktionen laufen
- STUDIO-LETTER-01: Schnellzugriff „Neuer Brief“ → Kündigung → ausfüllen → prüfen → speichern; Absender gemerkt
- SYS-02: Notiz-Schnelltool öffnet Werkzeuge; Notiz wird gespeichert; Rechner rechnet bei Enter
- SYS-03 (UI): Tabs; Prüfsumme rot/grün; Link-Warnung; Suchmaschine wirkt in Strg+K; Webnotiz in der Suche
- SYS-03-P06: Prüfsumme – Algorithmus erkannt, Abweichung gemeldet
- SYS-03-P07: Download abbrechen und fortsetzen ergibt dieselbe Prüfsumme (lokaler Testserver)
- SYS-03-P07: Fortsetzen-Zustand nur für dieselbe Adresse
- SYS-03-P08: Lesezeichen – Chrome-HTML-Import mit Ordnern, Export/Import verlustfrei
- SYS-03-P09/P22: Lesemodus ohne Skripte; Offline-Kopie ohne Skripte, mit Basis-Adresse
- SYS-03-P10/P11/P12/P25: Tor nur offiziell, Privat-Parameter je Browser, Standardbrowser lesen, Verlauf-Anleitung
- SYS-03-P13: Suchmaschine gespeichert, Standard DuckDuckGo, wirkt auf die Suchadresse
- SYS-03-P14: PDF-Befehl für Chrome/Edge (A4-Prüfung über pdfinfo)
- SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da)
- SYS-03-P15/P16: Echte Adressen bleiben ohne Warnung; Ziel wird erklärt
- SYS-03-P15/P16: Warnzeichen bei Phishing-Adressen
- SYS-03-P17: Formular-Hinweise (IBAN, Karte, Passwort, http) – nie Absenden
- SYS-03-P18/P19/P20/P21/P24: Downloads sortieren + rückgängig, Quarantäne, Defender-Auswertung, Webnotiz, Proxy
- SYS-03: Adressen werden sicher normalisiert (nur http/https, sonst Suche oder Ablehnung)
- SYS-03: Browser werden über bekannte Pfade gefunden; riskante und unfertige Downloads erkannt
- SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt
- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe
- Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit
- Studio-Klick: Unreal nur geführt – kein Installieren-Knopf, offizieller Installer-Link
- Studio-Klick: fehlendes Programm → genau eine Frage 'Studio braucht X. Jetzt installieren?', 'Später' installiert nichts
- W0-01/03/06: Assistent – Name, Ordner, Design, Pakete, Installation über Zustimmung, Vorlagen, Systemcheck
- W0b-01: Keine Fachwörter in Oberflächentexten (docs/SPRACHE.md)
- W0b-02/04/05: Widgets ausblenden, Dock sortieren, Bewegung reduzieren, Hinweis einmalig – alles gespeichert
- W0b-06: Design wechseln (Hell/Kontrast) baut das Fenster neu auf und speichert
