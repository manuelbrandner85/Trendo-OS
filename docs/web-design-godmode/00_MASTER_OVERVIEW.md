# Web Design Studio GODMODE – Überblick (07.11.2026)

Ziel: ein echtes, professionelles Webdesign-Studio **im bestehenden Studio „Webseite bauen“ (CORE-07)** – kein zweites System.
Freigabe des Nutzers: „Ja, so bauen“ (Aufteilung Puck + React Three Fiber + GrapesJS-Import + Theatre-Kern; AGPL-Teile nur clean-room).

## Ergebnis dieses Laufs (nachgewiesen)
- Visueller Editor (Puck, MIT) mit deutscher Oberfläche, 8 Bausteinen inkl. 3D-Szene (React Three Fiber) und Bewegungen, eingebettet in Trendo (NativeWebView, `file://`, ohne Netz).
- Speichern über die WebView-Brücke in `seite.trendo.json` (atomar, mit Sicherung `.bak`, Wiederherstellung bei beschädigter Datei).
- Export als fertige Seite (`veroeffentlichen/index.html`, `seite.js`, Bündel, `LIZENZEN.txt`) – im WebView geprüft: Inhalt da, keine Fehler.
- 10 Kerntests grün (`tests/Trendo.Core.Tests/WebDesignTests.cs`); echte Bedienung im Trendo-Fenster (Maus: Ziehen, Doppelklick, Speichern, Export) siehe 19/20.

## Repositories
| Repository | Branch | Commit | Stand | Lizenz | Verwendung |
|---|---|---|---|---|---|
| webstudio-is/webstudio | main | ee27776 | 2026-11-07 | AGPL-3.0 (alle 42 Pakete; css-data Drittanteil MIT) | kein Code übernommen – nur allgemeine Konzepte clean-room |
| puckeditor/puck | main | d05542f | 2026-11-06 | MIT | Editor-Kern, als npm-Paket @puckeditor/core 0.24.0 gebündelt |
| onlook-dev/onlook | main | 423e2e9 | 2026-07-21 | Apache-2.0 | kein Code übernommen – Produkt hängt an Cloud-Diensten; Idee data-oid für Modul 07 |
| GrapesJS/grapesjs | dev | e023d69 | 2026-11-03 | BSD-3-Clause (Kern); Studio SDK kommerziell → ausgeschlossen | geplant für Import/Migration (Modul 15), noch nicht eingebaut |
| pmndrs/react-three-fiber | master | d74622e | 2026-10-14 | MIT | 3D-Baustein, als @react-three/fiber 9.8.1 gebündelt |
| theatre-js/theatre | main | 6ea82b9 | 2024-04-11 (ruht) | core/r3f/dataverse Apache-2.0; studio + app AGPL-3.0 | vorerst nicht gebündelt (siehe 14) |

## Module
| Nr | Modul | Status | Stand |
|---|---|---|---|
| 01 | Native Web Design Workspace | teilweise | Bereich „Visueller Editor“ im Studio CORE-07, NativeWebView, Höhe nach Fenster; Docking/Vollbild noch nicht |
| 02 | Visual Drag-and-Drop Builder | umgesetzt (Grundstock) | Puck: Ziehen (echte Maus im Trendo-Fenster geprüft), Doppelklick/Eingabetaste zum Einfügen, Ebenen, Eigenschaften, Rückgängig; 8 Bausteine |
| 03 | AI Website Architect | vorhanden (Altbestand) | WebsiteStudio/WebsitePlan aus Beschreibung; Anbindung an den visuellen Editor offen |
| 04 | Responsive Design Engine | teilweise | Ansichten Handy 390/Tablet 820/Desktop 1440 (im Browser); unter file:// Container-Abfragen; frei definierbare Breakpoints offen |
| 05 | Design System Studio | geplant | Trendo-Farben im Editor-CSS; globale Tokens offen (Webstudio-Konzept clean-room) |
| 06 | Component & Template Factory | geplant | Baustein-Bibliothek erweiterbar über config; eigene Vorlagen offen |
| 07 | HTML/CSS/JS/React Editor | vorhanden (Altbestand) | Code-Editor im Studio (Dateien der gebauten Seite); Synchronisierung mit dem visuellen Editor offen |
| 08 | 3D Web Experience Studio | umgesetzt (Grundstock) | Baustein 3D-Szene (R3F): Form, Farbe, Drehung, Höhe, Licht, Schatten; GLB-Modelle offen |
| 09 | Motion & Animation Studio | teilweise | Einblenden/Schweben je Baustein (Web Animations API, beachtet „weniger Bewegung“); Zeitleiste offen |
| 10 | CMS & Dynamic Content | geplant | — |
| 11 | Website Interaction Studio | geplant | Knopf-Ziele; Ereignisse offen |
| 12 | Asset & Media Manager | vorhanden (Altbestand) | WebsiteMedia/WebsiteModels; Anbindung an Bild-Baustein offen |
| 13 | SEO & Performance Director | teilweise | Seitentitel + Beschreibung im Export; WebsiteQuality-Prüfung (Altbestand) noch nicht auf den Export angewendet |
| 14 | Website Preview Engine | umgesetzt | Vorschau-Knopf im Editor; Export im Browser öffnen |
| 15 | Import & Migration | geplant | GrapesJS-Kern (BSD-3) als Parser vorgesehen |
| 16 | Collaboration & Version Control | teilweise | Sicherung der vorigen Fassung (.bak) und Wiederherstellung; Git im Studio (Altbestand) |
| 17 | Customer Project Manager | geplant | — |
| 18 | Publishing Center | teilweise | Export als fertige, offline lauffähige Seite (index.html + seite.js + Bündel + Lizenzen); Hosting/Domains offen |
| 19 | Autonomous Website Production | geplant | über vorhandenes KI-Team/Claude-Login, noch nicht angebunden |
| 20 | QA & REA Certification | teilweise | 10 Kerntests (5 positiv, 5 negativ) grün; echte Prüfung im Trendo-Fenster dokumentiert |

Dateien: `src/Trendo.Core/Website/WebDesign.cs`, `src/Trendo.App/MainWindow.WebDesign.cs`, `src/Trendo.App/Assets/webdesign/`, `tools/webdesign-editor/`.
