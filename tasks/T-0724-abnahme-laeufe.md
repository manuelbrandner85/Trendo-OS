# T-0724: Abnahme – Echtläufe, Installation, Update, Deinstallation (Audit Phase E)

**Ziel:** Phase E des Profi-Audits (docs/audit/2026-11-16-profi-audit/BERICHT.md) mit echten Läufen belegen: Setup-Rundlauf abgeschottet, Unreal-Echtlauf, Echtlauf-Station vollständig, grobe Leistungszahlen.
**Freigabe:** Nutzer am 16.11.2026 („ja mach“, ausdrücklich inkl. Unreal-Lauf).
**Branch:** claude/T-0724-abnahme-laeufe (Basis claude/T-0685-b1-integration)
**Dateien/Bereich:** tools/echtlauf/ (Unreal-Fachlauf), scripts/setup-rundlauf-portabel.ps1 (neu), tests/Trendo.App.Tests/LeistungT0724UiTests.cs (neu), reports/echtlauf/2026-11-16/, reports/T-0724/, tasks/T-0724-*.
**Fertig, wenn:**
- [ ] Setup-Rundlauf (Installieren → Start mit Selbsttest → Update mit Datenerhalt → Deinstallieren) abgeschottet unter %TEMP%\trendo-t0724-*, ohne Registry/Admin/winget/msiexec, echtes Profil/Startmenü/Desktop nachweislich unberührt – oder ehrlich BLOCKIERT.
- [ ] Unreal-Echtlauf über die Station (eigenes Projekt im Temp-Ordner, kompilieren, Unreal-Tests, Startlauf, Zeitgrenzen).
- [ ] Echtlauf-Station einmal vollständig (mit --langsam), Bericht reports/echtlauf/2026-11-16/ aktualisiert.
- [ ] Startzeit kalt/warm und Öffnen großer Projekte (100 000 Zellen, 40 000 Dreiecke) gemessen, ohne erfundene Ziele.
- [ ] Rohlogs + SHA256SUMS unter reports/T-0724/, Datenschutz-Grep leer.
**Nicht tun:** Kein Volltest, keine Produktänderungen außer echten Fehlerbehebungen, QA-017/QA-019/Codesignatur nie als bestanden, keine Projekte des Nutzers anfassen, Unreal nicht ändern.
**Am Ende:** Branch pushen, Bericht `tasks/T-0724-abnahme-laeufe.bericht.md`.
