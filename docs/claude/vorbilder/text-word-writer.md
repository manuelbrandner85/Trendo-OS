# Vorbild-Analyse Trendo Text: Microsoft Word, LibreOffice Writer (Clean-Room)

- Programm: „Trendo Text“ im Studio „Texte & Tabellen“ (SYS-10) · Pilot des Gesamtumbaus 0.9.47 (GODMODE V31–V40, Gate P1)
- Vorbilder: Microsoft Word (Marktführer), LibreOffice Writer (freie Referenz, MPL), Google Docs (einfache Bedienung)
- Werkzeug: Funktionsumfang aus der öffentlichen Hilfe und Bedienung der Vorbilder. Kein Code, keine Grafiken, keine Oberfläche übernommen.
  REA nicht eingesetzt – es gab keine Datei, die analysiert werden musste; Dateiformate sind offene Standards (OOXML ECMA-376, ODF ISO/IEC 26300).
- Stand: 02.11.2026, Trendo 0.9.47

## Funktionsmatrix gegen das Vorbild

Stand: **da** = vorhanden und getestet · **neu** = mit dem Piloten 0.9.47 gebaut und getestet · **fehlt** = Lücke zum Vorbild (Plan unten).

| Bereich | Funktion im Vorbild | Trendo Text | Test |
|---|---|---|---|
| Start | Ausschneiden, Kopieren, Einfügen | da | T-0223 T1–T4 UI |
| Start | Format übertragen | da | T-0230 W1/W2/W5 |
| Start | Schriftart | **neu** | Pilot Text UI: Schriftart, Größe, Farbe … |
| Start | Schriftgröße (Liste und A+/A−) | **neu** (Liste), da (A+/A−) | Pilot Text UI · T-0223 T3 |
| Start | Fett, Kursiv, Unterstrichen, Durchgestrichen, Hoch-/Tiefgestellt | da | T-0223 T3 |
| Start | Groß-/Kleinschreibung | da | T-0223 T3 |
| Start | Schriftfarbe | **neu** | Pilot Text: Schrift, Farbe, Hervorhebung setzen … |
| Start | Texthervorhebung (Marker) | **neu** | Pilot Text UI |
| Start | Formatierung löschen | fehlt | – |
| Start | Aufzählung, Nummerierung | da | T-0223 T1–T4 UI |
| Start | Mehrstufige Liste | fehlt | – |
| Start | Einzug vergrößern/verkleinern | da | T-0230 W3/W4 |
| Start | Erstzeileneinzug, hängender Einzug | **neu** | Pilot Text: DOCX-/ODT-Rundweg |
| Start | Ausrichtung links/zentriert/rechts/Blocksatz | da | T-0223 T1–T4 UI |
| Start | Zeilenabstand (1,0 / 1,15 / 1,5 / 2,0) | **neu** | Pilot Text: PDF … mehr Zeilenabstand braucht mehr Seiten |
| Start | Abstand vor/nach dem Absatz | **neu** (vor), da (nach) | Pilot Text: DOCX-Rundweg |
| Start | Absatzrahmen, Schattierung | fehlt | – |
| Start | Formatvorlagen-Galerie | da | T-0230 W1/W2/W5 |
| Start | Eigene Formatvorlagen anlegen | fehlt | – |
| Start | Suchen und Ersetzen | da | T-0223 T5/T7 |
| Bearbeiten | Rechtsklick-Menü mit Formaten und Einfügen | **neu** | Pilot Text UI: Rechtsklick … |
| Bearbeiten | Markieren über mehrere Absätze | fehlt (größte Lücke: heute ein Eingabefeld je Absatz) | – |
| Bearbeiten | Autokorrektur beim Tippen | fehlt | – |
| Einfügen | Tabelle, Bild, Seitenumbruch | da | T-0223 T5/T6/T7/T9/T10 UI |
| Einfügen | Link, Kommentar, Fuß-/Endnote, Symbol | da | T-0230 W1/W2/W5 |
| Einfügen | Kopf-/Fußzeile, Seitenzahlen, Wasserzeichen | da | T-0230 W3/W4 |
| Einfügen | Formen, Textfeld, Diagramm, Formel, Deckblatt | fehlt | – |
| Layout | Ränder, Hoch/Quer, Spalten, Silbentrennung | da | T-0230: PDF mit Seitenlayout |
| Referenzen | Inhaltsverzeichnis, Beschriftungen | da | T-0230 W5/W6/W7 |
| Referenzen | Literaturverzeichnis, Index | fehlt | – |
| Sendungen | Serienbrief, Umschläge, Etiketten | da | T-0230 W6/W7 |
| Überprüfen | Rechtschreibung, Wörter zählen | da | T-0223 T8 |
| Überprüfen | Kommentare, Vergleichen, Änderungen annehmen/ablehnen | da | T-0230 W6/W7 |
| Überprüfen | Barrierefreiheit prüfen, Vorlesen | da | T-0230 W6/W7 |
| Ansicht | Lesemodus, Navigationsbereich, Zoom, Fokus | da | T-0230 |
| Ansicht | Seitenansicht mit echten Seitenumbrüchen, Lineal | fehlt (Blatt ist eine lange Seite) | – |
| Dateien | DOCX, ODT, PDF, TXT – öffnen und speichern | da | T-0223 T1 · T-0230 Abgleich mit Writer |
| Dateien | Automatisches Sichern, Wiederherstellen | da | T-0254 |
| KI | Umschreiben als Vorschlag, Diktat, Claude im Programm | da | 108-49 · Pilot Text UI (Rechtsklick „✳ Claude fragen“) |

## Nächste Lücken (Reihenfolge nach Nutzen)

1. Markieren über mehrere Absätze (Editor auf ein durchgehendes Textfeld je Seite umstellen) – Voraussetzung für Kopieren/Formatieren ganzer Passagen.
2. Seitenansicht mit echten Seitenumbrüchen und Lineal.
3. Formatierung löschen, mehrstufige Listen, eigene Formatvorlagen.
4. Formen, Textfeld, Formel; Literaturverzeichnis.

## Leistung

Test „Pilot Text Leistung“ (ohne Fenster gemessen, 02.11.2026): 300 formatierte Absätze (etwa 15 Seiten). Die Seite war nach 726 ms aufgebaut, ein Formatwechsel brauchte 854 ms (Grenzen im Test: 3 s bzw. 1,5 s).
Der Formatwechsel baut heute die ganze Seite neu auf. Nur den geänderten Absatz neu zu zeichnen, ist der nächste Leistungsschritt. Eine Messung mit 100+ Seiten auf echter Hardware steht aus.
