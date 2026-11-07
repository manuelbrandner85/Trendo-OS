# Umsetzungsstand (07.11.2026)
| Nr | Modul | Status | Stand |
|---|---|---|---|
| 01 | Native Web Design Workspace | teilweise | Bereich „Visueller Editor“ im Studio CORE-07, NativeWebView, Höhe nach Fenster; Docking/Vollbild noch nicht |
| 02 | Visual Drag-and-Drop Builder | umgesetzt (Grundstock) | Puck: Ziehen (echte Maus im Trendo-Fenster geprüft), Doppelklick/Eingabetaste zum Einfügen, Ebenen, Eigenschaften, Rückgängig; 8 Bausteine |
| 03 | AI Website Architect | vorhanden (Altbestand) | WebsiteStudio/WebsitePlan aus Beschreibung; Anbindung an den visuellen Editor offen |
| 04 | Responsive Design Engine | teilweise | Ansichten Handy 390/Tablet 820/Desktop 1440 (im Browser); unter file:// Container-Abfragen; frei definierbare Breakpoints offen |
| 05 | Design System Studio | teilweise (Tokens umgesetzt) | Seitenweite Tokens Akzent-/Text-/Hintergrundfarbe, Schrift, Rundung, Abstand als CSS-Variablen in allen Bausteinen; Trendo prüft sie beim Speichern (nur #RRGGBB, bekannte Schriften, Grenzen) – 2 Tests; im Browser geprüft. Komponenten-Stile/Bibliotheken offen |
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

## Echte Prüfung im Trendo-Fenster (Windows, WebView2, Testdatenordner)
- Editor lädt per `file://`, Trendo reicht die gespeicherte Seite hinein (Brücke beidseitig).
- Echte Maus: Baustein „Kopfbereich“ auf die Seite gezogen → erscheint und blendet ein; Eigenschaften-Feld deutsch.
- Speichern → `seite.trendo.json` mit beiden Bausteinen + `seite.trendo.json.bak`.
- Export → Hinweis mit Pfad; exportierte Seite im WebView geöffnet: Titel „visuell“, Überschriften „Willkommen | Willkommen“, keine Fehler.
- Gefundene und behobene Fehler: Endlosschleife bereit/Laden, leere iframe-Leinwand unter file://, Absturz beim Neuzeichnen, Umlaute im Titel als Entities, 3D-Versatz.
