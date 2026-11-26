# E-Mail & Kontakte – Testplan

Studio: E-Mails & Termine (SYS-09) · Bereich: Arbeiten & Büro · Seite: `post` · Stand: 0.9.110 (Stand 0eb9d52)

26 Tests sprechen dieses Programm an.

- LETTER-005: Tonwechsel ändert nur den Rahmen; Faktenwächter erkennt veränderte Zahlen, Daten, Aktenzeichen
- LETTER-011/020: Rückfragen statt erfundener Werte; Korrekturlesen zeigt Hinweise ohne still zu ändern
- LETTER-012: Echte Tesseract-Texterkennung eines gerenderten Briefs (deu), unsichere Wörter markiert, Sprachpaket per Hash geprüft
- LTR-QA-001 / LETTER-001/002: Brief aus strukturierten Fakten, Kategorie bestimmt Rückfragen und Warnungen
- LTR-QA-002/004 / LETTER-003/016: DOCX real mit LibreOffice zu PDF und ODT; A4, 1 Seite, Hash im Protokoll
- LTR-QA-003 / LETTER-004: Jede Vorlage ist versioniert; leere Platzhalter blockieren den Export
- LTR-QA-005 / LETTER-012: OCR-Unsicherheit wird markiert statt geraten
- LTR-QA-006 / LETTER-013/018: Chronik sortiert, Anlagen nummeriert, Verweise und Hashes stimmen
- LTR-QA-007/017 / LETTER-014/019: Unklares Zugangsdatum → keine erfundene Frist; Bestätigung nur mit Quelle durch Besitzer
- LTR-QA-008 / LETTER-007/023: Gericht – ungeklärte Anwaltspflicht/Form/Fristen sperren; nie „eingereicht“
- LTR-QA-009 / LETTER-014: Rechtsangabe ohne geprüfte amtliche Quelle wird gemeldet
- LTR-QA-010/011/012/020 / LETTER-023/024: Versand immer blockiert; Freigabe an Hash/Empfänger/Anlagen gebunden; „Versendet“ nur mit Nachweis
- LTR-QA-013 / LETTER-017/022: Akten und Briefköpfe getrennt pro Person; fremde/ungültige IDs abgewiesen; Versionen bleiben
- LTR-QA-014 / LETTER-021: Anweisungen im Eingangsbrief sind nur Daten; Aufforderungen und Behauptungen getrennt
- LTR-QA-015 / LETTER-012/022: Forensische Kopien nach 120 h gelöscht, Warnung 24 h vorher, auch aus Versionen
- LTR-QA-016 / LETTER-015: Übersetzung ist nur Entwurf, an deutsches Original (Hash) gebunden
- LTR-QA-019 / LETTER-018/024: Fehlende Anlage oder geänderter Empfänger verhindert Freigabe; widersprüchliche Beträge gemeldet
- SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit
- SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar
- SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler
- SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt
- T-0689 KI im Mailprogramm: Zusammenfassen und Antwort vorschlagen für die offene Nachricht; Auftrag „E-Mail-Entwurf“ landet im Schreibfenster; zurücknehmbar – gesendet wird dabei nie
- T-0689 Seite „Schreiben“: ohne Konto führt die goldene Aktion zum Konto-Assistenten (Text bleibt stehen), mit Konto ins Schreibfenster – „Im Mailprogramm öffnen“ bleibt erhalten
- T-0711 E-Mail „Schreiben“: Entwurf – Rückgängig, Notsicherung, Sperre gegen zweites Fenster, Karte, Zurückholen; als .eml gespeichert räumt die Sicherung weg
