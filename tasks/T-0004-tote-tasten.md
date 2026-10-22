# T-0004: IMP-13 – keine toten Schaltflächen + Studio-Abnahmetests

**Ziel:** Ein automatischer Test findet jede sichtbare Schaltfläche/Kachel/Karte ohne Wirkung. Pro Studio ein Abnahmetest: Seite öffnet sich, Überschrift = Katalogname, mindestens eine Hauptaktion ist bedienbar.
**Branch:** grok/T-0004-tote-tasten
**Dateien/Bereich:** `tests/Trendo.App.Tests/` (neue Tests), nur falls ein echter toter Knopf gefunden wird: minimale Reparatur in `src/Trendo.App/MainWindow*.cs`. `docs/build-log/2026-10-22-tote-tasten.md`. `AGENTS.md` (nur eine Regel, siehe 4).

**Vorgehen:**
1. Headless-UI-Test (Avalonia.Headless, wie `StudioClickTests.cs`): Startseite, Alle Studios und jede Studio-Seite aus `StudioPages`/`PageModules` öffnen. Jede sichtbare, aktivierte `Button` ohne Click-Handler und ohne Command sammeln (Reflection auf das Click-Event oder Klick + Prüfung, dass sich Ansicht, Audit-Log oder Zustand ändert). Ausnahmen nur mit Begründung in einer kleinen Liste im Test.
2. Pro Studio mit Katalogeintrag ein Abnahmefall (Theory/MemberData): Seite öffnet, `page-title` = `StudioCatalog.ById[id].Name`, keine tote Taste.
3. Gefundene tote Tasten: reparieren, wenn klar ist, was sie tun sollen (kleinste Änderung); sonst deaktivieren mit Tooltip „Noch nicht verfügbar“ und im Bericht listen. SPEC-07 nicht ausbauen.
4. `AGENTS.md` unter Ablauf ergänzen: „Terminalbefehle immer einzeln und einfach: keine Pipes (|), kein ;, kein &&, keine Variablen – sonst bricht die Sitzung ab.“
5. Alle drei Testprojekte laufen lassen.

**Fertig, wenn:**
- [ ] neuer Test grün, Liste toter Tasten im Bericht (vorher/nachher)
- [ ] Core/App/Api grün (Zahlen in den Bericht)

**Nicht tun:** keine Navigation umbauen, keine Katalognamen ändern, nichts außerhalb des Bereichs, `.grok/config.toml` nicht anfassen. Terminalbefehle nur einzeln, ohne Pipes oder Verkettung.
**Am Ende:** `git commit -m "…"`, `git push -u origin grok/T-0004-tote-tasten`, Bericht `tasks/T-0004-tote-tasten.bericht.md`. Keinen PR.