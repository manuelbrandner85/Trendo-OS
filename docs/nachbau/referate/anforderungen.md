# Referate & Hausarbeiten (STUDIO-REFERAT-01) – Anforderungen (Clean-Room)

Auftrag T-0780, Freigabe des Nutzers vom 26.11.2026. Grundlage sind nur öffentliche Angaben (Produktseiten, Vergleiche, Hilfeseiten) und allgemein bekannte Arbeitsweisen beim wissenschaftlichen Schreiben. Kein Download, keine Testversion, kein fremder Code, keine fremden Texte, Symbole oder Namen im Produkt.

## Vorbilder (Recherche 26.11.2026)

| Vorbild | Was es auszeichnet | Typische Funktionen | Beobachtbar über | Quelle (abgerufen 26.11.2026) |
|---|---|---|---|---|
| Citavi (Lumivero) | Ordnung vom Zitat zum Argument, Gliederung neben der Literatur, besonders für lange Arbeiten | Quellen mit Zitaten und Seiten, Gliederung, viele Zitierstile, Word-Einbindung; Windows und Browser | Produktseite, Vergleiche | geekchamp.com/compare-citavi-vs-zotero; g2.com/compare/qiqqa-vs-zotero-vs-citavi |
| Zotero + Textverarbeitung | Freie Literaturverwaltung mit Browser-Erweiterung, PDF-Leser, CSL-Stilen | Sammeln, Zitieren im Text, Literaturverzeichnis per Knopf | Handbuch, Vergleiche | geekchamp.com/compare-citavi-vs-zotero |
| Scrivener | Kapitel-zuerst-Schreiben: Gliederung („Binder“) links, Text je Abschnitt, Ausgabe als Ganzes | Abschnitte ziehen und umsortieren, Ziel-Wortzahl, Kompilieren nach DOCX/PDF | Produktseite, Listen | zipdo.co/best/dissertation-software |
| Notion | Seiten mit Überschriften, Markdown-Austausch | Gliederung als Seiten, Export als Markdown | Produktseite | – (allgemein bekannt, nicht gesondert belegt) |
| TUM-Vergleich Literaturverwaltung | neutrale Übersicht der Werkzeuge | – | PDF der Universitätsbibliothek (Stand Feb. 2025) | mediatum.ub.tum.de/doc/1320978 |

**Ehrlich:** Die meisten Treffer sind Vergleichsportale und Listen; sie taugen für den Funktionsumfang, nicht als Qualitätsbeleg.

Empfehlung (umgesetzt): Arbeitsweise von Scrivener (Gliederung links, Text je Abschnitt, eine Ausgabe am Ende) kombiniert mit der Quellenlogik von Citavi/Zotero (Zitat zeigt auf eine Quelle, Verzeichnis entsteht von selbst).

## Offene Alternativen (Lizenz)

| Projekt | Lizenz | Verwendung in Trendo |
|---|---|---|
| Zotero | AGPL-3.0 | nur Vorbild (Clean-Room), kein Code |
| JabRef | MIT | nur Vorbild; BibTeX als Austauschformat |
| Manuskript (Scrivener-ähnlich) | GPL-3.0 | nur Vorbild, kein Code |
| Pandoc | GPL-2.0+ | nicht verwendet |

Die Lizenzen der Tabelle sind aus den Projektangaben übernommen und für diesen Auftrag nicht erneut im Repository geprüft (es wird nichts davon eingebunden).

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| R-01 | Kette als Arbeitsplatz | Schritte Thema → Gliederung → Text → Literatur → Folien oben anklickbar, erledigte mit ✓ | `WissenStudios_Ui.Referat_click_to_files` (Klickweg) | Scrivener/Citavi Arbeitsweise | 1 |
| R-02 | Deckblatt | Art der Arbeit, Thema, Untertitel, Name, Klasse/Matrikel, Schule, Fach, Betreuung, Ort, Datum; leere Felder fallen weg | `WissenStudiosTests.Text_outputs` (Titel und „vorgelegt von …“ im DOCX/ODT) | Formvorgaben Schule/Hochschule | 1 |
| R-03 | Gliederung | Abschnitte in drei Ebenen, Nummern 1 / 1.1 / 1.1.1, aus Zeilen übernehmen („1 Einleitung“, „##“, Einrückung) | `WissenStudiosTests.Outline_and_citations` | Scrivener Binder | 1 |
| R-04 | Umsortieren mit der Maus | Abschnitt in der Gliederung ziehen, Rückgängig stellt her | `Referat_drag_menu_undo_block` (echte Mausereignisse) | Scrivener | 1 |
| R-05 | Rechtsklick | Unterabschnitt, nach oben/unten, Ebene höher/tiefer, löschen | `Referat_drag_menu_undo_block` (Menü öffnet per Rechtsklick, „Ebene tiefer“ wirkt) | übliche Bedienung | 1 |
| R-06 | Text je Abschnitt | Text mit Wortzahl, Vorschau mit Kurzbelegen | `Referat_click_to_files` | Scrivener | 1 |
| R-07 | Zitieren per Klick | Quelle anklicken setzt Verweis `[@kennung, S. n]`; Ausgabe macht daraus den Kurzbeleg des Stils | `Referat_click_to_files`, `Outline_and_citations` | Citavi/Zotero „Zitat einfügen“ | 1 |
| R-08 | Keine erfundenen Quellen | Verweis ohne Quelle wird rot markiert und sperrt jede Ausgabe mit Meldung | `Unknown_source_blocks_export`, `Referat_drag_menu_undo_block` | Leitsatz des Nutzers | 1 |
| R-09 | Quellen aus „Recherche mit Quellen“ | offenes Quellen-Dokument oder gespeicherte .tdoc übernehmen, Kennungen bleiben, keine Doppelten | `Referat_sources_from_cite_and_ris` | Vorhandenes Studio STUDIO-CITE-01 | 1 |
| R-10 | Fremde Quellen einlesen | RIS (Zotero/Citavi) und BibTeX (JabRef), auch per Ziehen auf die Arbeitsfläche | `Referat_sources_from_cite_and_ris` (Drop echt), `T0780_Fremdnachlauf.Referat_docx_odt_in_LibreOffice` | Zotero/Citavi Export | 1 |
| R-11 | Zitierstile | APA 7, Harvard, DIN ISO 690 (nah) für Kurzbeleg und Verzeichnis | `Outline_and_citations` (APA, DIN) | Stilvorgaben | 1 |
| R-12 | Literaturverzeichnis | sortiert, hängender Einzug, Titel kursiv | `Text_outputs` | Citavi/Zotero | 1 |
| R-13 | Inhaltsverzeichnis | echtes Verzeichnis-Feld in DOCX, Verzeichnis in ODT | `Text_outputs`, LibreOffice-Nachlauf („Inhaltsverzeichnis“) | Formvorgaben | 1 |
| R-14 | Seitenzahlen | „Seite n von m“ in der Fußzeile | `Text_outputs` (PageNumbers), LibreOffice-PDF + pdftotext „Seite 2 von N“ | Formvorgaben | 1 |
| R-15 | Ausgabe DOCX/ODT, in LibreOffice geprüft | LibreOffice öffnet beide, alle Kapitel, Kurzbelege und Verzeichnis lesbar | `T0780_Fremdnachlauf.Referat_docx_odt_in_LibreOffice` (echt) | Auftrag | 1 |
| R-16 | Ausgabe PDF | mehrseitig (Deckblatt, Verzeichnis, Text, Literatur) | `Referat_click_to_files`, `Text_outputs` | Auftrag | 1 |
| R-17 | Folien PPTX/ODP | Titelfolie, Gliederung, je Kapitel eine Folie, Sprechernotizen, Quellenfolie; Impress öffnet beide | `Slides`, `T0780_Fremdnachlauf.Folien_pptx_odp_in_LibreOffice` (echt) | Auftrag | 1 |
| R-18 | Weiter in Trendo Text/Präsentation | Ausgabe öffnet sich dort zum Weiterbearbeiten | Befehl „In Trendo Text öffnen“ (nicht eigens getestet) | Vorhandene Programme | 2 |
| R-19 | Formvorgaben prüfen | Thema, leere Abschnitte, unvollständige und nicht zitierte Quellen, Verweise ohne Quelle | `Unknown_source_blocks_export`, Status „✓ bereit“ in `Referat_click_to_files` | Citavi „Aufgaben“ | 1 |
| R-20 | KI: Gliederung/Abschnitt | Vorschau ändert nichts, „Ja, behalten“ schreibt ins offene Dokument, genau ein Rückgängig-Schritt | `Referat_ki_outline`, `Referat_ki_section_no_invented_sources`, Echtlauf `Real_local_ki` | Auftrag | 1 |
| R-21 | KI erfindet keine Quellen | erfundener Verweis, freier Kurzbeleg „(Name, Jahr)“ ohne passende Quelle und neue Quellen über „dokument.setzen“ werden abgelehnt | `Referat_ki_section_no_invented_sources` | Auftrag | 1 |
| R-22 | Deckblatt ohne Seitenzahl | Seitenzahl erst ab Inhaltsverzeichnis | – | Formvorgaben | 2 (fehlt) |
| R-23 | Fußnoten-Zitierweise | Belege als Fußnote statt Kurzbeleg | – | deutsche Geisteswissenschaften | 2 (fehlt) |
