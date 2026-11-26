# Sauberer Nachbau – Testergebnisse

Studio: Programme nachbauen (X-10) · Bereich: Entwickeln · Seite: `nachbau` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 15 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0711 Sauberer Nachbau: Beschreibung – Rückgängig, Notsicherung, Karte, Zurückholen; Speichern (BESCHREIBUNG.md, atomar) räumt weg | ✓ bestanden |
| T-0762 B3 (UI): Abgleich sperrt den Knopf, zeigt „Abbrechen“, schreibt nach Projektwechsel in die richtige Liste; Abbrechen beendet den Lauf (übersprungen ohne Python) | ✓ bestanden |
| T-0762 B4 (UI): beschädigte Anforderungsliste wird angezeigt statt still überschrieben | ✓ bestanden |
| T-0762 N5 (UI): Abgleich A → B → A – Bearbeitungen in A während des Laufs bleiben erhalten, nur der Status kommt hinzu (übersprungen ohne Python) | ✓ bestanden |
| T-0762 X-10 (UI): Anforderungen in der Tabelle anlegen und bearbeiten, Rückgängig, Test verknüpfen, Abgleich mit echtem Python-Testlauf, Export .md, Treffer nebeneinander | ✓ bestanden |
| T-0762 X-10: Abgleich mit echtem Python-Testlauf in „2-Umsetzung“ (übersprungen ohne Python) | ✓ bestanden |
| T-0762 X-10: Abgleich – kein Test = offen, Datei ohne Testname = teilweise, Test bestanden = erfüllt, fehlgeschlagen = offen, Pfad außerhalb = offen | ✓ bestanden |
| T-0762 X-10: Anforderungsliste – Anlegen mit Herkunft und Datum, Bearbeiten, Rückgängig, sicher gespeichert, Export als Markdown-Tabelle | ✓ bestanden |
| T-0762 X-10: Prüfbericht nennt Herkunft und Datum jedes Eintrags (Anforderungen, Tagebuch, Ähnlichkeitstreffer) | ✓ bestanden |
| T-0762 X-10: Vergleich nebeneinander markiert die gleichen Zeilen trotz Umbenennen, eigener Code bleibt unmarkiert | ✓ bestanden |
| X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung ohne Treffer schreibt Bericht | ✓ bestanden |
| X-10: Projekt mit Rollen-Ordnern, Tagebuch, Vergleich zweier Ordner und Prüfbericht | ✓ bestanden |
| X-10: Sauberer Nachbau – gleicher Code hat Ähnlichkeit 1, eigener Code mit anderer Struktur niedrig; Vergleich meldet nur Dateien über der Schwelle | ✓ bestanden |
| X-10: Ähnlichkeit erkennt kopierten Code trotz Umbenennen, eigener Code bleibt unauffällig | ✓ bestanden |
