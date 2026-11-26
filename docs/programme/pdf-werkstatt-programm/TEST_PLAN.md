# PDF-Werkstatt – Testplan

Studio: PDF-Werkstatt (STUDIO-PDFTOOLS-01) · Bereich: Arbeiten & Büro · Seite: `pdf-werkstatt-programm` · Stand: 0.9.110 (Stand 0eb9d52)

14 Tests sprechen dieses Programm an.

- Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme
- PDF-Werkstatt: eigene Seite im Studio, Werkzeugleiste mit allen Werkzeugen, Vorlage wählt das Werkzeug
- PW-01 UI: zwei PDFs hinzufügen, Reihenfolge tauschen, „Ausführen“ fügt zusammen; Ergebnis öffnet in Trendo PDF; Originale unverändert
- PW-01/03 UI: Teilen nach Bereichen und Bilder zu PDF über die Werkstatt
- PW-02 UI: Seiten ordnen – Seite anklicken, drehen, Seite löschen, nach vorn; Ergebnis hat die neue Folge
- PW-04 UI: Komprimieren mit JPEG (SkiaSharp) – gemessen kleiner, Bild als JPEG gespeichert, Meldung nennt vorher/nachher
- PW-05/06/07/08 UI: Stapel – Wasserzeichen und Seitenzahlen für jede Datei, Metadaten entfernen, Passwort setzen und mit Passwort entfernen
- PW-09 UI: Schwärzen per Maus – Balken ziehen, Ausführen; der Text unter dem Balken ist in der Ergebnisdatei nicht mehr lesbar
- PW-10 UI: Zwei PDFs vergleichen – Unterschiede mit Seitenangabe in der Arbeitsfläche und als Textdatei
- STUDIO-PDFTOOLS-01 Adapter: Arbeitsauftrag mit aufgelösten Seiten, Pflichtfeld Aktion, falsche Seitenangabe wird gemeldet
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf PDF-Werkstatt: LibreOffice-PDF (3 Seiten) ablegen, Seite 3 nach vorn ziehen, drehen, Seite löschen, Ausführen – qpdf prüft Struktur, Seitenzahl und Drehung, LibreOffice öffnet das Ergebnis
- T-0705 Klick bis Datei – PDF-Werkstatt: leere Liste, „Dateien hinzufügen …“, Ausführen fügt zusammen; Ergebnis hat alle Seiten in Reihenfolge
- T-0705 PDF-Werkstatt: PDFs aus dem Explorer auf den ersten Schritt ziehen, Seite 4 mit der Maus an den Anfang ziehen, Rechtsklick „Nach rechts drehen“ und „Seite löschen“; Ausführen schreibt genau diese Reihenfolge
