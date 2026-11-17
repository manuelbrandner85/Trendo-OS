# Bericht T-0718: Web – Veröffentlichen über das Freigabe-Tor mit echtem Nachweis (Audit C4)

Zweig `claude/T-0718-web-veroeffentlichen` (Basis `claude/T-0685-b1-integration`). Belege: `reports/T-0718/` (Rohlogs, Bilder, `SHA256SUMS`).

## 1. Bestandsaufnahme: Veröffentlichungswege im Code (vorher)
| Weg | Wo | Vorher | Jetzt |
|---|---|---|---|
| Export-Ordner `veroeffentlichen/` | Web Design Studio | lokal, eigener Projektordner | unverändert (Arbeitsstand, keine Wirkung nach außen) |
| ZIP-Paket `pakete/` | Web Design Studio (`WebDesignPublish.Package`) | ohne Tor | **über das Tor** (`web.veroeffentlichen.zip`), Paket wird wieder geöffnet und gegen die Dateiliste geprüft |
| In einen Ordner (USB-Stick, Netzlaufwerk, Ordner eines Webservers) | – | fehlte | **neu, über das Tor** (`web.veroeffentlichen.ordner`), Web Design Studio und Baukasten |
| Eigener Server (WebDAV: NAS im Heimnetz, Nextcloud, Webhoster) | – | fehlte | **neu, über das Tor** (`web.veroeffentlichen.server`), Anmeldung nur im Tresor |
| Netlify | Web Design Studio (`_wdConsent`, Karte der Seite) und Claude-Chat (Ja-Knopf) | **nicht** über das Tor (Audit audit-6 Nr. 3) | **über das Tor** (`web.veroeffentlichen.netlify`), Schlüssel wie bisher im Tresor |
| Website-Generator „Paket vorbereiten“ | `MainWindow.W4Web.cs` | schon über das Tor (`website.pack`) | unverändert |
| Baukasten ZIP/„Ordner mit fertiger Seite“ | `MainWindow.W4Builder.cs` | lokal | unverändert; neu „Veröffentlichen …“ über das Tor |
| GitHub-Repo hochladen | Claude-Chat (`ClaudeGitHubPush`) | nicht über das Tor | **nicht angefasst** (Code-Upload, kein Website-Weg) – offener Punkt |
| GitHub Pages, FTP/SFTP | – | gibt es im Code nicht | nicht gebaut |

## 2. Was jetzt gilt
- Kern `src/Trendo.Core/Website/WebPublishing.cs` (ohne Netz-Bibliothek, QA-019 grün): je Weg eine Aktion; Ziel der Freigabe = `Ort#Fingerabdruck` der gezeigten Dateien → Freigabe gilt genau für diese Dateien an genau diesem Ort, einmal. Ordner/ZIP führt der Kern selbst in `ApprovalGate.Execute` aus (Dateien atomar über `AtomicFile`, Fingerabdruck über dieselben Bytes, fremde nicht leere Ordner werden nie überschrieben, nur eigene Altdateien laut Merkzettel entfernt). Link- und Dateiprüfung (`CheckLinks`: href/src/srcset/poster/CSS url()).
- Server/Netlify: Netzarbeit in der App (`WebDavLink.cs`: MKCOL, PUT, danach GET jeder Datei und SHA-256-Vergleich), Ausführung in `ApprovalGate.Execute` mit genau der Freigabe. http nur auf diesem PC/im Heimnetz, sonst https; Anmeldung nie in der Adresse; Benutzer/Passwort erst nach dem Ja in den Tresor (`ISecretStore`). Im Testmodus lädt Trendo nur an 127.0.0.1.
- Rückfrage: echter Dialog `FreigabeDialog` (Wirkung, Ziel, Dateien, Größe, Vorschau = vollständige Dateiliste, „Abbrechen“ vorbelegt). Kinderprofil: keine Rückfrage, nichts passiert, Hinweis „Erwachsenen-Profil“.
- Rückmeldung: „Veröffentlicht in …/Hochgeladen zu …: n Dateien, x KB“, Nachprüfung, „Prüfung: n Links und Dateien geprüft – alles gefunden“ bzw. Befunde; Fehler „Nicht veröffentlicht: … Was tun: …“ (Passwort abgelehnt, Ordner fehlt, nicht erreichbar, Ordner nicht leer, Export geändert, Netlify nicht verbunden). Knopf „Ordner öffnen“/„Seite öffnen“.

## 3. Tests und Belege
- Core `T0718WebVeroeffentlichen` (7, neu): Tor verweigert ohne/offene/falsches Ziel/falsche Aktion/falsche Dateien/zweites Mal (6× `execution.blocked`, keine Datei), mit Freigabe genau einmal, Audit verifiziert; Änderung nach Freigabe; fremder Ordner; Aufräumen; Linkprüfung; ZIP; Kinderprofil; Rückfrage-Inhalt; Server-Adressen. Rot-Grün: auf altem Code nicht übersetzbar (Klasse fehlte).
- App `T0718WebVeroeffentlichen_Ui` (6, neu): echter Dialog (Abbrechen → nichts, Ja → genau eine Ausführung), Kinderprofil, **echter HTTP-Upload an WebDAV-Server auf 127.0.0.1** (falsches Passwort → 401 mit „Was tun“; richtig → Dateien byte-gleich, 6/6 zurückgelesen, Passwort nur im Tresor), Testmodus nie ins Internet + Netlify ohne Konto, ZIP, Baukasten.
- App `T0718WebVeroeffentlichen_Browser` (1, neu, **echter Chrome**): (a) Ordner → lokaler Server → Chrome: 0 Fehler, Bild; HTTP-Linkprüfung 3/3 = 200. (b) WebDAV 127.0.0.1 → Chrome lädt unter der Server-Adresse: 0 Fehler, Bild; Server-Log MKCOL/PUT/GET. Beleg `reports/T-0718/chrome/`.
- Angepasst (nicht geschwächt): `WebDesignStufe3Teil2_Ui.K8_publish` – statt Karten-Zustimmung jetzt Tor: Abbrechen → 0 Aufrufe, Ja → genau eine Ausführung, Freigabe danach verbraucht (zusätzlich geprüft).
- Läufe: Core-Filter (T0718, QA019, FreigabenKern, T0713FreigabenNachtrag, WebDesign*, W4_X03_Builder, Website*, Netlify, ApprovalGate) **295/295 bestanden** (`core-tests.log`). App 29 Klassen (Liste `app-classes.txt`, u. a. FreigabenKernUi inkl. Architekturtest unverändert, T0713FreigabenNachtragUi, DesignPaket4_Ui, UebersichtEinfach_Ui, ProgramHeadDesign_Ui, T0710_MenuBar_Ui, T0716_HuelleE4_Ui, alle WebDesign*_Ui/_Browser, Connections_Ui, W4_Core07_Ui, W4_X03_Ui, ClaudeChat_Ui, QA019_OfflineUi): **118 gesamt, 0 Fehler, 2 übersprungen** (Z6/Z3 PC-Läufe nur mit `TRENDO_PC_LAUF=1`, fremde Bereiche). Funktions-Baseline `tests/claude_handoff`: 11 OK (`baseline.log`). Kein Volltest.
- Oberfläche im Bild: `X-03-profi.png` (Baukasten mit „Veröffentlichen …“, nicht golden), `CORE-07-profi.png`.

## 4. Was ersetzt blieb / NICHT geprüft
- Der WebDAV-Server im Test ist ein kleiner eigener Server (HttpListener: MKCOL/PUT/GET, Basic-Anmeldung) – echtes HTTP über TCP, aber **kein** Nextcloud/Apache/NAS. Verhalten echter Anbieter (PROPFIND, Umleitungen, Sperren, https-Zertifikate): NICHT GETESTET.
- Netlify: **BLOCKIERT** – ohne Konto der Person nicht prüfbar; Tests nutzen weiter die Nachbildung `FakeNetlify` (ersetzt). Nichts wurde ins Internet hochgeladen.
- Ordnerwahl im Test über `FolderPickOverride` (Betriebssystem-Dialog ersetzt), alles danach echt.
- Upload läuft im Speicher (ganze Dateien); sehr große Sites (> einige 100 MB) nicht gemessen. Kein Abbrechen-Knopf während des Hochladens.

## 5. Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: TEILWEISE (Seite im Web Design Studio/Baukasten → veröffentlichter Ordner/Server, in Chrome geöffnet; Seitenbau selbst nicht Teil dieses Pakets).
2. Fremddatei/Export im Fremdprogramm: VOLLWERTIG für den Weg Export → Chrome (Ordner und Server, Beleg mit SHA-256); Import nicht Teil des Pakets.
3. Abbruch ≤ 30 s: TEILWEISE (Dateien atomar, Merkzettel atomar; ein Abbruch mitten im Hochladen hinterlässt einen Teilstand auf dem Server – neu hochladen behebt ihn).
4. KI-Fachaufgabe: NICHT GETESTET (nicht Teil des Pakets; KI-Aktionen zum Veröffentlichen bleiben gesperrt).
5. Klick bis echte Datei: VOLLWERTIG (UI-Klick → echter Dialog → Datei im Zielordner bzw. auf dem 127.0.0.1-Server).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: TEILWEISE (Einfach/Profi bleibt, keine neuen Kürzel/Rechtsklick).

## 6. Offene Punkte
- GitHub-Repo-Upload im Claude-Chat läuft noch nicht über das Tor (eigener Auftrag).
- GitHub Pages / FTP(S)/SFTP gibt es nicht; SFTP bräuchte eine Bibliothek oder `sftp.exe` und einen Testserver.
- Echter Lauf gegen einen echten WebDAV-Dienst (z. B. NAS der Person) und gegen Netlify mit Konto der Person.

## 7. Berührte Dateien
`src/Trendo.Core/Website/WebPublishing.cs` (neu), `src/Trendo.Core/Website/WebDesignPublish.cs`, `src/Trendo.App/WebDavLink.cs` (neu), `src/Trendo.App/MainWindow.WebDesignCode.cs`, `src/Trendo.App/MainWindow.WebDesignSite.cs`, `src/Trendo.App/MainWindow.W4Builder.cs`, `src/Trendo.App/MainWindow.Connections.cs`, `src/Trendo.App/WebsitePreview.cs`, `tests/Trendo.Core.Tests/T0718WebVeroeffentlichenTests.cs` (neu), `tests/Trendo.App.Tests/T0718WebVeroeffentlichenUiTests.cs` (neu), `tests/Trendo.App.Tests/WebDesignStufe3Teil2UiTests.cs`, `tasks/T-0718-*.md`, `reports/T-0718/`.

ENTWICKLUNGSBERICHT: Sprachen C# (.NET 10) · Rust eingesetzt: NEIN (Netz- und Dateiarbeit ohne Engpass, gleiche Sprache wie der Rest) · Tests siehe oben · offene Probleme siehe 4 und 6.
