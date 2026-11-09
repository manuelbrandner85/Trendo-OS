# Technische Spezifikation: KI-Team einsetzen

Studio CORE-03 · KI-Team einsetzen · Bereich KI & Automatik

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Mehrere KI-Helfer arbeiten gemeinsam an einer Aufgabe (lokale KI nötig).

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-t0003/UEBERSICHT.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **KI-Rollen und Teams** (`ki-rollen`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)

## Quellcode

- nicht zugeordnet

## Externe Programme

- keine zugeordnet

## Dateiformate

keine zugeordnet

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 3, 07./08.11.2026).

Umsetzung:
- Team-Lauf fortsetzen (Zwischenstand je Rolle, Wiederaufnahme nach Fehler oder Neustart): eingebaut (5d45824cd), 3 Kerntests grün (AgentResumeTests), KI-Team-Oberflächentests grün. Echter Lauf mit Claude: NOT RUN.
- Geprüfte Antwortform (typisierte Antworten): eingebaut (`AgentAnswer`), erster Nutzer Rolle „Planer“; JSON wird geprüft, bei fehlerhaftem JSON einmal mit den Fehlern nachgefragt, Freitext bleibt unverändert (ungeprüft). Tests: AgentAnswerTests (10), alle Agenten-Tests grün. Echter Lauf mit Claude: NOT RUN.
- Rollenbeschreibungen nach crewAI: geplant, NOT RUN.
