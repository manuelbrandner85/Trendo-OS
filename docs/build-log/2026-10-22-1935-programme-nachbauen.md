# X-10 – Programme nachbauen aus Name oder Beschreibung

Studio „Programme nachbauen“ nimmt jetzt einen Namen oder eine Beschreibung entgegen. Der alte Vergleichsordner bleibt und wird nicht in den Generator gelesen.

Ablauf: Schalter „Im Internet recherchieren“ (aus, wenn offline) liest nur Wikipedia per GET, ohne Weiterleitung und ohne Skripte. Quellen stehen mit Adresse, Länge und SHA-256 im Plan. Danach eine Funktionsliste zum Abhaken, dann „Los“. Die App bekommt einen eigenen Namen (Notizprogramm wird Merkzettel, etwas wie Paint wird Farbblatt). Gebaut wird zuerst die eigene Vorlage. Eine lokale KI darf nur nach einem Compilerfehler drei Dateien vorschlagen, und nur wenn `CodeGuard` sie durchlässt. Bis zu drei Runden, sonst wieder die Vorlage. Cloud-KI wird nicht benutzt.

„In Trendo öffnen“ legt ein eigenes Fenster mit Taskleisten-Eintrag an und trägt das Programm unter „Meine Programme“ ein. „Als Setup-Datei erstellen“ schreibt eine unsignierte Setup.exe in einen gewählten Ordner. Installieren verlangt einen Haken und läuft nur unter Windows, Windows fragt selbst nach. Folgeaufträge wie „mach die Knöpfe größer“ legen eine Fassung an. Rückgängig und Wiederholen lesen `fassungen/vNNN`.

Der Prompt-Kern von Verbesserung 42 registriert den Adapter `nachbauen` als `partial`. Die übrigen 53 Studios bleiben `not_implemented`. Matrix-Zeile und Zähler „1 registrierte Aktionen“ sind angepasst.

Nachweis: `dotnet test Trendo.sln -c Release` grün (Api 15, Core 527 bestanden und 10 übersprungen, App 215). Notiz-App: Build 0 Fehler, 3 Tests, `--selftest` = ok. Setup.exe nur im zusätzlichen Lauf mit gesetztem `TRENDO_VPK`, framework-abhängig, unsigniert, nicht installiert. Bilder unter den Render-Belegen des Laufs.

Offen: keine Windows-Installation, keine Signatur, kein selbstständiges Paket, kein echter Netzabruf, keine live angebundene lokale KI. `requirements.yaml` unverändert.
