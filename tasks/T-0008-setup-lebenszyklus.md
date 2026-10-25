# T-0008: Deinstallations-, Update- und Rückfalltest Edition C (IMP-16)

**Ziel:** Nachweis am Windows-PC, dass das Setup sauber installiert, aktualisiert, zurückfällt und deinstalliert, ohne Benutzerdaten zu verlieren.
**Branch:** claude/T-02xx (Sammelbranch)
**Auftrag:** Fertigstellungsplan C4. Manuel am 2026-10-25: Test auf diesem PC mit Sicherung.
**Dateien/Bereich:** `tools/messung/c4-setup-test.ps1` (neu), `reports/c4-setup/`, Bericht. Kein Produktcode.
**Fertig, wenn:**
- [ ] Deinstallieren, Installieren (alt), Update (neu), Rückfall (alt über neu), Deinstallieren und Wiederherstellen sind jeweils geprüft: Programmordner, Registry-Eintrag (nur Benutzer), Startmenü, App startet, Benutzerdaten bleiben.
- [ ] Am Ende ist dieselbe Version installiert wie vorher, die Benutzerdaten sind byte-gleich.
- [ ] Rohlog mit SHA-256 unter `reports/c4-setup/`.
**Nicht tun:** keine Admin-Rechte, keine systemweite Installation, keine Benutzerdaten verändern ohne Wiederherstellung, keine fremden Trendo-Prozesse beenden.
**Am Ende:** Bericht `tasks/T-0008-setup-lebenszyklus.bericht.md`.
