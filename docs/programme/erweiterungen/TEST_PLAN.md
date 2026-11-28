# Erweiterungen – Testplan

Studio: Trendo erweitern (X-04) · Bereich: Entwickeln · Seite: `erweiterungen` · Stand: 0.9.113 (Stand 1978f1a)

11 Tests sprechen dieses Programm an.

- T-0241 (UI): Not-Stopp verhindert das Installieren; Entfernen fragt vorher; Abbrechen ändert nichts
- T-0766 (UI): Katalog-Modul – Rechte werden vor der Installation angezeigt; Not-Stopp sperrt die Rechte-Freigabe
- T-0766 (UI): Werkstatt → bauen → ZIP → installieren (leerer Datenordner) → Rechte im eigenen Schritt → Knopf im Studio „Projekte“ → Tabelle im Projekt → Rückgängig → Rechte entziehen
- T-0766 Gegenprüfung (UI): Netz-Katalog erst laden, dann Rechte zeigen, dann fragen; Update nennt ersetzte Fassung und fragt nach dem Datenordner; Freigabe nur für die angezeigte Prüfsumme
- X-04 (UI): Beispiel installieren, im Baukasten öffnen, ausschalten sperrt Benutzen
- X-04 Katalog (UI): mitgelieferten Katalog zeigen und geprüft installieren
- X-04 Katalog: lokaler Katalog mit Prüfsummen, Installation nur bei passender Prüfsumme
- X-04 Katalog: nur GitHub-https oder lokale ZIP, Prüfsumme Pflicht, Kennung muss passen
- X-04: Beispiel-Erweiterung installieren (Ordner und ZIP), ein/aus, Prüfsumme stabil, entfernen
- X-04: Erweiterungs-Katalog – nur GitHub über https oder lokale ZIP, Prüfsumme mit 64 Hexzeichen Pflicht, unbekannte Art abgelehnt; Weiterleitungen nur zu GitHub auf 443
- X-04: Programmcode, falsche Rechte, kaputte Beschreibung und ZIP-Ausbruch werden abgewiesen
