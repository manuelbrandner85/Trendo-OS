# I42-19 Projektgrenze, Schutz und Forensik

**Status:** zur Freigabe durch Manuel

**Gesperrt in dieser Funktion:** Verbesserung 23 (Multi-User & Secure Identity) und Verbesserung 34 (Antivirus) werden nicht gebaut. Die vorhandene Projektgrenze, die vorhandene Forensik und das vorhandene Tor bleiben.

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 19. Wortlaut: „Sandbox, Berechtigungen, Redaction, Protokolle und die bestehende 120-Stunden-Forensikregel ohne Regression.“

**Wörter:** Sandbox = der Auftrag bleibt in seinem Projektordner und greift nicht auf fremde Ordner zu. Berechtigung = die Person darf diese Aktion überhaupt anstoßen. Schwärzung (Redaction) = erkennbare Geheimnisse werden im Text ersetzt. Protokoll (Audit) = eine kurze Liste, was ausgelöst wurde. Forensik = lokale Sicherung von Hinweisen, höchstens 120 Stunden, danach Löschung. Regression = eine bestehende Regel darf sich dabei nicht verändern.

## Ziel und Nutzen

Ein Auftrag bleibt in seinem Projekt. Pfade, Anhänge und Geheimnisse kommen nicht in ein zweites Projekt und nicht in die Protokolle. Die Forensik löscht Fälle nach höchstens 120 Stunden, mit Warnung 24 Stunden vorher. Diese Fristen bleiben, wie sie im Code stehen.

## Nutzerablauf

1. Der Auftrag nennt `projectId`. Der Ausführungskontext nennt dieselbe Kennung. Stimmen sie nicht überein, endet der Lauf sofort.
2. `Rbac.Demand` verlangt `Permission.RequestAction`, bevor der Auftrag anläuft.
3. Dateien löst `ProjectStore.ResolveInProject` nur im Projekt auf. Ein Ausbruch wird `ProjectIsolationException`.
4. Ein Anhang, der ein Verweis nach außen ist, wird nicht gelesen.
5. Protokolle nennen Phase, Längen und Kennungen. Geheimnis-Muster im Freigabetext werden ersetzt. Fälle der Forensik leben höchstens 120 Stunden.

## Anforderungen

- Die Projektgrenze ist `ProjectStore` in `src/Trendo.Core/ProjectStore.cs`. `Deny` meldet „Zugriff außerhalb des Projekts verweigert.“
- Rechte sind `Rbac` in `src/Trendo.Core/Rbac.cs`. Diese Funktion führt kein zweites Kontenmodell ein. Verbesserung 23 bleibt abgelehnt (`IMP-23`, `REJECTED`). Normale Dateirechte und die vorhandene Projektisolation bleiben, wie Abschnitt 12 des Masterprompts sie lässt.
- Schwärzung: `StudioWorkflow.Redact` und `CrashLog.Redact` in `src/Trendo.Core/CrashLog.cs` (Benutzerpfade werden zu `<benutzer>`).
- Audit: `src/Trendo.Core/AuditLog.cs`. `prompt.phase` speichert Zeichenzahl, nicht den Text. `ki.ask` speichert Längen.
- Anhänge: `TakeAttachments` liest kleine Textdateien nur, um die Zeichenzahl in `AttachmentNotes` zu schreiben. Der Dateiinhalt wird dort nicht abgelegt. Über 100 000 Byte bleibt der Hinweis „zu groß, nicht gelesen“. Ein Symlink (`LinkTarget`) bricht ab.
- Forensik: `ForensicsLab.MaxLife` ist 120 Stunden, `WarnBefore` ist 24 Stunden (`src/Trendo.Core/W3/Forensics.cs`). Eine längere Lebensdauer wirft `ArgumentOutOfRangeException`. `PurgeExpired` löscht abgelaufene Fälle. Diese Spezifikation ändert die Semantik nicht.
- Verbesserung 34 bleibt abgelehnt. `SPEC-12` im Katalog sagt, die 120-Stunden-Regel werde nicht angetastet. Ein Antivirus-Paket entsteht nicht.
- Zwei Projekte tauschen keine Artefakte, keinen Kontext und keine Schlüssel (Abschnitt 10, Prüffall 7).
- Ein Anhang oder Prompt, der eine Freigabe behauptet, umgeht `ApprovalGate` nicht (Prüffall 9).
- Schlüssel bleiben in `src/Trendo.Core/Secrets.cs` (`SecretStoreFactory`, auf Linux `InMemorySecretStore` laut `docs/improvement-42/repository-audit.md`).

## Betroffene Module

- `src/Trendo.Core/ProjectStore.cs`
- `src/Trendo.Core/Rbac.cs`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `TakeAttachments`, `Redact`, `TryNeed`
- `src/Trendo.Core/AuditLog.cs`
- `src/Trendo.Core/CrashLog.cs`
- `src/Trendo.Core/Secrets.cs`
- `src/Trendo.Core/W3/Forensics.cs` – `MaxLife`, `WarnBefore`, `Sha256`, `PurgeExpired`
- `src/Trendo.Core/ApprovalGate.cs`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Projekte_bleiben_getrennt`, `Anhang_injiziert_keine_Freigabe`, `Audit_ohne_Geheimnis`, `Forensik_120_Stunden`, `Entscheidungen_bleiben`

## Sicherheit und Datenschutz

- Lokal zuerst. Projektordner, Audit und Forensik bleiben auf diesem PC.
- Cloud nur nach Opt-in. Die Grenze selbst öffnet kein Netz.
- Kein kostenpflichtiger Dienst. Schutz und Forensik setzen kein Bezahl-Abo voraus.
- Keine Geheimnisse in Audit, Freigabetext und Absturzprotokoll. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Ein Prompt und ein Anhang ersetzen sie nicht.
- Verbesserung 23 und Verbesserung 34 bleiben gesperrt. Verbesserung 25 bleibt gesperrt und ist kein Teil dieser Grenze. Language & Localization Studio 2.0 bleibt gesperrt. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 1 legt Anhänge in diese Grenze.
- Funktion 18 bleibt das einzige Tor für Wirkungen.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Zwei Projekte: ein Pfad aus Projekt A ist in Projekt B keine gültige Auflösung.
- Ein Anhang mit einem Verweis nach außen endet als Fehlschlag und wird nicht gelesen.
- `ForensicsLab.MaxLife` bleibt 120 Stunden, `WarnBefore` 24 Stunden. 121 Stunden werden abgelehnt. Abgelaufene Fälle löscht `PurgeExpired`.
- `Entscheidungen_bleiben` findet `IMP-23`, `IMP-25` und `IMP-34` als `REJECTED`, `LANG-2.0` als `REJECTED` und `IMP-35` als `UNDECIDED`.
- Das Audit des Auftragslaufs enthält den Auftragstext nicht.

## Offene Fragen

- `TakeAttachments` liest den Anhang, speichert aber nur die Zeichenzahl. Soll ein späterer Adapter den Inhalt sehen, und wenn ja, nur nach einer weiteren Prüfung auf Prompt-Injection?
- `repository-audit.md` nennt `InMemorySecretStore` auf Linux. Diese Spezifikation ändert `Secrets.cs` nicht. Bleibt das der Ablageort, bis ein anderer Auftrag das entscheidet?

## Ist-Stand

Projektgrenze, Rechteprüfung, Schwärzung, Audit und die 120-Stunden-Konstanten sind im Code. Die genannten Tests liegen in `PromptActionTests.cs`. Diese Spezifikation ändert `decisions.yaml` nicht. Verbesserung 23 und 34 sind nicht implementiert und werden hier nicht begonnen.
