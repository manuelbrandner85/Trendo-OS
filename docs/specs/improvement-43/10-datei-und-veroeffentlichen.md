# I43-10 Echte Datei, Veröffentlichen nur nach Freigabe

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 10. Wortlaut: „Immer echte Datei/Ordner am Ende; Veröffentlichen nur nach ausdrücklicher Freigabe.“

## Ziel und Nutzen

Am Ende einer Arbeit liegt eine Datei oder ein Ordner, den die Person auf dem PC öffnen kann. Ins Internet kommt die Arbeit nur, wenn sie genau diese Veröffentlichung freigibt. Ein Paket auf der Festplatte ist noch keine Veröffentlichung.

## Nutzerablauf

1. Der Bau endet mit einem Ordner oder einer Datei. Der Name wird genannt.
2. Die Person öffnet den Ordner und sieht `index.html` oder die Fachdatei.
3. Will sie ein Paket oder eine Veröffentlichung, fragt Trendo vorher.
4. Sie bestätigt diese eine Aktion. Ziel und Umfang stehen im Fragetext.
5. Ohne diese Bestätigung bleibt alles lokal. Eine Ablehnung oder ein Ablauf löscht die Entwurfsdatei nicht und lädt nichts hoch.

## Anforderungen

- Jeder erfolgreiche Lauf von Punkt 3 endet mit einem vorhandenen Ordner. Der Nachweis ist die Datei auf der Platte, nicht die Meldung allein.
- Veröffentlichen ist eine eigene Handlung. Sie ist nicht im Bau enthalten.
- Die Freigabe läuft über `src/Trendo.Core/ApprovalGate.cs`: eine Anfrage, eine Zustimmung, ein Ziel, Ablauf nach zehn Minuten, einmal ausführen (`ApprovalState`).
- Für die Webseite ist das vorhandene Paket lokal: `WebsiteStudio.RequestPack` und `Pack`. Der Text sagt, dass nichts hochgeladen wird. `Pack` schreibt `NICHT-HOCHGELADEN.txt` und eine Zip neben dem Ordner.
- Die Oberfläche fragt zweimal: „Paket vorbereiten“, dann „Ja, Paket nur auf diesem PC speichern“ (`MainWindow.W4Web.cs`). Dazwischen ruft sie `Approve` auf.
- Ein echter Upload zu einem Hoster ist in diesem Punkt nur nach einer weiteren, ausdrücklichen Freigabe zulässig. Der vorhandene Code führt keinen Upload aus.

## Betroffene Module

- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Build` (Ordner), `RequestPack`, `Pack` (`website.pack`)
- `src/Trendo.Core/ApprovalGate.cs`
- `src/Trendo.App/MainWindow.W4Web.cs` – `AskPack`, `DoPack`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – Seiteneffekte bleiben bei `AwaitingApproval`
- `src/Trendo.Core/DevStudio.cs` – `SafeName` für den Ordnernamen
- `src/Trendo.App/StudioCatalog.cs` – „Webseite bauen“

## Sicherheit und Datenschutz

- Lokal zuerst. Ordner und Zip liegen auf diesem PC.
- Cloud nur nach Opt-in. Ein Hoster, ein Git-Push oder ein Formularversand starten nicht aus dem Bau.
- Kein kostenpflichtiger Dienst. Kein kostenpflichtiges Hosting ist vorausgesetzt.
- Keine Geheimnisse in der Zip. `NICHT-HOCHGELADEN.txt` enthält den Hinweis, kein Token.
- Freigabe über `ApprovalGate`. `Pack` ruft `gate.Execute` mit der Aktion `website.pack` und dem Ordner als Ziel. Eine abgelaufene oder fremde Freigabe führt die Wirkung nicht aus (`ApprovalGate.Execute`).
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 3 erzeugt den Ordner, den dieser Punkt verlangt.
- Punkt 1 zeigt diesen Ordner.
- Punkt 9 stellt sicher, dass eine ältere Version ebenfalls eine echte Datei bleibt.
- Keine Abhängigkeit zum KI-Laden (#252). Der Ordner entsteht auch ohne Modell.

## Akzeptanzkriterien

- Nach einem erfolgreichen Bau existiert der Ordner und darin `index.html`. Die Meldung nennt denselben Ordner.
- Ohne „Paket vorbereiten“ und ohne Zustimmung entsteht keine Zip.
- Mit Zustimmung entsteht genau eine neue Zip neben dem Ordner, und `NICHT-HOCHGELADEN.txt` liegt bei. Es gibt keinen Netz-Upload.
- Eine zweite Zustimmung mit derselben verbrauchten Freigabe-Id packt nicht noch einmal (`ApprovalState.Executed`).
- Schlägt der Bau fehl, bleibt kein halber Ordner liegen (`Build` löscht den angefangenen Ordner im `catch`).

## Offene Fragen

- Die Quelle sagt „Veröffentlichen“. Der Code speichert ein lokales Paket und lädt nicht hoch. Ist das lokale Paket die ganze Bedeutung von „Veröffentlichen“, oder kommt später ein Hoster, der eine eigene Freigabe braucht?
- Der zweite Klick ruft `Approve` und `Pack` direkt nacheinander auf. Soll die freigebende Person von der bauenden Person getrennt sein? `ApprovalGate.Decide` verlangt `Permission.ApproveAction`. Heute ist das `Principal.LocalOwner` für beides.

## Ist-Stand

Ordner, lokale Zip und der Hinweis „nichts hochgeladen“ sind in `main`. Ein Upload-Ziel ist nicht implementiert. Diese Spezifikation verlangt keinen Hoster, den die Quelle nicht nennt.
