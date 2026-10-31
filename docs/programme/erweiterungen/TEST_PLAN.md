# Erweiterungen – Testplan

Studio: Trendo erweitern (X-04) · Bereich: Entwickeln · Seite: `erweiterungen` · Stand: 0.9.28 (Stand 4ec46e4)

7 Tests sprechen dieses Programm an.

- T-0241 (UI): Not-Stopp verhindert das Installieren; Entfernen fragt vorher; Abbrechen ändert nichts
- X-04 (UI): Beispiel installieren, im Baukasten öffnen, ausschalten sperrt Benutzen
- X-04 Katalog (UI): mitgelieferten Katalog zeigen und geprüft installieren
- X-04 Katalog: lokaler Katalog mit Prüfsummen, Installation nur bei passender Prüfsumme
- X-04 Katalog: nur GitHub-https oder lokale ZIP, Prüfsumme Pflicht, Kennung muss passen
- X-04: Beispiel-Erweiterung installieren (Ordner und ZIP), ein/aus, Prüfsumme stabil, entfernen
- X-04: Programmcode, falsche Rechte, kaputte Beschreibung und ZIP-Ausbruch werden abgewiesen
