using System.Runtime.InteropServices;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 1 – CORE-05 „3D-Modelle erstellen“: 3D-Dateien umwandeln mit assimp 6.0.5 (BSD-3-Clause, offizielle Windows-Zip mit fester Prüfsumme).
/// assimp liest fremde Dateien in C++ – darum läuft die Umwandlung nie im Trendo-Fenster, sondern in einem eigenen kurzen Prozess
/// (<c>TrendoOS --assimp-convert &lt;dll&gt; &lt;ein&gt; &lt;aus&gt;</c>); stürzt assimp ab, bleibt Trendo offen. Die DLL wird erst zur Laufzeit geladen (kein unsafe-Code).</summary>
public static class AssimpConvert
{
    public const string Switch = "--assimp-convert";
    /// <summary>Eingabeformate (Auswahl der gängigen, die assimp 6 liest).</summary>
    public static readonly string[] Inputs = [".fbx", ".obj", ".dae", ".3ds", ".blend", ".ply", ".stl", ".gltf", ".glb", ".x", ".lwo", ".ase", ".ifc", ".off", ".3mf", ".dxf", ".ms3d", ".b3d"];
    /// <summary>Ausgabe-Endung → assimp-Exportkennung.</summary>
    public static readonly IReadOnlyDictionary<string, string> Outputs = new Dictionary<string, string>
    {
        [".glb"] = "glb2", [".gltf"] = "gltf2", [".obj"] = "obj", [".stl"] = "stlb", [".ply"] = "plyb", [".dae"] = "collada", [".fbx"] = "fbx", [".3mf"] = "3mf",
    };
    // aiProcess_Triangulate | JoinIdenticalVertices | ValidateDataStructure | SortByPType | GenSmoothNormals
    public const uint ImportFlags = 0x8 | 0x2 | 0x400 | 0x8000 | 0x40;
    public const long MaxInputBytes = 1L << 30;

    public static string? Check(string input, string output)
    {
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) return "Die 3D-Datei gibt es nicht.";
        if (!Inputs.Contains(Path.GetExtension(input).ToLowerInvariant())) return $"Format {Path.GetExtension(input)} wird nicht unterstützt.";
        var len = new FileInfo(input).Length;
        if (len == 0) return "Die 3D-Datei ist leer.";
        if (len > MaxInputBytes) return "Die 3D-Datei ist größer als 1 GB.";
        if (!Outputs.ContainsKey(Path.GetExtension(output).ToLowerInvariant())) return "Ziel muss GLB, glTF, OBJ, STL, PLY, DAE, FBX oder 3MF sein.";
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) return "Quelle und Ziel sind dieselbe Datei.";
        return null;
    }

    /// <summary>Argumente für den eigenen Prozess (Trendo selbst mit Schalter).</summary>
    public static IReadOnlyList<string> Args(string dll, string input, string output) => [Switch, dll, input, output];

    /// <summary>Umwandeln über einen eigenen Prozess (<paramref name="trendoExe"/> = Trendo selbst). <paramref name="run"/> für Tests austauschbar.</summary>
    public static (bool Ok, string Message) Convert(string trendoExe, string dll, string input, string output, int timeoutMs = 120_000,
        Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (Check(input, output) is { } why) return (false, why);
        if (string.IsNullOrWhiteSpace(dll) || !File.Exists(dll)) return (false, "assimp ist noch nicht eingerichtet – mit einem Klick (kostenlos, BSD-3).");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(trendoExe, Args(dll, input, output), timeoutMs);
        if (!r.Ok) return (false, r.Exit is < 0 or > 10 ? "assimp ist beim Lesen der Datei abgestürzt – Trendo läuft weiter." : (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]);
        return File.Exists(output) && new FileInfo(output).Length > 0 ? (true, output) : (false, "assimp hat keine Datei geschrieben.");
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ImportFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ExportScene(IntPtr scene, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint pre);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void ReleaseImport(IntPtr scene);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr ErrorString();

    /// <summary>Im eigenen Prozess: DLL laden, Datei einlesen, exportieren. Rückgabe = Exitcode (0 ok, 2 Eingabe, 3 Lesen, 4 Schreiben, 5 DLL), Meldung auf stderr.</summary>
    public static int RunInThisProcess(string dll, string input, string output, TextWriter err)
    {
        if (Check(input, output) is { } why) { err.WriteLine(why); return 2; }
        if (!File.Exists(dll)) { err.WriteLine("assimp-DLL fehlt."); return 5; }
        IntPtr lib;
        try { lib = NativeLibrary.Load(dll); }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException) { err.WriteLine("assimp lässt sich nicht laden: " + e.Message); return 5; }
        try
        {
            T F<T>(string n) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, n));
            var import = F<ImportFile>("aiImportFile"); var export = F<ExportScene>("aiExportScene"); var release = F<ReleaseImport>("aiReleaseImport"); var error = F<ErrorString>("aiGetErrorString");
            var scene = import(Path.GetFullPath(input), ImportFlags);
            if (scene == IntPtr.Zero) { err.WriteLine("assimp kann die Datei nicht lesen: " + Marshal.PtrToStringUTF8(error())); return 3; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                if (export(scene, Outputs[Path.GetExtension(output).ToLowerInvariant()], Path.GetFullPath(output), 0) != 0) { err.WriteLine("assimp kann nicht schreiben: " + Marshal.PtrToStringUTF8(error())); return 4; }
            }
            finally { release(scene); }
            return 0;
        }
        catch (EntryPointNotFoundException e) { err.WriteLine("Unerwartete assimp-DLL: " + e.Message); return 5; }
        finally { NativeLibrary.Free(lib); }
    }
}
