# Volltest 0.9.83 (07.11.2026)

- Durchlauf 2 (dieser Beleg, `dotnet.log`): Api 16/16, Core 1517/1520 (3 übersprungen), App 823/828 (4 übersprungen, 1 Fehler), Paket-Tests 9/9 (`py.log`).
- Der eine App-Fehler ist T-0206 (Webseiten-Studio, Code-Editor) mit Zeitüberschreitung nach 1 min 52 s. Währenddessen lief außerhalb des Repos ein Unreal-Prüflauf einer zweiten Sitzung und belastete Prozessor und Grafikkarte.
- Nachprüfung ohne Fremdlast: T-0206 dreimal einzeln grün, je etwa 6,5 s (`t0206-nachpruefung.log`).
- Durchlauf 1 am selben Stand (vor der Korrektur einer Doku-Zeile): App 824/828 grün einschließlich T-0206. Der einzige Fehler war damals eine Doku-Prüfung (Vorbild-Analyse ohne Zeile „- Programme:“), behoben in 29633793b.
