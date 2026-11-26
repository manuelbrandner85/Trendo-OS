# T-0772: Fräsen- und DXF-Restpunkte aus 0.9.109

**Ziel:** Die offenen Punkte aus 0.9.109 (T-0757…T-0760) erledigen oder ehrlich begründen: k5_dicht hängt, b2/e0/z1 über 150 s, Gravur p1 5,7 s, REGION und ezdxf-Linse nur gemeldet, „/shell:powershell“ in „Gefahren prüfen“ nicht rot.
**Branch:** claude/T-0772-fraesen-rest (Basis main 542dc1ce7 = Release 0.9.110), Etappe 0.9.111, freigegeben 26.11.2026
**Dateien/Bereich:** `src/Trendo.Core/Cad/CadContours*.cs`, `src/Trendo.Core/W6/CncCam.cs`, `src/Trendo.Core/Sys/Gefahren.cs`, neue Tests `tests/Trendo.Core.Tests/T0772*`, Belege `reports/T-0772/`, Abschnitt in `installer/neuerungen/0.9.111.md`
**Fertig, wenn:**
- [ ] k5_dicht wird in begrenzter Zeit fertig oder bricht mit klarer Meldung ab (Ursache gefunden, gemessen vorher/nachher)
- [ ] b2, e0, z1 und p1 profiliert; wo die Zeit im Algorithmus verloren geht, behoben – bei gleichem Ergebnis (Vergleichslauf alt/neu)
- [ ] Rust nur bei gemessenem Vorteil (sonst C#)
- [ ] REGION und ezdxf-Linse: geprüft, ob echt lesbar; sonst genaue Begründung; Python/ezdxf auf dem PC geprüft, nichts installiert
- [ ] `/shell:powershell -enc …` rot, Rot-Grün-Test, kein neuer Fehlalarm für harmloses „powershell“
- [ ] Rohlogs und SHA-256 unter `reports/T-0772/`, keine Grenzwerte gelockert, Zeittests robust (Median, relativ)
**Nicht tun:** kein Volltest (macht die Hauptsitzung), keine Version im csproj, kein Merge, kein Release, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0772-fraesen-rest.bericht.md`.
