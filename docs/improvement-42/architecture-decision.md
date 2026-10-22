# Verbesserung 42 – Architektur

Ein gemeinsamer Ablauf in `Trendo.Core.PromptAction`. Facharbeit machen nur registrierte Adapter. Die KI schlägt höchstens ein JSON mit `actionId` und `parameters` vor. Sie ruft keine Werkzeuge auf und erteilt keine Freigabe.

## Warum diese Form

Der Auftrag verlangt eine gemeinsame Kette für alle Katalogstudios und echte Adapter nur dort, wo eine vorhandene Fachfunktion wiederverwendet wird. Eine zweite KI, ein zweites Freigabetor und ein zweites Geheimnislager würden vorhandene Grenzen umgehen. Deshalb bleiben `IntelligenceCore`, `ApprovalGate`, `AuditLog`, `SecretStore`, `PromptLibrary` und `ProjectStore` die einzigen Dienste.

Die Soll-Namen aus Abschnitt 4 des Auftrags sind so abgebildet:

| Soll | Umsetzung |
|---|---|
| StudioPromptRequest, StudioActionSpec, StudioExecutionContext, StudioArtifact, StudioExecutionResult | `src/Trendo.Core/PromptAction/Model.cs` |
| IStudioActionAdapter | dasselbe, Methode `SideEffect`, Eigenschaft `Effect` (C# erlaubt nicht denselben Namen für beides) |
| IStudioCapabilityProvider | `CapabilityCatalog` plus `DeclaredLevel` am Adapter |
| IArtifactValidator | `Validate` am Adapter und `QaResult` |
| IApprovalService | vorhandenes `ApprovalGate` |
| IArtifactStore | `RunStore` schreibt `auftraege/{id}/lauf.json` über `ProjectStore` |
| IPromptModel | `IntelligencePromptModel` ruft `AskAsync` |

`PromptActions.CreateDefault()` war im Kern zuerst leer. Die Fachadapter stehen jetzt in derselben Methode. Der Stand je Studio steht in der Matrix. Testadapter, die eine Wirkung nur im Test simulieren, sind dort nicht registriert.

## Ablauf

`StudioWorkflow` hält den Zustandsautomaten aus dem Auftrag. Leer, unbekanntes Studio oder mehr als 20 000 Zeichen enden ehrlich mit `Failed`. Ohne Adapter endet der Lauf mit `Unsupported` und ohne Ausgabedatei. Fehlende Pflichtfelder werden gefragt. `???` im Prüftext des Testadapters blockiert, statt einen Inhalt zu raten.

Prüfung endet beim Plan (`Plan steht`). `Los` schreibt danach die Datei. Ohne echte Datei gibt es kein `QA bestanden`. Die Vorschau ist der gelesene Text oder der Satz, dass es keine Textvorschau gibt. Eine Folgeeingabe ruft `Revise` auf und zählt die Fassung nur hoch, wenn schon eine Datei da war. `Undo` holt `lauf.json.bak` zurück. Der Profi-Schalter ändert nur die Anzeige, keine Rechte.

`Vorlage: id` füllt eine `PromptLibrary`-Vorlage. Fehlende Platzhalter werden Fragen. Fassung und Kennung der Vorlage stehen im Lauf.

`Studio: ID` oder `Weiterleiten: Titel` setzt das Zielstudio und schreibt den Grund in `RoutingReason`. Sonst: „Bleibt im aktuellen Studio.“

Seiteneffekte (`Message`, `Payment`, `Publish`, `DeleteOrRights`, `Security`, `Machine`, `ExternalAi`) laufen nur in `ApprovalGate.Execute`. Die Freigabe heißt `prompt:{actionId}`, das Ziel ist der Idempotenzschlüssel. Der Schlüssel hängt an Projekt, Studio, Aktion, Wirkungsart und den Parametern. Neue Inhalte brauchen eine neue Freigabe. `claimed` oder `blocked` im Wirkungsbuch `auftraege/_wirkung.json` wird nicht erneut ausgeführt.

Die Laufdatei hat `SchemaVersion` 1. Eine neuere Fassung wird nicht ausgeführt. Vor dem Überschreiben entsteht `lauf.json.bak`; `TryRollback` holt sie zurück.

## KI und Einwilligung

`IntelligencePromptModel` gilt nur als lokal, wenn jede Backend-Adresse ein Loopback ist. Ein externes Modell wird nur gefragt, wenn der Auftrag `PrivacyMode.External` trägt und `ConsentStore` (`ki/einwilligung.json`) eine Einwilligung mit Umfang, Offenlegung und Frist von höchstens einem Tag hat. Widerruf gewinnt. Sonst schreibt das Audit `ki.external.blocked` und es geht kein Aufruf raus. Schlüssel `approval`, `approved`, `consent`, `freigabe`, `sideeffect` aus einem Modelltext werden verworfen.

## Oberfläche

`MainWindow.PromptAction.cs` hängt eine schmale Leiste über den Seiteninhalt, den `SafeView` liefert. Der Startbildschirm bleibt frei. Die 3D-Seite füllt das Fenster bis auf wenige Pixel; dort liegt dieselbe Leiste im linken Rollbereich (`MainWindow.W4Studio3D.cs`), damit die Vorschau bei 1366×768 sichtbar bleibt. Texte: „Was soll dieses Studio tun?“, „Auftrag prüfen“, „Abbrechen“, „Einmal freigeben“, „Antwort übernehmen“. Der letzte Knopf und die Freigabe bleiben unsichtbar, bis der Lauf sie braucht. Farben bleiben `TileBg`, `Accent`, `Text`, `Muted`. Die Prüfung läuft über `Task.Run`, damit das Fenster nicht auf einen Modellaufruf wartet. Der Status „Wird wirklich ausgeführt“ steht, bevor der Hintergrundauftrag beginnt.

## Was der Kern nicht tut

Kein SMTP, keine Zahlung, kein Git-Push, kein Blender, kein Unreal, kein LibreOffice-PDF. Keine Änderung an Design 41, an der 120-Stunden-Regel und an den abgelehnten Verbesserungen 23, 25, 34, Language Studio 2.0. Verbesserung 35 bleibt offen.

## Stand nach den Fachadaptern

Jede Katalog-ID hat mindestens einen Adapter. `not_implemented` kommt im Katalog nicht mehr vor. `ready` kommt nicht vor. Gesperrte Aktionen liegen in `SystemPromptAdapter.cs`. Lokale Dateien nutzen vorhandene Typen (`SitePage.ToHtml`, `CleanRoom.Create`, `ResourceMonitor.Snapshot`, `OpenSource.Explain`, `TripleMode.DetectHere`, `WorkflowEngine.StepLabel`) und rufen die gefährliche Methode nicht auf. Verbesserung 42 ist nicht vollständig implementiert.
