using System.Diagnostics;
using System.Text.Json;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Schreibt das Trendo-OS-Abbild auf einen USB-Stick oder eine externe SSD (Windows). Läuft als eigener Trendo-Prozess mit
/// Administratorrechten (Windows fragt per UAC), weil nur so roh auf ein Laufwerk geschrieben werden darf. Schutz: prüft selbst noch einmal,
/// dass das Ziel weder Start- noch Systemlaufwerk ist, nicht das Windows-Laufwerk enthält und groß genug ist; liest am Ende alles zurück.</summary>
public static class ImageWriter
{
    public const string Arg = "--abbild-schreiben";

    public sealed record Disk(int Number, bool IsBoot, bool IsSystem, long Size, string BusType, string Name, IReadOnlyList<string> Letters);

    /// <summary>Gründe, warum ein Laufwerk nicht beschrieben werden darf (leer = erlaubt). Rein, damit testbar.</summary>
    public static IReadOnlyList<string> Refuse(Disk d, long imageBytes, bool ssd, string systemLetter)
    {
        var r = new List<string>();
        if (d.IsBoot || d.IsSystem) r.Add("Das ist das Start- oder Systemlaufwerk dieses PCs.");
        if (d.Letters.Any(l => string.Equals(l.TrimEnd(':', '\\'), systemLetter.TrimEnd(':', '\\'), StringComparison.OrdinalIgnoreCase))) r.Add("Auf dem Laufwerk liegt Windows.");
        if (!ssd && !string.Equals(d.BusType, "USB", StringComparison.OrdinalIgnoreCase)) r.Add("Das ist kein USB-Laufwerk.");
        if (ssd && d.BusType is not ("USB" or "SD" or "MMC" or "Thunderbolt" or "1394")) r.Add("Das ist kein externes Laufwerk.");
        if (d.Size < Math.Max(imageBytes, (long)(MainWindow.MinStickGb * 1e9 * 0.93))) r.Add("Das Laufwerk ist zu klein.");
        return r;
    }

    static string Ps(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd(); var e = p.StandardError.ReadToEnd(); p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException(e.Trim().Length > 0 ? e.Trim() : "PowerShell-Fehler " + p.ExitCode);
        return o.Trim();
    }

    /// <summary>Laufwerk zum Buchstaben ermitteln (Datenträgernummer, Bus, Größe, alle Buchstaben darauf).</summary>
    public static Disk? DiskOf(string letter)
    {
        var l = letter.TrimEnd(':', '\\');
        if (l.Length != 1 || !char.IsAsciiLetter(l[0])) return null;
        var json = Ps($"$n=(Get-Partition -DriveLetter {l} -ErrorAction Stop).DiskNumber; $d=Get-Disk -Number $n; " +
            "$ls=@(Get-Partition -DiskNumber $n | Where-Object DriveLetter | ForEach-Object { [string]$_.DriveLetter }); " +
            "[pscustomobject]@{Number=$n;IsBoot=$d.IsBoot;IsSystem=$d.IsSystem;Size=$d.Size;Bus=[string]$d.BusType;Name=[string]$d.FriendlyName;Letters=$ls} | ConvertTo-Json -Compress");
        using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
        var letters = r.GetProperty("Letters").ValueKind == JsonValueKind.Array ? r.GetProperty("Letters").EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [r.GetProperty("Letters").GetString() ?? ""];
        return new Disk(r.GetProperty("Number").GetInt32(), r.GetProperty("IsBoot").GetBoolean(), r.GetProperty("IsSystem").GetBoolean(), r.GetProperty("Size").GetInt64(),
            r.GetProperty("Bus").GetString() ?? "", r.GetProperty("Name").GetString() ?? "", letters);
    }

    /// <summary>Einstieg im Prozess mit Administratorrechten: <c>--abbild-schreiben &lt;abbild&gt; &lt;laufwerk&gt; &lt;sha256&gt; &lt;bytes&gt; &lt;stick|ssd&gt; &lt;ergebnis.json&gt;</c>.</summary>
    public static int RunElevated(string[] a)
    {
        var i = Array.IndexOf(a, Arg);
        if (i < 0 || a.Length < i + 7) return 2;
        string image = a[i + 1], letter = a[i + 2], sha = a[i + 3], result = a[i + 6]; var bytes = long.Parse(a[i + 4], System.Globalization.CultureInfo.InvariantCulture); var ssd = a[i + 5] == "ssd";
        void Done(bool ok, string text) => File.WriteAllText(result, JsonSerializer.Serialize(new { ok, text }));
        try
        {
            if (!OperatingSystem.IsWindows()) { Done(false, "Nur unter Windows."); return 3; }
            var sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var d = DiskOf(letter);
            if (d is null) { Done(false, "Laufwerk nicht gefunden."); return 4; }
            if (Refuse(d, bytes, ssd, sys) is { Count: > 0 } no) { Done(false, "Nicht beschrieben: " + string.Join(" ", no)); return 5; }
            Ps($"Clear-Disk -Number {d.Number} -RemoveData -RemoveOEM -Confirm:$false"); // gibt das Laufwerk frei (keine eingebundenen Bereiche mehr)
            string? err;
            using (var src = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var dst = new FileStream($@"\\.\PhysicalDrive{d.Number}", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 0, FileOptions.WriteThrough))
                err = TrendoImage.WriteAndVerifyAsync(src, dst, bytes, sha).GetAwaiter().GetResult();
            try { Ps($"Update-Disk -Number {d.Number}"); } catch (IOException) { }
            Done(err is null, err ?? $"Trendo OS steht auf „{d.Name}“ und ist geprüft.");
            return err is null ? 0 : 6;
        }
        catch (Exception e) { Done(false, "Fehler beim Schreiben: " + e.Message); return 7; }
    }

    /// <summary>Aus der normalen Trendo-Oberfläche: Schreiber mit Administratorrechten starten (Windows fragt nach) und auf das Ergebnis warten.</summary>
    public static async Task<(bool Ok, string Text)> StartElevatedAsync(string image, string letter, TrendoImage.Manifest m, bool ssd, string workDir)
    {
        var result = Path.Combine(workDir, "schreiben-ergebnis.json"); if (File.Exists(result)) File.Delete(result);
        var psi = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Programmpfad unbekannt")) { UseShellExecute = true, Verb = "runas" };
        foreach (var x in new[] { Arg, image, letter, m.Sha256, m.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), ssd ? "ssd" : "stick", result }) psi.ArgumentList.Add(x);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return (false, "Der Schreiber konnte nicht starten.");
            await p.WaitForExitAsync();
        }
        catch (System.ComponentModel.Win32Exception) { return (false, "Abgebrochen – ohne Zustimmung von Windows darf Trendo nicht auf das Laufwerk schreiben."); }
        if (!File.Exists(result)) return (false, "Der Schreiber hat kein Ergebnis geliefert.");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(result));
        return (doc.RootElement.GetProperty("ok").GetBoolean(), doc.RootElement.GetProperty("text").GetString() ?? "");
    }
}
