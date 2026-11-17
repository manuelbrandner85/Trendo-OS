# T-0718: Web – Veröffentlichen über das Freigabe-Tor mit echtem Nachweis (Audit C4)

**Ziel:** Eine Website aus Trendo („Webseite bauen“: Web Design Studio, Baukasten; Netlify im Claude-Chat) wird nur über das Freigabe-Tor veröffentlicht und das Ergebnis ist ehrlich belegt.
**Branch:** claude/T-0718-web-veroeffentlichen (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/Website/WebPublishing.cs` (neu), `WebDesignPublish.cs`, `src/Trendo.App/MainWindow.WebDesignCode.cs`, `MainWindow.WebDesignSite.cs`, `MainWindow.W4Builder.cs`, `MainWindow.Connections.cs`, `WebDavLink.cs` (neu), `WebsitePreview.cs`, Tests, `reports/T-0718/`.
**Fertig, wenn:**
- [x] Bestandsaufnahme der Wege im Code (Ordner/ZIP, Netlify, GitHub-Repo, FTP/SFTP/WebDAV/GitHub Pages) im Bericht.
- [x] Jeder Veröffentlichungsweg läuft über `ApprovalGate` (Rückfrage mit Wirkung, Ziel, Dateien, Größe; „Abbrechen“ vorbelegt; Kinderprofil darf nicht); Ausführung im Kern mit genau dieser Freigabe (Ziel = Ort#Fingerabdruck), genau einmal, im Protokoll.
- [x] Nachweis ohne fremdes Konto: (a) Ordner/ZIP mit Link- und Dateiprüfung, Seite von lokalem Server in echtem Chrome geöffnet, (b) Upload an einen echten Server auf 127.0.0.1 (WebDAV), dort in echtem Chrome geöffnet; Beleg `reports/T-0718/` mit Rohlog und SHA256SUMS.
- [x] Netlify (fremdes Konto) über das Tor, Schlüssel nur im Tresor, ehrlich BLOCKIERT (ohne Konto nicht prüfbar).
- [x] Rückmeldung: Ort/Adresse, Anzahl Dateien, Prüfergebnis; Fehler verständlich mit „Was tun“.
- [x] Pflicht-Testklassen grün (Zahlen im Bericht), Funktions-Baseline grün; kein Volltest.
**Nicht tun:** nichts ins Internet hochladen, nichts simulieren, Architekturtest nicht lockern, keine neuen Konten, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0718-web-veroeffentlichen.bericht.md`.
