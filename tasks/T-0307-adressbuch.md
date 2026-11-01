# T-0307 – Kontakte als Adressbuch

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Kontakte als Adressbuch“).
- Vorher: Kontakte nur als einfache Liste (Name, E-Mail, Telefon, Notiz) unten auf „E-Mail & Kontakte“; vCard nur mit diesen Feldern.
- Jetzt: neue Seite „Adressbuch“ (`kontakte`, `MainWindow.AddressBook.cs`) auf dem **vorhandenen** Kontaktspeicher `MailCenter` (`W3/AddressBook.cs`), kein zweites System:
  - Kontakt erweitert um Mobil, Firma, Straße, Ort, Geburtstag, Gruppen, Foto, Favorit, Webseite (ältere Daten bleiben lesbar).
  - Drei Spalten: Gruppen (Alle, Favoriten, Bald Geburtstag, Doppelte, eigene Gruppen) · Liste mit Buchstaben, Avatar (Foto oder Initialen) und Suche über alle Felder (auch Ziffern) · Kontaktkarte.
  - Karte: E-Mail schreiben (öffnet „E-Mail & Kontakte“ mit Empfänger), Bearbeiten (zweispaltig, Speichern oben), Favorit, als vCard, Löschen nach „Ja“.
  - Doppelte: gleicher Name (auch „Muster, Erika“), gleiche E-Mail oder Nummer (+49/0 egal); Zusammenführen nach „Ja“ füllt Lücken, vereint Gruppen und Notizen.
  - Geburtstage: nächster Termin mit Alter (29.2. berücksichtigt); „Geburtstage in den Kalender“ legt jährliche Ganztags-Termine mit Erinnerung an (erneut ausführen erzeugt keine Doppel).
  - vCard 3.0 mit allen Feldern und Foto hinaus; hinein auch vCard 2.1/4.0 (Gruppenpräfix, TYPE=CELL, ADR, BDAY ohne Bindestriche, CATEGORIES, ORG, PHOTO base64).
  - „E-Mail & Kontakte“ behält die Schnellanlage und verweist auf das Adressbuch.
- Nebenbei behoben: Farben von Avataren, Album-Platzhaltern und Haushaltsbuch-Kategorien hingen von `string.GetHashCode` ab und wechselten bei jedem Start – jetzt stabil (`StableHash`).
- Vorschaubild: Render-Schalter `TRENDO_KONTAKTE=1` bzw. `=bearbeiten`.
- Tests: `AddressBookTests` (Felder, Gruppen, Suche, alte Daten, Doppelte, Zusammenführen, Geburtstage inkl. 29.2. und Jahreswechsel, vCard-Rundweg mit Foto, fremde vCard 4.0), `AddressBook_Ui` (echte Klicks: anlegen, Foto, falsche E-Mail abgewiesen, Favorit, Gruppe, Suche, Geburtstage in den Kalender ohne Doppel, Doppelte Nein/Ja, E-Mail schreiben, Löschen Nein/Ja); `SYS09_Mail`, `SYS09_Ui` grün.
