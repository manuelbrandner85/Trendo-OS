# Zeitleisten (STUDIO-TIMELINE-01) – Anforderungen (Clean-Room)

Auftrag T-0780, Freigabe des Nutzers vom 26.11.2026. Grundlage sind nur öffentliche Angaben (Dokumentation, Produktseiten, Vergleiche). Kein Download, kein fremder Code, keine fremden Grafiken. Die Spaltennamen der TimelineJS-Tabelle (Year, Month, Day, Headline, Group …) werden nur zum Einlesen fremder Dateien erkannt (Austauschformat).

## Vorbilder (Recherche 26.11.2026)

| Vorbild | Was es auszeichnet | Typische Funktionen | Beobachtbar über | Quelle (abgerufen 26.11.2026) |
|---|---|---|---|---|
| TimelineJS (Knight Lab) | Erzählende Zeitleiste aus einer Tabelle, Medien je Ereignis | Tabelle mit Jahr/Monat/Tag, Gruppen, Bilder; negative Jahre = v. Chr. (Beschriftung „BCE“) | offizielle Doku/FAQ | timeline.knightlab.com/docs/faq.html; knightlab.zendesk.com/hc/en-us/articles/218289103 |
| Preceden | Web-Editor mit Spuren („Layers“) und Zeiträumen | Ereignisse und Zeiträume, Ebenen, Zoom, Export als Bild/PDF | Produktseite (Herstellervergleich, werblich) | preceden.com/timeline-js-alternative; preceden.com/aeon-timeline-alternative |
| Aeon Timeline | Desktop-Programm für datenreiche Zeitleisten (Autoren, Projekte) | Ereignisse, Zeiträume, Beziehungen, eigene Kalender | Produktseite, Vergleiche | preceden.com/aeon-timeline-alternative; wifitalents.com/best/historical-timeline-software |
| TimeToast | Einfacher Web-Zeitstrahl, gute Bewertung | Ereignisse mit Bild, Zeiträume | Vergleich | wifitalents.com/best/historical-timeline-software |

**Ehrlich:** Für Preceden und Aeon fanden sich keine öffentlichen Angaben zur Eingabe von Daten vor Christus; die Vergleichsseiten stammen teils vom Hersteller selbst.

Empfehlung (umgesetzt): Spuren und Zeiträume wie Preceden/Aeon, Austausch über die TimelineJS-Tabelle, dazu eine echte Behandlung von „v. Chr.“ und ungefähren Daten.

## Offene Alternativen (Lizenz)

| Projekt | Lizenz | Verwendung in Trendo |
|---|---|---|
| TimelineJS3 | MPL-2.0 (laut Knight-Lab-Lizenzseite timeline.knightlab.com/docs/license.html) | nur Vorbild und Tabellenformat, kein Code |
| vis-timeline | Apache-2.0 / MIT (Projektangabe, nicht erneut geprüft) | nicht verwendet |

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| Z-01 | Ereignisse | Datum + Titel + Beschreibung, Spur wählbar | `WissenStudios_Ui.Timeline_click_to_files` | alle Vorbilder | 1 |
| Z-02 | Zeiträume/Epochen | Beginn–Ende als Balken, Überlappende in eigenen Reihen | `Timeline_click_to_files`, `WissenStudiosTests.Layout_no_overlap` | Preceden/Aeon | 1 |
| Z-03 | Mehrere Spuren | Spuren anlegen, umbenennen, färben, entfernen (Einträge wandern) | `Timeline_click_to_files` (Spur, Zuordnung) | Preceden Layers | 1 |
| Z-04 | Daten vor Christus | „44 v. Chr.“, „-753“, „500 BC“, kein Jahr 0, Zeitenwende richtig | `WissenStudiosTests.Dates`, `Positions`, `Bad_dates` | TimelineJS negative Jahre | 1 |
| Z-05 | Ungefähre/grobe Daten | „ca. 1200“, „um 800“, „1990er“, „3. Jh. v. Chr.“, „2. Jt. v. Chr.“ – gestrichelt und mit „ca.“ dargestellt | `Dates`, Bild geprüft | Geschichtsunterricht | 1 |
| Z-06 | Zoom | Mausrad um die Mausstelle, + / −, „Alles zeigen“ | `Timeline_mouse` (Mausrad) | alle Vorbilder | 1 |
| Z-07 | Verschieben | Ziehen auf freier Fläche, ← / → | `Timeline_mouse` | alle Vorbilder | 1 |
| Z-08 | Eintrag in der Zeit ziehen | Ziehen ändert das Datum (gerundet nach Genauigkeit), Rückgängig | `Timeline_mouse` (100 Jahre, ±5) | Preceden/Aeon | 1 |
| Z-09 | Bilder an Ereignissen | Bild wählen oder auf die Zeitleiste ziehen; verkleinert eingebettet; in SVG/PNG/PDF sichtbar | `Timeline_import_image_lines` | TimelineJS Media | 1 |
| Z-10 | Rechtsklick, Entf | Menü (Bild, Verdoppeln, Alles zeigen, Löschen), Entf löscht | `Timeline_mouse` | übliche Bedienung | 1 |
| Z-11 | Export SVG (Inkscape) | gültiges SVG, Inkscape liest alle Texte und rendert PNG | `Svg_pdf`, `T0780_Fremdnachlauf.Zeitleiste_svg_in_Inkscape` (echt) | Auftrag | 1 |
| Z-12 | Export PNG | doppelte Auflösung, dieselben Bausteine wie am Bildschirm | `Timeline_click_to_files` | Auftrag | 1 |
| Z-13 | Export PDF | eine Seite, echter Text (pdftotext liest ihn) | `Svg_pdf`, Fremdnachlauf (pdftotext) | Auftrag | 1 |
| Z-14 | CSV ein/aus | Semikolon, UTF-8 mit BOM, Rundreise; LibreOffice Calc liest sie | `Csv_json_roundtrip`, Fremdnachlauf (Calc) | Auftrag | 1 |
| Z-15 | JSON ein/aus | eigene Form mit Bildern; TimelineJS-JSON einlesbar | `Csv_json_roundtrip`, `TimelineJs_import` | TimelineJS | 1 |
| Z-16 | TimelineJS-Tabelle einlesen | Year/Month/Day/End…/Headline/Text/Group, Titelzeile | `TimelineJs_import`, `Timeline_import_image_lines`, Fremdnachlauf | TimelineJS | 1 |
| Z-17 | Zeilen aus Text | „Datum \| Titel \| Spur \| Beschreibung“, Zeiträume „1618 – 1648“, Unlesbares gemeldet | `Lines`, `Timeline_import_image_lines` | – | 1 |
| Z-18 | KI: Ereignisse aus Text | Vorschau ohne Änderung, „Ja, behalten“, ein Rückgängig-Schritt; unlesbare Daten abgelehnt | `Timeline_ki_events`, Echtlauf `Real_local_ki` | Auftrag | 1 |
| Z-19 | Lesbarkeit | Beschriftungen überdecken sich nicht, rechts nicht abgeschnitten | `Layout_no_overlap`, Bild geprüft | – | 1 |
| Z-20 | Eigene Kalender, Beziehungen zwischen Einträgen | wie Aeon | – | Aeon | 2 (fehlt) |
| Z-21 | Erzählansicht (Folie je Ereignis) | wie TimelineJS | – | TimelineJS | 2 (fehlt) |
