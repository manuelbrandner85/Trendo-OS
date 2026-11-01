# Trendo Tabelle – Testplan

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `tabelle` · Stand: 0.9.32 (Stand 345fe84)

33 Tests sprechen dieses Programm an.

- Fenster: Klick auf eine Kategorie im Fenster bleibt im selben Fenster; ohne Klick eigenes Fenster; offene Seite wird wiederverwendet
- Fenster: maximiert ohne Rand bis an die Seiten, die Leiste unten bleibt sichtbar
- SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien
- SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen
- SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt
- SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung
- SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm
- SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX
- SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen)
- SYS-10-P04: Zuletzt verwendet – neueste zuerst
- SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs
- SYS-10-P07: Text aus einem echten PDF
- SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract
- SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen
- SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung)
- SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita)
- SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen
- SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig)
- SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt
- SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version
- SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF)
- SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“)
- SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert)
- SYS-10-P25: Barrierefreiheits-Bericht listet Mängel
- T-0228 E1: Fenster fixieren – Kopfzeile bleibt beim Blättern sichtbar, wird in XLSX gespeichert
- T-0228 E1: tippen ins Raster, Enter geht nach unten, Formel rechnet, Formelleiste zeigt Inhalt, Entf/Strg+Z/Strg+Y
- T-0228 E2/E3: Diagramm über Einfügen → Säulen, rechnet live mit, Entfernen; Als PDF legt Datei unter Tabellen ab
- T-0228 E5–E9: Bedingt über Eingabefeld, Auswahlliste mit Pfeil und Prüfung, Als Tabelle, Blitzvorschau Strg+E, Notiz, Blattschutz
- T-0228 G1: Startseite mit Vorlagen; Vorlage Haushaltsbuch rechnet; Statusleiste zeigt Summe/Mittelwert/Anzahl (G5)
- T-0228 Menüband: Σ-Summe, Fett, Euro, Sortieren, Ausfüllen (Mo, Di …), Kopieren/Einfügen mit angepasster Formel, Blätter
- T-0228: Speichern als XLSX und wieder öffnen; erscheint unter „Zuletzt geöffnet“
- T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg
- X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens
