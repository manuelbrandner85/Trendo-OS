# Mathe-Helfer – Testergebnisse

Studio: Mathe-Helfer (STUDIO-MATH-01) · Bereich: Wissen & Lernen · Seite: `mathe-helfer-programm` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 11 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| T-0781 ECHT: lokale KI (Ollama) übersetzt Textaufgaben – Mathe-Helfer (Rechteck) und Naturwissenschaft (Glucose), gerechnet vom Kern, Rückgängig (nur mit TRENDO_T0781_ECHT) | übersprungen |
| T-0781 FREMD: Aufgabenblatt aus LibreOffice (Text) über die Oberfläche einlesen, rechnen, als PDF/Markdown exportieren; Laborheft als CSV/PDF – Dateien für die Gegenprobe im Fremdprogramm (nur mit TRENDO_T0781_FREMD) | übersprungen |
| T-0781 Gegenprüfung (Einfrieren): Riesenaufgabe hält die Oberfläche nicht an – klare Meldung, nichts angelegt; unsichere Probe wird gelb („?“) angezeigt | ✓ bestanden |
| T-0781 Gegenprüfung (MITTEL): fertige Ergebnisse wie „x + 0 = 5“ und „1x = 5“ lehnt die Sperre ab; Löschen/Kopieren sind für die KI gesperrt (KI ersetzt) | ✓ bestanden |
| T-0781 Mathe-Helfer: Absturz vor dem Speichern – die automatische Sicherung (20-s-Takt der Hülle) bringt die Rechnungen zurück | ✓ bestanden |
| T-0781 Mathe-Helfer: Aufgabenblatt aus einer Fremddatei (Text) über „Aufgabenblatt öffnen …“ – jede Zeile gerechnet, Unverständliches übergangen, Datei unverändert | ✓ bestanden |
| T-0781 Mathe-Helfer: Fehler verständlich, nichts wird angelegt; Entf und Rechtsklick „Kopieren“ wirken | ✓ bestanden |
| T-0781 Mathe-Helfer: KI übersetzt eine Textaufgabe – Vorschau ändert nichts, „Ja, behalten“ legt die vom Kern gerechnete Rechnung an (ein Rückgängig-Schritt), Rückgängig nimmt sie heraus (KI ersetzt) | ✓ bestanden |
| T-0781 Mathe-Helfer: die KI darf kein Ergebnis liefern – „x = 5“ und direkt gesetzte Rechnungen werden abgelehnt, das Dokument bleibt unverändert (KI ersetzt) | ✓ bestanden |
| T-0781 Mathe-Helfer: leeres Rechenblatt → Aufgabe tippen → „Rechnen“ → Ergebnis, Lösungsweg, Probe und Graph; Rückgängig; Export PDF, LaTeX, Markdown | ✓ bestanden |
| T-0781 Naturwissenschaft: KI übersetzt „Wie lange fällt ein Stein aus 20 m?“ in die Formel – gerechnet vom Programm, ein Rückgängig-Schritt; Faktoren der KI werden abgelehnt (KI ersetzt) | ✓ bestanden |
| T-0781 Naturwissenschaft: Periodensystem anklicken → Steckbrief; Reaktion, Molmasse, Physik und Einheiten → „Ins Laborheft“; Export PDF und CSV | ✓ bestanden |
| T-0781 Studios eingebunden: Katalog (Wissen & Lernen), Symbol, Profil, Team, Fähigkeiten, KI-Fachaktion, Bild | ✓ bestanden |
