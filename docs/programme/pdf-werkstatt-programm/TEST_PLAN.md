# PDF-Werkstatt – Testplan

Studio: PDF-Werkstatt (STUDIO-PDFTOOLS-01) · Bereich: Arbeiten & Büro · Seite: `pdf-werkstatt-programm` · Stand: 0.9.103 (Stand ef4dc8b)

9 Tests sprechen dieses Programm an.

- PDF-Werkstatt: eigene Seite im Studio, Werkzeugleiste mit allen Werkzeugen, Vorlage wählt das Werkzeug
- PW-01 UI: zwei PDFs hinzufügen, Reihenfolge tauschen, „Ausführen“ fügt zusammen; Ergebnis öffnet in Trendo PDF; Originale unverändert
- PW-01/03 UI: Teilen nach Bereichen und Bilder zu PDF über die Werkstatt
- PW-02 UI: Seiten ordnen – Seite anklicken, drehen, Seite löschen, nach vorn; Ergebnis hat die neue Folge
- PW-04 UI: Komprimieren mit JPEG (SkiaSharp) – gemessen kleiner, Bild als JPEG gespeichert, Meldung nennt vorher/nachher
- PW-05/06/07/08 UI: Stapel – Wasserzeichen und Seitenzahlen für jede Datei, Metadaten entfernen, Passwort setzen und mit Passwort entfernen
- PW-09 UI: Schwärzen per Maus – Balken ziehen, Ausführen; der Text unter dem Balken ist in der Ergebnisdatei nicht mehr lesbar
- PW-10 UI: Zwei PDFs vergleichen – Unterschiede mit Seitenangabe in der Arbeitsfläche und als Textdatei
- STUDIO-PDFTOOLS-01 Adapter: Arbeitsauftrag mit aufgelösten Seiten, Pflichtfeld Aktion, falsche Seitenangabe wird gemeldet
