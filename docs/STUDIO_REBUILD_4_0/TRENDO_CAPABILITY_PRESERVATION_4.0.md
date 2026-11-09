# Trendo Funktionssicherung 4.0

> Entwurf. Gilt für jeden Umbau eines bestehenden Studios.

## Ausgangslage

- 1531 Funktionen stehen in Funktionskatalogen (`docs/spec/kataloge/`). 28 Studios haben noch keinen Katalog.
- Bis zu 100 Rückgängig-Schritte, automatische Sicherung und 20 Versionen je Datei gibt es für `.tdoc`-Dokumente (`ProgramDocument.cs`).
- Die Prüfungen „keine tote Taste“ und „Studio-Abnahme“ laufen für jedes Studio (`DeadButtonTests`).

## Vor jeder Änderung

1. Funktionsliste des Programms prüfen. Fehlt sie, zuerst aus dem Code anlegen.
2. Für jede Funktion ohne Test einen Test schreiben, der das heutige Verhalten festhält.
3. Eine echte alte Projektdatei als Testdatei ablegen (`tests/`-Vorlagen, ohne persönliche Daten).
4. Exporte einmal erzeugen und Merkmale festhalten (Format, Größe, Inhalt).
5. Eigener Zweig; der alte Stand bleibt über Git erreichbar.

## Nach jeder Änderung

1. Alle Tests des Programms und der Volltest vor dem Upload.
2. Alte Projektdatei öffnen, ändern, speichern, wieder öffnen.
3. Exporte mit den festgehaltenen Merkmalen vergleichen.
4. Öffnungszeit vergleichen (`WarmMs` im Programmkatalog).
5. Bild des echten Programms ansehen.

## Regel für den Austausch

Ein alter Editor bleibt, bis der neue jede Funktion seiner Liste mit grünem Test übernommen hat. Eine Funktion wird nur gestrichen, wenn der Nutzer das ausdrücklich entscheidet.

## Zuerst zu schließen

Funktionskataloge für die 27 Studios ohne Katalog (ohne das abgelehnte SPEC-07). Die Liste steht im Inventar: Spalte „Funktionen (Katalog)“ mit dem Wert 0.
