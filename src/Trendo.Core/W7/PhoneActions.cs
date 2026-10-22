using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record PhoneNotice(string App, string Title);

/// <summary>Dieselben adb-Befehle für Kabel und WLAN. Der Unterschied ist nur die Seriennummer.</summary>
public static class PhoneActions
{
    public static (bool Ok, string Args, string Message) MirrorArgs(string serial, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (!MobileBridge.SafeSerial(serial)) return (false, "", "Seriennummer ungültig.");
        return (true, $"-s {serial} --window-title Handy", "Bildschirm wird auf diesem PC gezeigt");
    }

    public static (bool Ok, string Args, string Message) PackageArgs(string serial, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (!MobileBridge.SafeSerial(serial)) return (false, "", "Seriennummer ungültig.");
        return (true, $"-s {serial} shell pm list packages -3", "Installierte Apps");
    }

    public static (bool Ok, string Args, string Message) NoticeArgs(string serial, bool consent)
    {
        if (!consent) return (false, "", "Erst zustimmen.");
        if (!MobileBridge.SafeSerial(serial)) return (false, "", "Seriennummer ungültig.");
        return (true, $"-s {serial} shell dumpsys notification", "Benachrichtigungen");
    }

    public static IReadOnlyList<string> ParsePackages(string output) =>
        output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("package:", StringComparison.Ordinal))
            .Select(l => l["package:".Length..].Trim())
            .Where(n => Regex.IsMatch(n, @"^[A-Za-z0-9._]{2,120}$"))
            .Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Nur App und Überschrift. Der Mitteilungstext wird nicht übernommen.</summary>
    public static IReadOnlyList<PhoneNotice> ParseNotifications(string dumpsys)
    {
        var list = new List<PhoneNotice>();
        string? pkg = null;
        foreach (var raw in dumpsys.Split('\n'))
        {
            var line = raw.Trim();
            var pm = Regex.Match(line, @"\bpkg=([A-Za-z0-9._]+)");
            if (pm.Success && line.Contains("NotificationRecord", StringComparison.Ordinal)) pkg = pm.Groups[1].Value;
            var tm = Regex.Match(line, @"android\.title=String \((.{1,80})\)\s*$");
            if (!tm.Success || pkg is null) continue;
            var title = tm.Groups[1].Value.Trim();
            if (title.Length > 0) list.Add(new PhoneNotice(pkg, title));
            pkg = null;
        }
        return list.Distinct().Take(12).ToList();
    }

    public static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return line.Length > 180 ? line[..180] : line;
    }
}
