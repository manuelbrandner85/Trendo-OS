# Stammbaum (STUDIO-FAMILY-01) – Anforderungen (Clean-Room)

Auftrag T-0788, Freigabe des Nutzers vom 26.11.2026 (Etappe 0.9.115 „Wissen & Lernen E“). Grundlage sind nur öffentliche Angaben: Fachartikel, Vergleichsseiten, Blogberichte von Anwendern, die öffentliche Beschreibung des GEDCOM-Formats. Kein Download, keine Testversion, kein fremder Code, keine fremden Grafiken oder Texte. GEDCOM-Tags (INDI, FAM, BIRT …) sind Teil des offenen Austauschformats und werden nur zum Lesen und Schreiben dieses Formats verwendet.

## Vorbilder (Recherche 28.11.2026)

| Vorbild | Was es auszeichnet | Typische Funktionen | Beobachtbar über | Quelle (abgerufen 28.11.2026) |
|---|---|---|---|---|
| Family Tree Maker | laut Fachtest 2026 meistgenutztes Desktop-Programm | Personen, Familien, Ereignisse, Quellen, Baumansichten, Berichte, GEDCOM | Fachtest | techradar.com/best/genealogy-tools |
| RootsMagic | kostenlose Grundversion, starke Problemsuche | „Problem Search/Problem Alerts“: Geburt nach Tod des Vaters/der Mutter, Alter beim Tod (< 110), Heiratsalter (12–90), als „kein Problem“ markierbar | Anwenderberichte, Forum | geneamusings.com/2013/07/new-rootsmagic-6-feature-problem-alerts.html; geneamusings.com/2024/01/saturday-night-genealogy-fun-run.html; community.rootsmagic.com/t/parents-married-after-persons-birth/14498 |
| Legacy Family Tree | funktionsreich (Fachtest „für Funktionen“) | Quellen, Berichte, Prüfungen | Fachtest, Vergleich | techradar.com/best/genealogy-tools; en.wikipedia.org/wiki/Comparison_of_genealogy_software |
| Gramps (offen) | freie Genealogie-Software | GEDCOM-Export mit Schutz lebender Personen (nur Familienbeziehungen, ohne Geburt/Tod/Ereignisse), Prüfwerkzeug „Daten prüfen“ | Handbuch-Spiegel, Wikipedia, Changelog | en.wikipedia.org/wiki/Gramps_(software); linux.com (Track your ancestors with GRAMPS) |
| FamilySearch (Web) | Prüfregeln für Datenprobleme | z. B. „Geburt, bevor die Mutter Kinder haben konnte“ (Mutter unter 12) | Hilfe-Center | familysearch.org/en/help/helpcenter/article/what-are-all-the-possible-data-problems-in-family-tree |

Format: GEDCOM 5.5.1 (Fassung 15.11.2019, weiterhin das meistgenutzte Austauschformat; Zeilen höchstens 255 Byte einschließlich Zeilenende, CONC/CONT, Zeichensätze ANSEL, UTF-8, UNICODE, ASCII; Trennen nach einem Leerzeichen gilt als fehlerhaft) – Quellen: tamurajones.net/GEDCOM551AnnotatedEdition.xhtml, tamurajones.net/AWikiTreeGEDCOMProblem.xhtml, gedcom.io/gedcom551-errata/, wiki.genealogy.net/GEDCOM-Tags.

**Ehrlich:** Die Vergleichsseiten sind teils Sammellisten ohne eigenen Test. Für Legacy und Gramps fanden sich keine Fachtests 2026 mit Funktionsvergleich; die genauen Standardgrenzen der RootsMagic-Prüfungen für Elternalter ließen sich öffentlich nicht belegen.

Empfehlung (umgesetzt): Bedienung wie Family Tree Maker/RootsMagic (Personenliste links, Baum in der Mitte, Eigenschaften rechts), Prüfung wie RootsMagic „Problem Search“, Schutz Lebender wie Gramps, Austausch über GEDCOM 5.5.1.

## Offene Alternativen (Lizenz)

| Projekt | Lizenz | Verwendung in Trendo |
|---|---|---|
| Gramps | GPL-2.0-or-later (laut Wikipedia) | nur Vorbild für Bedienung und Funktionsumfang, kein Code (Clean-Room) |
| Ancestris | GPL (Projektangabe, nicht erneut geprüft) | nicht verwendet |

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| S-01 | Personen | Vorname, Nachname, Geschlecht, Notiz; anlegen über Menüband oder ersten Schritt | `T0788Stammbaum_Ui.Click_to_files` | alle Vorbilder | 1 |
| S-02 | Familien | Vater, Mutter, Partner, Kind anlegen; Familie (Paar + Kinder) wird mitgeführt | `Click_to_files`, `T0788StammbaumTests.Lines` | alle Vorbilder | 1 |
| S-03 | Ereignisse | Geburt, Taufe, Heirat, Tod, Begräbnis, Beruf, Wohnort … mit Datum und Ort, hinzufügen/entfernen | `Click_to_files` (Geburt/Tod), `Mouse_and_keys` (Rückgängig) | alle Vorbilder | 1 |
| S-04 | Ungefähre Daten | „um“, „vor“, „nach“, „zwischen … und …“, „von … bis …“, Text in Klammern; deutsche Eingabe → GEDCOM | `T0788StammbaumTests.Dates`, `Input`, `Input_rejected` | GEDCOM 5.5.1 | 1 |
| S-05 | Orte | Ortsliste mit Zahl der Ereignisse, Umbenennen in allen Ereignissen, Koordinaten | `Mouse_and_keys` (Umbenennen), `Read_foreign` (Koordinaten) | FTM/Gramps | 1 |
| S-06 | Quellen | Quellen anlegen, an Person oder Ereignis mit Fundstelle angeben; im Bericht nummeriert | `Report`, `Read_foreign` | alle Vorbilder | 1 |
| S-07 | Baum Vorfahren | Ahnentafel nach rechts, Generationen 2–6, fehlende Eltern als „+ Vater/+ Mutter“ zum Anklicken | `Layouts`, `Click_to_files` (Klick auf „+ Mutter“) | FTM/RootsMagic | 1 |
| S-08 | Baum Nachkommen | Kinder, Enkel … nach unten, Partner im Kasten | `Layouts`, `Mouse_and_keys` | FTM/RootsMagic | 1 |
| S-09 | Zoom und Verschieben | Mausrad um die Mausstelle, Ziehen, + / − / 0, Einpassen | `Mouse_and_keys` | alle Vorbilder | 1 |
| S-10 | Auswahl und Mitte | Klick wählt, Doppelklick oder „In die Mitte“ setzt die Person in die Mitte | `Mouse_and_keys` („In die Mitte“ über Menüband; Doppelklick nur gebaut) | alle Vorbilder | 1 |
| S-11 | Rechtsklick, Entf | Menü (Mitte, Vater, Mutter, Partner, Kind, Einpassen, Löschen), Entf löscht, Rückgängig | `Mouse_and_keys` | übliche Bedienung | 1 |
| S-12 | GEDCOM lesen | 5.5.1 mit CONC/CONT, Kennungen bleiben; Fehlerzeilen mit Nummer gemeldet | `Read_foreign`, `Broken_lines`, `Import_gedcom` | GEDCOM 5.5.1 | 1 |
| S-13 | Zeichensätze | UTF-8 (mit/ohne BOM), UTF-16, ANSI/Windows-1252, ANSEL erkannt und für gängige Zeichen umgerechnet, unbekannte Zeichen gezählt und gemeldet; falsche Angaben („ASCII“ mit UTF-8) erkannt | `Ansel`, `Encodings`, `Import_gedcom` (ANSEL per Ziehen) | GEDCOM 5.5.1 | 1 |
| S-14 | GEDCOM schreiben | immer UTF-8, Kopf mit GEDC 5.5.1 und SUBM, Zeilen ≤ 255 Byte, CONC nie an Leerzeichen | `Roundtrip_lossless`, `Long_values` | GEDCOM 5.5.1, WikiTree-Befund | 1 |
| S-15 | Rundreise ohne Verlust | unterstützte Angaben gleich nach Export und erneutem Einlesen; unbekannte Tags (_UID, _FREL, CHAN, QUAY, PEDI) und Datensätze (NOTE, REPO, OBJE, eigene) bleiben erhalten; zweiter Export zeichengleich | `Roundtrip_lossless`, `Import_gedcom`, `T0788_Fremdnachlauf` (Rundreise im Beleg) | Auftrag | 1 |
| S-16 | Prüfungen | Fehler: Geburt nach Tod, Taufe vor Geburt, Begräbnis vor Tod, Eltern jünger als Kind, Geburt nach Tod der Mutter / mehr als 1 Jahr nach Tod des Vaters, Heirat vor Geburt / nach Tod, eigener Vorfahre; Hinweise: Mutter < 12 / > 55, Vater < 13 / > 80, Heirat < 13, Alter > 120, unlesbare Daten; ungefähre Daten mit ±10 Jahren | `Checks_find_contradictions`, `Checks_no_false_alarm`, `Click_to_files` (unten sichtbar) | RootsMagic, FamilySearch | 1 |
| S-17 | Bericht | Personen mit Ereignissen, Eltern, Partnern, Kindern, Quellen, Notizen; Prüfung, Quellen- und Ortsverzeichnis; PDF, DOCX, ODT | `Report`, `T0788_Fremdnachlauf.Bericht_in_LibreOffice` (echt) | FTM/Legacy | 1 |
| S-18 | Baum als Bild | SVG (Inkscape), PNG, PDF – ohne Platzhalter | `Layouts`, `Click_to_files`, `T0788_Fremdnachlauf.Baum_in_Inkscape_und_Calc` (echt) | alle Vorbilder | 1 |
| S-19 | Personenliste | CSV (Semikolon, UTF-8 mit BOM) für Tabellenprogramme | `Click_to_files`, Fremdnachlauf (Calc) | üblich | 1 |
| S-20 | Lebende schützen | Haken „Lebende ausblenden“ (Standard an) gilt für GEDCOM, Bericht, Bild und Tabelle; lebend = kein Tod und jünger als 110 Jahre, sonst geschätzt über Eltern/Kinder/Partner/Heirat; im Zweifel geschützt | `Living_hidden`, `Click_to_files` | Gramps | 1 |
| S-21 | KI: Personen aus Text | Vorschau ändert nichts, „Ja, behalten“ fügt ein, ein Rückgängig; vorhandene Personen, Familien, Quellen und der Schutz bleiben gesperrt; unlesbare Daten abgelehnt | `Ki_check`, `T0788Stammbaum_Ui.Ki_persons`, Echtlauf `Real_local_ki` | Auftrag | 1 |
| S-22 | Ziehen und Ablegen | GEDCOM-Datei auf die Arbeitsfläche ziehen; leerer Stammbaum → hinein, sonst neuer Reiter | `Import_gedcom` | übliche Bedienung | 1 |
| S-23 | Einfach / Profi | Einfach: Personenzeile, Baum, eine goldene Hauptaktion; Rest unter „Mehr Werkzeuge“ | `Simple_level`, `UebersichtEinfach_Ui` | Trendo-Regel | 1 |
| S-24 | Ortskarte | Orte mit Koordinaten auf einer Karte | – | FTM/Gramps | 2 (fehlt) |
| S-25 | Medien | Fotos und Urkunden an Personen | – (OBJE wird nur erhalten, nicht angezeigt) | alle Vorbilder | 2 (fehlt) |
| S-26 | Weitere Ansichten | Sanduhr, Fächer, Zeitstrahl je Person | – | FTM/RootsMagic | 2 (fehlt) |
| S-27 | GEDCOM 7 / ANSEL schreiben | Export als GEDCOM 7.0 oder ANSEL | – (bewusst nur 5.5.1 UTF-8) | GEDCOM | 2 (fehlt) |
| S-28 | Mehrere Namen, Duplikate | zweiter Name, Personen zusammenführen | – (zweiter NAME bleibt nur erhalten) | RootsMagic | 2 (fehlt) |
