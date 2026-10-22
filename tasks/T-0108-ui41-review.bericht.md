# T-0108 Bericht: Review und Freigabe

**Auftrag:** Vorher/Nachher, dann Stopp für die Frage an Manuel: JA oder NEIN zum Premium-Desktop und zur Übertragung auf alle Studios.
**Branch:** `cursor/bauplan-t0005-t0012-f36c`

Der Rollout steht schon auf `main`. PR #237 ist gemergt (`2eef442`). `tasks/UI-041-PREMIUM-EXPERIENCE.bericht.md` hält fest, dass der Besitzer JA gesagt hat und die Hülle auf die übrigen Studios liegt. Diese Sitzung hat die Frage nicht erneut gestellt und das Design nicht noch einmal gebaut.

Windows-Bilder unter `reports/design-41/pc-nachher/` gibt es nicht. Die vorhandenen Vergleiche sind Linux-Aufnahmen mit `tools/Trendo.Render`.

Blender und Unreal Engine sind auf dem PC installiert. In den Linux-Bildern fehlt ihr Dock-Eintrag, weil diese Box die Windows-Programme nicht startet. Das ist kein Befund über den PC.

## Geänderte Dateien

- `reports/design-41/vergleich/UNTERSCHIEDE.md` – der Dock-Satz nennt die PC-Installationen.
- `tasks/UI-041-PREMIUM-EXPERIENCE.bericht.md` – dieselben zwei Sätze, sonst unverändert.
- Dieser Bericht.

## Testergebnis

Wie T-0005: Api 15 grün, Core 531 grün plus 10 übersprungen, App 222 grün. Kein Rohlog mit SHA-256 unter `reports/` für einen PC-Lauf. Deshalb kein `TESTED` und kein `PC_VERIFIED`.

## Offene Punkte

- QA-017 am Gerät, QA-019 und die Codesignatur sind nicht bestanden.
- DPI 125 % und 150 % am Windows-PC fehlen.
- SPEC-07 bleibt abgelehnt. Logo bleibt Entwurf A.
- IMP-41 in `decisions.yaml` erwähnt den Rollout nach gesondertem JA. Der vorhandene Bericht sagt, dieses JA ist erfolgt. Diese Sitzung hat das nicht neu bestätigt.

## Nicht geprüft

- Kein Bildvergleich am Windows-PC, keine Stoppuhr, kein Kontrastmessgerät, kein zweiter Durchgang durch jede Studio-Karte von Hand.
