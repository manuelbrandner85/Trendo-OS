# Sicher prüfen – Testergebnisse

Studio: Surfen & Herunterladen (SYS-03) · Bereich: System & Sicherheit · Seite: `netz-pruefen` · Stand: 0.9.36 (Stand 208ed9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 14 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 2

| Test | Ergebnis |
|---|---|
| SYS-03 (UI): Tabs; Prüfsumme rot/grün; Link-Warnung; Suchmaschine wirkt in Strg+K; Webnotiz in der Suche | ✓ bestanden |
| SYS-03-P06: Prüfsumme – Algorithmus erkannt, Abweichung gemeldet | ✓ bestanden |
| SYS-03-P07: Download abbrechen und fortsetzen ergibt dieselbe Prüfsumme (lokaler Testserver) | ✓ bestanden |
| SYS-03-P07: Fortsetzen-Zustand nur für dieselbe Adresse | ✓ bestanden |
| SYS-03-P08: Lesezeichen – Chrome-HTML-Import mit Ordnern, Export/Import verlustfrei | ✓ bestanden |
| SYS-03-P09/P22: Lesemodus ohne Skripte; Offline-Kopie ohne Skripte, mit Basis-Adresse | ✓ bestanden |
| SYS-03-P10/P11/P12/P25: Tor nur offiziell, Privat-Parameter je Browser, Standardbrowser lesen, Verlauf-Anleitung | ✓ bestanden |
| SYS-03-P13: Suchmaschine gespeichert, Standard DuckDuckGo, wirkt auf die Suchadresse | ✓ bestanden |
| SYS-03-P14: PDF-Befehl für Chrome/Edge (A4-Prüfung über pdfinfo) | ✓ bestanden |
| SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da) | übersprungen |
| SYS-03-P15/P16: Echte Adressen bleiben ohne Warnung; Ziel wird erklärt | ✓ bestanden |
| SYS-03-P15/P16: Warnzeichen bei Phishing-Adressen | nicht im Lauf |
| SYS-03-P17: Formular-Hinweise (IBAN, Karte, Passwort, http) – nie Absenden | ✓ bestanden |
| SYS-03-P18/P19/P20/P21/P24: Downloads sortieren + rückgängig, Quarantäne, Defender-Auswertung, Webnotiz, Proxy | ✓ bestanden |
| SYS-03: Adressen werden sicher normalisiert (nur http/https, sonst Suche oder Ablehnung) | nicht im Lauf |
| SYS-03: Browser werden über bekannte Pfade gefunden; riskante und unfertige Downloads erkannt | ✓ bestanden |
| SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt | ✓ bestanden |
