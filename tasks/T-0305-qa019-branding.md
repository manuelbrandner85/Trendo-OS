# T-0305 – Regeln wiederhergestellt: Kern ohne Netz (QA-019), kein Trendo-Branding in Nutzer-Dokumenten

- Anlass: Der komplette Lauf der Kern-Tests nach T-0304 zeigte zwei rote Prüfungen.
  - `QA019_Offline`: Der Kern verwies auf `System.Net.Http` – eingeführt mit T-0274 (Höhendienst) und T-0279 (3D-Bibliothek), die `HttpClient` direkt im Kern nutzten.
  - `NoBrandingInDocumentsTests`: „Erstellt mit Trendo OS“ stand im Tagesabschluss der Kasse (T-0304), im Messbericht der Punktwolke (T-0264) und im CNC-Prüfbericht.
- Lösung:
  - Neue Schnittstelle `Trendo.Core.IWebFetch` (+ `WebReply`): Der Kern ruft nur noch darüber ab; die App liefert `HttpFetch` (HttpClient), die Tests `TestFetch` mit den bisherigen Attrappen. Verhalten unverändert (Tageslimit-Meldung, Fehlerstatus als verständliche Meldung, Zwischenspeicher).
  - `AssetLibrary` und `ElevationService` nehmen `IWebFetch`; die Karten-Seite fängt zusätzlich `IOException`.
  - Texte: „Erstellt am …“ statt „Erstellt mit Trendo OS am …“.
- Tests: kompletter Kern-Lauf 970 bestanden, 3 übersprungen, 0 Fehler; `Library3D_Ui`, `PlanPhoto_Ui`, `SPEC09_Ui`, `SPEC13_Ui`, `SPEC13_UiNet`, `SPEC13_UiPack`, `SPEC14_Ui`, `Kasse_Ui` grün.
