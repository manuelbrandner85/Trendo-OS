namespace Trendo.Core;

/// <summary>Rechts- und Steuerwerte (Deutschland) an einer Stelle, jeweils mit Quelle und Prüfdatum.
/// Geprüft am 21.10.2026. Ohne Gewähr – keine Steuerberatung.</summary>
public static class LegalRates
{
    public const string Checked = "21.10.2026";

    /// <summary>§ 12 Abs. 1 UStG – https://www.gesetze-im-internet.de/ustg_1980/__12.html (geprüft 21.10.2026)</summary>
    public const decimal VatStandard = 19m;
    /// <summary>§ 12 Abs. 2 UStG – https://www.gesetze-im-internet.de/ustg_1980/__12.html (geprüft 21.10.2026)</summary>
    public const decimal VatReduced = 7m;

    /// <summary>§ 19 Abs. 1 UStG (seit 1.1.2025): Vorjahr ≤ 25.000 €, laufendes Jahr ≤ 100.000 €; im Gründungsjahr gilt 25.000 €.
    /// https://www.gesetze-im-internet.de/ustg_1980/__19.html · BMF-Schreiben 18.03.2025 (Abschn. 19.1 UStAE)
    /// https://www.bundesfinanzministerium.de/Content/DE/Downloads/BMF_Schreiben/Steuerarten/Umsatzsteuer/Umsatzsteuer-Anwendungserlass/2025-03-18-sonderregelung-kleinunternehmer.pdf (geprüft 21.10.2026)</summary>
    public const decimal SmallBusinessPrevYear = 25_000m, SmallBusinessCurrentYear = 100_000m;

    /// <summary>Kleinunternehmerregelung anwendbar? Umsätze netto (Gesamtumsatz nach § 19 Abs. 2 UStG).</summary>
    public static (bool Ok, string Reason) SmallBusiness(decimal prevYear, decimal currentYear, bool foundedThisYear = false)
    {
        if (foundedThisYear) return currentYear <= SmallBusinessPrevYear ? (true, "Gründungsjahr: bis 25.000 € steuerfrei.") : (false, "Gründungsjahr: über 25.000 € – schon der Umsatz, der die Grenze überschreitet, ist steuerpflichtig.");
        if (prevYear > SmallBusinessPrevYear) return (false, "Vorjahresumsatz über 25.000 € – Regelbesteuerung.");
        return currentYear <= SmallBusinessCurrentYear ? (true, "Vorjahr bis 25.000 € und laufendes Jahr bis 100.000 €.") : (false, "Laufendes Jahr über 100.000 € – ab dem Umsatz, der die Grenze überschreitet, Regelbesteuerung.");
    }

    /// <summary>Entfernungspauschale § 9 Abs. 1 Satz 3 Nr. 4 EStG, je vollem Entfernungs-km.
    /// Ab 2026: 0,38 € ab km 1 (Steueränderungsgesetz 2025, BGBl. 2025 I Nr. 363) – https://esth.bundesfinanzministerium.de/lsth/2026/tabellarische-Uebersicht/9.html ·
    /// https://www.recht.bund.de/bgbl/1/2025/363/regelungstext.pdf. 2022–2025: 0,30 € bis km 20, 0,38 € ab km 21; 2021: 0,35 € ab km 21 (Steuerentlastungsgesetz 2022) –
    /// https://finanzamt.brandenburg.de/fa/de/steuern/aktuelle-steuerinformation/steuerentlastungsgesetz-2022/ (geprüft 21.10.2026)</summary>
    public static (double First20, double From21) CommuteRates(int year) => year >= 2026 ? (0.38, 0.38) : year >= 2022 ? (0.30, 0.38) : year == 2021 ? (0.30, 0.35) : (0.30, 0.30);
    /// <summary>Höchstbetrag § 9 Abs. 1 Satz 3 Nr. 4 Satz 2 EStG (gilt nicht bei eigenem/überlassenem Pkw).</summary>
    public const double CommuteCap = 4500;

    /// <summary>Reisekosten-Pauschale § 9 Abs. 1 Satz 3 Nr. 4a EStG (höchste BRKG-Sätze): Kraftwagen 0,30 €, andere motorbetriebene Fahrzeuge 0,20 € je km.
    /// https://ao.bundesfinanzministerium.de/lsth/2026/tabellarische-Uebersicht/Reisekosten-bei-Ausw%C3%A4rtst%C3%A4tigkeiten.html (geprüft 21.10.2026)</summary>
    public const double TravelCar = 0.30, TravelOtherMotor = 0.20;

    /// <summary>§ 5 BRKG (Bundesbedienstete): 0,20 €/km, höchstens 130 € je Dienstreise (Behörde kann 150 € festsetzen); 0,30 €/km ohne Höchstbetrag bei erheblichem dienstlichen Interesse.
    /// https://www.gesetze-im-internet.de/brkg_2005/__5.html (geprüft 21.10.2026)</summary>
    public const double BrkgSmall = 0.20, BrkgSmallCap = 130, BrkgLarge = 0.30;

    /// <summary>§ 6 Abs. 1 Nr. 4 Satz 2 EStG: 1 % des Bruttolistenpreises je Monat; Fahrten Wohnung–Arbeit 0,03 % je km (§ 8 Abs. 2 Satz 3 EStG).
    /// Reine E-Autos: ¼ bis zur Preisgrenze, sonst ½. Grenze nach Anschaffung: 2019–2023 60.000 €, 1.1.2024–30.6.2025 70.000 €, ab 1.7.2025 100.000 €
    /// (Investitionssofortprogramm, BGBl. 2025 I Nr. 161) – https://www.buzer.de/gesetz/17036/a323202.htm ·
    /// Abrundung auf volle 100 € nach Bildung des Bruchteils: BMF 05.11.2021 Rz. 12 – https://www.lfst.bayern.de/fileadmin/RESSOURCEN/INFORMATIONEN/Steuerinfos/Weitere_Themen/Elektromobilitaet/BMFS_Elektromobilitaet_20211105.pdf (geprüft 21.10.2026)</summary>
    public static double EvPriceLimit(DateOnly acquired) => acquired >= new DateOnly(2025, 7, 1) ? 100_000 : acquired >= new DateOnly(2024, 1, 1) ? 70_000 : 60_000;
}
