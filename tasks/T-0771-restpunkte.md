# T-0771: Restpunkte 0.9.110 und lastrobuste Tests

**Ziel:** Die offenen Punkte aus 0.9.110 schließen (N-a, N2, Prüfer-N1, N-b) und die unter Last wackelnden Tests robust machen, ohne Grenzwerte zu lockern.
**Branch:** claude/T-0771-restpunkte (Etappe 0.9.111, freigegeben 26.11.2026)
**Dateien/Bereich:** `src/Trendo.Core/PageBuilder.cs` (EmbedUrl), `src/Trendo.Core/Sys/ChildWrite.cs` (TestScope), `src/Trendo.Core/Profile.cs` (ProfileRole), Katalog-Erzeuger (`tools/Trendo.Render`, `ProgramCatalog`) und `docs/programme/`, `src/Trendo.App/WebsitePreview.cs`, die genannten Tests, `installer/neuerungen/0.9.111.md`.
**Fertig, wenn:**
- [ ] N-a: protokollrelative, Wurzel- und kodierte Linkziele werden im eingebetteten App-JSON zu „#“ (Rot-Grün)
- [ ] N2: Test-Freigaben zählen je Ort; zweites Öffnen desselben Profils verliert seine Freigabe nicht, dauerhafte Freigaben bleiben (Rot-Grün)
- [ ] Prüfer-N1: Ersteinrichtung nur in einem wirklich neuen Datenordner; Kind bleibt Kind nach Löschen der Rollendateien; echte Ersteinrichtung geht weiter (Rot-Grün)
- [ ] N-b: Programmkatalog vollständig neu erzeugt (katalog.json, KATALOG.md, alle Programmseiten) mit TRX; Erzeuger meldet veraltete Vorbild-Namen früh; VorbildAnalysenTests und ProgramCatalogUiTests grün
- [ ] Lastrobuste Tests: Ursache je Test, Änderung ohne gelockerte Grenze, je Test 5× einzeln und 1× unter künstlicher CPU-Last
- [ ] Neuerungen 0.9.111 (Abschnitt zu diesem Paket); Version im csproj unverändert
**Nicht tun:** kein Volltest als Abnahme (macht die Hauptsitzung), kein Push nach main, kein Merge, kein Release, keine Versionsänderung.
**Am Ende:** Branch pushen, Bericht `tasks/T-0771-restpunkte.bericht.md`.
