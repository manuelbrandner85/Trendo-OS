# Design-Editor für Print on Demand: Anforderungen

Stand 09.11.2026. Vorbild für Bedienung und Abläufe: Kittl. Eigene Umsetzung, eigene Gestaltung im Trendo-Stil.

**Grenzen.** Die Nutzungsbedingungen von Kittl untersagen Bots und Scraper, Dekompilieren und das Nachbilden der Plattform (Abschnitt 7). Deshalb wurde kittl.com nicht mit REA untersucht und nicht mit einem Konto ausgemessen. Quellen sind nur öffentliche Seiten: Produktseite des Editors, Artikel zu Tastaturkürzeln und Apps, Beschreibung des MCP-Zugangs. Es werden keine Grafiken, Schriften, Symbole, Vorlagen, Texte oder Namen von Kittl übernommen. Eine Rechtsberatung ist das nicht.

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| K-01 | Arbeitsfläche | Druckbereich auf großer Fläche, Mausrad zoomt, Ziehen mit mittlerer Taste verschiebt | `PodDesign_Ui` | Editor-Seite | 1 |
| K-02 | Elemente | Text, Rechteck, Kreis, Bild hinzufügen und löschen | `PodDesignTests`, `PodDesign_Ui` | Editor-Seite | 1 |
| K-03 | Greifen | Verschieben, Skalieren, Drehen mit der Maus; Alt skaliert von der Mitte | `PodDesign_Ui` | Tastatur-Artikel | 1 |
| K-04 | Einrasten | Rastet an Kanten und Mitten; Strg schaltet es beim Ziehen aus | `PodDesignTests` | Tastatur-Artikel | 1 |
| K-05 | Reihenfolge | `]` und `[` eine Ebene, mit Umschalt ganz nach vorn oder hinten | `PodDesign_Ui` | Tastatur-Artikel | 1 |
| K-06 | Gruppe | Strg+G fasst zusammen, Gruppe bewegt sich gemeinsam | `PodDesign_Ui` | Tastatur-Artikel | 1 |
| K-07 | Bearbeiten | Strg+C, V, X, Rückgängig | `PodDesign_Ui` | Tastatur-Artikel | 1 |
| K-08 | Ansicht | Plus und Minus zoomen, Umschalt+1 bis 3 passen ein, Strg+' Raster, Umschalt+R Lineale | `PodDesign_Ui` | Tastatur-Artikel | 1 |
| K-09 | Ebenenliste | Ebenen sehen, auswählen, ausblenden | `PodDesign_Ui` | Editor-Seite | 1 |
| K-10 | Text | Text jederzeit änderbar, Schrift, Größe, Fett, Umriss, Schatten | `PodDesign_Ui` | Editor-Seite | 1 |
| K-11 | Farben | Eigene Palette speichern und anwenden | `PodDesign_Ui` | Editor-Seite | 1 |
| K-12 | Produktvorschau | Entwurf wird Motiv und erscheint auf dem Produkt | `PodDesign_Ui` | Editor-Seite | 1 |
| K-13 | Ausgabe | PNG mit Transparenz in Druckgröße, SVG | `PodDesignTests`, `PodDesign_Ui` | Editor-Seite | 1 |
| K-14 | Anbindung | Offizieller MCP-Zugang von Kittl für Claude im Studio, Anmeldung durch die Person | `PodKittlTests`; echter Lauf braucht ein Konto | MCP-Seite | 1 |
| K-15 | Zeichenstift, Formen verbinden | Bézier-Pfade, Vereinigen, Abziehen | offen | Editor-Seite | 2 |
| K-16 | Schrift auf Kurve, Texteffekte | Text auf Bogen, weitere Effekte | offen | Tastatur-Artikel | 2 |
| K-17 | KI im Editor | Hintergrund entfernen, Vektorisieren, Bild erzeugen | offen, lokale KI vorhanden | Editor-Seite | 2 |
| K-18 | Vorlagen | Eigene Vorlagen für Produkte | offen | Editor-Seite | 2 |
