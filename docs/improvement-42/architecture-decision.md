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

In Phase A+B lieferte `PromptActions.CreateDefault()` eine leere Liste. Seit Phase 1 des Masters bis 94 steht dort nur das Arbeitsblatt für X-11. Die übrigen 53 Studios bleiben `not_implemented`. Testadapter in `tests/Trendo.Core.Tests/PromptActionTests.cs` sind nicht registriert und erscheinen nicht in der Matrix.

## Ablauf

`StudioWorkflow` hält den Zustandsautomaten aus dem Auftrag. Leer, unbekanntes Studio oder mehr als 20 000 Zeichen enden ehrlich mit `Failed`. Ohne Adapter endet der Lauf mit `Unsupported` und ohne Ausgabedatei. Fehlende Pflichtfelder werden gefragt. `???` im Prüftext des Testadapters blockiert, statt einen Inhalt zu raten.

`Vorlage: id` füllt eine `PromptLibrary`-Vorlage. Fehlende Platzhalter werden Fragen. Fassung und Kennung der Vorlage stehen im Lauf.

`Studio: ID` oder `Weiterleiten: Titel` setzt das Zielstudio und schreibt den Grund in `RoutingReason`. Sonst: „Bleibt im aktuellen Studio.“

Seiteneffekte (`Message`, `Payment`, `Publish`, `DeleteOrRights`, `Security`, `Machine`, `ExternalAi`) laufen nur in `ApprovalGate.Execute`. Die Freigabe heißt `prompt:{actionId}`, das Ziel ist der Idempotenzschlüssel. Der Schlüssel hängt an Projekt, Studio, Aktion, Wirkungsart und den Parametern. Neue Inhalte brauchen eine neue Freigabe. `claimed` oder `blocked` im Wirkungsbuch `auftraege/_wirkung.json` wird nicht erneut ausgeführt.

Die Laufdatei hat `SchemaVersion` 1. Eine neuere Fassung wird nicht ausgeführt. Vor dem Überschreiben entsteht `lauf.json.bak`; `TryRollback` holt sie zurück.

## KI und Einwilligung

`IntelligencePromptModel` gilt nur als lokal, wenn jede Backend-Adresse ein Loopback ist. Ein externes Modell wird nur gefragt, wenn der Auftrag `PrivacyMode.External` trägt und `ConsentStore` (`ki/einwilligung.json`) eine Einwilligung mit Umfang, Offenlegung und Frist von höchstens einem Tag hat. Widerruf gewinnt. Sonst schreibt das Audit `ki.external.blocked` und es geht kein Aufruf raus. Schlüssel `approval`, `approved`, `consent`, `freigabe`, `sideeffect` aus einem Modelltext werden verworfen.

## Oberfläche

`MainWindow.PromptAction.cs` hängt eine schmale Leiste über den Seiteninhalt, den `SafeView` liefert. Der Startbildschirm bleibt frei. Die 3D-Seite füllt das Fenster bis auf wenige Pixel; dort liegt dieselbe Leiste im linken Rollbereich (`MainWindow.W4Studio3D.cs`), damit die Vorschau bei 1366×768 sichtbar bleibt. Texte: „Was soll dieses Studio tun?“, „Auftrag prüfen“, „Abbrechen“, „Einmal freigeben“, „Antwort übernehmen“. Der letzte Knopf und die Freigabe bleiben unsichtbar, bis der Lauf sie braucht. Farben bleiben `TileBg`, `Accent`, `Text`, `Muted`. Die Prüfung läuft über `Task.Run`, damit das Fenster nicht auf einen Modellaufruf wartet. Der Status „Wird wirklich ausgeführt“ steht, bevor der Hintergrundauftrag beginnt.

## Was diese Phase nicht tut

Keine Brief-, Office-, Mail-, Geschäfts-, Software-, 3D-, Spiele- oder Medienadapter. Kein SMTP, keine Zahlung, kein Git-Push, kein Blender, kein Unreal, kein LibreOffice-PDF. Keine Änderung an Design 41, an der 120-Stunden-Regel und an den abgelehnten Verbesserungen 23, 25, 34, Language Studio 2.0. Verbesserung 35 bleibt offen.

## Nachtrag: Ein Arbeitsplatz (Master bis 94, Phase 1)

`PromptActions.CreateDefault()` registriert nur `ArbeitsplatzBlattAdapter` für X-11. Er schreibt `arbeitsplatz/blatt-vN.md` über `ProjectStore` und liefert zwei Prüfungen, die die Datei wirklich lesen. `SideEffectClass` bleibt `None`. Die Seite „Ein Arbeitsplatz“ zeigt das Ergebnis von `StudioWorkflow` und setzt keinen eigenen Status. 53 Studios bleiben ohne Adapter. Verbesserung 86 ist nicht eingebaut. Die 120-Stunden-Regel ist nicht angefasst.
