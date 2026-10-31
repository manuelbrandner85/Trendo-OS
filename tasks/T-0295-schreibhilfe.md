# T-0295 – Trendo Text: Schreibhilfe mit Ja/Nein

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Trendo Text: Schreibhilfe Ja/Nein“).
- Neu: Knopf „✨ Schreibhilfe“ im Menüband; rechts die Vorschläge im gemeinsamen Ja/Nein-Baustein (T-0289).
- Regeln (`Trendo.Core.Office.WritingAid`, lokal): doppelte Wörter, doppelte Leerzeichen, Leerzeichen vor Satzzeichen, gerade → deutsche Anführungszeichen, einheitliche Schreibweisen nach Duden (z. B., d. h., u. a., z. T., E-Mail, okay), Füllwörter, Sätze über 30 Wörter (Ja markiert den Satz), dazu Rechtschreibung mit dem ersten Wörterbuch-Vorschlag, wenn das deutsche Wörterbuch installiert ist.
- „Ja“ ändert nur, wenn der Text an der Stelle noch stimmt, und ist mit Strg+Z rückgängig machbar; „Nein“ wird gemerkt (z. B. bleibt „Email“, wenn man es so will).
- Tests: `WritingAid_Ui` (Regeln, Anwenden prüft die Stelle, kein Fehlalarm bei korrektem Text; Ja/Nein/Rückgängig in der Oberfläche); alle 31 Tests rund um Trendo Text grün.
