# Verbesserung 43 – Übersicht der Einzelspezifikationen

**Status dieser Ausarbeitung:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md` (Stand `main`, Commit `af4f3a7`)

## Abweichung: elf Punkte, nicht zwanzig Funktionen

`docs/spec/IMPROVEMENT_43.md` ist vorhanden. Die Datei enthält **elf nummerierte Punkte**, nicht zwanzig Funktionen.

Die zweite Zeile der Quelle lautet: „Freigegeben von Manuel am 22.10.2026 (alle 11 Punkte: JA). Ergänzt Verbesserung 42.“ Darunter steht die Leitlinie: so einfach wie möglich für Nutzer, aber hoch professionell. Lokale KI ist der Standard, ein Bezahl-Abo ist nicht nötig.

Diese Übersicht und die elf Dateien daneben formulieren **nur diese elf Punkte** aus. Es wurden keine neun weiteren Funktionen ergänzt.

Die zwanzig Funktionen aus Abschnitt 3 von `docs/spec/IMPROVEMENT_42.md` gehören zu Verbesserung 42. Sie sind hier nicht als Verbesserung 43 übernommen.

Die Zeile „alle 11 Punkte: JA“ bleibt in der Quelldatei stehen. Sie ist die Freigabe der kurzen Liste. Die ausformulierten Spezifikationen in diesem Ordner haben den Status **zur Freigabe durch Manuel**. `requirements.yaml` ist unverändert. Nichts aus diesem Ordner ist `IMPLEMENTED`, `TESTED` oder `PC_VERIFIED`.

## Die elf Punkte

| Nr. | Funktion, Wortlaut gekürzt | Datei | Gesperrt | KI-Laden #252 |
|---|---|---|---|---|
| 1 | Live-Vorschau in jedem Studio | [01-live-vorschau.md](01-live-vorschau.md) | nein | keine Abhängigkeit |
| 2 | Eingabe: Plan, „Los“, Schritte, Folgeeingabe | [02-eingabe-plan-los.md](02-eingabe-plan-los.md) | nein | erst nach Merge von #252 |
| 3 | Profi-Webseiten-Baukasten per Eingabe | [03-webseiten-baukasten.md](03-webseiten-baukasten.md) | nein | KI-Texte: erst nach Merge von #252 |
| 4 | Moderne Web-Techniken | [04-web-techniken.md](04-web-techniken.md) | nein | keine Abhängigkeit |
| 5 | Blender nach Webseite (GLB) | [05-blender-nach-webseite.md](05-blender-nach-webseite.md) | nein | keine Abhängigkeit |
| 6 | Unreal nach Webseite | [06-unreal-nach-webseite.md](06-unreal-nach-webseite.md) | nein | keine Abhängigkeit |
| 7 | Stil-Vorlagen mit einem Klick | [07-stil-vorlagen.md](07-stil-vorlagen.md) | nein | keine Abhängigkeit |
| 8 | Qualitätsprüfung mit Note und „Jetzt beheben“ | [08-qualitaetspruefung.md](08-qualitaetspruefung.md) | nein | keine Abhängigkeit |
| 9 | Versionen und Rückgängig | [09-versionen-und-rueckgaengig.md](09-versionen-und-rueckgaengig.md) | nein | keine Abhängigkeit |
| 10 | Echte Datei; Veröffentlichen nur nach Freigabe | [10-datei-und-veroeffentlichen.md](10-datei-und-veroeffentlichen.md) | nein | keine Abhängigkeit |
| 11 | Einfach-Modus und Profi-Modus | [11-einfach-und-profi.md](11-einfach-und-profi.md) | nein | Einfach-Modus über Punkt 2: erst nach Merge von #252 |

## Gesperrte und nicht zu aktivierende Punkte

Geprüft gegen `docs/spec/decisions.yaml` und `docs/spec/MASTERPROMPT.md` Abschnitt 12. Keiner der elf Punkte ist eine dieser Erweiterungen. Keine Einzelspezifikation ist deshalb als gesperrt markiert.

| Punkt | Register | Markierung in diesem Ordner |
|---|---|---|
| 23 | `IMP-23`, REJECTED, Ultimate Multi-User & Secure Identity GODMODE | gesperrt, nicht Bestandteil der elf Punkte |
| 25 | `IMP-25`, REJECTED, Ultimate Smart Migration & Data Freedom GODMODE | gesperrt, nicht Bestandteil der elf Punkte |
| 34 | `IMP-34`, REJECTED, Ultimate Native Antivirus & Cyber Protection GODMODE | gesperrt, nicht Bestandteil der elf Punkte |
| 86 | kein Eintrag `IMP-86`, kein Abschnitt „Verbesserung 86“ in `docs/spec/MASTERPROMPT.md`, kein Treffer in `requirements.yaml` | im Repo nicht vorhanden; nichts wird auf einen fehlenden Punkt 86 bezogen |
| Language 2.0 | `LANG-2.0` und `SPEC-07`, REJECTED (Basis bleibt) | gesperrt, nicht Bestandteil der elf Punkte |
| 35 | `IMP-35`, im Register `UNDECIDED` | **nie aktivieren**. `decisions.yaml` wird hier nicht geändert |

## KI-Laden

Pull Request [#252](https://github.com/manuelbrandner85/Trendo-OS/pull/252) heißt „KI-Laden: lokale KI, kostenlose Online-Dienste, automatische Wahl“. Stand dieser Ausarbeitung: Entwurf, nicht gemergt. Wo eine Funktion diese Modellwahl braucht, steht in der Datei der Satz **erst nach Merge von #252**. Pfade, die nur auf dem Branch von #252 liegen, sind hier keine vorhandenen Module.

## Was der Code auf `main` schon enthält

`docs/build-log/2026-10-22-CORE-07-webseite.md` und `tasks/T-0430-webseite-profi.bericht.md` beschreiben Code für die Punkte 3 bis 8 im Studio „Webseite bauen“ (`CORE-07`, Seite `dev`). Punkte 1, 2, 9 und 11 bezeichnet derselbe Bericht als offen beim gemeinsamen Prompt-Kern. Diese Übersicht übernimmt die Testergebnisse von dort nicht als neue Abnahme. Der Ist-Stand in den Einzelspezifikationen nennt nur Dateien, die auf `main` liegen.

## Gemeinsame Grenzen

Für alle elf Punkte gilt die Leitlinie der Quelle und die Vorgabe dieser Ausarbeitung:

- Lokal zuerst. Dateien und Prüfung bleiben auf dem PC.
- Cloud nur nach ausdrücklichem Opt-in, widerrufbar, mit sichtbarem Zweck.
- Kein kostenpflichtiger Dienst als Voraussetzung. Trendo setzt kein Bezahl-Abo voraus.
- Keine Geheimnisse in Seiten, Protokollen, Commits oder Vorschau-Dateien. Geheimnisse bleiben im vorhandenen `src/Trendo.Core/Secrets.cs`.
- Veröffentlichung, Versand, Zahlung, Gerätebefehle und externe KI laufen über `src/Trendo.Core/ApprovalGate.cs`.
- Studio-Namen kommen aus `src/Trendo.App/StudioCatalog.cs`.
