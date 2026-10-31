# T-0270 – Kameras & Roboter als echtes Programm „Roboter-Werkstatt“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): RoboDK, MoveIt-Planungsansicht; kein Code übernommen (siehe `docs/claude/vorbilder/roboter-robodk.md`)
- Umgesetzt: Arm in Draufsicht (Reichweite, Wegpunkte, Klick setzt Ziel) und Seitenansicht in realen Proportionen (schattierte Glieder, Gelenkgehäuse, Greifer, Kontaktschatten), Wegpunkte als Liste (hinzufügen, verschieben, löschen, Gelenkwinkel/Fehler je Punkt), Weg simulieren, Greifbahn (Pick & Place), Programm-CSV; „Bild prüfen“ mit eingeblendeten Fundstellen, Maske, Belichtung/Schärfe, Bewegung zwischen zwei Bildern, größtes Objekt als Arm-Ziel; Steuerung (Simulation/Trockenlauf/echt nur nach Freigabe), Not-Aus und Kameras unverändert; Rechner sofort (Servo, Fahrprofil, Reichweite, Kalibrierung).
- Damit hat kein Studio mehr eine FachTool-Formularseite; FachBedienungTests auf die allgemeinen Prüfungen reduziert.
- Tests: App 2 neu (echte Maus) ersetzen CORE09_Ui/CORE09_UiPlus; CORE09_CameraRobotUi grün
