# Print on Demand – Testplan

Studio: Print on Demand (STUDIO-POD-01) · Bereich: Business & Geld · Seite: `pod-programm` · Stand: 0.9.112 (Stand d6f9fcd)

19 Tests sprechen dieses Programm an.

- Büro und Geld: Print on Demand zeigt seine Aufgaben auf der Startseite
- K-01 bis K-03, K-09, K-10: Elemente hinzufügen, mit der Maus verschieben und skalieren, Text ändern, Ebene ausblenden, Rückgängig
- K-05 bis K-08: Tasten ordnen, gruppieren, kopieren, zoomen, Raster und Lineale
- K-11: Farbe in die Palette, von dort anwenden; ungültige Farbe ändert nichts
- K-12, K-13: Entwurf wird PNG in Druckgröße mit Transparenz, wird Motiv und besteht die Druckprüfung; leerer Entwurf wird abgelehnt
- K-14: Kittl ist von Haus aus aus, Knopf schaltet um und nennt die Anmeldung
- POD-Design: Bricht die Anmeldung ab, bleibt der Knopf und ein Hinweis erklärt es
- POD-Design: Kittl-Schalter schaltet die Anbindung ein und aus und bietet die Anmeldung an
- POD: Motiv prüfen, Preis für Wunschgewinn, Markenwarnung, Checkliste, Verkaufstext als Datei
- Printful: Die Oberfläche bietet erst den Schlüssel, nach dem Verbinden den Katalog
- Printful: Mockups laden das Design hoch, erzeugen Bilder und leeren danach die Ablage
- Printful: Ohne Auswahl, Ablage, Name oder Preis passiert nichts
- Printful: Produkt anlegen leert die Ablage erst, wenn Printful die Datei hat; nie ein Bestellaufruf
- Printful: Ungültiger Schlüssel wird nicht gespeichert, gültiger schon, Trennen löscht ihn
- Printify: Die Oberfläche bietet erst den Schlüssel, danach Katalog und die Freigabe-Bestätigung
- Printify: Ohne bestätigte manuelle Freigabe entsteht keine Bestellung; mit ihr erst Anlegen, dann ein eigener Fertigungsaufruf
- Printify: Ohne gültigen Schlüssel wird nichts gespeichert
- Printify: Produkt anlegen zeigt Mockups und steht danach im Shop; kein Bestellaufruf
- T-0753: POD-Testlauf in der Oberfläche ohne eigene Schlüssel – BLOCKIERT mit Anleitung, keine Anfrage; Entwurf wird Artikel im Shop
