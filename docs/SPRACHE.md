# Sprachleitfaden für Trendo OS

Ziel: Jeder versteht sofort, was gemeint ist, auch ohne Computerwissen.

## Grundregeln
1. **Du-Form, freundlich, kurz.** „Wähle einen Ordner.“ statt „Bitte selektieren Sie das Zielverzeichnis.“
2. **Ein Gedanke pro Satz.** Höchstens zwei Sätze pro Hinweis.
3. **Sag, was passiert und was es bringt.** „Trendo sichert deine Daten jeden Tag.“
4. **Ehrlich.** Was nicht fertig ist, heißt „Noch nicht gebaut“. Nichts wird schöngeredet.
5. **Knöpfe sind Verben.** „Installieren“, „Reparieren“, „Verstanden“, „Weiter“.
6. **Fehler ohne Schuld, mit Ausweg.** „Hat nicht geklappt – im Systemcheck kannst du es erneut versuchen.“

## Wörterliste (statt Fachwort → so schreiben)
| Fachwort | In Trendo |
|---|---|
| Komponente, Dependency | Zusatzprogramm |
| Audit-Log, Hash-Kette | Protokoll („vollständig und unverändert“) |
| Secrets-Backend, Token | sicher gespeicherte Schlüssel |
| Laufzeit, Runtime, Ollama-Laufzeit | „Die KI läuft auf diesem PC“ |
| API, kostenpflichtige API | Online-Dienst (kostet Geld) |
| Adapter | Verbindung zu Trendo |
| Spezifikation, APPROVED_SPEC | Geplant |
| Consent, Lizenz zustimmen | Zustimmen |
| Admin, UAC | „Windows fragt einmal nach Erlaubnis“ |
| Backup | Sicherung |
| Restore | Wiederherstellen |
| Theme | Design / Aussehen |

## Ausnahmen
Eigennamen bleiben (Ollama, Velopack, LibreOffice). Technische Details stehen nur in Protokollen und in docs/, nicht in der Oberfläche.

Automatisch geprüft: `tests/Trendo.App.Tests` (Test „W0b-01“) sucht verbotene Fachwörter in den Texten der Oberfläche.
