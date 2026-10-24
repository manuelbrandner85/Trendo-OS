# I42-02 Studio-Kontext und Weiterleitung

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 2. Wortlaut: „Zuordnung zum aktuellen Studio oder bewusstes Routing auf ein anderes Studio, stets mit sichtbarer Begründung.“

**Wörter:** Weiterleitung (Routing) = der Auftrag wechselt das Studio nur, wenn die Person das ausdrücklich schreibt. Begründung = ein sichtbarer Satz, warum der Auftrag hier bleibt oder woanders hingeht. Katalog = die feste Liste aller Studios.

## Ziel und Nutzen

Der Auftrag bleibt im Studio, das die Person gerade offen hat. Nur wenn sie ein anderes Studio ausdrücklich nennt, wechselt Trendo dorthin und sagt in einem Satz, warum. Ein stiller Wechsel findet nicht statt.

## Nutzerablauf

1. Die Leiste merkt sich die Kennung des offenen Studios (`ModuleOf` bzw. `CORE-05` auf der 3D-Seite).
2. Ohne weitere Angabe bleibt der Auftrag dort. Die Begründung lautet „Bleibt im aktuellen Studio.“
3. Schreibt sie `Studio:` und eine Katalog-Kennung oder `Weiterleiten:` und den Titel, setzt der Ablauf das Zielstudio.
4. Die Begründung erscheint in der Leiste (`auftrag-detail`).
5. Ein unbekannter Name endet mit einer Fehlermeldung. Es wird kein Studio erfunden.

## Anforderungen

- Die Kennungen und Titel kommen aus `CapabilityCatalog`, und der Katalog stimmt mit `StudioCatalog.ById` überein (54 Einträge, Test `Katalog_und_Abdeckung_stimmen`).
- `StudioWorkflow.Route` wertet die Felder `studio` und `weiterleiten` aus (`PromptFields`).
- Die Begründung liegt in `RunState.RoutingReason` und im Ergebnis `StudioExecutionResult.RoutingReason`.
- Ein Auftrag, der `SPEC-07` („Texte übersetzen“) nennt, öffnet Language & Localization Studio 2.0 nicht. Der Katalogeintrag lässt keinen neuen Übersetzungsadapter zu.
- Ein Auftrag, der `SPEC-12` oder `X-02` nennt, aktiviert Verbesserung 34 nicht und Verbesserung 35 nicht.
- Die sichtbare Begründung ist ein Satz. Ein interner Code allein genügt nicht.

## Betroffene Module

- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Route`
- `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` – `Find`, `Title`, `Profiles`
- `src/Trendo.Core/PromptAction/PromptFields.cs` – Zeilen `Feld: Wert`
- `src/Trendo.App/MainWindow.PromptAction.cs` – zeigt `RoutingReason` in `auftrag-detail`
- `src/Trendo.App/StudioCatalog.cs`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Routing_mit_Grund`

## Sicherheit und Datenschutz

- Lokal zuerst. Die Zuordnung liest nur den Auftragstext und den Katalog auf diesem PC.
- Cloud nur nach Opt-in. Eine Weiterleitung ist kein Netzaufruf.
- Kein kostenpflichtiger Dienst.
- Keine Geheimnisse in der Begründung. Sie nennt Titel und Kennung, keine Schlüssel. Ablage bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` ersetzt die Weiterleitung nicht. Ein neues Studio mit Wirkung nach außen wartet trotzdem auf die Freigabe von Funktion 18.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 1 liefert den Text und das Ausgangsstudio.
- Funktion 9 liefert die erlaubten Kennungen.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ohne `Studio:` und ohne `Weiterleiten:` bleibt die Kennung gleich, und die Begründung heißt „Bleibt im aktuellen Studio.“
- `Weiterleiten:` mit einem Titel aus dem Katalog setzt diese Kennung und nennt den Titel in der Begründung.
- Eine unbekannte Kennung endet als Fehlschlag „Das genannte Studio gibt es nicht.“ Es entsteht keine Datei.
- `SPEC-07` bekommt durch die Weiterleitung keinen Übersetzungsadapter.

## Offene Fragen

- Die Weiterleitung steht nur als Textzeile im Auftrag. Soll die Leiste das Zielstudio zeigen, bevor die Arbeit dort beginnt?
- Reicht bei `SPEC-07` die vorhandene Meldung „Nicht unterstützt“, solange kein Adapter registriert ist?

## Ist-Stand

`Route` und die Anzeige der Begründung sind im Ablauf und in der Leiste. Der Test `Routing_mit_Grund` prüft die Begründung an einem Testadapter. In der Produktion ist kein Fachadapter registriert, deshalb endet ein bekanntes Studio danach als „Nicht unterstützt“.
