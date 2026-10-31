# T-0290 – Posteingang: Suche, Merken, Weiterleiten, Als Aufgabe

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Posteingang als Mailprogramm“).
- Befund: Der Posteingang war schon dreispaltig (Ordner · Liste · Lesen) mit Antworten, Senden nur nach Bestätigung und Termin-Erkennung.
- Neu: Suche nach Absender/Betreff, Ordner „⭐ Gemerkt“ (lokal, auf dem Server ändert sich nichts), Weiterleiten (Entwurf mit Kopf der Originalnachricht), „☑ Als Aufgabe“ (Datum aus dem Text als Fälligkeit, Liste „E-Mail“), Konto als einklappbare Zeile, sobald es eingerichtet ist.
- Senden bleibt unverändert: nur nach „Ja, jetzt senden“.
- Offen: Anhänge anzeigen und speichern (der IMAP-Leser liefert heute nur den Text), Ordner auf dem Server.
- Tests: neu `MailPlus_Ui` (Suche, Gemerkt); `Teil4BueroUiTests` um Merken, Als Aufgabe und Weiterleiten über die echte IMAP-Attrappe erweitert.
