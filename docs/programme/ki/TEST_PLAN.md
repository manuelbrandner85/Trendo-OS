# KI – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `ki` · Stand: 0.9.36 (Stand ccb1ad1)

43 Tests sprechen dieses Programm an.

- Alle Studios: Bereich und Suche filtern die Karten, Alle zeigt wieder alles
- CORE-01: Suchfeld + Enter zeigt Treffer, Klick öffnet die Seite; Strg+Umschalt+F fokussiert die Suche, Strg+K öffnet die Befehlsleiste
- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Design: Aktuelle Projekte – Leerzustand ohne Demodaten, danach echtes Projekt
- Design: Dock-Klick auf Dateien startet direkt (nur Owner) und wird auditiert; Browser offline deaktiviert
- Design: KI-Assistent zeigt 'Nicht verbunden', Eingabe deaktiviert
- Design: Systemstatus zeigt Messwert in % oder 'n. v.', nie Mockup-Werte ohne Messung
- Design: die neun bekannten Studios zeigen auf der Studios-Seite den echten Status aus requirements.yaml
- Einstellungen: linker Abschnitt schaltet um, Aussehen bleibt zuerst klickbar
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
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
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
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
