# Rechts- und Steuerwerte in den W5/W6-Studios

Geprüft am **21.10.2026** an amtlichen Quellen. Im Code liegen die Werte zentral in `src/Trendo.Core/W6/LegalRates.cs`, die Quelle steht dort jeweils dabei. Ohne Gewähr, keine Steuerberatung.

| Wert | Stand | Rechtsgrundlage | Quelle (abgerufen 21.10.2026) | Verwendet in |
|---|---|---|---|---|
| Umsatzsteuer 19 % / 7 % | unverändert | § 12 Abs. 1, 2 UStG | https://www.gesetze-im-internet.de/ustg_1980/__12.html | W5 Finanzen (Buchung, Rechnung), SPEC-08 Preis |
| Kleinunternehmer: Vorjahr ≤ 25.000 €, laufendes Jahr ≤ 100.000 € (Gründungsjahr 25.000 €) | seit 1.1.2025 | § 19 Abs. 1 UStG, Abschn. 19.1 UStAE | https://www.gesetze-im-internet.de/ustg_1980/__19.html · https://www.bundesfinanzministerium.de/Content/DE/Downloads/BMF_Schreiben/Steuerarten/Umsatzsteuer/Umsatzsteuer-Anwendungserlass/2025-03-18-sonderregelung-kleinunternehmer.pdf | `LegalRates.SmallBusiness` (neu); Rechnungshinweis „Gemäß § 19 UStG …“ (W5) |
| Entfernungspauschale 0,38 € ab km 1, Höchstbetrag 4.500 € (nicht bei Pkw) | ab 2026 | § 9 Abs. 1 S. 3 Nr. 4 EStG, StÄndG 2025 (BGBl. 2025 I Nr. 363) | https://esth.bundesfinanzministerium.de/lsth/2026/tabellarische-Uebersicht/9.html · https://www.recht.bund.de/bgbl/1/2025/363/regelungstext.pdf · https://www.bundesfinanzministerium.de/Content/DE/Pressemitteilungen/Finanzpolitik/2025/09/2025-09-10-steueraenderungsgesetz-2025.html | SPEC-16 F14 |
| Entfernungspauschale früher: 2022–2025 0,30 € / ab km 21 0,38 €; 2021 0,30 € / ab km 21 0,35 €; bis 2020 0,30 € | historisch | § 9 EStG a. F., Steuerentlastungsgesetz 2022 | https://finanzamt.brandenburg.de/fa/de/steuern/aktuelle-steuerinformation/steuerentlastungsgesetz-2022/ | SPEC-16 F14 |
| Dienstwagen 1 % / Monat, 0,03 % je Entfernungs-km; E-Auto ¼ bis Preisgrenze, sonst ½; Hybrid ½ | 2026 | § 6 Abs. 1 Nr. 4 S. 2 EStG, § 8 Abs. 2 EStG | https://www.buzer.de/gesetz/17036/a323202.htm (Investitionssofortprogramm, BGBl. 2025 I Nr. 161) | SPEC-16 F15 |
| E-Auto-Preisgrenze: 60.000 € (Anschaffung 2019–2023), 70.000 € (1.1.2024–30.6.2025), 100.000 € (ab 1.7.2025) | 2026 | § 6 Abs. 1 Nr. 4 S. 2 Nr. 3, § 52 Abs. 12 EStG | wie oben · https://www.berlin.de/politik-und-verwaltung/rundschreiben/download.php/4329835 | SPEC-16 F15 |
| Abrundung auf volle 100 € erst nach Bildung des Bruchteils | 2026 | BMF 05.11.2021, Rz. 12 | https://www.lfst.bayern.de/fileadmin/RESSOURCEN/INFORMATIONEN/Steuerinfos/Weitere_Themen/Elektromobilitaet/BMFS_Elektromobilitaet_20211105.pdf | SPEC-16 F15 |
| Reisekosten-Pauschale: Kraftwagen 0,30 €/km, andere motorbetriebene Fahrzeuge 0,20 €/km | 2026 | § 9 Abs. 1 S. 3 Nr. 4a EStG | https://ao.bundesfinanzministerium.de/lsth/2026/tabellarische-Uebersicht/Reisekosten-bei-Ausw%C3%A4rtst%C3%A4tigkeiten.html | SPEC-16 F16 |
| BRKG: 0,20 €/km, höchstens 130 € je Dienstreise; 0,30 €/km ohne Höchstbetrag bei erheblichem dienstlichen Interesse | 2026 | § 5 BRKG | https://www.gesetze-im-internet.de/brkg_2005/__5.html | SPEC-16 F16 (Option „Öffentlicher Dienst“) |
| Impressum „Angaben gemäß § 5 DDG“ | seit 14.05.2024 | § 5 DDG | https://www.gesetze-im-internet.de/ddg/__5.html | W5 Start & Übergabe |

Nicht geändert, weil das keine Rechtswerte sind: Lautheitsziele der Plattformen (SPEC-15), DIN-Faustregeln (Treppe, Wanderzeit), Zerspan-Kennwerte.
