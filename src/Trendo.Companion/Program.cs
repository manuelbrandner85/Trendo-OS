using Trendo.Companion;
using Trendo.Core;

var cfgDir = Environment.GetEnvironmentVariable("TRENDO_COMPANION_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "trendo-companion");
var stateFile = Path.Combine(cfgDir, "zustand.json");
string Arg(string n, string d) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; }

int? Opt(string n, int min, int max)
{
    var i = Array.IndexOf(args, n);
    if (i < 0) return null;
    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var v) || v < min || v > max)
        throw new ArgumentException($"{n} braucht eine ganze Zahl von {min} bis {max}.");
    return v;
}

bool ApplyLimits(CompanionState s)
{
    var dirty = false;
    if (Opt("--code-minuten", 1, 60) is int m) { s.CodeValidMinutes = m; dirty = true; }
    if (Opt("--token-tage", 1, 365) is int d) { s.TokenValidHours = d * 24; dirty = true; }
    if (Opt("--token-stunden", 1, 24 * 365) is int h) { s.TokenValidHours = h; dirty = true; }
    return dirty;
}

try
{
    switch (args.FirstOrDefault())
    {
        case "pair":
            var s = CompanionState.Load(stateFile);
            if (args.Contains("--name")) s.Name = Arg("--name", s.Name);
            ApplyLimits(s);
            var code = s.NewCode(DateTime.Now); s.Save(stateFile);
            Console.WriteLine($"Kopplungscode für „{s.Name}“: {code}  ({s.CodeMinutes} Minuten gültig)");
            Console.WriteLine("Am PC: Trendo → System → Geräteverbund → Adresse dieses Geräts + Code eingeben.");
            Console.WriteLine("Der Begleiter muss dafür im Heimnetz hören: „trendo-companion netz an“ oder „serve --lan“.");
            return 0;
        case "status":
            var st = CompanionState.Load(stateFile);
            Console.WriteLine($"Name: {st.Name}\nModell: {LinuxSysInfo.Model(Environment.GetEnvironmentVariable("TRENDO_SYSROOT") ?? "/")}\nGekoppelte PCs: {st.Pcs.Count}" + string.Concat(st.Pcs.Select(p => $"\n  – {p.Name} seit {p.At:dd.MM.yyyy HH:mm} · gültig bis {st.TokenExpiry(p):dd.MM.yyyy HH:mm}")));
            Console.WriteLine(st.ListenLan
                ? "Netz: Heimnetz ist freigegeben (Einstellung „netz an“). Start mit --local bleibt auf diesem Rechner."
                : "Netz: nur dieser Rechner (127.0.0.1). Freigabe fürs Heimnetz: trendo-companion netz an");
            Console.WriteLine($"Code gültig {st.CodeMinutes} Minuten · Token gültig {st.TokenLifetime.TotalDays:0} Tage.");
            var locked = st.PairGuards.Where(g => g.LockedUntil is DateTime u && DateTime.Now < u).ToList();
            if (locked.Count > 0) Console.WriteLine("Sperren: " + string.Join(", ", locked.Select(g => $"{g.Ip} bis {g.LockedUntil:dd.MM.yyyy HH:mm} (aufheben: forget)")));
            return 0;
        case "forget":
            var f = CompanionState.Load(stateFile); f.Pcs.Clear(); f.Code = null; f.FailedTries = 0; f.PairGuards.Clear(); f.Save(stateFile);
            Console.WriteLine("Alle Kopplungen und vorübergehenden Sperren entfernt."); return 0;
        case "netz":
            var mode = args.ElementAtOrDefault(1);
            var nz = CompanionState.Load(stateFile);
            if (mode is "an")
            {
                nz.ListenLan = true; nz.Save(stateFile);
                Console.WriteLine("Heimnetz freigegeben. Gilt beim nächsten „serve“ ohne --local.");
                Console.WriteLine("Nur im eigenen Netz nutzen. Den Port nicht ins Internet weiterleiten.");
                return 0;
            }
            if (mode is "aus")
            {
                nz.ListenLan = false; nz.Save(stateFile);
                Console.WriteLine("Heimnetz aus. Der Begleiter hört nur noch auf diesem Rechner (127.0.0.1).");
                return 0;
            }
            Console.WriteLine(nz.ListenLan ? "Heimnetz: an" : "Heimnetz: aus (nur 127.0.0.1)");
            return mode is null ? 0 : 2;
        case "serve":
            if (!int.TryParse(Arg("--port", "8787"), out var port) || port is < 1 or > 65535)
            { Console.WriteLine("--port braucht eine Zahl von 1 bis 65535."); return 2; }
            var cfg = CompanionState.Load(stateFile);
            if (ApplyLimits(cfg)) cfg.Save(stateFile);
            var listenLan = CompanionServer.ShouldListenOnLan(args.Contains("--lan"), args.Contains("--local"), cfg.ListenLan);
            using (var srv = new CompanionServer(port, stateFile, Environment.GetEnvironmentVariable("TRENDO_SYSROOT") ?? "/", loopbackOnly: !listenLan, log: m => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {m}")))
            {
                srv.Start();
                if (srv.LoopbackOnly)
                    Console.WriteLine($"trendo-companion {CompanionServer.Version} hört nur auf {srv.ListenPrefix} – nicht aus dem Netz erreichbar. Heimnetz bewusst öffnen: --lan oder „netz an“.");
                else
                    Console.WriteLine($"trendo-companion {CompanionServer.Version} hört auf {srv.ListenPrefix} (alle Schnittstellen, Port {port}). Nur im eigenen Heimnetz nutzen, nicht ins Internet weiterleiten.");
                Console.WriteLine($"Fähigkeiten: {string.Join(", ", srv.Caps())} · Token gültig {cfg.TokenLifetime.TotalDays:0} Tage · Code {cfg.CodeMinutes} Minuten.");
                var done = new TaskCompletionSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
                AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
                await done.Task;
            }
            return 0;
        case "--version": Console.WriteLine(CompanionServer.Version); return 0;
        default:
            Console.WriteLine("""
trendo-companion – Begleiter für Raspberry Pi / Linux
  pair [--name N] [--code-minuten 10]
      Kopplungscode anzeigen. Standard: 10 Minuten gültig.
  serve [--port 8787] [--lan] [--local] [--token-tage 30] [--token-stunden N] [--code-minuten 10]
      Standard: nur dieser Rechner (127.0.0.1), nicht das Netz.
      --lan öffnet das Heimnetz bewusst (alle Schnittstellen). Nicht ins Internet weiterleiten.
      --local erzwingt 127.0.0.1 und gewinnt gegen --lan und gegen „netz an“.
      Zugriffstokens gelten standardmäßig 30 Tage (--token-tage oder --token-stunden). Danach neu koppeln.
      Stehen beide Token-Schalter, gilt --token-stunden.
      Kopplung: höchstens 5 Versuche pro Minute und Client. Nach 5 falschen Codes 15 Minuten Sperre nur für diese Adresse.
  netz an|aus
      Heimnetz-Freigabe speichern. „an“ gilt beim nächsten serve ohne --local.
  status | forget | --version
      forget entfernt Kopplungen und vorübergehende Sperren.
""");
            return args.Length == 0 ? 0 : 2;
    }
}
catch (ArgumentException e) { Console.WriteLine(e.Message); return 2; }
