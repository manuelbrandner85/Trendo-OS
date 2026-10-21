# Baulog – eine Datei pro Eintrag
Ab 21.10.2026 21:15 MESZ: **jeder Eintrag ist eine eigene Datei**, damit parallele Builder keine Konflikte erzeugen.

- Name: `docs/build-log/<JJJJ-MM-TT>-<HHMM>-<modul>.md`, z. B. `2026-10-21-2120-STUDIO-LETTER-01.md`
- Inhalt: 1. Zeile `# <Modul> – <Kurztitel>`, danach was gebaut/getestet wurde, Nachweis-Pfade, offene Punkte.
- Bestehende Dateien nie ändern; Korrekturen als neue Datei.
- Ältere Einträge (bis 21:10) stehen unverändert in `docs/BUILD_LOG.md` (eingefroren).
