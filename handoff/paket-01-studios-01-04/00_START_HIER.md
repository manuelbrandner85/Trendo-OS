# TRENDO OS – ÜBERGABEPAKET 01 / 04 STUDIOS
**Entscheidungsstand 05.11.2026 · Umsetzungsstatus: APPROVED_SPEC (nicht gebaut)**

Dieses ZIP umfasst **genau vier fachliche Studios**:

1. Studio 1 – Spieleentwicklung (`CORE-06`): 18 genehmigte Verbesserungen **plus ausdrücklich bestätigte Unity-Integration**.
2. Studio 2 – 3D & Blender (`CORE-05`): 20 genehmigte Verbesserungen.
3. Studio 3 – Film, Video & VFX (`CORE-08`, Video): 20 genehmigte Verbesserungen.
4. Studio 4 – Musik, Audio & Sound (`STUDIO-MUSIK-01` + `CORE-08`, Audio): 20 genehmigte Verbesserungen.

**Gesamt: 78 bestätigte Verbesserungspunkte + zusätzliche verbindliche Unity-Entscheidung.**

Zusätzliche, paketübergreifende Nutzerentscheidung: **REA/Clean-Room-Nachbau nützlicher Fachfunktionen vor unnötigen Extra-Installationen**; Unity als engine-spezifische Integration in allen künftigen Paketen berücksichtigen, wo sie fachlich relevant ist. Siehe `03_GLOBALE_REGELN_REA_UNITY.md`.

**Dieses erste Paket enthält NICHT:** Studio 5 Bildbearbeitung (20× JA, reserviert für Paket 02 mit Studios 5–8); Studio 6–8 noch offen; Web-Verbesserungen (22× JA, für späteres zuständiges Fachpaket reserviert); alle übrigen Studios ohne eigene JA/NEIN-Entscheidung. Nichts hier hebt ältere NEIN-Entscheidungen auf.

## Übergabe an Claude Code
1. ZIP entpacken; Ordner lokal **innerhalb des bestehenden Trendo-OS-Repos** unter `handoff/paket-01-studios-01-04/` bereitstellen. Vorhandene Dateien nicht überschreiben; bei Konflikt abweichenden Namen verwenden.
2. `01_CLAUDE_STARTAUFTRAG.md` an Claude Code übergeben.
3. Claude beginnt READ-ONLY mit Ist-Audit, priorisierten Aufgaben und überprüfbaren Belegen. Keine Installation, kein Git-Push/Merge/Release ohne die nach Repo-Regeln erforderliche Freigabe.
4. `decisions/APPROVALS.csv`, `qa/TESTFAELLE.csv`, `studios/` als genaue Projektdaten nutzen.

**Wichtig:** 156 Testfälle sind konkret spezifizierte Positiv-/Negativ-Abnahmen, noch keine ausgeführten Tests. Dieses Paket wurde als Datei-/Schema-Archiv validiert, nicht in Trendo OS implementiert.
