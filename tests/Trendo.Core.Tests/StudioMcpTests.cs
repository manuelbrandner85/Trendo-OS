using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>C5/C6: MCP-Einstellung je Projektordner (aus der vorhandenen Claude-Einstellung übernommen, nie global), sichtbarer Start der Programme, Unity im Batchmode.</summary>
public class StudioMcpTests
{
    static readonly JsonObject User = JsonNode.Parse("""
        {"mcpServers":{"blender":{"command":"python.exe","args":["-I","start.py"]},"Unreal":{"command":"py.exe","args":["bridge.py"]},
         "godot":{"command":"node.exe","args":["index.js"],"env":{"GODOT_PATH":"godot.exe"}},"rea":{"command":"node.exe"}}}
        """)!.AsObject();
    static string Dir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-mcp-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact(DisplayName = "MCP +1: 3D-Studio bekommt genau den Blender-Server im Projektordner")]
    public void Blender_for_3d()
    {
        var d = Dir(); try
        {
            var r = StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: User);
            Assert.Equal(["blender"], r.Added); Assert.Empty(r.Missing);
            var s = JsonNode.Parse(File.ReadAllText(Path.Combine(d, ".mcp.json")))!["mcpServers"]!.AsObject();
            Assert.Equal(["trendo-blender"], s.Select(kv => kv.Key)); Assert.Equal("python.exe", s["trendo-blender"]!["command"]!.ToString());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP +2: Spiel mit Godot bekommt Godot und Blender (samt Umgebung), mit Unreal Unreal und Blender")]
    public void Game_by_engine()
    {
        var d = Dir(); try
        {
            Assert.Equal(["godot", "blender"], StudioMcp.EnsureProjectConfig(d, "CORE-06", "Godot 4", User).Added);
            Assert.Equal("godot.exe", JsonNode.Parse(File.ReadAllText(Path.Combine(d, ".mcp.json")))!["mcpServers"]!["trendo-godot"]!["env"]!["GODOT_PATH"]!.ToString());
            Assert.Equal(["unreal", "blender"], StudioMcp.EnsureProjectConfig(d, "CORE-06", "Unreal Engine 5.8", User).Added);
            Assert.DoesNotContain("trendo-godot", File.ReadAllText(Path.Combine(d, ".mcp.json"))); // eigene alte Einträge ersetzt
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP +3: fremde Einträge in der Projektdatei bleiben erhalten")]
    public void Keeps_foreign()
    {
        var d = Dir(); try
        {
            File.WriteAllText(Path.Combine(d, ".mcp.json"), """{"mcpServers":{"mein-server":{"command":"x.exe"}},"andere":1}""");
            StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: User);
            var t = File.ReadAllText(Path.Combine(d, ".mcp.json")); Assert.Contains("mein-server", t); Assert.Contains("\"andere\"", t); Assert.Contains("trendo-blender", t);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP +4: Startargumente – Unreal mit .uproject, Godot im Editor mit Projektordner, Blender mit neuester .blend")]
    public void Launch_args()
    {
        var d = Dir(); try
        {
            File.WriteAllText(Path.Combine(d, "Spiel.uproject"), "{}"); File.WriteAllText(Path.Combine(d, "project.godot"), ""); File.WriteAllText(Path.Combine(d, "a.blend"), "");
            Assert.Equal([Path.Combine(d, "Spiel.uproject")], StudioMcp.LaunchArgs("unreal", d));
            Assert.Equal(["--editor", "--path", d], StudioMcp.LaunchArgs("godot", d));
            Assert.Equal([Path.Combine(d, "a.blend")], StudioMcp.LaunchArgs("blender", d));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP +5: Unity nur im Batchmode mit Log im Projekt, kein MCP-Eintrag")]
    public void Unity_batch()
    {
        var a = StudioMcp.UnityBatchArgs(@"C:\p", "Trendo.Build.Run");
        Assert.Equal(["-batchmode", "-nographics", "-quit", "-projectPath", @"C:\p", "-executeMethod", "Trendo.Build.Run", "-logFile", Path.Combine(@"C:\p", "Logs", "trendo-unity.log")], a);
        Assert.Equal(["blender"], StudioMcp.Needs("CORE-06", "Unity 6"));
    }

    [Fact(DisplayName = "MCP −1: fehlender Server wird gemeldet statt erfunden")]
    public void Missing_server()
    {
        var d = Dir(); try { var r = StudioMcp.EnsureProjectConfig(d, "CORE-06", "Godot", JsonNode.Parse("""{"mcpServers":{"blender":{"command":"b.exe"}}}""")!.AsObject()); Assert.Equal(["godot"], r.Missing); Assert.DoesNotContain("trendo-godot", File.ReadAllText(Path.Combine(d, ".mcp.json"))); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP −2: ohne Claude-Einstellung entsteht eine leere, gültige Datei")]
    public void No_user_config()
    {
        var d = Dir(); try { var r = StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: new JsonObject()); Assert.Equal(["blender"], r.Missing); Assert.Empty(JsonNode.Parse(File.ReadAllText(Path.Combine(d, ".mcp.json")))!["mcpServers"]!.AsObject()); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP −3: beschädigte Projektdatei wird gesichert (.kaputt), nicht still überschrieben")]
    public void Broken_file_backed_up()
    {
        var d = Dir(); try { File.WriteAllText(Path.Combine(d, ".mcp.json"), "{kaputt"); StudioMcp.EnsureProjectConfig(d, "CORE-05", userConfig: User); Assert.Equal("{kaputt", File.ReadAllText(Path.Combine(d, ".mcp.json.kaputt"))); }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "MCP −4: Studios ohne Programm bekommen keine Server; Einträge ohne Befehl und eigene trendo-Einträge der Person werden nicht übernommen")]
    public void Nothing_for_other_studios()
    {
        Assert.Empty(StudioMcp.Needs("SYS-10"));
        Assert.Null(StudioMcp.FindServer(JsonNode.Parse("""{"mcpServers":{"blender":{"args":[]},"trendo-blender":{"command":"x"}}}""")!.AsObject(), "blender"));
    }

    [Fact(DisplayName = "MCP −5: ohne Projektdatei des Programms keine fremden Startargumente")]
    public void Launch_without_project_files()
    {
        var d = Dir(); try { Assert.Empty(StudioMcp.LaunchArgs("unreal", d)); Assert.Empty(StudioMcp.LaunchArgs("godot", d)); Assert.Empty(StudioMcp.LaunchArgs("blender", d)); Assert.Empty(StudioMcp.LaunchArgs("unity", d)); }
        finally { Directory.Delete(d, true); }
    }
}
