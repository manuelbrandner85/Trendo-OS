# T-0289 – Programme schlagen vor, du sagst Ja oder Nein

- Freigabe: Manuel im Chat am 31.10.2026 („viel intelligenter, schlage vor, ich sage ja oder nein“; Auswahl „KI-Vorschläge in Programmen“).
- Gemeinsamer Baustein: Karte „✨ Trendo schlägt vor“ mit Ja/Nein je Vorschlag. „Nein“ wird dauerhaft gemerkt (`UserSettings.DismissedSuggestions`), „Ja“ führt genau den beschriebenen Schritt aus – mit dem Rückgängig des Programms. Beides steht im Audit-Protokoll. Nichts geschieht ohne Klick.
- Regeln lokal und nachvollziehbar (`Trendo.Core.Assist.Suggest`), keine Cloud:
  - **Kalender:** offene Aufgaben, die in 7 Tagen fällig oder überfällig sind → freie Zeit werktags 9–18 Uhr vor der Fälligkeit (90 min bei hoher Priorität), nicht doppelt.
  - **Aufgaben:** E-Mails im Posteingang mit Frist im Betreff („bis 15.11.“, „Frist: …“) oder Bitte („bitte unterschreiben/überweisen/bestätigen“) → Aufgabe mit Fälligkeit in der Liste „E-Mail“.
  - **Abläufe:** volle Downloads (≥ 5 PDFs) → „Downloads aufräumen“; viele Fotos ohne Datum → „Fotos mit Datum versehen“ (bleibt freigabepflichtig).
  - **Videoschnitt:** „✨ Stille finden“ (FFmpeg silencedetect, ≥ 1 s unter −35 dB) → Vorschlag, die stillen Stellen herauszuschneiden (`VideoEdit.CutOut`).
- **Fehler behoben:** Bei mehrreihigem Menüband ragte die Arbeitsfläche der Programme über die unterste Menüband-Reihe und verdeckte Knöpfe – Arbeitsfläche jetzt auf ihren Bereich begrenzt (alle Programme).
- Tests: Kern `SuggestTests` (3); App `Suggestions_Ui` (Ja/Nein/gemerkt/Rückgängig in allen vier Programmen); 105 Programm- und Querschnitt-Tests grün.
