# Trendo – vorhandene Web-Architektur (im Code geprüft)
- Studio CORE-07 „Webseite bauen“ (`src/Trendo.App/StudioCatalog.cs`), Oberfläche `MainWindow.W4Web.cs` (Beschreibung → Seite, Stile, Vorschau, Code-Editor, 3D/Medien, Qualität) eingebettet in `MainWindow.W4Dev.cs` (Tab „Software“).
- Kern `src/Trendo.Core/Website/`: `WebsiteStudio` (Bauen/Ändern mit Vorschau der Änderung), `WebsitePlan` (Seitenplan, KI liefert nur Texte), `WebsiteHtml` (echte HTML/CSS/JS, Bibliotheken aus dem Programm), `WebsiteQuality` (lokale Prüfung: Handy, Ladezeit, Barrierefreiheit, SEO, Links), `WebsiteFiles` (GLB, Medien).
- `src/Trendo.App/WebsitePreview.cs`: Bildvorschau über lokalen Chrome/Edge; QA-019 verbietet Netz/Sockets in Trendo.Core.
- `Avalonia.Controls.WebView` 12.1.1 (`NativeWebView`) – bereits im Claude-Chat genutzt; Brücke `invokeCSharpAction` → `WebMessageReceived` (`e.Body`), Rückweg `InvokeScript`.
- Agenten: `AgentCenter`/`StudioTeams` (Web-Team), Claude über den Login der Person (`W2/ClaudeCode.cs`).
- Node 24 / npm 11 auf dem Entwickler-PC (nur zum Bauen des Bündels, nicht zur Laufzeit).
