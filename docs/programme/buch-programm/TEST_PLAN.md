# Bücher schreiben – Testplan

Studio: Bücher schreiben (STUDIO-BOOK-01) · Bereich: Kreativ & Medien · Seite: `buch-programm` · Stand: 0.9.110 (Stand 0eb9d52)

16 Tests sprechen dieses Programm an.

- Bücher: Kapitelübersicht, neues Kapitel, Wortziel, EPUB-Export lässt sich öffnen
- Gegenprüfung 1: übergeben → Ergebnis → Rückgängig → Rückgängig → Wiederholen ×2 – Tafel, uebergaben.json und Dateien stimmen bei jedem Schritt überein
- Gegenprüfung 2: eine kaputte Tafel in einem anderen Projekt stört den Export eines normalen Dokuments nicht
- Gegenprüfung 3: erneuter Export wird neue Fassung (alte bleibt als Beleg), „Karte wieder öffnen“ übergibt erneut
- Gegenprüfung 5: Alt+↓ in der aufgeklappten Studio-Auswahl sortiert die Kette nicht um
- Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme
- Großprojekt-Kette frei: Vorlage bekommt Studios, Rechtsklick und Alt+↑ sortieren um, Strg+Enter übergibt, abgelegte Datei wird Ergebnis
- Großprojekt: nach einem Neustart kommt der Export des wieder geöffneten Dokuments an die Karte zurück
- T-0707 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ändert in drei Programmen das offene Dokument, Datei auf der Platte, Rückgängig (nur mit TRENDO_ASSIST_ECHT)
- T-0707: Drei Programme aus drei Fächern – Auftrag → KI-Plan → Vorschau → Ja → ein Rückgängig-Schritt im offenen Dokument → echte Datei → Rückgängig (KI ersetzt)
- T-0707: KI liefert nicht reparierbares JSON – die Kette fragt einmal nach und arbeitet mit der Korrektur weiter
- T-0707: KI-Befehl im Menüband („Weiterschreiben mit Claude“) ohne Claude – vorher still nichts, jetzt dieselbe Kette ins offene Dokument
- T-0707: Programme außerhalb der Hülle (Text, Tabelle, Geld …) – einheitlich „Noch nicht im Dokument möglich“, Liste aus dem Katalog
- T-0707: Rechte in der Kette – beim Not-Stopp ändert die KI nichts; Claude-Brücke prüft mit derselben Regel (Text bleibt Text)
- T-0725 Bücher: leeres Buch → „Manuskript öffnen …“ übernimmt eine Word-Datei (Kapitel, fett, kursiv), Rückgängig holt den alten Stand, Ablegen einer .md; Export Word, Druck-PDF und OpenDocument enthalten die Kapitel
- T-0725 Echtlauf Bücher: LibreOffice schreibt ein Word-Manuskript (Titel, Kapitel, fett, kursiv), Trendo übernimmt es per „Manuskript öffnen …“; LibreOffice öffnet das Trendo-DOCX und -ODT, qpdf prüft das Druck-PDF
