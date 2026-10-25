# Prüfung der offenen Pull Requests früherer Agenten (Punkt O)

**Stand:** 2026-10-25, gegen `main` @ `39c27cd` · **Geprüft von:** Claude Code (nur lesen)

**Vorgehen:**
- PR-Köpfe geholt über `refs/pull/*/head`. `gh` wurde nicht benutzt.
- Für jeden PR: Commits gegen `main` (`git cherry`), geänderte Dateien, Probe-Zusammenführung mit `git merge-tree` und Suche nach Resten des früheren Agenten.
- **Kein PR-Code wurde gebaut oder ausgeführt.**

**Merge:** Ein „Übernehmen“ heißt, Claude bringt den PR auf den heutigen `main`, löst die Konflikte, lässt alle Tests laufen und lässt QA (bei Sicherheit auch Security) prüfen. Erst nach Manuels OK geht der Stand nach `main`.

**Schließen:** Schließen ist nur auf GitHub möglich. `gh` darf nicht benutzt werden (falsches Konto). Deshalb schließt Manuel die PRs selbst, oder er meldet `gh` mit seinem eigenen Konto an.

## 1. Verbesserung 42 – Fachadapter

| PR | Inhalt | Befund | Vorschlag |
|---|---|---|---|
| **#277** „Aufträge an vorhandene Dienste hängen“ | Gesamter Stapel #244–#258 plus Anschluss an vorhandene Dienste. 11 Adapter: Brief → DOCX, Tabelle → SUMME, E-Mail-Entwurf, Rechnung/Angebot, Software-Vorlage, 3D-OBJ, Browser-Spiel/Unreal-Datei, Bildumwandlung mit ffmpeg, Rechnen/Projektordner, Wissen/Karte/Sendeablauf, übrige Studios. Rund 5 000 Zeilen, große Testdatei `PromptActionTests` (+1 255). | 21 Commits, nichts davon auf `main`. **3 Konflikte** mit `main`: `CapabilityCatalog.cs` und `studio-capability-matrix.md` (CORE-01-Zeile aus T-0218) sowie `W6UiTests.cs` (SPEC-03-Test). Wirkungen bleiben laut Code gesperrt (kein Versand, keine Zahlung, kein Push). Keine Reste des früheren Agenten. | **Übernehmen** – auf `main` bringen, Konflikte lösen, volle Tests, QA- und Security-Review (Freigabe-Gate). |
| #244, #245, #247, #249, #250, #251, #253, #254, #255, #256, #257, #258 | Einzelstufen desselben Stapels (Entwürfe, aufeinander aufbauend) | Vollständig in #277 enthalten | **Schließen** (mit Hinweis „in #277 enthalten“) |

## 2. Funktionen und Fehlerbehebungen

| PR | Inhalt | Befund | Vorschlag |
|---|---|---|---|
| **#272** Begleiter-Sicherheit | Begleiter nur Loopback als Standard, Kopplungs-Sperre, Token-Ablauf, alte Versuche entfernen | 2 neue Commits, zusammenführbar ohne Konflikt, 274 neue Testzeilen. `main` hat schon `loopbackOnly`, aber nicht Sperre und Ablauf. | **Übernehmen** (mit Security-Review) |
| **#279** Blender ohne Konsolenfenster | `Integrations.cs`: Blender ohne schwarzes Konsolenfenster starten, Test `ProcessStartTests` | klein, ohne Konflikt | **Übernehmen** |
| **#280** Messwerte im Hintergrund | Systemwerte alle 2 s außerhalb des UI-Threads, Pinsel zwischenspeichern | 1 Konflikt in `MainWindow.axaml.cs`, weil T-0218 die Messwerte auf Ringe umgestellt hat | **Übernehmen** (Konflikt von Hand lösen) |
| **#268** Glas-Unschärfe vorab berechnen | Unschärfe einmal vorab statt live (GPU-Last), Pi ohne Unschärfe | ohne Konflikt; überschneidet sich mit dem neuen Sparprofil (T-0107) | **Übernehmen**, an `LowPower` anpassen |
| **#278** Tests: fehlende Programme sichtbar überspringen | 9 Commits: Begleiter-Port, SYS-03-PDF-Timeout, SPEC-10-Schwellen, Studio-Karten-Klicks, xunit v3 | Konflikte; xunit v3 ist auf `main` schon da | **Teilweise übernehmen:** Begleiter-Port, PDF-Timeout, SPEC-10-Schwellen. Rest schließen. |
| **#271** Paket 44 Fachprogramme | Fachprogramme nur bei Bedarf, kurze Versionsprüfung, Installation nur über Herstellerseite oder Paketkennung | 7 Commits, ohne Konflikt, rund 1 700 Zeilen mit Tests | **Übernehmen** nach Review – hängt inhaltlich an Grundsatz #275 |
| #248 Programme nachbauen (X-10) | Plant aus Prompt, liest Wikipedia (Schalter), baut eigene Avalonia-App, unsigniertes Setup | Entwurf, Konflikte, rund 2 900 Zeilen; erzeugt und startet fremden Code | **Zurückstellen** – erst eigener Auftrag mit Sicherheitskonzept, dann neu aufsetzen |
| #252 KI-Laden | Lokale KI und kostenlose Online-Dienste | Enthält einen Anbieter des früheren Agenten als Eintrag und Text (seit 2026-10-24 entfernt); Konflikte | **Schließen**. Die Idee „kostenlose Online-Dienste als Ersatz“ ist mit X-05 auf `main` vorhanden. |
| #274 Audit Studios/Barrierefreiheit | 12 Commits, 45 Quelldateien | Überholt durch T-0202/T-0212; Tests nutzen den entfernten Schlüssel des früheren Anbieters | **Schließen** |
| #263 T-0214, #264 T-0210, #270 T-0213, #266 T-0213 (Liste) | Fassungen des früheren Agenten zu Aufträgen | T-0210 und T-0214 sind auf `main` mit Bericht erledigt; für T-0213 liegt die Vorschlagsliste vor, die Namen entscheidet Manuel | **Schließen** |
| #104 Dateibereich 0.5.0 | Stand 21.10. | überholt (Dateien-Studio auf `main`), Konflikte | **Schließen** |

## 3. Spezifikation und Pläne (nur Doku)

| PR | Befund | Vorschlag |
|---|---|---|
| #262 Verbesserung 43 – elf Einzelspezifikationen | 13 Dateien, nur neue Dateien, ohne Konflikt, keine Reste | **Übernehmen** als Doku |
| #273 Verbesserung 42 – zwanzig Einzelspezifikationen | 22 Dateien, nur neue Dateien, ohne Konflikt, keine Reste | **Übernehmen** als Doku |
| #259 Master bis 94, #260 Bauplan T-0005…T-0012 | Arbeitsbefehle an den früheren Agenten im Text; Konflikte | **Schließen**. Die Inhalte stehen schon als Aufträge im Fertigstellungsplan (C4/C5, I). |
| #261 Plan T-0211 | T-0211 ist erledigt | **Schließen** |

## 4. Werkzeuge früherer Agenten

| PR | Vorschlag |
|---|---|
| #236 `.cursorignore`/Agentenkontext | **Schließen** (Werkzeug des früheren Agenten) |
| #265 docs/state Projektgedächtnis | **Schließen** (ersetzt durch `CLAUDE.md`, Fertigstellungsplan und Berichte) |
| #267 Merge-Skript | **Schließen** (Claude merged per Fast-Forward nach OK) |
| #269 CI-Workflow | **Schließen** – der Titel enthält einen Rechnernamen. Eine CI richtet Claude bei Bedarf neu ein. |

## 5. Entscheidung von Manuel nötig

| PR | Frage |
|---|---|
| #275 Grundsatz „jedes Fachprogramm und Tool ist installierbar“ | Soll GRUNDSATZ-01 mit den vorgeschlagenen Abnahmekriterien in `requirements.yaml`? Der Wortlaut stammt laut PR von dir. Bei JA übernimmt Claude ihn sauber (PR schließen, Eintrag neu); dann auch #271. |

## Reihenfolge beim Übernehmen
1. Klein und ohne Konflikt: #279, #272, #262, #273, #268
2. #280
3. #277 (größter Block, eigener Review)
4. #271 nach Entscheidung zu #275
5. Teile von #278

## Ergebnis der Übernahme (Freigabe Manuel, 2026-10-25)
**Übernommen** (Merge-Commits auf `claude/T-02xx`, danach `main`): #279, #272, #262, #273, #268, #280, #277 (enthält #244–#258).

**Anpassungen bei der Übernahme:**
- **#268:** Glas folgt nur dem Leistungsprofil (T-0107), der doppelte Pi-Erkenner ist entfernt.
- **#280:** Messung im Hintergrund, zusammengeführt mit den Ringen und Hellmodus-Farben aus T-0218.
- **#277:**
  - Konflikte gelöst: Katalog/Matrix mit CORE-01 nach T-0218, SPEC-03-Test.
  - **SPEC-07 ist im Register abgelehnt:** Der Übersetzungsadapter aus dem PR ist nicht übernommen, die Sperre fängt jeden Auftrag ab.
  - Unreal-Test hängt nicht mehr an einer installierten Engine, die Test-Aufräumroutine kommt mit schreibgeschützten Git-Dateien zurecht.

**Review:**
- QA: keine hohen Befunde, 1 mittel (toter Code und Test zum Sparprofil aus #268), mehrere niedrig. Behoben.
- Sicherheit: keine hohen Befunde.
  - **B1 (mittel, behoben):** Lesemodus und Download verbinden nur noch zu öffentlichen Adressen. Geprüft wird die tatsächlich aufgelöste IP: mapped IPv6, 0.0.0.0, CGNAT, ULA, Link-Local und interne Namen werden abgelehnt.
  - **B2 (mittel, behoben):** Der Freigabetext zeigt die Pflichtfelder immer vollständig, weitere Angaben werden nur gezählt. Der Text steht neben dem Freigabe-Knopf.
  - **B3 (mittel, behoben):** Ein Auftrag installiert nur für den eigenen Benutzer, nie systemweit mit Administrator-Abfrage. Solche Programme laufen weiter über Einstellungen → Zusatzprogramme.
  - **N1–N3 und N6 (niedrig, behoben):** systemd-Härtung, Protokoll ohne Steuerzeichen, Kopplungscode genau 6 Ziffern, Freigabe gilt nicht für nachträglich geänderte Angaben.
  - **Offen (niedrig):**
    - N4: Begleiter im LAN ohne TLS (vorbestehend, dokumentiert).
    - N5: Installation prüft die Rechte des Besitzers statt des Freigebenden; abgedeckt durch das Gate.
    - N7: Website-Vorschau startet Chrome mit `--no-sandbox` und `--remote-allow-origins=*` (vorbestehend).
- Neue Negativtests für B1–B3 und N3.

**Auf GitHub schließen (Manuel):** alle unter „Schließen“ genannten PRs. Übernommene PRs markiert GitHub selbst als gemergt, sobald ihr Stand in `main` liegt. Die gestapelten Entwürfe #244–#258 zielen auf andere Branches und müssen eventuell von Hand geschlossen werden.

**Tests (Release, Windows-PC, Stand nach allen Korrekturen):** Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 654 bestanden, 1 übersprungen (von 655) · App 307 bestanden, 1 übersprungen (von 308), 22 min 54 s · Paket-Tests OK.

## Nachtrag: #275 und #271 (Freigabe Manuel, 2026-10-25)
- **#275:** GRUNDSATZ-01 mit den Abnahmekriterien AK-1 bis AK-8 von Manuel bestätigt.
  - Eingetragen in `requirements.yaml` → `principles`, zusammen mit PAKET-44 (`packages`), außerdem im Generator `scripts/generate_requirements.py` und in `CLAUDE.md`.
  - Der PR wurde nicht gemergt, sondern sein Inhalt neu eingetragen. Auf GitHub schließen.
- **#271 übernommen:** Fachprogramme nur bei Bedarf, kurze Versionsprüfung, Installation nur nach Zustimmung und Freigabe.
  - Aufträge (Prompt) installieren weiterhin nur für den eigenen Benutzer (B3).
- **Tests (Release):** Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 666 bestanden, 1 übersprungen (von 667) · App 322 bestanden, 1 übersprungen (von 323), 36 min 17 s · Paket-Tests OK.
