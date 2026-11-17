using System.Diagnostics;
using System.Text;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0694: Beispiel-Kontoauszüge (handgeschrieben nach Spezifikation, neutrale Namen, öffentliche Beispiel-IBANs mit gültiger Prüfziffer) und der Aufruf
/// des fremden Programms pdftotext – gemeinsam für Core- und Oberflächen-Tests (in Trendo.App.Tests verlinkt).</summary>
public static class GeldBeispiele
{
    /// <summary>pdftotext (Poppler/Xpdf) aus dem Suchpfad – ein fremdes Programm, das das PDF unabhängig von PDFsharp liest. null = auf diesem PC nicht vorhanden.</summary>
    public static string? PdfToText()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in new[] { "pdftotext.exe", "pdftotext" })
                try { var f = Path.Combine(dir.Trim('"'), name); if (File.Exists(f)) return f; } catch (ArgumentException) { }
        return null;
    }

    public static string ReadPdfText(string tool, string pdf)
    {
        var psi = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var a in new[] { "-enc", "UTF-8", "-layout", pdf, "-" }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!; var text = p.StandardOutput.ReadToEnd(); p.WaitForExit(30000);
        Assert.True(p.ExitCode == 0, "pdftotext meldet Fehler: " + p.StandardError.ReadToEnd());
        return text;
    }

    public const string Camt = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
          <BkToCstmrStmt>
            <GrpHdr><MsgId>MSG-2026-11-0001</MsgId><CreDtTm>2026-11-13T18:00:00</CreDtTm></GrpHdr>
            <Stmt>
              <Id>STMT-2026-011</Id><ElctrncSeqNb>11</ElctrncSeqNb><CreDtTm>2026-11-13T18:00:00</CreDtTm>
              <Acct><Id><IBAN>DE89370400440532013000</IBAN></Id><Ccy>EUR</Ccy></Acct>
              <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">1000.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-11-01</Dt></Dt></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">2176.72</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-11-13</Dt></Dt></Bal>
              <Ntry>
                <Amt Ccy="EUR">2034.22</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-10</Dt></BookgDt><ValDt><Dt>2026-11-10</Dt></ValDt><AcctSvcrRef>2026111000001</AcctSvcrRef>
                <NtryDtls><TxDtls><Refs><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
                  <RltdPties><Dbtr><Nm>Beispiel GmbH</Nm></Dbtr><DbtrAcct><Id><IBAN>DE02120300000000202051</IBAN></Id></DbtrAcct></RltdPties>
                  <RmtInf><Ustrd>Rechnung RE-2026-001</Ustrd><Ustrd>vielen Dank</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">800.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-03</Dt></BookgDt><ValDt><Dt>2026-11-03</Dt></ValDt>
                <NtryDtls><TxDtls><Refs><EndToEndId>MIETE-2026-11</EndToEndId></Refs>
                  <RltdPties><Cdtr><Nm>Hausverwaltung Muster</Nm></Cdtr><CdtrAcct><Id><IBAN>DE02120300000000202051</IBAN></Id></CdtrAcct></RltdPties>
                  <RmtInf><Ustrd>Miete November</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">42.50</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-05</Dt></BookgDt><ValDt><Dt>2026-11-05</Dt></ValDt><AddtlNtryInf>Sammellastschrift</AddtlNtryInf>
                <NtryDtls>
                  <TxDtls><Amt Ccy="EUR">30.00</Amt><RltdPties><Cdtr><Nm>Stadtwerke Musterstadt</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Abschlag Strom</Ustrd></RmtInf></TxDtls>
                  <TxDtls><Amt Ccy="EUR">12.50</Amt><RltdPties><Cdtr><Nm>Papierladen Beispiel</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Druckerpapier</Ustrd></RmtInf></TxDtls>
                </NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">15.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><RvslInd>true</RvslInd><Sts>BOOK</Sts><BookgDt><Dt>2026-11-12</Dt></BookgDt><ValDt><Dt>2026-11-12</Dt></ValDt>
                <NtryDtls><TxDtls><RltdPties><Cdtr><Nm>Kunde Beispiel</Nm></Cdtr></RltdPties><RmtInf><Ustrd>Gutschrift irrtuemlich</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="EUR">99.00</Amt><CdtDbtInd>DBIT</CdtDbtInd><Sts>PDNG</Sts><BookgDt><Dt>2026-11-14</Dt></BookgDt>
                <NtryDtls><TxDtls><RmtInf><Ustrd>Vormerkung Kartenzahlung</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;

    public const string Mt940 = ":20:STARTUMS\r\n:25:37040044/0532013000\r\n:28C:00011/001\r\n:60F:C261101EUR1000,00\r\n" +
        ":61:2611101110C2034,22NTRFNONREF//B26111000001\r\n:86:166?00GUTSCHRIFT?109310?20EREF+NOTPROVIDED?21SVWZ+Rechnung RE-2026-00?221 vielen Dank?30COBADEFFXXX?31DE02120300000000202051?32Beispiel GmbH\r\n" +
        ":61:2611031103D800,00NDDTNONREF\r\n:86:105?00LASTSCHRIFT?20EREF+MIETE-2026-11?21SVWZ+Miete November?32Hausverwaltung\r\n?33 Muster\r\n" +
        ":61:261105D42,5NMSCNONREF\r\n:86:Kartenzahlung Papierladen Beispiel\r\n" +
        ":61:2611121112RC15,00NSTONONREF\r\n:86:159?00STORNO?20SVWZ+Gutschrift irrtuemlich?32Kunde Beispiel\r\n" +
        ":62F:C261113EUR2176,72\r\n-\r\n";

    public static readonly (string Bank, string Csv)[] CsvLayouts =
    [
        ("Sparkasse (CSV-CAMT)", "\"Auftragskonto\";\"Buchungstag\";\"Valutadatum\";\"Buchungstext\";\"Verwendungszweck\";\"Glaeubiger ID\";\"Mandatsreferenz\";\"Kundenreferenz (End-to-End)\";\"Sammlerreferenz\";\"Lastschrift Ursprungsbetrag\";\"Auslagenersatz Ruecklastschrift\";\"Beguenstigter/Zahlungspflichtiger\";\"Kontonummer/IBAN\";\"BIC (SWIFT-Code)\";\"Betrag\";\"Waehrung\";\"Info\"\n" +
            "\"DE89370400440532013000\";\"10.11.26\";\"10.11.26\";\"GUTSCHR. UEBERWEISUNG\";\"Rechnung RE-2026-001 vielen Dank\";\"\";\"\";\"\";\"\";\"\";\"\";\"Beispiel GmbH\";\"DE02120300000000202051\";\"BYLADEM1001\";\"2034,22\";\"EUR\";\"Umsatz gebucht\"\n" +
            "\"DE89370400440532013000\";\"03.11.26\";\"03.11.26\";\"FOLGELASTSCHRIFT\";\"Miete November\";\"DE98ZZZ09999999999\";\"M-17\";\"\";\"\";\"\";\"\";\"Hausverwaltung Muster\";\"DE02120300000000202051\";\"BYLADEM1001\";\"-800,00\";\"EUR\";\"Umsatz gebucht\"\n"),
        ("DKB (neues Format)", "\"Girokonto\";\"DE89370400440532013000\"\n\"\"\n\"Kontostand vom 13.11.2026:\";\"2.234,22 €\"\n\"\"\n\"Buchungsdatum\";\"Wertstellung\";\"Status\";\"Zahlungspflichtige*r\";\"Zahlungsempfänger*in\";\"Verwendungszweck\";\"Umsatztyp\";\"IBAN\";\"Betrag (€)\";\"Gläubiger-ID\";\"Mandatsreferenz\";\"Kundenreferenz\"\n" +
            "\"10.11.26\";\"10.11.26\";\"Gebucht\";\"Beispiel GmbH\";\"Erika Muster\";\"Rechnung RE-2026-001 vielen Dank\";\"Eingang\";\"DE02120300000000202051\";\"2.034,22\";\"\";\"\";\"\"\n" +
            "\"03.11.26\";\"03.11.26\";\"Gebucht\";\"Erika Muster\";\"Hausverwaltung Muster\";\"Miete November\";\"Ausgang\";\"DE02120300000000202051\";\"-800,00\";\"\";\"\";\"\"\n"),
        ("ING", "Umsatzanzeige;Datei erstellt am: 13.11.2026 18:00\n;Letztes Update: aktuell\n\nIBAN;DE89 3704 0044 0532 0130 00\nKontoname;Girokonto\nBank;Beispielbank\nKunde;Erika Muster\nZeitraum;01.11.2026 - 13.11.2026\nSaldo;2.234,22;EUR\n\nBuchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\n" +
            "10.11.2026;10.11.2026;Beispiel GmbH;Gutschrift;Rechnung RE-2026-001 vielen Dank;2.234,22;EUR;2.034,22;EUR\n03.11.2026;03.11.2026;Hausverwaltung Muster;Lastschrift;Miete November;200,00;EUR;-800,00;EUR\n"),
        ("Volksbank/Raiffeisenbank", "Bezeichnung Auftragskonto;IBAN Auftragskonto;BIC Auftragskonto;Bankname Auftragskonto;Buchungstag;Valutadatum;Name Zahlungsbeteiligter;IBAN Zahlungsbeteiligter;BIC (SWIFT-Code) Zahlungsbeteiligter;Buchungstext;Verwendungszweck;Betrag;Waehrung;Saldo nach Buchung;Bemerkung;Kategorie;Steuerrelevant;Glaeubiger ID;Mandatsreferenz\n" +
            "Girokonto;DE89370400440532013000;COBADEFFXXX;Beispielbank;10.11.2026;10.11.2026;Beispiel GmbH;DE02120300000000202051;BYLADEM1001;Gutschrift;Rechnung RE-2026-001 vielen Dank;2034,22;EUR;2234,22;;;;;\n" +
            "Girokonto;DE89370400440532013000;COBADEFFXXX;Beispielbank;03.11.2026;03.11.2026;Hausverwaltung Muster;DE02120300000000202051;BYLADEM1001;Basislastschrift;Miete November;-800,00;EUR;200,00;;;;;\n"),
        ("N26 (englisch, Komma, ISO-Datum)", "\"Date\",\"Payee\",\"Account number\",\"Transaction type\",\"Payment reference\",\"Amount (EUR)\",\"Amount (Foreign Currency)\",\"Type Foreign Currency\",\"Exchange Rate\"\n" +
            "\"2026-11-10\",\"Beispiel GmbH\",\"DE02120300000000202051\",\"Income\",\"Rechnung RE-2026-001 vielen Dank\",\"2034.22\",\"\",\"\",\"\"\n\"2026-11-03\",\"Hausverwaltung Muster\",\"DE02120300000000202051\",\"Direct Debit\",\"Miete November\",\"-800.0\",\"\",\"\",\"\"\n"),
        ("getrennte Spalten Soll/Haben", "Datum;Buchungstext;Empfänger;Soll;Haben\n10.11.2026;Rechnung RE-2026-001 vielen Dank;Beispiel GmbH;;2.034,22\n03.11.2026;Miete November;Hausverwaltung Muster;800,00;\nSumme;;;800,00;2.034,22\n"),
    ];
}
