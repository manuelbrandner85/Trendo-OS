# Verbesserung 42 – Übersicht der Einzelspezifikationen

**Status dieser Ausarbeitung:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md` (Stand `main`, Commit `af4f3a7`)

## Anzahl: zwanzig Funktionen

`docs/spec/IMPROVEMENT_42.md` ist vorhanden. Abschnitt 3 heißt „Die 20 obligatorischen Funktionsbereiche“ und enthält die Nummern 1 bis 20. Es sind **zwanzig Funktionen**. Es wurde keine Funktion ergänzt und keine weggelassen.

Die Zeile am Anfang der Quelle sagt: Die kurze Liste ist mit „JA“ freigegeben. Das ist kein Nachweis, dass die Funktionen schon gebaut sind. Diese Zeile bleibt in der Quelldatei stehen. Die ausformulierten Texte in diesem Ordner haben den Status **zur Freigabe durch Manuel**. Die Statusdateien `requirements.yaml` und `docs/spec/decisions.yaml` sind unverändert. Nichts aus diesem Ordner ist als gebaut, getestet oder am PC geprüft markiert (die internen Stufen heißen `IMPLEMENTED`, `TESTED` und `PC_VERIFIED`).

Abschnitt 6 der Quelle nennt eine Reihenfolge der Studios: Brief, Office, E-Mail, Business, Software, 3D, Spiele, Medien. Das sind keine zusätzlichen Funktionen. Sie hängen an Funktion 10, sobald ein Adapter registriert ist. Ein Adapter ist das Programmstück, das eine Studio-Aktion wirklich ausführt. Die Liste `PromptActions.CreateDefault()` in `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs` ist auf diesem Stand leer. Deshalb führt noch kein Studio über diese Kette eine Fachdatei aus.

Die vorhandenen Unterlagen unter `docs/improvement-42/` (Bestandsaufnahme, Architektur, Matrix, Freigaben, Testbericht, Bedienungsanleitung) bleiben unverändert. Diese Übersicht ersetzt sie nicht.

## Wörter

- **Studio:** ein Arbeitsbereich in Trendo, zum Beispiel Briefe oder 3D.
- **Auftrag:** der Text, den die Person dem Studio gibt.
- **Adapter:** das Programmstück, das eine Aktion dieses Studios wirklich ausführt. Fehlt er, entsteht keine Scheindatei.
- **Opt-in:** die Person schaltet etwas ausdrücklich ein. Sonst bleibt es aus.
- **Einwilligung:** die Zustimmung, Daten an eine externe KI zu schicken, mit Zweck, Umfang und Ablauf.
- **Freigabe:** eine einmalige menschliche Bestätigung, bevor etwas nach außen wirkt (senden, zahlen, veröffentlichen, ein Gerät steuern).
- **Prüfsumme (SHA-256):** ein Fingerabdruck der Datei. Damit prüft man, ob die Datei unverändert ist.
- **Protokoll (Audit):** eine kurze Liste, was ausgelöst wurde, ohne den vollen Auftragstext und ohne Schlüssel.
- **Lokal zuerst:** Dateien und Prüfung bleiben auf diesem PC.
- **Fassung:** die Versionsnummer des gespeicherten Auftrags.
- **Reife:** ob eine Aktion fertig, teilweise, blockiert oder noch nicht eingebaut ist.
- **Forensik:** lokale Sicherung von Hinweisen. Hier höchstens 120 Stunden, danach wird gelöscht.
- **KI-Laden:** das Laden lokaler Modelle und die Wahl kostenloser Online-Dienste. Das ist Pull Request #252 und noch nicht übernommen.

## Die zwanzig Funktionen

| Nr. | Funktion, Wortlaut gekürzt | Datei | Gesperrt | KI-Laden #252 |
|---|---|---|---|---|
| 1 | Einheitliche Eingabe pro Studio | [01-einheitliche-eingabe.md](01-einheitliche-eingabe.md) | nein | keine Abhängigkeit |
| 2 | Studio-Kontext und Weiterleitung | [02-studio-kontext-routing.md](02-studio-kontext-routing.md) | nein | keine Abhängigkeit |
| 3 | Auftrag erkennen | [03-auftrag-erkennen.md](03-auftrag-erkennen.md) | nein | KI-Vorschlag: erst nach Merge von #252 |
| 4 | Prüfung von Studio, Aktion und Format | [04-schema-pruefung.md](04-schema-pruefung.md) | nein | keine Abhängigkeit |
| 5 | Gezielte Rückfragen | [05-gezielte-rueckfragen.md](05-gezielte-rueckfragen.md) | nein | keine Abhängigkeit |
| 6 | Vorlagen aus der PromptLibrary | [06-prompt-vorlagen.md](06-prompt-vorlagen.md) | nein | keine Abhängigkeit |
| 7 | Vorhandene KI, keine zweite Engine | [07-vorhandene-ki.md](07-vorhandene-ki.md) | nein | erst nach Merge von #252 |
| 8 | Lokal zuerst, Einwilligung, Widerruf | [08-lokal-zuerst.md](08-lokal-zuerst.md) | nein | Online-Dienste und automatische Wahl: erst nach Merge von #252 |
| 9 | Fähigkeiten je Studio | [09-faehigkeiten-katalog.md](09-faehigkeiten-katalog.md) | nein; Einträge `SPEC-07`, `SPEC-12` und `X-02` siehe Datei | keine Abhängigkeit |
| 10 | Fachadapter ausführen | [10-adapter-ausfuehrung.md](10-adapter-ausfuehrung.md) | nein | keine Abhängigkeit |
| 11 | Bearbeitbare Artefakte | [11-bearbeitbare-artefakte.md](11-bearbeitbare-artefakte.md) | nein | keine Abhängigkeit |
| 12 | Fortschritt und Belege | [12-fortschritt-und-belege.md](12-fortschritt-und-belege.md) | nein | keine Abhängigkeit |
| 13 | Deterministische Fachprüfung | [13-deterministische-pruefung.md](13-deterministische-pruefung.md) | nein | keine Abhängigkeit |
| 14 | Fachprüfung je Studio | [14-fach-qa.md](14-fach-qa.md) | nein | keine Abhängigkeit |
| 15 | Vorschau aus dem Artefakt | [15-vorschau.md](15-vorschau.md) | nein | keine Abhängigkeit |
| 16 | Änderungsrunden | [16-aenderungsrunden.md](16-aenderungsrunden.md) | nein | keine Abhängigkeit |
| 17 | Export und echte Datei | [17-export-und-datei.md](17-export-und-datei.md) | nein | keine Abhängigkeit |
| 18 | Menschliche Freigabe | [18-freigabe.md](18-freigabe.md) | nein | keine Abhängigkeit |
| 19 | Projektgrenze, Schutz und Forensik | [19-projekt-sicherheit-forensik.md](19-projekt-sicherheit-forensik.md) | nein; baut 23 und 34 nicht | keine Abhängigkeit |
| 20 | Tests, Diagnose und Wiederaufnahme | [20-tests-diagnose-wiederaufnahme.md](20-tests-diagnose-wiederaufnahme.md) | nein | keine Abhängigkeit |

## Gesperrte und nicht zu aktivierende Punkte

Geprüft gegen `docs/spec/decisions.yaml` und `docs/spec/MASTERPROMPT.md` Abschnitt 12. Keine der zwanzig Funktionen ist eine dieser Erweiterungen. Keine Einzelspezifikation ist deshalb als Ganzes gesperrt. Wo ein Katalogeintrag die Sperre berührt, steht das in der Datei.

| Punkt | Register | Markierung in diesem Ordner |
|---|---|---|
| 23 | `IMP-23`, abgelehnt (`REJECTED`). Mehrbenutzer und eine eigene Identitätswelt | gesperrt, nicht Bestandteil der zwanzig Funktionen. Die normale Projektgrenze bleibt in Funktion 19 |
| 25 | `IMP-25`, abgelehnt (`REJECTED`). Ein großes Paket zum Umziehen von Daten | gesperrt, nicht Bestandteil der zwanzig Funktionen |
| 34 | `IMP-34`, abgelehnt (`REJECTED`). Ein eigenes Antivirus-Paket | gesperrt. Der Katalogeintrag `SPEC-12` in Funktion 9 nimmt das Paket nicht auf |
| 86 | kein Eintrag `IMP-86`, kein Abschnitt „Verbesserung 86“ im Masterprompt, kein Treffer in `requirements.yaml` | im Projekt nicht vorhanden. Nichts wird auf einen fehlenden Punkt 86 bezogen |
| Language 2.0 | `LANG-2.0` und `SPEC-07`, abgelehnt (`REJECTED`). Der große Ausbau „Texte übersetzen“ | gesperrt. Das kleine Basisstudio bleibt. Ein 2.0-Ausbau wird nicht beschrieben |
| 35 | `IMP-35`, nicht entschieden (`UNDECIDED`). Selbstständige Fehlerbehebung als Gesamtpaket | **nie aktivieren**. Der Katalogeintrag `X-02` bleibt dabei. `decisions.yaml` wird hier nicht geändert |

## KI-Laden

Pull Request [#252](https://github.com/manuelbrandner85/Trendo-OS/pull/252) heißt „KI-Laden: lokale KI, kostenlose Online-Dienste, automatische Wahl“. Stand dieser Ausarbeitung: Entwurf, noch nicht in `main` übernommen (nicht gemergt). Wo eine Funktion dieses Laden oder diese Wahl braucht, steht in der Datei der Satz **erst nach Merge von #252**. Dateien, die nur auf dem Branch von #252 liegen, gelten hier nicht als vorhandene Module.

Betroffen sind der KI-Vorschlag in Funktion 3, die Modellwahl in Funktion 7 und die Online-Dienste in Funktion 8. Der vorhandene Aufruf `IntelligenceCore.AskAsync` bleibt der eine Eingang für Fragen an die KI. Eine zweite, parallele KI entsteht hier nicht.

## Was der Code auf `main` schon enthält

Der gemeinsame Ablauf liegt unter `src/Trendo.Core/PromptAction/`. Die Eingabeleiste liegt in `src/Trendo.App/MainWindow.PromptAction.cs`. `docs/improvement-42/architecture-decision.md` beschreibt, welcher Wunschname aus der Quelle auf welchen vorhandenen Typ zeigt. `PromptActions.CreateDefault()` registriert keinen Adapter. Jedes Studio steht deshalb auf „nicht eingebaut“ (im Code `not_implemented`). Diese Übersicht übernimmt keine Testergebnisse als Abnahme der zwanzig Funktionen. Der Ist-Stand in den Einzelspezifikationen nennt nur Dateien, die auf `main` (Commit `af4f3a7`) liegen.

`src/Trendo.App/StudioCatalog.cs` hat 54 Studios. Der Test `Katalog_und_Abdeckung_stimmen` in `tests/Trendo.App.Tests/PromptBarTests.cs` verlangt dieselbe Anzahl und dieselben Kennungen im Fähigkeitenkatalog.

## Gemeinsame Grenzen

Für alle zwanzig Funktionen gilt die Quelle und die Vorgabe dieser Ausarbeitung:

- Lokal zuerst. Dateien und Prüfung bleiben auf dem PC.
- Eine Cloud-KI nur nach ausdrücklichem Opt-in. Die Zustimmung ist widerrufbar und nennt den Zweck.
- Kein kostenpflichtiger Dienst als Voraussetzung. Trendo setzt kein Bezahl-Abo voraus.
- Keine Geheimnisse in Seiten, Protokollen, Commits oder Vorschau-Dateien. Schlüssel bleiben in `src/Trendo.Core/Secrets.cs`.
- Veröffentlichen, Senden, Zahlen, Gerätebefehle und externe KI laufen über das vorhandene Freigabetor `src/Trendo.Core/ApprovalGate.cs`.
- Die Namen der Studios kommen aus `src/Trendo.App/StudioCatalog.cs`.
