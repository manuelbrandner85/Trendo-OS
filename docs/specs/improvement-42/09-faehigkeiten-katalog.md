# I42-09 Fähigkeiten je Studio

**Status:** zur Freigabe durch Manuel

**Gesperrte Einträge in dieser Funktion:** `SPEC-07` bleibt bei Language & Localization Studio 2.0 gesperrt. `SPEC-12` nimmt Verbesserung 34 nicht auf. `X-02` aktiviert Verbesserung 35 nicht. Das Register selbst ist damit nicht als Ganzes gesperrt.

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 9. Wortlaut: „Für jeden bestehenden Studioeintrag eindeutige unterstützte Aktionen, Adapter, Outputtypen und Capability-Level deklarieren.“

**Wörter:** Fähigkeit = was ein Studio wirklich tun kann. Adapter = das Programmstück für eine konkrete Aktion. Ausgabe (Output) = die Art der Datei, die dabei entstehen darf. Reife (Capability-Level) = bereit, teilweise, blockiert oder noch nicht eingebaut. „Bereit“ gilt nur mit einem echten, nachgewiesenen Lauf.

## Ziel und Nutzen

Jedes Studio aus dem Katalog hat genau eine Zeile: was es schon kann, welche Aktion vorgesehen ist, welche Eingaben und Ausgaben dazugehören und wie reif das ist. „Bereit“ steht nur dort, wo ein registrierter Adapter und ein echter Lauf das belegen. Alles andere bleibt ehrlich auf „nicht eingebaut“, „teilweise“ oder „blockiert“.

## Nutzerablauf

1. Die Leiste zeigt eine Zeile `CapabilityCatalog.UiLine` für das offene Studio, zum Beispiel „nicht eingebaut“ und den Blocker.
2. Die Person sieht daran, dass ein Knopf keine Arbeit vortäuscht, die es nicht gibt.
3. Eine Matrix listet jede Katalog-Kennung einmal.
4. Kommt später ein Adapter dazu, wechselt die Reife nur nach dessen `DeclaredLevel` und nur mit Beleg.
5. Ein Studio ohne Zeile gibt es in diesem Ablauf nicht. Der Auftrag endet mit „Dieses Studio gibt es nicht.“

## Anforderungen

- `StudioCatalog.ById` hat 54 Einträge. `CapabilityCatalog.Ids` nennt dieselben Kennungen, Titel und Bereiche (`Katalog_und_Abdeckung_stimmen`).
- Reife ist `CapabilityLevel`: `Ready`, `Partial`, `Blocked`, `NotImplemented`. `Level` liest nur registrierte Adapter. Ohne Adapter ist jeder Eintrag `NotImplemented`.
- `ready` nur mit real ausgeführtem End-to-End-Beleg. `IStudioActionAdapter.Evidence` ist leer, solange kein Test die Aktion wirklich ausführt. Der Testadapter in `tests/Trendo.Core.Tests/PromptActionTests.cs` ist nicht registriert und steht nicht in der Matrix.
- `SPEC-07` („Texte übersetzen“): Blocker „Language & Localization Studio 2.0 bleibt abgelehnt. Kein neuer Übersetzungsadapter.“ Das Basisstudio bleibt im Katalog. Ein 2.0-Ausbau wird nicht deklariert.
- `SPEC-12`: Blocker „Verbesserung 34 bleibt abgelehnt. Die 120-Stunden-Regel wird nicht angetastet.“
- `X-02`: Blocker „Verbesserung 35 ist nicht entschieden und wird nicht aktiviert.“
- `CORE-11` legt kein zweites Freigabe- oder Geheimnissystem an. `CORE-01` und `SYS-12` ändern Design 41 nicht aus einem Prompt.
- `CapabilityCatalog.Markdown` erzeugt die Tabelle. `docs/improvement-42/studio-capability-matrix.md` muss damit übereinstimmen (`Matrix_deckt_den_Katalog`).
- Abschnitt 10, Prüffall 15: jede echte Katalog-Kennung genau einmal, keine Phantom-Kennung, keine fehlende Kennung.

## Betroffene Module

- `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` – `StudioProfile`, `Level`, `UiLine`, `Markdown`
- `src/Trendo.App/StudioCatalog.cs` – `ById`, 54 Einträge
- `src/Trendo.Core/PromptAction/Model.cs` – `CapabilityLevel`, `StatusText.German`
- `src/Trendo.App/MainWindow.PromptAction.cs` – Text `auftrag-faehigkeit`
- `docs/improvement-42/studio-capability-matrix.md`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Matrix_deckt_den_Katalog`
- `tests/Trendo.App.Tests/PromptBarTests.cs` – `Katalog_und_Abdeckung_stimmen`

## Sicherheit und Datenschutz

- Lokal zuerst. Die Matrix ist eine Liste im Programm, kein Abruf.
- Cloud nur nach Opt-in. Eine Fähigkeitszeile startet keinen Anbieter.
- Kein kostenpflichtiger Dienst.
- Keine Geheimnisse in der Matrix. Sie nennt Bausteine und Blocker, keine Schlüssel. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Eine Zeile „bereit“ schaltet eine Wirkung nach außen nicht frei.
- Verbesserung 23 und 25 bleiben abgelehnt und sind keine Katalogaktion. Verbesserung 34 bleibt an `SPEC-12` gesperrt. Language & Localization Studio 2.0 bleibt an `SPEC-07` gesperrt. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren (`X-02`).

## Abhängigkeiten

- Funktion 10 registriert die Adapter, aus denen `Level` die Reife liest. Ohne sie bleibt alles `not_implemented`.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich. `X-05` („Kostenlose KI finden“) bleibt ein Katalogeintrag ohne Adapter.

## Akzeptanzkriterien

- `StudioCatalog.ById.Count` und `CapabilityCatalog.Ids` sind gleich, und jede Kennung kommt einmal vor. Der Test verlangt 54.
- Mit `PromptActions.CreateDefault()` ist jede Kennung `NotImplemented`.
- Die Datei `docs/improvement-42/studio-capability-matrix.md` stimmt mit `CapabilityCatalog.Markdown` überein.
- `SPEC-07`, `SPEC-12` und `X-02` behalten die Blocker-Sätze aus dem Katalog. Ein Adapter für Language 2.0, Verbesserung 34 oder Verbesserung 35 wird nicht registriert.

## Offene Fragen

- Die Matrix unter `docs/improvement-42/` ist die Datei, die der Test vergleicht. Soll eine spätere Adapter-Änderung dieselbe Datei neu schreiben, oder bleibt sie der Stand bis zur nächsten Abnahme?
- `DeclaredLevel.Ready` verlangt `Evidence` als Testname. Welcher vorhandene Test darf der erste Beleg sein, ohne einen Lauf zu erfinden?

## Ist-Stand

Katalog, Matrix und die beiden Tests sind auf `main`. Alle 54 Zeilen sind `not_implemented`, weil `CreateDefault()` leer ist. Die Leiste zeigt „nicht eingebaut“ und den Blocker.
