# Tagesplaner – Testplan

Studio: Tagesplaner (STUDIO-DAYPLAN-01) · Bereich: Arbeiten & Büro · Seite: `tagesplaner-programm` · Stand: 0.9.110 (Stand 0eb9d52)

15 Tests sprechen dieses Programm an.

- T-0707 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ändert in drei Programmen das offene Dokument, Datei auf der Platte, Rückgängig (nur mit TRENDO_ASSIST_ECHT)
- T-0707: Drei Programme aus drei Fächern – Auftrag → KI-Plan → Vorschau → Ja → ein Rückgängig-Schritt im offenen Dokument → echte Datei → Rückgängig (KI ersetzt)
- T-0707: KI liefert nicht reparierbares JSON – die Kette fragt einmal nach und arbeitet mit der Korrektur weiter
- T-0707: KI-Befehl im Menüband („Weiterschreiben mit Claude“) ohne Claude – vorher still nichts, jetzt dieselbe Kette ins offene Dokument
- T-0707: Programme außerhalb der Hülle (Text, Tabelle, Geld …) – einheitlich „Noch nicht im Dokument möglich“, Liste aus dem Katalog
- T-0707: Rechte in der Kette – beim Not-Stopp ändert die KI nichts; Claude-Brücke prüft mit derselben Regel (Text bleibt Text)
- T-0719 Karten: GPX im Garmin-Aufbau auf die Karte ziehen – jede Spur/Route einzeln, ein Rückgängig-Schritt; alte Karten-Datei öffnen und bearbeiten; Assistenzweg schreibt einen Wegpunkt ins Dokument
- T-0719 Karten: leere Karte → erster Schritt → Route mit der Maus, Punkt ziehen, Punkt aus der Mitte einfügen, Rechtsklick löschen, Wegpunkt setzen/ziehen/umbenennen, Rückgängig je Schritt, GPX-Datei
- T-0719 Tagesplaner: alte Datei (Plan mit Termin-Kopien) öffnen – Zeitleiste zeigt alles, Termin ziehen hält Plan und Termin gleich; Assistenzweg schreibt einen Block, der in der Zeitleiste steht
- T-0719 Tagesplaner: leerer Tag → erster Block, im Block benennen, ziehen (verschieben), Kante ziehen (Dauer), Doppelklick neu, Rechtsklick Termin/länger, Pfeiltaste, Entf, Rückgängig je Schritt, Kalenderdatei mit Ical.Net geprüft
- T-0728 C3: Claude ändert über die Brücke in drei Zügen – am Ende des Laufs ist es EIN Rückgängig-Schritt „Claude-Auftrag“; Strg+Z nimmt alles zurück (Claude selbst ersetzt durch die Werkzeugaufrufe)
- T-0730 Tagesplaner: beim Umbenennen im Block verschieben Pfeil hoch/runter den Block nicht, Entf/F2 wirken nur im Textfeld, Enter übernimmt den Namen
- T-0730 Vektor: Strg+G und „Gruppieren“ mit nur einer Form werfen nicht, ändern nichts und geben einen Hinweis; zwei Formen werden gruppiert
- Tagesplaner: Beispieltag planen, Pause und Morgen-Liste, abhaken, Kalenderdatei, Termine und Aufgaben aus dem Kalender, auf morgen schieben
- V-01: der kurze Hinweis („Vorlage … geöffnet.“) steht in der Kopfzeile; lange Ergebnisse und Fehler bleiben eine Karte; „?“ zeigt die Hilfe; ab zwei Dokumenten erscheinen die Reiter
