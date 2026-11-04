# SYS-09 – E-Mails & Termine: Funktionskatalog

Funktionen nach den Vorbildern (Thunderbird, Outlook, Google Kontakte, Google Kalender, Microsoft To Do, Planka – nur Funktionen, Clean-Room), Stand 04.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

## E-Mail und Posteingang

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| EM-01 | Konto, Abrufen, Nachricht lesen (EML, Anhänge) | Thunderbird | umgesetzt | SYS09_Mail |
| EM-02 | Entwürfe, Gesendet, Unterhaltungen, Allen antworten, Weiterleiten | Outlook | umgesetzt | MailPlusTests |
| EM-03 | Ordner, Liste, Lesebereich | Outlook | umgesetzt | MailPlus_Ui |
| EM-04 | Regeln vorschlagen und freigeben, Termine aus E-Mails | Outlook | umgesetzt | O1O6_PimUi |
| EM-05 | Senden nur nach ausdrücklichem Ja | – | umgesetzt | SYS09_Mail |

## Adressbuch

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| AB-01 | Kontakte mit Gruppen, Favoriten, Foto, Suche über alle Felder | Google Kontakte | umgesetzt | AddressBookTests |
| AB-02 | Doppelte finden und zusammenführen | Google Kontakte | umgesetzt | AddressBookTests |
| AB-03 | Geburtstage, in den Kalender | Outlook | umgesetzt | AddressBook_Ui |
| AB-04 | vCard hinein und hinaus (mit Foto) | Thunderbird | umgesetzt | AddressBookTests |
| AB-05 | CSV im Google- und Outlook-Format hinein und hinaus | Google Kontakte, Outlook | umgesetzt | AddressBookPlusTests |
| AB-06 | Telefonnummern vereinheitlichen (+49 …) | Google „Korrigieren und verwalten“ | umgesetzt | AddressBookPlusTests |
| AB-07 | Adressetiketten als PDF (Zweckform-Formate, angebrochener Bogen) | Word/Outlook Etiketten | umgesetzt | AddressBookPlusTests |
| AB-08 | Etiketten der gewählten Gruppe aus der Oberfläche | Outlook | umgesetzt | AddressBook_Ui |

## Kalender und Aufgaben

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| KA-01 | Monatskalender mit Terminen, Wiederholung, Erinnerung | Google Kalender | umgesetzt | CalendarGrid_Ui |
| AU-01 | Aufgabentafel mit Spalten | Planka, Microsoft To Do | umgesetzt | TodoBoard_Ui |
| AU-02 | Aufgaben verwalten | Microsoft To Do | umgesetzt | TaskManagerTests |
