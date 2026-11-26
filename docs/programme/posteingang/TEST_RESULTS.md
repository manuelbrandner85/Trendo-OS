# Posteingang – Testergebnisse

Studio: E-Mails & Termine (SYS-09) · Bereich: Arbeiten & Büro · Seite: `posteingang` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 12 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Mailprogramm: Anhang ansehen und speichern, Allen antworten (Kopie ohne eigene Adresse), Entwurf speichern/öffnen, Senden → Gesendet, Unterhaltungen, Tasten N/R | ✓ bestanden |
| O1/O2/O6: Konto (Passwort geschützt), Posteingang nur lesend, Nachricht öffnen, Antworten nur nach Bestätigung senden, Regel erst nach Freigabe | ✓ bestanden |
| Posteingang: Suche nach Absender/Betreff, Ordner „Gemerkt“ zeigt nur gemerkte Nachrichten | ✓ bestanden |
| SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit | ✓ bestanden |
| SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen | ✓ bestanden |
| SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet) | ✓ bestanden |
| SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar | ✓ bestanden |
| SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler | ✓ bestanden |
| SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt | ✓ bestanden |
| T-0689 KI im Mailprogramm: Zusammenfassen und Antwort vorschlagen für die offene Nachricht; Auftrag „E-Mail-Entwurf“ landet im Schreibfenster; zurücknehmbar – gesendet wird dabei nie | ✓ bestanden |
| T-0689 Mailprogramm lesen: Leerzustand „Konto einrichten“ → Assistent → Ordner und Seiten vom Server (TLS) → HTML sicher, Bilder erst nach Klick, Link mit Rückfrage → Anhang bytegleich → gelesen, verschieben, Papierkorb, endgültig nur mit Rückfrage → Suche → ohne Netz lesbar | ✓ bestanden |
| T-0689 Mailprogramm schreiben: Strg+N, Anhang, Blindkopie, formatiert → Rückfrage → echte Zustellung (SMTP) und Kopie in „Gesendet“ → Antwort mit Zitat und Bezug → Entwurf sichert sich selbst, übersteht den Neustart, liegt auch im Postfach | ✓ bestanden |
