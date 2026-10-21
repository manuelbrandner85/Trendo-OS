using Trendo.Companion;
using Trendo.Core;

var cfgDir = Environment.GetEnvironmentVariable("TRENDO_COMPANION_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "trendo-companion");
var stateFile = Path.Combine(cfgDir, "zustand.json");
string Arg(string n, string d) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }

switch (args.FirstOrDefault())
{
    case "pair":
        var s = CompanionState.Load(stateFile);
        if (args.Contains("--name")) s.Name = Arg("--name", s.Name);
        var code = s.NewCode(DateTime.Now); s.Save(stateFile);
        Console.WriteLine($"Kopplungscode für „{s.Name}“: {code}  (10 Minuten gültig)");
        Console.WriteLine("Am PC: Trendo → System → Geräteverbund → Adresse dieses Geräts + Code eingeben.");
        return 0;
    case "status":
        var st = CompanionState.Load(stateFile);
        Console.WriteLine($"Name: {st.Name}\nModell: {LinuxSysInfo.Model(Environment.GetEnvironmentVariable("TRENDO_SYSROOT") ?? "/")}\nGekoppelte PCs: {st.Pcs.Count}" + string.Concat(st.Pcs.Select(p => $"\n  – {p.Name} seit {p.At:dd.MM.yyyy HH:mm}")));
        return 0;
    case "forget":
        var f = CompanionState.Load(stateFile); f.Pcs.Clear(); f.Code = null; f.Save(stateFile);
        Console.WriteLine("Alle Kopplungen entfernt."); return 0;
    case "serve":
        var port = int.Parse(Arg("--port", "8787"));
        using (var srv = new CompanionServer(port, stateFile, Environment.GetEnvironmentVariable("TRENDO_SYSROOT") ?? "/", loopbackOnly: args.Contains("--local"), log: m => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {m}")))
        {
            srv.Start();
            Console.WriteLine($"trendo-companion {CompanionServer.Version} läuft auf Port {port} · Fähigkeiten: {string.Join(", ", srv.Caps())}");
            var done = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
            await done.Task;
        }
        return 0;
    case "--version": Console.WriteLine(CompanionServer.Version); return 0;
    default:
        Console.WriteLine("trendo-companion – Begleiter für Raspberry Pi / Linux\n  pair [--name N]   Kopplungscode anzeigen\n  serve [--port 8787] [--local]\n  status | forget | --version");
        return args.Length == 0 ? 0 : 2;
}
