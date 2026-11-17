# Bericht T-0735: Recherche mit Quellen + Sicher recherchieren + Freie Bilder & Klänge

**Branch:** `claude/T-0735-recherche-quellen` (eigene Kopie von `main` in `%TEMP%\trendo-auftrag-T-0735`)
**Status:** umgesetzt, berührte Testklassen grün, echter Abruf belegt; Volltest NICHT gelaufen (macht die Hauptsitzung).

## Was war → was ist jetzt

| Punkt | Vorher (Audit) | Jetzt | Beleg |
|---|---|---|---|
| STUDIO-CITE-01 Export | MD, BibTeX, TXT | zusätzlich **RIS** (CRLF, ohne BOM) und **CSL-JSON** – offline | `CiteFormatsTests.Ris_rundreise`, `Csl_json`; UI `RechercheQuellen_Ui.Cite_doi_ris_csl` |
| STUDIO-CITE-01 Import | nur BibTeX | „BibTeX/RIS einlesen …“ (Endung oder Inhalt), RIS aus Zotero-Form mit Umbrüchen, A1/T2/Y1, Warnungen bei kaputten Blöcken | `CiteFormatsTests.Ris_fremdform`, UI-Test (RIS hin und zurück gleich) |
| STUDIO-CITE-01 DOI | fehlte | Feld „Quelle aus DOI“ + Band-Befehl „Aus DOI ergänzen“: öffentliche Crossref-Schnittstelle ohne Konto, ein Rückgängig-Schritt, gleiche DOI ergänzt statt doppelt; **Herkunft** (Dienst, Adresse, Zeit, SHA-256) an der Quelle, in RIS (N1) und CSL-JSON (note) | `CiteFormatsTests.Doi_aus_crossref` (Probe), **echt:** `Cite_Echt` + `reports/T-0735/crossref-…json` |
| SYS-08 Suche | keine | Suchzeile mit Dienstwahl (Wikipedia-Schnittstelle, DuckDuckGo-HTML), Trefferliste in der Mitte mit Titel, Auszug, Adresse, „Herkunft: Dienst · Zeit · Suchadresse“; Klick liest im Lesemodus; links bleiben die Treffer. .onion-, Heimnetz- und Werbetreffer fallen weg (kein Darknet-Abruf). „Suchen“ ist die eine goldene Hauptaktion | `ResearchSearchTests.Wikipedia_treffer`, `Duckduckgo_treffer`; UI `Sys08_suche_und_sammlung`; **echt:** `Sys08_Echt` (Wikipedia 20 Treffer + Seite gelesen, DuckDuckGo 10 Treffer) |
| SYS-08 Sammlung | bei jedem Start gelöscht | Schalter „Sammlung behalten (auch nach Neustart)“: an → `recherche/sammlung` (Quellen, Suchen, letzte Treffer, Markierungen, Notizen bleiben); aus (Standard) → flüchtig wie bisher. Suchen stehen im Bericht („## Suchen“) | `ResearchSearchTests.Sammlung_behalten`, UI `Sys08_suche_und_sammlung` (Neustart mit gleichem Datenordner), `Sys08_fluechtig` |
| X-07 KI-Aktion | „Quelle holen“ = Blocked | „Holen: ja / Asset: Kennung oder polyhaven.com/a/…“: Plan ohne Netz → einmalige Freigabe (ApprovalGate, Wirkungsbuch) → Laden über `PromptServices.ReadBytes` (in der App `ReadOnlyWeb.GetAsync`), MD5 geprüft, erst alles geprüft dann ins Projekt `assets/<id>/`, **HERKUNFT.txt + herkunft.json je Datei** (Adresse, Größe, SHA-256, Lizenz, Quelle) | `ConnectedPromptTests.Quelle_wird_nach_freigabe_geholt` (Netz ersetzt), `PromptActionTests.Quelle_wird_offline_nicht_geholt`; **echt:** `X07_Echt` (HDRI 1,6 MB von Poly Haven nach Freigabe) |
| X-07 Knopf | „Laden“ schrieb nur LIZENZ.txt | schreibt zusätzlich HERKUNFT.txt/herkunft.json je Datei | `W4_X07_Ui` grün (Beleg-Inhalt dort nicht eigens geprüft) |

Netzwege (QA-019): kein HttpClient im Kern; Kern baut nur Adressen und liest Antworten, Abruf über `ReadOnlyWeb` (App) bzw. `PromptServices.ReadBytes`. `QA019_Offline` grün.

## Geänderte Dateien
- Neu: `src/Trendo.Core/Kreativ/CiteFormats.cs`, `src/Trendo.Core/W3/ResearchSearch.cs`, `src/Trendo.App/MainWindow.CiteQuellen.cs`, `src/Trendo.App/MainWindow.ResearchSearch.cs`, `tests/Trendo.Core.Tests/CiteFormatsTests.cs`, `tests/Trendo.Core.Tests/ResearchSearchTests.cs`, `tests/Trendo.App.Tests/RechercheQuellenUiTests.cs`, `reports/T-0735/*`, diese Auftragsdateien
- Geändert: `src/Trendo.Core/Kreativ/KreativKits4.cs` (nur Feld `Source.Origin`), `src/Trendo.Core/W3/Research.cs` (Sammlung behalten, Suchen im Bericht), `src/Trendo.Core/Assets.cs` (Beleg-Hilfen), `src/Trendo.Core/PromptAction/StudyPromptAdapter.cs` (LicenseFetchAdapter), `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` (X-07-Zeile), `src/Trendo.App/MainWindow.Research.cs`, `src/Trendo.App/MainWindow.KreativStudios4.cs` (Quellen: Herkunft, Import, DOI-Befehl, Exporte, Hilfe), `src/Trendo.App/MainWindow.W4Assets.cs`, `tests/Trendo.Core.Tests/PromptActionTests.cs` (Liste der Wirkungs-Aktionen um `quelle.holen` erweitert; Test „nicht geholt“ → „offline nicht geholt“), `tests/Trendo.Core.Tests/ConnectedPromptTests.cs`, `docs/improvement-42/{studio-capability-matrix,architecture-decision,developer-guide,security-approvals}.md` (fünfte Wirkungs-Aktion)
- Gemeinsame Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer/neuerungen): **nicht** angefasst.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- Core gezielt (CiteFormats, ResearchSearch, ConnectedPrompt, PromptAction, W3, ResearchMarks, KreativKits4, AssetLibrary, QA019, QA004, NoFixedUser): **152 bestanden, 0 Fehler** (`reports/T-0735/testklassen-core.log`)
- App gezielt je Klasse: SYS08_Ui 1, ResearchMarks_Ui 1, Runde11_Ui 2, MediaBrowser_Ui 3, W4_X07_Ui 2, KreativStudios4_Ui 5, MediaIcons_Ui 1, MediaPro_Ui 1, StudiosTechnikWissen_Ui 39, T0711_HuelleEtappe1b_Ui 11, T0710_MenuBar_Ui 8, RechercheQuellen_Ui 6 – **alle grün** (`reports/T-0735/testklassen-app.log`)
- Echtlauf: `reports/T-0735/testlauf-echt.log` + Rohdaten + `SHA256SUMS`
- Bild geprüft: Render SYS-08 (Suchzeile, eine goldene Hauptaktion, Schalter „Sammlung behalten“) und STUDIO-CITE-01 (Beispiel) angesehen.
- Datenschutz-Grep vor dem Commit leer.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** für CITE (leere Liste → DOI → vollständige APA-Quelle → RIS/CSL), SYS-08 (Suche → Treffer → Lesetext → Bericht).
2. Fremddatei ein/aus, Export im Fremdprogramm: **TEILWEISE** – RIS-Import einer Zotero-förmigen Datei und Rundreise geprüft; Öffnen im Fremdprogramm **BLOCKIERT** (kein Zotero/Citavi/EndNote/Pandoc auf dem PC, nichts installiert).
3. Abbruch verliert ≤ 30 s: **TEILWEISE** – Quellen über die Programm-Hülle (Autosave/Wiederherstellung unverändert, nicht neu geprüft); SYS-08-Sammlung schreibt sofort (JSONL/AtomicFile) – Prozessabbruch selbst NICHT GETESTET.
4. KI führt Fachaufgabe aus, rückgängig machbar: **TEILWEISE** – X-07 „Quelle holen“ lädt nach Freigabe wirklich (echt belegt); Ergebnis landet im Projekt, nicht im offenen Dokument, kein Rückgängig (Dateien bleiben, Wirkungsbuch verhindert Doppelung).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** mit ersetztem Netz (UI-Klick → RIS/CSL-Datei), echter Netzweg separat ohne Klick (`*_Echt`).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Enter in Such-/DOI-Feld; kein Rechtsklick-Menü und kein Ziehen für Treffer/DOI ergänzt.

## Was NICHT geprüft wurde
- Volltest aller Testprojekte, Api-Tests, Paket-Tests.
- Ob Zotero/Citavi/Pandoc die RIS/CSL-Dateien annehmen (BLOCKIERT, s. o.).
- Rot-Grün nicht gegen den alten Stand ausgeführt (neue Tests rufen neue Methoden auf bzw. erwarten `quelle.holen` mit Wirkung – auf altem Code schlagen sie fehl, das ist abgeleitet, nicht gelaufen).
- DuckDuckGo kann die HTML-Suche zeitweise mit einer Prüfseite ablehnen (Fehlermeldung vorhanden); kein Umgehen.
- Bedienstufe „Einfach“ für die neuen Felder nicht im Bild geprüft; DOI-Feld steht im Eigenschaften-Bereich.

## Offene Punkte
- Literaturverzeichnis als .docx (Audit-Wunsch) nicht Teil dieses Pakets.
- X-07: nur Poly Haven (CC0); weitere freie Quellen (Openverse, Freesound) ohne Konto prüfen.
- SYS-08: Rechtsklick „In Quellen übernehmen“ auf Treffer, Zurück/Vor.

## Gegenprüfung behoben (17.11.2026)
Unabhängige Gegenprüfung: 1 mittlerer, 3 niedrige Befunde. Alle vier behoben, je mit Test oder Nachweis.
1. **MITTEL – Prüfsummen der Belege:** In `.gitattributes` fehlte `reports/T-0735/** -text`, git hatte vier mit CRLF erzeugte Belege auf LF umgestellt (`cite-echt.csl.json`, `cite-echt.ris`, `fremdpruefung-python.log`, `x07-herkunft.json`). Jetzt: Regel ergänzt (nur diese Zeile), die vier Dateien mit den ursprünglichen CRLF-Bytes neu eingecheckt (Rückumwandlung ergibt genau die eingetragenen Summen, nichts neu erzeugt). README-Satz zu den Zeilenenden berichtigt, nur die README-Summe neu. Nachweis: `sha256sum -c SHA256SUMS` 15/15 OK im Arbeitsordner und nach frischem Klon.
2. **NIEDRIG – „Sammlung behalten“ ausschalten:** Jetzt Rückfrage vor dem Abschalten („Abbrechen“ vorbelegt), sie nennt die Folge (beim nächsten Start gelöscht, vorher „Als Bericht speichern“). `ResearchDesk.SetKeep` ist ganz oder gar nicht: Scheitert ein Verschieben, wandert alles zurück, der Schalter bleibt, und es kommt eine verständliche Meldung („… Es wurde nichts geändert.“), die die Oberfläche in der Meldungszeile zeigt. Tests: Core `ResearchSearchTests.Sammlung_behalten_fehler_ohne_halben_zustand` (Rot auf altem Code, dann grün), App `RechercheQuellen_Ui.Sys08_behalten_aus_mit_rueckfrage` (Rückfrage, Abbrechen, Fehler mit Meldung, dann Ausschalten).
3. **NIEDRIG – halbe Asset-Sammlung:** `LicenseFetchAdapter` schreibt jetzt alles in einen Zwischenordner (`assets/.laden-<id>-…`), prüft dort die SHA-256 und übernimmt dann Datei für Datei; vorhandene Dateien werden erst beiseitegelegt und bei einem Fehler zurückgeholt, neue wieder entfernt, der Zwischenordner immer gelöscht. Test: Core `ConnectedPromptTests.Quelle_holen_schreibfehler_ohne_halbe_sammlung` mit erzwungenem Fehler beim Ablegen und beim Übernehmen (Rot auf altem Code, dann grün). Netz dort ersetzt; der echte Abruf `X07_Echt` lief erneut grün.
4. **NIEDRIG – *_Echt-Tests ohne Internet:** Melden sich jetzt mit `Assert.Skip` (xUnit v3, Muster wie in den übrigen App-Echtlauf-Tests) sichtbar als übersprungen. Nachweis: Lauf mit unerreichbarem Proxy → 3 × [SKIP] „Übersprungen – kein Internet …“; mit Internet laufen alle drei echt und grün. Rot auf altem Code nicht erneut ausgeführt (laut Gegenprüfung meldeten sie dort „bestanden“).

Tests nach der Behebung: Build 0 Fehler. Core gezielt (ResearchSearch, ConnectedPrompt, PromptAction, W3, ResearchMarks, CiteFormats, KreativKits4, QA019, QA004, QA017, NoFixedUser, DesignHonesty, T0717_HuelleE5, *Research*, *Assets*) 167/167 bestanden. App: RechercheQuellen_Ui 7 (inkl. echter Abrufe), SYS08_Ui 1, ResearchMarks_Ui 1, Runde11_Ui 2, FreigabenKernUi 5, DesignHonesty 5, QA004_UiApproval 3, QA019_OfflineUi 1, QA017_Keyboard 3, StudiosTechnikWissen_Ui 39, T0711_HuelleEtappe1b_Ui 11, T0710_MenuBar_Ui 8, KreativStudios4_Ui 5 – 0 Fehler. Kein Volltest.
Offen: Rückfrage-Fenster selbst (echter Dialog) nicht im Bild geprüft, im Test durch `ResearchKeepOffConfirm` ersetzt. Ein Prozessabbruch mitten im Übernehmen kann einen Ordner `assets/.laden-…` oder `*.trendo-alt` hinterlassen (kein Aufräumen beim Start).
