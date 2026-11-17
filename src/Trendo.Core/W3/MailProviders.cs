namespace Trendo.Core;

/// <summary>Wie sich ein Anbieter anmelden lässt.</summary>
public enum MailAuth
{
    /// <summary>Normales Postfach-Passwort.</summary>
    Password,
    /// <summary>Eigenes App-Passwort nötig (meist nur mit Bestätigung in zwei Schritten).</summary>
    AppPassword,
    /// <summary>Nur noch Anmeldung über den Anbieter (OAuth) – kann Trendo nicht, ehrlich blockiert.</summary>
    OAuthOnly,
}

/// <summary>Bekannter E-Mail-Anbieter mit Servereinstellungen. IMAP immer über TLS (Port 993); SMTP über TLS (465) oder STARTTLS (587).</summary>
public sealed record MailProvider(string Name, string[] Domains, string ImapHost, string SmtpHost, int SmtpPort, bool SmtpImplicitTls, MailAuth Auth, string Hint, bool SavesSentItself = false, int ImapPort = 993)
{
    /// <summary>Kurzer Stand für die Tabelle im Assistenten.</summary>
    public string State => Auth switch { MailAuth.Password => "geht mit Passwort", MailAuth.AppPassword => "geht mit App-Passwort", _ => "blockiert (nur Anbieter-Anmeldung)" };
}

/// <summary>T-0689: Tabelle der bekannten Anbieter für den Konto-Assistenten. Die Angaben folgen den öffentlich dokumentierten Einstellungen der Anbieter;
/// gegen keinen dieser Anbieter wurde mit einem echten Konto geprüft (siehe Bericht T-0689).</summary>
public static class MailProviders
{
    public static readonly IReadOnlyList<MailProvider> All =
    [
        new("GMX", ["gmx.de", "gmx.net", "gmx.at", "gmx.ch"], "imap.gmx.net", "mail.gmx.net", 587, false, MailAuth.Password,
            "In GMX unter Einstellungen → POP3/IMAP Abruf den Zugriff einschalten. Mit Bestätigung in zwei Schritten: dort ein anwendungsspezifisches Passwort anlegen."),
        new("WEB.DE", ["web.de"], "imap.web.de", "smtp.web.de", 587, false, MailAuth.Password,
            "In WEB.DE unter Einstellungen → POP3/IMAP Abruf den Zugriff einschalten. Mit Bestätigung in zwei Schritten: dort ein anwendungsspezifisches Passwort anlegen."),
        new("Telekom (t-online.de)", ["t-online.de", "magenta.de"], "secureimap.t-online.de", "securesmtp.t-online.de", 465, true, MailAuth.AppPassword,
            "Im Telekom-Kundencenter ein eigenes „Passwort für E-Mail-Programme“ einrichten und hier eintragen (nicht das Telekom-Login-Passwort)."),
        new("Gmail", ["gmail.com", "googlemail.com"], "imap.gmail.com", "smtp.gmail.com", 465, true, MailAuth.AppPassword,
            "Google-Konto → Sicherheit → Bestätigung in zwei Schritten einschalten, dann unter „App-Passwörter“ ein Passwort für „Mail“ erzeugen und hier eintragen. Ohne App-Passwort lässt Google nur die Anmeldung über Google zu – die kann Trendo nicht.", SavesSentItself: true),
        new("Outlook.com / Hotmail / Live", ["outlook.com", "outlook.de", "hotmail.com", "hotmail.de", "live.com", "live.de", "msn.com"], "outlook.office365.com", "smtp-mail.outlook.com", 587, false, MailAuth.OAuthOnly,
            "Microsoft lässt für diese Postfächer keine Anmeldung mit Passwort oder App-Passwort mehr zu, nur noch die Anmeldung über Microsoft (OAuth). Die kann Trendo noch nicht. Ausweg: in Outlook.com eine Weiterleitung an ein anderes Postfach einrichten oder dort die Webseite nutzen.", SavesSentItself: true),
        new("iCloud Mail", ["icloud.com", "me.com", "mac.com"], "imap.mail.me.com", "smtp.mail.me.com", 587, false, MailAuth.AppPassword,
            "Unter account.apple.com → Anmeldung und Sicherheit ein „anwendungsspezifisches Passwort“ erzeugen und hier eintragen."),
        new("Yahoo Mail", ["yahoo.com", "yahoo.de"], "imap.mail.yahoo.com", "smtp.mail.yahoo.com", 465, true, MailAuth.AppPassword,
            "In den Yahoo-Kontoeinstellungen unter Sicherheit ein „App-Passwort“ erzeugen und hier eintragen."),
        new("Posteo", ["posteo.de", "posteo.net"], "posteo.de", "posteo.de", 465, true, MailAuth.Password, "Normales Posteo-Passwort."),
        new("mailbox.org", ["mailbox.org"], "imap.mailbox.org", "smtp.mailbox.org", 465, true, MailAuth.Password, "Normales Passwort; mit Bestätigung in zwei Schritten ein eigenes Passwort für Mailprogramme anlegen."),
        new("freenet", ["freenet.de"], "mx.freenet.de", "mx.freenet.de", 587, false, MailAuth.Password, "In freenet Mail unter Einstellungen den Zugriff für Mailprogramme einschalten."),
    ];

    /// <summary>Anbieter zur Adresse (nach dem Teil hinter dem @), sonst null.</summary>
    public static MailProvider? For(string email)
    {
        var domain = email.Split('@').LastOrDefault()?.Trim().ToLowerInvariant() ?? "";
        return All.FirstOrDefault(p => p.Domains.Contains(domain));
    }
}
