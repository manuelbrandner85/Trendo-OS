# Sauberer Nachbau – Testplan

Studio: Programme nachbauen (X-10) · Bereich: Entwickeln · Seite: `nachbau` · Stand: 0.9.112 (Stand d6f9fcd)

15 Tests sprechen dieses Programm an.

- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0711 Sauberer Nachbau: Beschreibung – Rückgängig, Notsicherung, Karte, Zurückholen; Speichern (BESCHREIBUNG.md, atomar) räumt weg
- T-0762 B3 (UI): Abgleich sperrt den Knopf, zeigt „Abbrechen“, schreibt nach Projektwechsel in die richtige Liste; Abbrechen beendet den Lauf (übersprungen ohne Python)
- T-0762 B4 (UI): beschädigte Anforderungsliste wird angezeigt statt still überschrieben
- T-0762 N5 (UI): Abgleich A → B → A – Bearbeitungen in A während des Laufs bleiben erhalten, nur der Status kommt hinzu (übersprungen ohne Python)
- T-0762 X-10 (UI): Anforderungen in der Tabelle anlegen und bearbeiten, Rückgängig, Test verknüpfen, Abgleich mit echtem Python-Testlauf, Export .md, Treffer nebeneinander
- T-0762 X-10: Abgleich mit echtem Python-Testlauf in „2-Umsetzung“ (übersprungen ohne Python)
- T-0762 X-10: Abgleich – kein Test = offen, Datei ohne Testname = teilweise, Test bestanden = erfüllt, fehlgeschlagen = offen, Pfad außerhalb = offen
- T-0762 X-10: Anforderungsliste – Anlegen mit Herkunft und Datum, Bearbeiten, Rückgängig, sicher gespeichert, Export als Markdown-Tabelle
- T-0762 X-10: Prüfbericht nennt Herkunft und Datum jedes Eintrags (Anforderungen, Tagebuch, Ähnlichkeitstreffer)
- T-0762 X-10: Vergleich nebeneinander markiert die gleichen Zeilen trotz Umbenennen, eigener Code bleibt unmarkiert
- X-10 (UI): Projekt anlegen, Tagebuch-Eintrag, Prüfung ohne Treffer schreibt Bericht
- X-10: Projekt mit Rollen-Ordnern, Tagebuch, Vergleich zweier Ordner und Prüfbericht
- X-10: Sauberer Nachbau – gleicher Code hat Ähnlichkeit 1, eigener Code mit anderer Struktur niedrig; Vergleich meldet nur Dateien über der Schwelle
- X-10: Ähnlichkeit erkennt kopierten Code trotz Umbenennen, eigener Code bleibt unauffällig
