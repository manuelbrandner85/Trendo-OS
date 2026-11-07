# Sicher prüfen – Testplan

Studio: Surfen & Herunterladen (SYS-03) · Bereich: System & Sicherheit · Seite: `netz-pruefen` · Stand: 0.9.36 (Stand 0893360)

17 Tests sprechen dieses Programm an.

- SYS-03 (UI): Tabs; Prüfsumme rot/grün; Link-Warnung; Suchmaschine wirkt in Strg+K; Webnotiz in der Suche
- SYS-03-P06: Prüfsumme – Algorithmus erkannt, Abweichung gemeldet
- SYS-03-P07: Download abbrechen und fortsetzen ergibt dieselbe Prüfsumme (lokaler Testserver)
- SYS-03-P07: Fortsetzen-Zustand nur für dieselbe Adresse
- SYS-03-P08: Lesezeichen – Chrome-HTML-Import mit Ordnern, Export/Import verlustfrei
- SYS-03-P09/P22: Lesemodus ohne Skripte; Offline-Kopie ohne Skripte, mit Basis-Adresse
- SYS-03-P10/P11/P12/P25: Tor nur offiziell, Privat-Parameter je Browser, Standardbrowser lesen, Verlauf-Anleitung
- SYS-03-P13: Suchmaschine gespeichert, Standard DuckDuckGo, wirkt auf die Suchadresse
- SYS-03-P14: PDF-Befehl für Chrome/Edge (A4-Prüfung über pdfinfo)
- SYS-03-P14: Seite als PDF – A4, Prüfsumme im Protokoll (nur wenn Chrome/Edge da)
- SYS-03-P15/P16: Echte Adressen bleiben ohne Warnung; Ziel wird erklärt
- SYS-03-P15/P16: Warnzeichen bei Phishing-Adressen
- SYS-03-P17: Formular-Hinweise (IBAN, Karte, Passwort, http) – nie Absenden
- SYS-03-P18/P19/P20/P21/P24: Downloads sortieren + rückgängig, Quarantäne, Defender-Auswertung, Webnotiz, Proxy
- SYS-03: Adressen werden sicher normalisiert (nur http/https, sonst Suche oder Ablehnung)
- SYS-03: Browser werden über bekannte Pfade gefunden; riskante und unfertige Downloads erkannt
- SYS-03: Dock-Browser öffnet Browser & Downloads; Adresse wird geöffnet, javascript: abgelehnt; offline gesperrt
