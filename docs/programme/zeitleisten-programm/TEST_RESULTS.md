# Zeitleisten – Testergebnisse

Studio: Zeitleisten (STUDIO-TIMELINE-01) · Bereich: Wissen & Lernen · Seite: `zeitleisten-programm` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 13 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| T-0780 ECHT: lokale KI (Ollama) – Referate „Gliederung“ und „Abschnitt“, Zeitleisten „Ereignisse aus Text“ im offenen Programm, Rückgängig (nur mit TRENDO_T0780_ECHT) | übersprungen |
| T-0780 H4: ungültige Farbe im Modell – Zeichenfläche und PNG-Ausgabe brechen nicht ab, Standardfarbe statt Fehler | ✓ bestanden |
| T-0780 M1: JSON-Datei mit Nicht-Bild als „image“ einlesen – Bild weggelassen und gemeldet, echtes Bild neu kodiert (JPEG) | ✓ bestanden |
| T-0780 M2 (2. Gegenprüfung): unechtes PNG mit gültigem Kopf – Import verwirft und meldet es, PNG-Export und Zeichenfläche laufen weiter | ✓ bestanden |
| T-0780 Referate (UI): Abschnitt mit der Maus in der Gliederung verschieben, Rechtsklick-Menü, Rückgängig; Verweis ohne Quelle sperrt die Ausgabe sichtbar | ✓ bestanden |
| T-0780 Referate (UI): Quellen aus „Recherche mit Quellen“ übernehmen (offenes Dokument) und RIS aus Zotero einlesen – Kennungen bleiben, Doppelte nicht doppelt | ✓ bestanden |
| T-0780 Referate (UI): Stufe Einfach – eine goldene Hauptaktion, Kette und Gliederung sichtbar, Thema auch ohne rechte Spalte eintragbar | ✓ bestanden |
| T-0780 Referate (UI): leere Vorlage → Thema → Gliederung → Text → Quelle → Zitat per Klick → Word, PDF, ODT, PPTX, ODP als echte Dateien | ✓ bestanden |
| T-0780 Referate KI: „Abschnitt schreiben“ mit Zitat aus der Liste kommt ins Dokument; erfundene Quelle ([@neu] oder „(Müller, 2019)“) und neue Quellen per „dokument.setzen“ werden abgelehnt (KI ersetzt) | ✓ bestanden |
| T-0780 Referate KI: „Gliederung“ – Vorschau ändert nichts, „Ja, behalten“ schreibt die Gliederung ins offene Dokument als EIN Rückgängig-Schritt, Rückgängig stellt alles her (KI ersetzt) | ✓ bestanden |
| T-0780 Zeitleisten (UI): Eintrag mit der Maus in der Zeit verschieben, Mausrad zoomt, Ziehen auf freier Fläche verschiebt die Ansicht, Rechtsklick-Menü, Entf löscht, Rückgängig | ✓ bestanden |
| T-0780 Zeitleisten (UI): TimelineJS-Tabelle einlesen, Bild an ein Ereignis hängen (verkleinert, in SVG eingebettet), Zeilen aus Text einfügen | ✓ bestanden |
| T-0780 Zeitleisten (UI): leere Vorlage → erstes Ereignis (44 v. Chr.) → Zeitraum → Spur → SVG, PNG, PDF, CSV, JSON als echte Dateien | ✓ bestanden |
| T-0780 Zeitleisten KI: „Ereignisse aus Text einfügen“ – Vorschau ändert nichts, „Ja, behalten“ fügt die Ereignisse als EIN Rückgängig-Schritt ein; unlesbare Daten werden abgelehnt (KI ersetzt) | ✓ bestanden |
