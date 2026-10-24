# I42-20 Tests, Diagnose und Wiederaufnahme

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 20. Wortlaut: „Unit-, Integrations-, E2E-, Sicherheits-, Abbruch-/Recovery-Tests und nachvollziehbare Betriebsdiagnose ohne sensitive Datenlecks.“

**Wörter:** Unit-Test = Prüfung einer einzelnen Funktion. Integrations-Test = Prüfung, ob Bausteine zusammenpassen. E2E = Prüfung des ganzen Wegs von der Eingabe bis zur Datei. Sicherheits-Test = Prüfung, dass Grenzen und Freigaben halten. Recovery = der Auftrag lässt sich nach Abbruch oder Neustart wieder aufnehmen, ohne eine Wirkung zu doppeln. Diagnose = der sichtbare Stand, ohne Auftragstext und ohne Schlüssel. Datenleck = ein Geheimnis oder der volle Auftragstext steht dort, wo er nicht hingehört.

## Ziel und Nutzen

Die Auftragskette hat Tests, die einen Lauf wirklich ausführen, und eine Diagnose, die den Stand zeigt, ohne den Auftragstext oder Schlüssel auszugeben. Abbruch, Neustart und ein zweiter Versuch lassen den Zustand wiederherstellbar und führen eine Wirkung nicht doppelt aus. Verbesserung 35 wird dadurch nicht aktiviert.

## Nutzerablauf

1. Ein Entwickler startet die vorhandenen Testprojekte.
2. Ein fehlgeschlagener Auftrag zeigt Phase, Fehler und Warnung in der Leiste.
3. Nach einem Abbruch oder einem offenen Wirkungsversuch lädt `Resume` den Stand und sendet nicht noch einmal.
4. `Retry` auf `Failed` oder `Executing` prüft erneut, lässt einen `claimed`-Schlüssel aber stehen.
5. Ein Absturz schreibt ein geschwärztes Protokoll. Der Auftragstext steht dort nicht, weil der Lauf ihn nicht an `CrashLog` übergibt.

## Anforderungen

- Vorhandene Tests der Kette:
  - `tests/Trendo.Core.Tests/PromptActionTests.cs` für Ablauf, Freigabe, Einwilligung, Projekte, Audit, Vorlage, Routing, Fassung, Forensik und die gesperrten Entscheidungen
  - `tests/Trendo.App.Tests/PromptBarTests.cs` für die Leiste auf Brief, Start und 3D und für die 54 Katalogkennungen
  - `tests/Trendo.Core.Tests/Tests.cs` – `QA004_ApprovalGate`
- Ein End-to-End-Lauf der acht Studiogruppen aus Abschnitt 6 braucht einen registrierten Adapter. Den gibt es nicht. Solche Läufe sind nicht als bestanden zu führen.
- Diagnose ist das Audit `prompt.phase` und der Statustext der Leiste. Ein zweites Telemetrie-Ziel gibt es nicht. Das Audit enthält keine Geheimnisse und nicht den Auftragstext (Funktion 19).
- Wiederaufnahme ist `Resume` und `Retry`. `TryRollback` holt die Laufdatei zurück (Funktion 16).
- `CrashLog` in `src/Trendo.Core/CrashLog.cs` schwärzt Benutzerpfade und behält höchstens 20 Dateien. Das ist das Absturzprotokoll von SYS-13, nicht ein Produkt von Verbesserung 35.
- `src/Trendo.Core/W3/SelfHealing.cs` bleibt der vorhandene Baustein. Diese Funktion schaltet Verbesserung 35 nicht an. `X-02` im Katalog sagt: „Verbesserung 35 ist nicht entschieden und wird nicht aktiviert.“
- Abschnitt 10, Prüffall 8: Abbruch, Neustart und Retry bleiben konsistent, ohne doppelte Wirkung. Prüffall 12: Module 41 sowie 23, 25, 34, Language Studio 2.0 und 35 bleiben ohne Regression und ohne stille Aktivierung.
- `src/Trendo.Core/Progress.cs` liest `requirements.yaml`. Diese Spezifikation ändert die Datei nicht und setzt keinen Status auf `TESTED` oder `PC_VERIFIED`.
- QA-017, QA-019 und die Codesignatur werden nicht als bestanden markiert.

## Betroffene Module

- `tests/Trendo.Core.Tests/PromptActionTests.cs`
- `tests/Trendo.App.Tests/PromptBarTests.cs`
- `tests/Trendo.Core.Tests/Tests.cs`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Resume`, `Retry`, `Cancel`
- `src/Trendo.Core/AuditLog.cs`
- `src/Trendo.Core/CrashLog.cs`
- `src/Trendo.Core/W3/SelfHealing.cs` – vorhanden, nicht als Verbesserung 35 aktivieren
- `src/Trendo.Core/Progress.cs`
- `docs/spec/decisions.yaml` – nur lesend, der Test `Entscheidungen_bleiben` prüft die Werte

## Sicherheit und Datenschutz

- Lokal zuerst. Tests legen ihre Ordner unter dem temporären Verzeichnis an (`trendo-v42-`, `trendo-forensik-`) und löschen sie im `Dispose`.
- Cloud nur nach Opt-in. Die Tests `Cloud_ohne_Einwilligung_bleibt_stumm` und `Einwilligung_widerrufen` erwarten, dass kein Cloud-Aufruf entsteht.
- Kein kostenpflichtiger Dienst. Die Tests setzen kein Bezahl-Abo voraus.
- Keine Geheimnisse in der Diagnose. `Audit_ohne_Geheimnis` ist der vorhandene Test. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Recovery führt `Execute` nicht ein zweites Mal aus.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktionen 1 bis 19 sind der Gegenstand der Tests. Ein grüner Kern-Test ist keine Abnahme der fehlenden Adapter.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich. Tests, die dessen Branch brauchen, liegen hier nicht.

## Akzeptanzkriterien

- `dotnet test` der drei Testprojekte bleibt der Nachweis für den vorhandenen Kern. Ein rot bleibender Test ist kein stilles „bestanden“.
- `Resume` auf `claimed` sendet nicht noch einmal. `Retry` auf `done` ebenso.
- Die Diagnose eines Auftragslaufs enthält den Prompt-Text und keinen Schlüssel.
- `IMP-35` bleibt `UNDECIDED`. `SelfHealing` wird durch diese Spezifikation nicht zum Paket Verbesserung 35.
- Es wird kein E2E der acht Studiogruppen als bestanden geführt, solange `CreateDefault()` leer ist.

## Offene Fragen

- Reicht das Audit als Betriebsdiagnose, oder soll die Leiste die letzten `prompt.phase`-Zeilen zeigen?
- Sobald der erste Fachadapter kommt: welcher eine Happy-Path und welcher eine Failure-Pfad gelten als der erste E2E-Beleg? Die Quelle nennt das Briefstudio als erstes P0.

## Ist-Stand

Die genannten Testdateien sind auf `main`. Sie prüfen den Kern mit einem Testadapter, der nicht in `CreateDefault()` steht. Eine Abnahme der zwanzig Funktionen als implementiert ist das nicht. `requirements.yaml` bleibt unverändert.
