# Trendo Tabelle – Testergebnisse

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `tabelle` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 41 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio | ✓ bestanden |
| Claude auch in der Studio-Übersicht, in der Suche und im Fokusmodus | ✓ bestanden |
| Claude im Studio: Auswahl in Trendo Tabelle geht als Zustand mit; Internet und Befehle bei Bedarf immer an, je Projekt auf „sicher“ stellbar; Vorgabe abschaltbar | ✓ bestanden |
| Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet | ✓ bestanden |
| Markieren & Claude fragen: Zellbereich in Trendo Tabelle geht mit ins Eingabefeld; Knopf auf der Seite; Strg+Umschalt+A | ✓ bestanden |
| SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien | ✓ bestanden |
| SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt | übersprungen |
| SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung | ✓ bestanden |
| SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm | ✓ bestanden |
| SYS-10 wie Microsoft 365: Dokumente suchen (alle Wörter im Namen), Anheften mit zeigt sie oben unter „Angeheftet“, Lösen nimmt sie weg; gelöschte Datei verschwindet | ✓ bestanden |
| SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX | ✓ bestanden |
| SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen) | ✓ bestanden |
| SYS-10-P04: Zuletzt verwendet – neueste zuerst | ✓ bestanden |
| SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs | ✓ bestanden |
| SYS-10-P07: Text aus einem echten PDF | ✓ bestanden |
| SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract | übersprungen |
| SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen | ✓ bestanden |
| SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung) | ✓ bestanden |
| SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita) | ✓ bestanden |
| SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen | ✓ bestanden |
| SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig) | ✓ bestanden |
| SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt | ✓ bestanden |
| SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version | ✓ bestanden |
| SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF) | ✓ bestanden |
| SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“) | ✓ bestanden |
| SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert) | ✓ bestanden |
| SYS-10-P25: Barrierefreiheits-Bericht listet Mängel | ✓ bestanden |
| T-0228 E1: Fenster fixieren – Kopfzeile bleibt beim Blättern sichtbar, wird in XLSX gespeichert | ✓ bestanden |
| T-0228 E1: tippen ins Raster, Enter geht nach unten, Formel rechnet, Formelleiste zeigt Inhalt, Entf/Strg+Z/Strg+Y | ✓ bestanden |
| T-0228 E2/E3: Diagramm über Einfügen → Säulen, rechnet live mit, Entfernen; Als PDF legt Datei unter Tabellen ab | ✓ bestanden |
| T-0228 E5–E9: Bedingt über Eingabefeld, Auswahlliste mit Pfeil und Prüfung, Als Tabelle, Blitzvorschau Strg+E, Notiz, Blattschutz | ✓ bestanden |
| T-0228 G1: Startseite mit Vorlagen; Vorlage Haushaltsbuch rechnet; Statusleiste zeigt Summe/Mittelwert/Anzahl (G5) | ✓ bestanden |
| T-0228 Menüband: Σ-Summe, Fett, Euro, Sortieren, Ausfüllen (Mo, Di …), Kopieren/Einfügen mit angepasster Formel, Blätter | ✓ bestanden |
| T-0228: Speichern als XLSX und wieder öffnen; erscheint unter „Zuletzt geöffnet“ | ✓ bestanden |
| T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg | ✓ bestanden |
| T-0682 Tabelle: fremde XLSX – Strg+S lässt das Original bytegleich; Kopie und „Trotzdem“ mit Sicherung „Name.original.xlsx“ | ✓ bestanden |
| T-0682: ODT wie DOCX geschützt; ODS und ODP kann Trendo nicht zurückschreiben – nur „Als Kopie speichern“ (XLSX/PPTX), Strg+S lässt das Original; ältere Sicherung wird nicht überschrieben | ✓ bestanden |
| T-0701 Tabelle: fremde XLSX öffnen – kein Verlust-Hinweis, verbundene Zelle wird als Ganzes gewählt; verbinden, kursiv (Strg+I), eigenes Zahlenformat, Rechtsklick-Menü; Strg+S schreibt alles in die Datei, Formatcode und Diagramm-Einzelheiten bleiben | ✓ bestanden |
| T-0714 Tabelle: Werte und Formel eingetippt, Fenster ohne Speichern geschlossen → Karte → Wiederherstellen bringt die Mappe; Speichern räumt weg | ✓ bestanden |
| T-0714 Umzug: Sicherungen der früheren Notsicherung (wiederherstellen/text.docx, cad.tcad, tabelle.xlsx) werden beim ersten Start angeboten, mit Zeitpunkt; Zurückholen und Verwerfen gehen | ✓ bestanden |
| T-0724 Leistung: Tabelle mit 100 000 Zellen öffnen – alle Werte kommen an (Zeit wird nur gemessen) | ✓ bestanden |
| Tabelle F3: Autofilter – Knopf unter „Daten“, rechts je Spalte Werte mit Anzahl, Haken weg blendet Zeilen aus, „Alle“ und „Autofilter aus“ | ✓ bestanden |
| Tabelle F3: Pivot – „⊞ Pivot“ öffnet den Bauer rechts mit Vorschlag (Kategorie/Betrag), erstellt ein neues Blatt mit Formeln, das mitrechnet | ✓ bestanden |
