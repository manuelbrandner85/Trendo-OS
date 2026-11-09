# Bericht T-0603: Videoschnitt-Pilot, Design-Editor, Konsolen-Studio, Studio-Ordnung

**Zweige:** `claude/T-0600-pilot-videoschnitt`, `claude/T-0601-pod-design`, `claude/T-0602-konsole-studio`, `claude/T-0604-studio-ordnung`, zusammengeführt in `claude/T-0603-sammel`. Nicht auf `main`.

## Inhalt

- Videoschnitt V1 bis V10: siehe `docs/STUDIO_REBUILD_4_0/pilot/VIDEOSCHNITT_ABNAHME_4.0.md`.
- Print on Demand: Design-Editor und Anbindung an Kittl über dessen offiziellen MCP-Zugang. Anforderungen: `docs/nachbau/kittl/anforderungen.md`.
- Neues Studio „Konsolenspiele portieren“ (STUDIO-CONSOLE-01) mit console-skill, fester Stand `a508929f`.
- Studio-Übersicht: Zuletzt benutzt, Anzahl je Bereich, Unterteilung des Bereichs System & Sicherheit, alphabetisch; „KI-Anweisungen schreiben“ zu „KI & Automatik“.

## Testergebnis

- Volltest vor der Studio-Ordnung: Api 16 bestanden; App 939 bestanden, 4 übersprungen; Paket-Tests OK; Core 2143 bestanden, 5 übersprungen, 1 Fehler (`T-0241`, Dateisperre). Core danach komplett: 2144 bestanden, 5 übersprungen.
- Studio-Ordnung gezielt: 15 von 15 Oberflächen-Tests, Matrix und Katalog neu erzeugt. Kein neuer Volltest nach der Studio-Ordnung.

## Nicht geprüft

- Kittl: kein echter Lauf, dafür braucht es ein Konto und die Anmeldung durch die Person. kittl.com wurde wegen der Nutzungsbedingungen nicht mit REA untersucht.
- Konsolen-Studio: kein echter Port, kein Lauf mit einem echten Spiel, keine Skills von Claude ausgeführt.
- Videoschnitt: siehe Abnahme.

## Offen

Merge nach `main` nur nach OK. VFX als Studio und Umbau der Programm-Hülle sind nicht entschieden. Die Funktionskataloge, Bewertungen und die weiteren Studios aus Rebuild 4.0 sind nicht begonnen. Stufe 2 des Design-Editors (K-15 bis K-18) ist offen.

## Konsolen-Studio: echter Lauf ohne Spiel (09.11.2026)

Feste Fassung a508929 über dieselben Git-Schritte wie im Studio geladen, 11 Skill-Ordner vorhanden. Die mitgelieferte Testsuite und `console-anywhere doctor` liefen auf diesem PC; Ergebnis siehe Commit-Text.

Die 2 Fehler der Testsuite (559 Tests, 31 übersprungen) sind `test_anywhere.TestPackage.test_elf_name_baked_in` und `test_launch_script_reads_system_cnf`. Beide rufen `/bin/sh` auf, das es unter Windows nicht gibt (`FileNotFoundError`). Das erzeugte `play.sh` selbst wurde dadurch auf diesem PC nicht geprüft.

Ein Port ist hier nicht möglich: Auf dem PC liegt kein ISO-Abbild, und Trendo darf keines beschaffen. Für Windows fehlt CMake, das nach den Projektregeln nicht ohne die Person systemweit installiert wird. Linux und Steam Deck brauchen ein Linux-System oder WSL2, Android zusätzlich JDK 17, Gradle, Android SDK und adb.
