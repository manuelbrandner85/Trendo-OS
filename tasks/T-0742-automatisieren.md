# T-0742: Aufgaben automatisieren – KI legt einen echten Ablauf an

**Ziel:** Aus einem Satz legt die KI einen echten Ablauf in der vorhandenen `WorkflowEngine` an (Auslöser, Bedingung, Schritte), der im Ablauf-Editor erscheint, änderbar und rückgängig machbar ist und erst nach Bestätigung startet; zeitgesteuerte Abläufe laufen nach ausdrücklicher Zustimmung auch bei geschlossenem Trendo (Windows-Aufgabe im eigenen Konto, ohne Admin).
**Herkunft:** Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 17, SYS-05; Lücken laut audit-4 Teil C: „läuft nur bei offenem Trendo“, „KI-Aktion nur Plan“.
**Branch:** claude/T-0742-automatisieren
**Dateien/Bereich:** `src/Trendo.Core/W2/Workflows.cs`, neu `src/Trendo.Core/W2/WorkflowSentence.cs`, neu `src/Trendo.Core/W2/WorkflowTaskPlan.cs`, `src/Trendo.App/MainWindow.Workflows.cs`; gemeinsam (unvermeidbar): `src/Trendo.App/MainWindow.ItemLinks.cs` (nur der Ablauf-Eintrag `WfLink`), `src/Trendo.App/Program.cs` (Schalter `--ablauf-faellig`), `.gitattributes` (eine Zeile für den Beleg). Tests, Beleg `reports/T-0742/`.
**Fertig, wenn:**
- [x] Satz → KI (über `Intel.AskAsync`, Claude-Abo oder lokale KI) → geprüfter Ablauf in der Engine, AUS, im Editor sichtbar und änderbar, „Rückgängig“ entfernt ihn
- [x] Einschalten eines KI-Ablaufs nur nach Rückfrage (Freigabe im Kern, an den Inhalt gebunden)
- [x] Bedingung „nur an diesen Wochentagen“; alte Freigaben bleiben gültig
- [x] Windows-Aufgabe nur nach Zustimmung, sichtbar, ein Klick entfernt sie; „neue Datei“/KI-Schritte zeigen deutlich „nur bei offenem Trendo“
- [x] Echtbeleg: Satz → Claude → Ablauf → echte Windows-Aufgabe startet TrendoOS.exe ohne Fenster → Datei entsteht (`reports/T-0742/`)
- [x] berührte Testklassen grün (kein Volltest, macht die Hauptsitzung)
**Nicht tun:** kein zweiter Workflow-Kern, kein Dienst, keine Registry, kein Admin; Agenten-Dateien (KI-Team-Paket) nicht anfassen; PromptAction-Kette (`ablauf.planen`) unverändert lassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0742-automatisieren.bericht.md`.
