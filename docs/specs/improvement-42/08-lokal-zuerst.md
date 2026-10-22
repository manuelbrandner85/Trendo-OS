# I42-08 Lokal zuerst, Einwilligung, Widerruf

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 8. Wortlaut: „Anbieterberechtigungen, Datenschutz, Kostenhinweis, explizite Einwilligung, Offline-Verhalten und Widerruf.“

**Wörter:** Lokal zuerst = Daten bleiben auf diesem PC. Opt-in = die Person schaltet eine externe KI ausdrücklich ein. Einwilligung = die Zustimmung mit Umfang, Kostenhinweis, Zweck und Ablaufdatum. Widerruf = die Zustimmung gilt ab dann nicht mehr. Offline = es ist keine KI im Netz erreichbar, und Trendo wechselt nicht still dorthin. Anbieter = ein Dienst, der die KI-Antwort liefert.

## Ziel und Nutzen

Die Arbeit bleibt auf dem PC, solange die Person nichts anderes erlaubt. Eine externe KI braucht eine Einwilligung mit Umfang, Kosten- und Datensatz und einem Ende. Widerruf und Ablauf gelten sofort. Ein stiller Wechsel ins Netz findet nicht statt.

## Nutzerablauf

1. Die Leiste zeigt „Cloud-KI ist aus.“ und sendet `PrivacyMode.Local`.
2. Will die Person eine externe Quelle, sieht sie vorher Umfang, Kosten, Daten und Zweck.
3. Die Einwilligung gilt höchstens einen Tag (`ConsentStore.Grant`).
4. Ohne diese Einwilligung oder nach Widerruf bleibt der Auftrag lokal. Ein Cloud-Aufruf wird nicht gestartet.
5. Ist keine lokale Quelle erreichbar, sagt `AskAsync` das. Es gibt keinen stillen Netz-Fallback.

## Anforderungen

- Standard ist aus. `ProviderConsent.Deny` und eine fehlende Datei `ki/einwilligung.json` erlauben nichts (`ConsentStore.Load`).
- `Grant` verlangt Umfang und Offenlegung. Die Frist ist länger als null und höchstens ein Tag. Eine Dauerfreigabe gibt es nicht.
- `AllowsExternal` ist nur wahr, wenn erlaubt, nicht widerrufen, Ablauf in der Zukunft und Umfang gesetzt sind.
- `Revoke` setzt `ExternalAllowed` auf falsch und schreibt `RevokedAt`. Das Audit nennt `ki.consent.revoke` ohne den Auftragstext.
- `Suggest` schreibt `ki.external.blocked`, wenn das Modell nicht lokal ist und die Einwilligung fehlt, abgelaufen ist oder `PrivacyMode` nicht `External` ist.
- Die Leiste hat keinen Knopf, der `Grant` aufruft. Sie setzt immer `PrivacyMode.Local`.
- Abschnitt 10, Prüffall 4: ohne Opt-in kein Cloud-Request. Eine verweigerte oder entzogene Einwilligung bleibt wirksam.
- Werte in `ProviderConsent` sind keine Geheimnisse. Schlüssel liegen weiter in `src/Trendo.Core/Secrets.cs`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/ConsentStore.cs` – `ProviderConsent`, `Grant`, `Revoke`
- `src/Trendo.Core/PromptAction/Model.cs` – `PrivacyMode`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Suggest`
- `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs` – `IsLocal`, `Loopback`
- `src/Trendo.Core/W2/IntelligenceCore.cs` – Offline-Satz, wenn keine Quelle erreichbar ist
- `src/Trendo.App/MainWindow.PromptAction.cs` – Satz „Cloud-KI ist aus.“
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Cloud_ohne_Einwilligung_bleibt_stumm`, `Einwilligung_widerrufen`

## Sicherheit und Datenschutz

- Lokal zuerst. `PrivacyMode.Local` ist der Wert der Leiste.
- Cloud nur nach Opt-in, mit Umfang, Offenlegung und Frist, widerrufbar über `Revoke`.
- Kein kostenpflichtiger Dienst. Ein Bezahl-Abo ist keine Voraussetzung. Die Einwilligung nennt Kosten, sie bucht sie nicht.
- Keine Geheimnisse in `ki/einwilligung.json`. Das Audit speichert Umfang und Zeitpunkt, keinen Schlüssel. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Die Einwilligung für eine externe KI ist die Freigabe einer Zahlung, eines Versands oder einer Veröffentlichung nicht. `SideEffectClass.ExternalAi` bleibt eine eigene Wirkung.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Die Liste der kostenlosen Online-Dienste und die automatische Modellwahl sind der KI-Laden, Pull Request #252 (Entwurf, nicht gemergt). Das gilt erst nach Merge von #252.
- `ConsentStore` auf diesem Branch ist die Einwilligung. Dateien, die nur auf dem Branch von #252 liegen, sind hier keine vorhandenen Module.
- Funktion 7 ruft auf diesem Branch nur lokale Quellen auf.

## Akzeptanzkriterien

- Ohne Datei `ki/einwilligung.json` ist `AllowsExternal` falsch.
- Eine Einwilligung ohne Offenlegung oder mit einer Frist über einem Tag wird abgelehnt.
- Nach `Revoke` bleibt `AllowsExternal` falsch, auch wenn `ExpiresAt` noch in der Zukunft liegt.
- Ein Modell, das nicht lokal ist, erzeugt bei `PrivacyMode.Local` keinen Vorschlag und den Audit-Eintrag `ki.external.blocked`.

## Offene Fragen

- Die Leiste kann `Grant` nicht auslösen. Wo bestätigt die Person Umfang, Kosten und Zweck, bevor `PrivacyMode.External` überhaupt gesetzt werden darf?
- `IntelligencePromptModel.CompleteAsync` liefert bei einem nicht lokalen Modell null, auch wenn die Einwilligung gilt. Bleibt der Cloud-Aufruf damit bis nach dem Merge von #252 zu?

## Ist-Stand

`ConsentStore`, die beiden Tests und der Satz „Cloud-KI ist aus.“ sind auf `main`. Die Leiste setzt die Einwilligung nicht. Der KI-Laden #252 ist nicht gemergt.
