# KI-Vorlagen – Testplan

Studio: KI-Anweisungen schreiben (CORE-12) · Bereich: KI & Automatik · Seite: `ki-vorlagen` · Stand: 0.9.110 (Stand 0eb9d52)

8 Tests sprechen dieses Programm an.

- Anweisungs-Test: falsche Regeln werden gemeldet; Ergebnis zählt Durchläufe beider Fassungen; Regeln bleiben gespeichert
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- CORE-12: Platzhalter mit Standardwert ausfüllen, fehlende werden genannt
- CORE-12: Qualitätsprüfung erkennt kurze Aufträge, Geheimnisse, offene Platzhalter; Masterprompt hat Regeln
- CORE-12: Versionen speichern und zurückholen, Mitgelieferte nicht löschbar
- T-0711 KI-Vorlagen: Vorlagen-Editor – Rückgängig, Notsicherung, Karte, Zurückholen; Speichern räumt weg
- T-0740 KI-Anweisungen: Texteditor – Tippen, Rückgängig im Feld (Knopf und Rechtsklick), „Lücke einfügen“ als ein Schritt, Lücken hervorgehoben, Speichern
- T-0740 KI-Anweisungen: Vorlage als .md speichern, ändern, einlesen → Rückfrage → Ersetzen (neue Version) und Als Kopie; Alle als .json; Ablegen
