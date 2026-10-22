# I42-07 Vorhandene KI, keine zweite Engine

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 7. Wortlaut: „`Intel.AskAsync` bzw. reale vorhandene KI-Abstraktion verwenden; keine zweite Model-Routing-/Conversation-Engine.“

**Wörter:** `AskAsync` = der eine vorhandene Eingang für Fragen an die KI. Engine = dieses eine System. Eine zweite Engine wäre ein paralleles System und ist hier verboten. Loopback = eine Adresse nur auf diesem PC, zum Beispiel `127.0.0.1`. Modellwahl = welches KI-Modell benutzt wird. Das Laden neuer Modelle ist der KI-Laden.

## Ziel und Nutzen

Wenn der Auftrag einen Vorschlag braucht, fragt Trendo die KI, die schon eingebaut ist. Es entsteht keine zweite Modellverwaltung, kein zweiter Verlauf und keine zweite Wahl der Quelle. Ohne erreichbare lokale KI bleibt der Auftrag bei Feldern und Rückfragen.

## Nutzerablauf

1. Die Leiste hängt `IntelligencePromptModel` an `Intel` (`IntelligenceCore`).
2. Nur wenn ein Adapter existiert und `TryParse` nichts findet, ruft `Suggest` das Modell.
3. `CompleteAsync` ruft `IntelligenceCore.AskAsync` mit der Frage, der Systemanweisung und `UseKnowledge: false`.
4. Die Antwort wird als JSON gelesen. Alles andere wird verworfen.
5. Ist keine lokale KI erreichbar, bleibt der Satz aus `AskAsync`: gerade ist keine KI auf diesem PC erreichbar. Der Auftrag rät die Aktion nicht.

## Anforderungen

- Der eine Eingang ist `IntelligenceCore.AskAsync` in `src/Trendo.Core/W2/IntelligenceCore.cs`. Die Hülle ist `IntelligencePromptModel` in `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs`.
- `IsLocal` ist wahr, wenn jede Backend-Adresse ein Loopback ist (`BackendsAreLocal`, `IntelligencePromptModel.Loopback`).
- `CompleteAsync` liefert null, sobald `IsLocal` falsch ist. Diese Hülle spricht keinen Cloud-Anbieter an.
- `AskAsync` protokolliert Quelle, Modell und Längen, nicht den Fragetext (`ki.ask`). Der lokale Verlauf `ki/verlauf.jsonl` speichert Frage und Antwort auf dem PC (`IntelligenceCore.Save`).
- Die Leiste der Aufträge setzt `PrivacyMode.Local` und zeigt „Cloud-KI ist aus.“
- Eine zweite Routing- oder Conversation-Engine wird nicht angelegt. `KiRequest` und `IKiBackend` bleiben die vorhandenen Typen.
- Das Modell bekommt keine Shell, keinen Browser, kein Git, keine Zahlung und kein Gerät. Werkzeuge ruft nur ein registrierter Adapter auf (Funktion 10).

## Betroffene Module

- `src/Trendo.Core/W2/IntelligenceCore.cs` – `AskAsync`, `KiRequest`, `KiAnswer`
- `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Suggest`
- `src/Trendo.App/MainWindow.PromptAction.cs` – `new IntelligencePromptModel(Intel, BackendsAreLocal)`
- `src/Trendo.App/MainWindow.W2.cs` – bisheriger Aufruf von `Intel.AskAsync` außerhalb der Leiste
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Lokale_KI_gibt_nicht_frei`, `Cloud_ohne_Einwilligung_bleibt_stumm`

## Sicherheit und Datenschutz

- Lokal zuerst. `CompleteAsync` läuft nur bei Loopback.
- Cloud nur nach Opt-in. Diese Hülle führt den Cloud-Aufruf auch dann nicht aus. Die Einwilligung steht in Funktion 8.
- Kein kostenpflichtiger Dienst. `IntelligenceCore` beschreibt die Quellen als lokal. Ein Bezahl-Abo ist keine Voraussetzung.
- Keine Geheimnisse im Audit von `AskAsync`. Der Fragetext steht im lokalen `ki/verlauf.jsonl`, nicht im Audit. Schlüssel bleiben in `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. `Suggest` wirft Freigabe-Felder aus dem Modelltext weg.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Das Laden der Modelle und die automatische Wahl sind der KI-Laden, Pull Request #252 (Entwurf, nicht gemergt). Das gilt erst nach Merge von #252. Pfade, die nur auf dem Branch von #252 liegen, sind hier keine vorhandenen Module.
- Der Aufruf `AskAsync` auf diesem Branch bleibt gültig für eine bereits eingestellte lokale Quelle.
- Funktion 3 ist der einzige Leser des JSON-Vorschlags.

## Akzeptanzkriterien

- Ein Auftrag ohne Modell und ohne parsebare Felder rät keine Aktion und legt keine Datei an.
- `CompleteAsync` gibt null zurück, wenn eine Backend-Adresse kein Loopback ist. Es geht kein HTTP-Aufruf an diese Adresse aus dieser Hülle.
- Das Audit `ki.ask` enthält Längen, Quelle und Modell, nicht den Fragetext.
- Es gibt nach dieser Funktion weiterhin eine Klasse `IntelligenceCore` und keine zweite Engine daneben.

## Offene Fragen

- `Suggest` prüft die Einwilligung für eine externe Quelle, und `CompleteAsync` liefert bei `IsLocal == false` trotzdem null. Soll die Hülle so bleiben, bis #252 gemergt ist?
- `UseKnowledge: false` lässt `CORE-10` weg. Soll der Auftrag das Wissen des Projekts später ausdrücklich anschalten?

## Ist-Stand

Die Hülle und der Aufruf `AskAsync` sind auf `main`. Die Leiste setzt sie ein. Ohne Adapter ruft `Validate` `Suggest` nicht auf, weil der Lauf vorher bei „Nicht unterstützt“ endet. Der KI-Laden #252 ist nicht gemergt.
