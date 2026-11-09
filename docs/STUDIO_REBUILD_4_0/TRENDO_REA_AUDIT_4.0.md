# Trendo REA-Audit 4.0

> Entwurf aus dem read-only Audit vom 09.11.2026. Es wurde kein Programmcode geändert.

## Stand des Repositorys

- Arbeitszweig `claude/T-0260-karten-programm`, Stand `e6038f724`.
- 807 C#-Dateien mit rund 145 000 Zeilen unter `src/`, 652 Testdateien.
- 58 Studios im Katalog, 98 angemeldete Programme, 1531 Funktionen in Funktionskatalogen.
- Letzter vollständig grüner Volltest: Release 0.9.84, 08.11.2026 (`reports/volltest-0.9.84`).
- Danach kam viel Arbeit dazu. Der letzte Volltest über den heutigen Stand hatte fünf Fehler der zwei neuen Studios. Sie sind behoben und einzeln geprüft, ein neuer Volltest steht aus.

## Was „REA“ in Trendo heute wirklich ist

Es gibt **kein REA-System im Produkt**. Das Wort steht für drei getrennte Dinge:

| Bedeutung | Wo | Was es leistet | Was es nicht leistet |
|---|---|---|---|
| Vorbild-Analyse (Clean-Room) | `docs/claude/vorbilder/*.md`, 59 Dateien | Funktionen eines Profi-Programms mit Stand ✓ ◐ ✗ | von Hand gepflegt, keine Messung |
| Programmkatalog und Ampel | `src/Trendo.Core/Programs/ProgramCatalog.cs`, `QualityLight.cs`, `docs/programme/` | erzeugt je Programm 11 Dokumente und eine Punktzahl | prüft Vorhandensein, nicht Qualität |
| REA-Werkzeug | externer MCP-Server auf dem Entwicklungs-PC | Reverse Engineering fertiger Programme | gehört nicht zum Produkt, läuft nicht beim Nutzer |

Dazu kommen `docs/ALL_STUDIOS_GITHUB_GODMODE/` (GitHub-Recherche je Studio) und die Prüfketten `StudioWorkflow` und `RunStore` mit den Studio-Adaptern.

**Empfehlung:** Die 15 Aufgaben aus Phase 3 als Ausbau von `ProgramCatalog` und `QualityLight` umsetzen. Das ist kein zweites System.

## Befunde zur Messung

Diese Punkte begrenzen, wie weit man den heutigen Zahlen trauen darf.

1. **Die Ampel misst Vorhandensein.** 30 Punkte gibt es für eine erkannte Arbeitsfläche, 15 für eine verlinkte Analyse. Ein grünes Programm ist deshalb nicht abgenommen.
2. **Die Erkennung der Arbeitsfläche hat Lücken.** `spiele-programm`, `film-programm` und `pod-programm` haben echte Oberflächen und stehen trotzdem auf Rot, weil der Erkenner ihre Bedienelemente nicht kennt.
3. **Die Test-Zuordnung ist ungenau.** Bei „E-Mail & Kontakte“ stehen auch Tests von CORE-11 und SPEC-12. Die Zahl „Tests je Programm“ ist deshalb zu hoch.
4. **Die Formatliste ist geschätzt.** Bei „E-Mail“ erscheint `.gltf`, weil sie aus dem Quelltext geraten wird.
5. **Die Fach-Engine-Liste enthält Fremdkörper**, zum Beispiel `Button`.
6. **`PC_VERIFIED` in `requirements.yaml` ist älter als vieles im Code.** 53 Studios tragen diesen Status, aber viele Nachweise stammen aus `reports/pc-t0003`. Spätere Ausbauten sind davon nicht abgedeckt.
7. **Funktionskataloge fehlen bei 28 Studios**, darunter `CORE-07` (Webseite bauen), `CORE-02` (KI fragen) und `CORE-11` (Daten schützen). Für diese Studios gibt es keine prüfbare Liste, gegen die man einen Funktionsverlust feststellen könnte.

## Spezifiziert, implementiert, getestet, abgenommen

| Stufe | Studios | Grundlage |
|---|---|---|
| spezifiziert | 57 | `approval_status: APPROVED_SPEC` |
| abgelehnt | 1 | SPEC-07, bleibt abgelehnt |
| implementiert | 46 | `implementation_status: IMPLEMENTED` |
| in Arbeit | 11 | `IN_PROGRESS` |
| als PC-geprüft geführt | 53 | `PC_VERIFIED`, siehe Befund 6 |
| ohne Testlauf geführt | 5 | `NOT_RUN` |
| **abgenommen nach 4.0** | **0** | es gibt noch kein Abnahmeprotokoll |

QA-017, QA-019 und die Codesignatur werden nach `CLAUDE.md` nicht als bestanden markiert. Die W1- und W7-Issues bleiben offen.

## Abgleich des Auftrags mit dem Register

| Im Auftrag genannt | Im Register |
|---|---|
| Webdesign | CORE-07 |
| 3D | CORE-05 |
| Film & Video | CORE-08 und STUDIO-FILM-01 |
| CAD & Engineering | Programm in CORE-05, dazu SPEC-02, SPEC-03, SPEC-14 |
| Game Development | CORE-06 |
| Office | SYS-10 |
| Audio, Bild | Programme in CORE-08 und STUDIO-MUSIK-01 |
| Architektur | SPEC-01 |
| Scanning | SPEC-09 |
| Software | CORE-07, X-01 und weitere Entwicklerbereiche |
| E-Commerce | STUDIO-ONLINE-BUSINESS-01, STUDIO-POD-01 |
| Datenanalyse, Wissenschaft | SPEC-05 |
| Cybersecurity | SPEC-12 |
| Robotik | CORE-09 |
| GIS | SPEC-13 |
| Produktdesign | SPEC-08 |
| Digital Twins | SPEC-10 |
| VFX | **kein eigenes Studio**, nur Teile im Videoschnitt |

## Widersprüche, die der Nutzer entscheiden muss

1. **„Keine Kategorie auslassen“ gegen die Ablehnung von SPEC-07.** SPEC-07 bleibt laut `CLAUDE.md` und Auftrag draußen.
2. **VFX** gibt es nicht als Studio. Es entsteht nur, wenn es neu freigegeben wird.
3. **Getrennte Zweige und kein Merge ohne Freigabe** gegen den bisherigen Ablauf: Zwei Sitzungen arbeiten auf einem Zweig und laden nach grünem Test nach `main` hoch.
4. **Andockbare Panels und mehrere Monitore** verändern die Programm-Hülle aller 98 Programme.
