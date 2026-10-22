# I42-18 Menschliche Freigabe

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 18. Wortlaut: „Aktionsgebundene Freigaben für Senden, Bezahlen, Publizieren, Geräte/Maschinen, Sicherheit, Boot und andere Seiteneffekte; Ablehnung/Timeout sicher behandeln.“

**Wörter:** Freigabe = eine einmalige menschliche Bestätigung für genau diese Aktion. Seiteneffekt = etwas, das nach außen wirkt: senden, zahlen, veröffentlichen, ein Gerät oder den Start des Rechners (Boot) anfassen. Timeout = die Freigabe läuft nach zehn Minuten ab. Ablehnung = die Person sagt nein, dann passiert die Wirkung nicht. Idempotenz = derselbe Vorgang läuft nicht ein zweites Mal.

## Ziel und Nutzen

Senden, Bezahlen, Veröffentlichen, Geräte, Sicherheit und Boot laufen nur nach einer einmaligen Freigabe für genau diese Aktion. Ein „Ja“ im Auftragstext gilt nicht. Ablehnung, Abbruch und Ablauf führen die Wirkung nicht aus und löschen den lokalen Entwurf nicht.

## Nutzerablauf

1. Eine Aktion mit `SideEffectClass` ungleich `None` bleibt nach dem lokalen Entwurf bei „Freigabe nötig“.
2. Die Leiste zeigt „Einmal freigeben“. Der Text nennt Aktion und die ersten Parameter.
3. Die Person bestätigt diese eine Aktion. `ApprovalGate.Execute` prüft Kennung, Aktion und Ziel erneut.
4. Ablehnung setzt „Abgebrochen“ und den Satz, dass nichts davon ausgeführt wurde.
5. Nach zehn Minuten ist die Freigabe abgelaufen. Ein zweiter Versuch mit derselben verbrauchten Freigabe läuft nicht.

## Anforderungen

- Das Tor ist `ApprovalGate` in `src/Trendo.Core/ApprovalGate.cs`. Zustände: `Pending`, `Approved`, `Denied`, `Executed`, `Expired`. Die Frist im Auftrag ist zehn Minuten (`TimeSpan.FromMinutes(10)`).
- Die Aktionskennung ist `prompt:` plus `ActionId`. Das Ziel ist der Idempotenzschlüssel aus Projekt, Studio, Aktion, Wirkungsart und Parametern (`StudioWorkflow.Key`, SHA-256).
- Die Wirkungsarten im Code heißen `SideEffectClass`: keine, Nachricht, Zahlung, Veröffentlichung, Löschen oder Rechte, Sicherheit, Maschine, externe KI. Die Werte sind `None`, `Message`, `Payment`, `Publish`, `DeleteOrRights`, `Security`, `Machine`, `ExternalAi`.
- Die Tabelle in Abschnitt 7 der Quelle nennt zusätzlich Boot, Treiber und OS-Konfiguration. Dafür gibt es keinen eigenen Enum-Wert. Ein Adapter darf sie nicht unter `None` ausführen.
- `CanPerformSideEffect == false` endet als „Nicht unterstützt“ mit dem Satz, dass nichts gesendet, bezahlt oder veröffentlicht wurde.
- `EffectLedger` in `RunStore.cs` führt `claimed`, `done` und `blocked` in `auftraege/_wirkung.json`. Ein `claimed` oder `blocked` Schlüssel wird nicht erneut ausgeführt. `Retry` und `Resume` wiederholen die Wirkung nicht.
- `Deny` und ein Abbruch vor der Wirkung rufen `ApprovalGate.Deny` auf. Die lokale Datei aus `Produce` bleibt liegen.
- Die Leiste lässt Antrag und Zustimmung durch `Principal.LocalOwner` zu. `Decide` verlangt `Permission.ApproveAction`.
- Abschnitt 10, Prüffall 3 und 14: ohne Sendefreigabe null Nachrichten. Risikoaktionen ohne Freigabe bleiben blockiert.
- Ein Dialog allein ist kein Tor. `Execute` prüft Ziel und Aktion unmittelbar vor dem Lauf.

## Betroffene Module

- `src/Trendo.Core/ApprovalGate.cs` – `Request`, `Approve`, `Deny`, `Execute`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Approve`, `Deny`, `RunSideEffect`, `Key`, `Describe`
- `src/Trendo.Core/PromptAction/RunStore.cs` – `EffectLedger`
- `src/Trendo.Core/PromptAction/Model.cs` – `SideEffectClass`
- `src/Trendo.Core/Rbac.cs` – `Permission.RequestAction`, `Permission.ApproveAction`
- `src/Trendo.App/MainWindow.PromptAction.cs` – Knopf `auftrag-freigabe`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Freigabe_einmal_und_retry`, `Freigabe_laeuft_ab`, `Prompt_ersetzt_keine_Freigabe`, `Kein_zweiter_Versuch_nach_Fehler`
- `tests/Trendo.Core.Tests/Tests.cs` – `QA004_ApprovalGate`

## Sicherheit und Datenschutz

- Lokal zuerst. Bis zur Freigabe bleibt die Wirkung im Entwurf.
- Cloud nur nach Opt-in. `ExternalAi` ist eine eigene Wirkungsklasse und zusätzlich Funktion 8.
- Kein kostenpflichtiger Dienst. Eine Zahlung wird nicht an einen Bezahldienst durchgereicht, nur weil der Text einen Betrag nennt. `Payment` ohne Freigabe bucht nichts.
- Keine Geheimnisse im Freigabetext. `Describe` kürzt und `Redact` ersetzt erkennbare Geheimnis-Muster. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe nur über `src/Trendo.Core/ApprovalGate.cs`. Der Knopf erscheint nur bei `RunStatus.AwaitingApproval`.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren. Sie legt kein zweites Tor neben `ApprovalGate` an (`CORE-11`).

## Abhängigkeiten

- Funktion 10 liefert den Adapter mit `Effect` und `CanPerformSideEffect`.
- Funktion 8 bleibt für die externe KI zuständig. Die Freigabe der Wirkung ersetzt die Einwilligung nicht.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ohne `Approve` führt `RunSideEffect` die Wirkung nicht aus.
- Mit einer Freigabe entsteht genau ein Beleg. `Retry` mit demselben Schlüssel sendet nicht ein zweites Mal.
- Eine abgelaufene Freigabe wirft `ApprovalException` und sendet nichts.
- Ein Prompt, der „freigabe“ enthält, setzt `ApprovalState` nicht auf `Approved`.

## Offene Fragen

- Boot, Treiber und OS-Konfiguration stehen in Abschnitt 7 und nicht als eigener `SideEffectClass`-Wert. Welcher vorhandene Wert (`Security` oder `Machine`) soll sie tragen, bis das entschieden ist?
- Antrag und Zustimmung laufen heute beide als `Principal.LocalOwner`. Die Quelle verlangt eine menschliche Bestätigung. Reicht dieselbe lokale Person?

## Ist-Stand

Tor, Idempotenz, Ablauf, Ablehnung und die Tests sind auf `main`. Die Leiste zeigt „Einmal freigeben“ nur bei „Freigabe nötig“. Ohne Adapter mit Wirkung erreicht ein Studio diesen Status nicht. `MailCenter` sendet keine Nachricht.
