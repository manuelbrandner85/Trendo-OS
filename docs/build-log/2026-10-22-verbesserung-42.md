# Verbesserung 42 – Kern (Phase A+B)

Gemeinsamer Ablauf in `src/Trendo.Core/PromptAction`: Rückfrage, Vorlage, Routing, lokale Datei, Freigabe über das vorhandene `ApprovalGate`, Einwilligung, Projektgrenze, Audit ohne Auftragstext. `PromptActions.CreateDefault()` ist leer. Die Matrix nennt 54 Studios, alle `not_implemented`.

Die Leiste hängt an `SafeView`, nicht an Startmenü oder Fensterrahmen. Auf der 3D-Seite liegt sie im linken Rollbereich, damit die Vorschau bei 1366×768 sichtbar bleibt (ohne Leiste 55 px, Grenze 48 px).

Tests: Api 15, Core 511 bestanden und 10 übersprungen, App 207 bestanden. Beleg: `docs/improvement-42/test-and-evidence-report.md`.

Offen: die acht Studiogruppen. Nächster Schritt ist das Briefstudio über `Letters.WriteDocx`, PDF nur wenn `soffice` wirklich da ist.
