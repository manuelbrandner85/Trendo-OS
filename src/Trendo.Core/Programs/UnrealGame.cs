using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>3D-Spiele in Unreal Engine 5 ohne Fachwissen (Paket 01 S1-01/S1-04/S1-05, Nutzerwunsch 07.11.2026 „Open World, Programme automatisch“):
/// Trendo schreibt ein C++-Spiel (Figur mit Kamera, Steuerung über Enhanced Input, Gegner und Bosse, Münzen, Gegenstände, sprechende Figuren, Fahrzeuge,
/// Regeln und Aufgaben, Spielstand, Menü und Anzeige) und baut die Welt aus dem Trendo-Level – als 3D-Level oder verteilt in der Open World von Unreal.
/// Kompilieren (Build.bat), Unreal-Tests („Automation RunTests Trendo“) und ein Startlauf (-trendocheck, optional -trendoshot) laufen über die offiziellen Kommandozeilen.</summary>
public static class UnrealGame
{
    public static readonly (string Id, string Label, string Map)[] Worlds =
    [
        ("level", "3D-Level aus dem Trendo-Raster", "/Engine/Maps/Templates/Template_Default"),
        ("openworld", "Open World (Gelände, Himmel, World Partition)", "/Engine/Maps/Templates/OpenWorld"),
    ];

    /// <summary>Welt des Spiels: aus dem Dokument („world“), sonst Open World für AAA+ und Level für die anderen Größen.</summary>
    public static string WorldOf(JsonObject d) => d["world"]?.ToString() is "level" or "openworld" ? d["world"]!.ToString() : d["scale"]?.ToString() == "aaa" ? "openworld" : "level";

    /// <summary>Modulname aus dem Projektordner (nur Buchstaben und Ziffern, wie das .uproject).</summary>
    public static string ModuleName(string projectDir) => Path.GetFileNameWithoutExtension(Directory.GetFiles(projectDir, "*.uproject").Single());

    /// <summary>C++-Spiel und Welt ins Unreal-Projekt schreiben (überschreibt nur Trendo-eigene Dateien).</summary>
    public static void Write(string projectDir, string level, string world)
    {
        var name = ModuleName(projectDir);
        var up = Path.Combine(projectDir, name + ".uproject");
        var o = JsonNode.Parse(File.ReadAllText(up))!.AsObject();
        o["Modules"] = new JsonArray(new JsonObject { ["Name"] = name, ["Type"] = "Runtime", ["LoadingPhase"] = "Default" });
        File.WriteAllText(up, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        void Put(string rel, string text) { var f = Path.Combine(projectDir, rel.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, text.Replace("\r\n", "\n")); }
        string Target(string type, string suffix) => $"using UnrealBuildTool;\n\npublic class {name}{suffix}Target : TargetRules\n{{\n\tpublic {name}{suffix}Target(TargetInfo Target) : base(Target)\n\t{{\n\t\tType = TargetType.{type};\n\t\tDefaultBuildSettings = BuildSettingsVersion.Latest;\n\t\tIncludeOrderVersion = EngineIncludeOrderVersion.Latest;\n\t\tExtraModuleNames.Add(\"{name}\");\n\t}}\n}}\n";
        Put($"Source/{name}.Target.cs", Target("Game", ""));
        Put($"Source/{name}Editor.Target.cs", Target("Editor", "Editor"));
        Put($"Source/{name}/{name}.Build.cs", $"using UnrealBuildTool;\n\npublic class {name} : ModuleRules\n{{\n\tpublic {name}(ReadOnlyTargetRules Target) : base(Target)\n\t{{\n\t\tPCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;\n\t\tPublicDependencyModuleNames.AddRange(new[] {{ \"Core\", \"CoreUObject\", \"Engine\", \"InputCore\", \"EnhancedInput\", \"Json\" }});\n\t}}\n}}\n");
        Put($"Source/{name}/{name}.cpp", $"// Von Trendo erzeugt.\n#include \"Modules/ModuleManager.h\"\n\nIMPLEMENT_PRIMARY_GAME_MODULE(FDefaultGameModuleImpl, {name}, \"{name}\");\n");
        foreach (var (file, text) in Sources) Put($"Source/{name}/{file}", text + "\n");
        var map = Worlds.FirstOrDefault(w => w.Id == world).Map ?? Worlds[0].Map;
        var ini = Path.Combine(projectDir, "Config", "DefaultEngine.ini");
        var old = File.Exists(ini) ? File.ReadAllText(ini) : "";
        var keep = string.Join("\n", old.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("GameDefaultMap=") && !l.StartsWith("EditorStartupMap=") && !l.StartsWith("GlobalDefaultGameMode=") && l != "[/Script/EngineSettings.GameMapsSettings]"));
        Put("Config/DefaultEngine.ini", $"[/Script/EngineSettings.GameMapsSettings]\nGameDefaultMap={map}\nEditorStartupMap={map}\nGlobalDefaultGameMode=/Script/{name}.TrendoGameMode\n\n" + keep.Trim() + "\n");
        Put("Config/DefaultInput.ini", "[/Script/Engine.InputSettings]\nDefaultPlayerInputClass=/Script/EnhancedInput.EnhancedPlayerInput\nDefaultInputComponentClass=/Script/EnhancedInput.EnhancedInputComponent\n");
        var game = Path.Combine(projectDir, "Config", "DefaultGame.ini");
        var g = File.Exists(game) ? File.ReadAllText(game) : "";
        if (!g.Contains("DirectoriesToAlwaysStageAsNonUFS")) File.WriteAllText(game, g.TrimEnd() + "\n\n[/Script/UnrealEd.ProjectPackagingSettings]\n+DirectoriesToAlwaysStageAsNonUFS=(Path=\"Trendo\")\n");
        Put("Content/Trendo/level.json", level);
    }

    /// <summary>Level-Beschreibung für Unreal: gleiche Daten wie für Unity/Godot plus Welt-Art.</summary>
    public static string LevelJson(string name, GameStudio.GameSettings s, string world)
    {
        var o = JsonNode.Parse(EngineTargets.LevelJson(name, s))!.AsObject();
        o["world"] = world;
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    // ───────────── Kommandozeilen (offiziell) ─────────────
    public static string BuildBat(string engineDir) => Path.Combine(engineDir, "Engine", "Build", "BatchFiles", "Build.bat");
    public static string EditorCmd(string engineDir) => Path.Combine(engineDir, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");
    public static string Editor(string engineDir) => Path.Combine(engineDir, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
    public static List<string> CompileArgs(string uproject) => [Path.GetFileNameWithoutExtension(uproject) + "Editor", "Win64", "Development", "-Project=" + uproject, "-WaitMutex", "-NoHotReload"];
    public static List<string> TestArgs(string uproject, string log) => [uproject, "-ExecCmds=Automation RunTests Trendo", "-TestExit=Automation Test Queue Empty", "-unattended", "-nullrhi", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    public static List<string> CheckArgs(string uproject, string log, bool screenshot) =>
        screenshot ? [uproject, "-game", "-trendocheck", "-trendoshot", "-windowed", "-ResX=1600", "-ResY=900", "-unattended", "-nosplash", "-log", "-abslog=" + log]
                   : [uproject, "-game", "-trendocheck", "-nullrhi", "-unattended", "-nosplash", "-nopause", "-log", "-abslog=" + log];
    public static List<string> PlayArgs(string uproject) => [uproject, "-game", "-windowed", "-ResX=1600", "-ResY=900"];

    /// <summary>Ergebnis des Kompilierens aus dem Build-Protokoll.</summary>
    public static bool CompileOk(string log) => log.Contains("Result: Succeeded");

    /// <summary>Unreal-Testergebnisse (Name → bestanden) aus dem Protokoll.</summary>
    public static List<(string Test, bool Ok)> ParseTests(string log) =>
        [.. System.Text.RegularExpressions.Regex.Matches(log, @"Test Completed\. Result=\{(\w+)\} Name=\{[^}]*\} Path=\{(Trendo\.[^}]+)\}").Select(m => (m.Groups[2].Value, m.Groups[1].Value == "Success"))];

    /// <summary>Startlauf: „TRENDO-OK …“ bzw. „TRENDO-FEHLER …“ und die Kennzahlen.</summary>
    public static (bool Ok, string Text) ParseCheck(string log)
    {
        var line = log.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Contains("TRENDO-OK") || l.Contains("TRENDO-FEHLER")) ?? "";
        var info = log.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Contains("TRENDO-SPIEL")) ?? "";
        var details = info.Length > 0 ? info[(info.IndexOf("TRENDO-SPIEL") + 12)..].Trim() : "";
        return line.Contains("TRENDO-OK") ? (true, details) : (false, line.Length > 0 ? line[(line.IndexOf("TRENDO-FEHLER") + 13)..].Trim() : "Kein Startbericht – das Spiel lief nicht bis zum Ende.");
    }

    static readonly Dictionary<string, string> Sources = new()
    {
        ["TrendoTypes.h"] = """
// Von Trendo erzeugt: Spieldaten und Regeln (ohne Welt testbar).
#pragma once

#include "CoreMinimal.h"

/** Regel bzw. Aufgabe: Wenn When ≥ N, dann Act (einmal). Mit Titel = Aufgabe mit Fortschritt. */
struct FTrendoRule { FString When; int32 N = 0; FString Act; FString Title; };

/** Ein Level als Raster wie im Trendo-Editor. */
struct FTrendoLevelRows { FString Name; TArray<FString> Rows; };

/** Das ganze Spiel aus Trendo (level.json). */
struct FTrendoData
{
	FString Name = TEXT("Spiel"); FString World = TEXT("level");
	int32 Speed = 5; int32 Lives = 3; FString Weapon; bool bMenu = true;
	TArray<FString> Abilities; TArray<FTrendoLevelRows> Levels; TArray<FTrendoRule> Rules;
	TMap<FString, FString> Texts; TMap<FString, float> Props;

	bool Has(const FString& Ability) const { return Abilities.Contains(Ability); }
	FString Key(int32 Level, int32 X, int32 Y, const FString& K) const;
	FString Text(int32 Level, int32 X, int32 Y, const FString& K, const FString& Default) const;
	float Prop(int32 Level, int32 X, int32 Y, const FString& K, float Default) const;
	/** level.json lesen; false mit Grund bei kaputter Datei. */
	static bool Parse(const FString& Json, FTrendoData& Out, FString& Error);
};

/** Spielstand, Fortschritt und Regeln. */
struct FTrendoState
{
	int32 Level = 0, Score = 0, Lives = 3, Coins = 0, Defeated = 0, Items = 0; float Seconds = 0; float Boost = 0;
	bool bWon = false, bOver = false, bDoorsOpen = false;
	TArray<FString> Inventory; TArray<bool> Fired; TSet<FString> Given; FString Note;

	void Reset(const FTrendoData& D) { *this = FTrendoState(); Lives = D.Lives; Fired.Init(false, D.Rules.Num()); }
	double RuleValue(const FTrendoRule& R) const;
	/** Wirkung (Regel oder sprechende Figur): leben, tueren, schneller, punkte, sieg. */
	void Apply(const FString& What);
	/** Regeln prüfen; gibt die Zahl der neu erfüllten zurück. */
	int32 CheckRules(const FTrendoData& D);
	/** Aufgaben mit Fortschritt, eine je Zeile. */
	FString Quests(const FTrendoData& D) const;
	/** Sprechende Figur: nimmt den gewünschten Gegenstand und belohnt; sonst ihr Text. */
	void TalkTo(const FTrendoData& D, int32 X, int32 Y);
};
""",
        ["TrendoTypes.cpp"] = """
// Von Trendo erzeugt: Spieldaten und Regeln.
#include "TrendoTypes.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

FString FTrendoData::Key(int32 Level, int32 X, int32 Y, const FString& K) const
{
	return (Level == 0 ? FString() : FString::Printf(TEXT("%d:"), Level + 1)) + FString::Printf(TEXT("%d,%d:"), X, Y) + K;
}

FString FTrendoData::Text(int32 Level, int32 X, int32 Y, const FString& K, const FString& Default) const
{
	const FString* V = Texts.Find(Key(Level, X, Y, K));
	return V && !V->IsEmpty() ? *V : Default;
}

float FTrendoData::Prop(int32 Level, int32 X, int32 Y, const FString& K, float Default) const
{
	const float* V = Props.Find(Key(Level, X, Y, K));
	return V ? *V : Default;
}

bool FTrendoData::Parse(const FString& Json, FTrendoData& Out, FString& Error)
{
	TSharedPtr<FJsonObject> O;
	if (!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json), O) || !O.IsValid()) { Error = TEXT("level.json ist beschädigt."); return false; }
	Out = FTrendoData();
	O->TryGetStringField(TEXT("name"), Out.Name);
	O->TryGetStringField(TEXT("world"), Out.World);
	O->TryGetNumberField(TEXT("speed"), Out.Speed);
	O->TryGetNumberField(TEXT("lives"), Out.Lives);
	O->TryGetStringField(TEXT("weapon"), Out.Weapon);
	O->TryGetBoolField(TEXT("menu"), Out.bMenu);
	const TArray<TSharedPtr<FJsonValue>>* A;
	if (O->TryGetArrayField(TEXT("abilities"), A)) for (const auto& V : *A) Out.Abilities.Add(V->AsString());
	if (O->TryGetArrayField(TEXT("levels"), A))
		for (const auto& V : *A)
		{
			FTrendoLevelRows L; const TSharedPtr<FJsonObject>& LO = V->AsObject(); if (!LO.IsValid()) continue;
			LO->TryGetStringField(TEXT("name"), L.Name);
			const TArray<TSharedPtr<FJsonValue>>* R; if (LO->TryGetArrayField(TEXT("rows"), R)) for (const auto& RV : *R) L.Rows.Add(RV->AsString());
			if (L.Rows.Num() > 0) Out.Levels.Add(L);
		}
	if (O->TryGetArrayField(TEXT("rules"), A))
		for (const auto& V : *A)
		{
			const TSharedPtr<FJsonObject>& RO = V->AsObject(); if (!RO.IsValid()) continue;
			FTrendoRule R; RO->TryGetStringField(TEXT("when"), R.When); RO->TryGetNumberField(TEXT("n"), R.N); RO->TryGetStringField(TEXT("act"), R.Act); RO->TryGetStringField(TEXT("title"), R.Title);
			Out.Rules.Add(R);
		}
	if (O->TryGetArrayField(TEXT("texts"), A))
		for (const auto& V : *A) { const TSharedPtr<FJsonObject>& T = V->AsObject(); if (T.IsValid()) Out.Texts.Add(T->GetStringField(TEXT("key")), T->GetStringField(TEXT("value"))); }
	if (O->TryGetArrayField(TEXT("props"), A))
		for (const auto& V : *A) { const TSharedPtr<FJsonObject>& P = V->AsObject(); if (P.IsValid()) Out.Props.Add(P->GetStringField(TEXT("key")), (float)P->GetNumberField(TEXT("value"))); }
	if (Out.Levels.Num() == 0) { Error = TEXT("level.json enthält kein Level."); return false; }
	return true;
}

double FTrendoState::RuleValue(const FTrendoRule& R) const
{
	if (R.When == TEXT("muenzen")) return Coins;
	if (R.When == TEXT("punkte")) return Score;
	if (R.When == TEXT("gegner")) return Defeated;
	if (R.When == TEXT("sekunden")) return Seconds;
	if (R.When == TEXT("gegenstaende")) return Items;
	return 0;
}

void FTrendoState::Apply(const FString& What)
{
	if (What == TEXT("leben")) Lives++;
	else if (What == TEXT("tueren")) bDoorsOpen = true;
	else if (What == TEXT("schneller")) Boost = 2;
	else if (What == TEXT("punkte")) Score += 10;
	else if (What == TEXT("sieg")) bWon = true;
}

int32 FTrendoState::CheckRules(const FTrendoData& D)
{
	int32 New = 0;
	if (Fired.Num() != D.Rules.Num()) Fired.Init(false, D.Rules.Num());
	for (int32 i = 0; i < D.Rules.Num(); i++)
	{
		if (Fired[i] || RuleValue(D.Rules[i]) < D.Rules[i].N) continue;
		Fired[i] = true; Apply(D.Rules[i].Act); New++;
		if (!D.Rules[i].Title.IsEmpty()) Note = TEXT("✓ ") + D.Rules[i].Title;
	}
	return New;
}

FString FTrendoState::Quests(const FTrendoData& D) const
{
	FString S;
	for (int32 i = 0; i < D.Rules.Num(); i++)
	{
		const FTrendoRule& R = D.Rules[i]; if (R.Title.IsEmpty()) continue;
		S += Fired.IsValidIndex(i) && Fired[i] ? TEXT("✓ ") + R.Title : FString::Printf(TEXT("%s: %d/%d"), *R.Title, FMath::Min((int32)RuleValue(R), R.N), R.N);
		S += TEXT("\n");
	}
	return S;
}

void FTrendoState::TalkTo(const FTrendoData& D, int32 X, int32 Y)
{
	const FString K = FString::Printf(TEXT("%d:%d,%d"), Level, X, Y);
	const FString Need = D.Text(Level, X, Y, TEXT("need"), FString());
	if (!Need.IsEmpty() && !Given.Contains(K) && Inventory.RemoveSingle(Need) > 0)
	{
		Given.Add(K); Apply(D.Text(Level, X, Y, TEXT("give"), TEXT("punkte"))); Note = D.Text(Level, X, Y, TEXT("thanks"), TEXT("Danke!"));
	}
	else Note = Given.Contains(K) ? D.Text(Level, X, Y, TEXT("thanks"), TEXT("Danke!")) : D.Text(Level, X, Y, TEXT("say"), TEXT("Hallo!"));
}
""",
        ["TrendoActors.h"] = """
// Von Trendo erzeugt: Spielobjekte (Kacheln, Gegner, Geschoss).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "TrendoActors.generated.h"

class UStaticMeshComponent;

/** Ein Objekt aus dem Trendo-Raster: Boden, Münze, Stacheln, Ziel, Feder, Speicherpunkt, Schlüssel, Tür, Gegenstand, Figur, Fahrzeug. */
UCLASS()
class ATrendoThing : public AActor
{
	GENERATED_BODY()
public:
	ATrendoThing();
	/** Kachel-Zeichen wie in Trendo (#, o, ^, Z, F, C, K, D, G, N, V). */
	UPROPERTY(VisibleAnywhere) FString Tile;
	UPROPERTY(VisibleAnywhere) int32 TileX = 0;
	UPROPERTY(VisibleAnywhere) int32 TileY = 0;
	UPROPERTY(VisibleAnywhere) FString Kind;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Mesh;
	void Setup(TCHAR InTile, int32 X, int32 Y, const FLinearColor& Color, float Size, bool bSolid);
};

/** Gegner läuft hin und her; Trefferpunkte wie in Trendo, ab 2 ein Boss. */
UCLASS()
class ATrendoEnemy : public AActor
{
	GENERATED_BODY()
public:
	ATrendoEnemy();
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Mesh;
	UPROPERTY(VisibleAnywhere) int32 Hp = 1;
	UPROPERTY(VisibleAnywhere) int32 MaxHp = 1;
	/** Im Menü still (von GameMode gesetzt). */
	bool bFrozen = false;
	virtual void Tick(float DeltaSeconds) override;
	virtual void BeginPlay() override;
private:
	FVector Start; float T = 0;
};

/** Geschoss der Waffe „Werfen“. */
UCLASS()
class ATrendoShot : public AActor
{
	GENERATED_BODY()
public:
	ATrendoShot();
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Mesh;
	FVector Dir = FVector::ForwardVector; float Life = 1.5f;
	virtual void Tick(float DeltaSeconds) override;
};

/** Farbe für eine Form setzen (Engine-Grundmaterial mit Farbparameter). */
void TrendoPaint(UStaticMeshComponent* Mesh, const FLinearColor& Color);
""",
        ["TrendoActors.cpp"] = """
// Von Trendo erzeugt: Spielobjekte.
#include "TrendoActors.h"
#include "TrendoGameMode.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Kismet/GameplayStatics.h"
#include "UObject/ConstructorHelpers.h"

static UStaticMesh* TrendoShape(const TCHAR* Path) { return LoadObject<UStaticMesh>(nullptr, Path); }

void TrendoPaint(UStaticMeshComponent* Mesh, const FLinearColor& Color)
{
	if (!Mesh) return;
	if (UMaterialInterface* Base = LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial")))
	{
		UMaterialInstanceDynamic* M = UMaterialInstanceDynamic::Create(Base, Mesh);
		M->SetVectorParameterValue(TEXT("Color"), Color);
		Mesh->SetMaterial(0, M);
	}
}

ATrendoThing::ATrendoThing()
{
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	RootComponent = Mesh;
}

void ATrendoThing::Setup(TCHAR InTile, int32 X, int32 Y, const FLinearColor& Color, float Size, bool bSolid)
{
	Tile = FString::Chr(InTile); TileX = X; TileY = Y;
	const bool Round = InTile == 'o' || InTile == 'G' || InTile == 'K';
	Mesh->SetStaticMesh(TrendoShape(Round ? TEXT("/Engine/BasicShapes/Sphere.Sphere") : InTile == 'N' ? TEXT("/Engine/BasicShapes/Cylinder.Cylinder") : TEXT("/Engine/BasicShapes/Cube.Cube")));
	Mesh->SetWorldScale3D(FVector(Size));
	Mesh->SetCollisionProfileName(bSolid ? TEXT("BlockAll") : TEXT("OverlapAllDynamic"));
	Mesh->SetGenerateOverlapEvents(!bSolid);
	TrendoPaint(Mesh, Color);
}

ATrendoEnemy::ATrendoEnemy()
{
	PrimaryActorTick.bCanEverTick = true;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	RootComponent = Mesh;
	Mesh->SetStaticMesh(TrendoShape(TEXT("/Engine/BasicShapes/Cube.Cube")));
	Mesh->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Mesh->SetGenerateOverlapEvents(true);
}

void ATrendoEnemy::BeginPlay() { Super::BeginPlay(); Start = GetActorLocation(); TrendoPaint(Mesh, MaxHp > 1 ? FLinearColor(0.6f, 0.05f, 0.3f) : FLinearColor(0.8f, 0.2f, 0.6f)); }

void ATrendoEnemy::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bFrozen) return;
	T += DeltaSeconds;
	SetActorLocation(Start + FVector(FMath::Sin(T * 1.5f) * 150.f, 0, 0));
}

ATrendoShot::ATrendoShot()
{
	PrimaryActorTick.bCanEverTick = true;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Form"));
	RootComponent = Mesh;
	Mesh->SetStaticMesh(TrendoShape(TEXT("/Engine/BasicShapes/Sphere.Sphere")));
	Mesh->SetWorldScale3D(FVector(0.2f));
	Mesh->SetCollisionProfileName(TEXT("NoCollision"));
}

void ATrendoShot::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Life -= DeltaSeconds;
	const FVector From = GetActorLocation(), To = From + Dir * 1500.f * DeltaSeconds;
	FHitResult Hit; FCollisionQueryParams Q; Q.AddIgnoredActor(this);
	if (APawn* P = UGameplayStatics::GetPlayerPawn(this, 0)) Q.AddIgnoredActor(P);
	ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	if (GM)
		for (ATrendoEnemy* E : GM->Enemies)
			if (IsValid(E) && E->Hp > 0 && FMath::PointDistToSegment(E->GetActorLocation(), From, To) < 70.f) { GM->Damage(E); Destroy(); return; }
	if (GetWorld()->LineTraceSingleByChannel(Hit, From, To, ECC_Visibility, Q) || Life <= 0) { Destroy(); return; }
	SetActorLocation(To);
}
""",
        ["TrendoCharacter.h"] = """
// Von Trendo erzeugt: Spielfigur mit Kamera (dritte Person).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "InputActionValue.h"
#include "TrendoCharacter.generated.h"

class USpringArmComponent; class UCameraComponent; class UStaticMeshComponent; class UInputAction; class UInputMappingContext;

/** Spielfigur: WASD/Pfeile laufen, Leertaste springt (Doppelsprung), Umschalt sprintet, X/J/linke Maustaste greift an, E steigt aus, Esc pausiert. Controller ebenso. */
UCLASS()
class ATrendoCharacter : public ACharacter
{
	GENERATED_BODY()
public:
	ATrendoCharacter();
	UPROPERTY(VisibleAnywhere) USpringArmComponent* Arm;
	UPROPERTY(VisibleAnywhere) UCameraComponent* Camera;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* Body;
	UPROPERTY(VisibleAnywhere) UStaticMeshComponent* VehicleMesh;
	FVector Checkpoint; FString Vehicle; float Fuel = 0, Safe = 0, NoEnter = 0, Cool = 0; bool bSprint = false;

	/** Tempo mit Sprint (Fähigkeit) und Fahrzeug, in cm/s. */
	float WalkSpeed() const;
	void ApplyAbilities();
	void Respawn();
	void SetVehicle(const FString& Kind);

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* Input) override;
	virtual void NotifyActorBeginOverlap(AActor* Other) override;

private:
	UPROPERTY() UInputMappingContext* Mapping;
	UPROPERTY() UInputAction* MoveAction;
	UPROPERTY() UInputAction* LookAction;
	UPROPERTY() UInputAction* JumpAction;
	UPROPERTY() UInputAction* SprintAction;
	UPROPERTY() UInputAction* AttackAction;
	UPROPERTY() UInputAction* UseAction;
	UPROPERTY() UInputAction* PauseAction;
	UPROPERTY() UInputAction* UpAction;
	UPROPERTY() UInputAction* DownAction;
	UPROPERTY() UInputAction* EnterAction;
	void BuildInput();
	void Move(const FInputActionValue& V);
	void Look(const FInputActionValue& V);
	void JumpPressed();
	void JumpReleased();
	void SprintOn() { bSprint = true; ApplyAbilities(); }
	void SprintOff() { bSprint = false; ApplyAbilities(); }
	void Attack();
	void Use();
	void PausePressed();
	void MenuUp();
	void MenuDown();
	void MenuEnter();
	bool bThrust = false;
};
""",
        ["TrendoCharacter.cpp"] = """
// Von Trendo erzeugt: Spielfigur mit Kamera, Steuerung über Enhanced Input (im Code angelegt, keine Assets nötig).
#include "TrendoCharacter.h"
#include "TrendoGameMode.h"
#include "TrendoActors.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Kismet/GameplayStatics.h"

ATrendoCharacter::ATrendoCharacter()
{
	PrimaryActorTick.bCanEverTick = true;
	GetCapsuleComponent()->InitCapsuleSize(40.f, 90.f);
	bUseControllerRotationYaw = false;
	GetCharacterMovement()->bOrientRotationToMovement = true;
	GetCharacterMovement()->RotationRate = FRotator(0, 720, 0);
	GetCharacterMovement()->JumpZVelocity = 650.f;
	GetCharacterMovement()->AirControl = 0.6f;
	Arm = CreateDefaultSubobject<USpringArmComponent>(TEXT("Kameraarm"));
	Arm->SetupAttachment(RootComponent); Arm->TargetArmLength = 520.f; Arm->bUsePawnControlRotation = true; Arm->SocketOffset = FVector(0, 0, 80);
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Kamera"));
	Camera->SetupAttachment(Arm);
	Body = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Koerper"));
	Body->SetupAttachment(RootComponent); Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Body->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cylinder.Cylinder")));
	Body->SetRelativeScale3D(FVector(0.7f, 0.7f, 1.8f));
	VehicleMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Fahrzeug"));
	VehicleMesh->SetupAttachment(RootComponent); VehicleMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	VehicleMesh->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube")));
	VehicleMesh->SetRelativeLocation(FVector(0, 0, -70)); VehicleMesh->SetRelativeScale3D(FVector(1.6f, 1.0f, 0.4f)); VehicleMesh->SetVisibility(false);
}

void ATrendoCharacter::BeginPlay()
{
	Super::BeginPlay();
	Checkpoint = GetActorLocation();
	TrendoPaint(Body, FLinearColor(0.31f, 0.7f, 1.f));
	TrendoPaint(VehicleMesh, FLinearColor(0.98f, 0.45f, 0.09f));
	ApplyAbilities();
	if (APlayerController* PC = Cast<APlayerController>(GetController()))
		if (auto* Sub = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer())) { BuildInput(); Sub->AddMappingContext(Mapping, 0); }
}

float ATrendoCharacter::WalkSpeed() const
{
	const ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	const float Base = 300.f + 40.f * (GM ? GM->Data.Speed : 5) + (GM ? GM->State.Boost * 100.f : 0);
	return Base * (bSprint && GM && GM->Data.Has(TEXT("sprint")) ? 1.6f : 1.f) * (Vehicle == TEXT("wagen") ? 1.8f : Vehicle == TEXT("ufo") ? 1.3f : 1.f);
}

void ATrendoCharacter::ApplyAbilities()
{
	const ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this));
	JumpMaxCount = GM && GM->Data.Has(TEXT("doppelsprung")) ? 2 : 1;
	GetCharacterMovement()->MaxWalkSpeed = WalkSpeed();
}

void ATrendoCharacter::SetVehicle(const FString& Kind)
{
	Vehicle = Kind; Fuel = Kind == TEXT("ufo") ? 10.f : 0.f;
	VehicleMesh->SetVisibility(!Kind.IsEmpty());
	ApplyAbilities();
}

void ATrendoCharacter::Respawn() { SetActorLocation(Checkpoint); GetCharacterMovement()->Velocity = FVector::ZeroVector; }

void ATrendoCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Safe = FMath::Max(0.f, Safe - DeltaSeconds); NoEnter = FMath::Max(0.f, NoEnter - DeltaSeconds); Cool = FMath::Max(0.f, Cool - DeltaSeconds);
	if (Vehicle == TEXT("ufo") && bThrust && Fuel > 0)
	{
		FVector V = GetCharacterMovement()->Velocity; V.Z = FMath::Min(V.Z + 2400.f * DeltaSeconds, 500.f); GetCharacterMovement()->Velocity = V;
		if (GetCharacterMovement()->IsMovingOnGround()) GetCharacterMovement()->SetMovementMode(MOVE_Falling);
		Fuel -= DeltaSeconds;
		if (Fuel <= 0) { SetVehicle(FString()); if (ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this))) GM->State.Note = TEXT("Treibstoff leer"); }
	}
	if (GetActorLocation().Z < -5000.f) if (ATrendoGameMode* GM = Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(this))) GM->Hit(true);
}

static UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type, bool bWhenPaused = false)
{
	UInputAction* A = NewObject<UInputAction>(Outer, Name); A->ValueType = Type; A->bTriggerWhenPaused = bWhenPaused; return A;
}

void ATrendoCharacter::BuildInput()
{
	if (Mapping) return;
	Mapping = NewObject<UInputMappingContext>(this, TEXT("TrendoSteuerung"));
	MoveAction = MakeAction(this, TEXT("Laufen"), EInputActionValueType::Axis2D);
	LookAction = MakeAction(this, TEXT("Umsehen"), EInputActionValueType::Axis2D);
	JumpAction = MakeAction(this, TEXT("Springen"), EInputActionValueType::Boolean);
	SprintAction = MakeAction(this, TEXT("Sprinten"), EInputActionValueType::Boolean);
	AttackAction = MakeAction(this, TEXT("Angreifen"), EInputActionValueType::Boolean);
	UseAction = MakeAction(this, TEXT("Aussteigen"), EInputActionValueType::Boolean);
	PauseAction = MakeAction(this, TEXT("Pause"), EInputActionValueType::Boolean, true);
	UpAction = MakeAction(this, TEXT("MenueHoch"), EInputActionValueType::Boolean, true);
	DownAction = MakeAction(this, TEXT("MenueRunter"), EInputActionValueType::Boolean, true);
	EnterAction = MakeAction(this, TEXT("MenueWaehlen"), EInputActionValueType::Boolean, true);
	auto Map2D = [this](const FKey& Key, bool bSwizzle, bool bNegate)
	{
		FEnhancedActionKeyMapping& M = Mapping->MapKey(MoveAction, Key);
		if (bSwizzle) M.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(Mapping));
		if (bNegate) M.Modifiers.Add(NewObject<UInputModifierNegate>(Mapping));
	};
	Map2D(EKeys::W, true, false); Map2D(EKeys::S, true, true); Map2D(EKeys::D, false, false); Map2D(EKeys::A, false, true);
	Map2D(EKeys::Up, true, false); Map2D(EKeys::Down, true, true); Map2D(EKeys::Right, false, false); Map2D(EKeys::Left, false, true);
	Mapping->MapKey(MoveAction, EKeys::Gamepad_Left2D);
	Mapping->MapKey(LookAction, EKeys::Mouse2D); Mapping->MapKey(LookAction, EKeys::Gamepad_Right2D);
	for (const FKey& K : { EKeys::SpaceBar, EKeys::Gamepad_FaceButton_Bottom }) Mapping->MapKey(JumpAction, K);
	for (const FKey& K : { EKeys::LeftShift, EKeys::RightShift, EKeys::Gamepad_LeftShoulder }) Mapping->MapKey(SprintAction, K);
	for (const FKey& K : { EKeys::X, EKeys::J, EKeys::LeftMouseButton, EKeys::Gamepad_FaceButton_Left }) Mapping->MapKey(AttackAction, K);
	for (const FKey& K : { EKeys::E, EKeys::Gamepad_FaceButton_Right }) Mapping->MapKey(UseAction, K);
	for (const FKey& K : { EKeys::Escape, EKeys::P, EKeys::Gamepad_Special_Right }) Mapping->MapKey(PauseAction, K);
	for (const FKey& K : { EKeys::Up, EKeys::W, EKeys::Gamepad_DPad_Up }) Mapping->MapKey(UpAction, K);
	for (const FKey& K : { EKeys::Down, EKeys::S, EKeys::Gamepad_DPad_Down }) Mapping->MapKey(DownAction, K);
	for (const FKey& K : { EKeys::Enter, EKeys::SpaceBar, EKeys::Gamepad_FaceButton_Bottom }) Mapping->MapKey(EnterAction, K);
}

void ATrendoCharacter::SetupPlayerInputComponent(UInputComponent* Input)
{
	Super::SetupPlayerInputComponent(Input);
	BuildInput();
	if (UEnhancedInputComponent* EI = Cast<UEnhancedInputComponent>(Input))
	{
		EI->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ATrendoCharacter::Move);
		EI->BindAction(LookAction, ETriggerEvent::Triggered, this, &ATrendoCharacter::Look);
		EI->BindAction(JumpAction, ETriggerEvent::Started, this, &ATrendoCharacter::JumpPressed);
		EI->BindAction(JumpAction, ETriggerEvent::Completed, this, &ATrendoCharacter::JumpReleased);
		EI->BindAction(SprintAction, ETriggerEvent::Started, this, &ATrendoCharacter::SprintOn);
		EI->BindAction(SprintAction, ETriggerEvent::Completed, this, &ATrendoCharacter::SprintOff);
		EI->BindAction(AttackAction, ETriggerEvent::Started, this, &ATrendoCharacter::Attack);
		EI->BindAction(UseAction, ETriggerEvent::Started, this, &ATrendoCharacter::Use);
		EI->BindAction(PauseAction, ETriggerEvent::Started, this, &ATrendoCharacter::PausePressed);
		EI->BindAction(UpAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuUp);
		EI->BindAction(DownAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuDown);
		EI->BindAction(EnterAction, ETriggerEvent::Started, this, &ATrendoCharacter::MenuEnter);
	}
}

static ATrendoGameMode* TrendoGM(const UObject* O) { return Cast<ATrendoGameMode>(UGameplayStatics::GetGameMode(O)); }

void ATrendoCharacter::Move(const FInputActionValue& V)
{
	const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing() || !Controller) return;
	const FVector2D In = V.Get<FVector2D>(); const FRotator Yaw(0, Controller->GetControlRotation().Yaw, 0);
	AddMovementInput(FRotationMatrix(Yaw).GetUnitAxis(EAxis::X), In.Y);
	AddMovementInput(FRotationMatrix(Yaw).GetUnitAxis(EAxis::Y), In.X);
}

void ATrendoCharacter::Look(const FInputActionValue& V)
{
	const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing()) return;
	const FVector2D In = V.Get<FVector2D>(); AddControllerYawInput(In.X); AddControllerPitchInput(-In.Y);
}

void ATrendoCharacter::JumpPressed() { const ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing()) return; bThrust = true; Jump(); }
void ATrendoCharacter::JumpReleased() { bThrust = false; StopJumping(); }
void ATrendoCharacter::Attack() { ATrendoGameMode* GM = TrendoGM(this); if (!GM || !GM->Playing() || Cool > 0) return; Cool = 0.33f; GM->Attack(); }
void ATrendoCharacter::Use() { ATrendoGameMode* GM = TrendoGM(this); if (GM && GM->Playing()) GM->Dismount(); }
void ATrendoCharacter::PausePressed() { if (ATrendoGameMode* GM = TrendoGM(this)) GM->MenuKey(TEXT("escape")); }
void ATrendoCharacter::MenuUp() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("up")); }
void ATrendoCharacter::MenuDown() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("down")); }
void ATrendoCharacter::MenuEnter() { if (ATrendoGameMode* GM = TrendoGM(this); GM && !GM->Playing()) GM->MenuKey(TEXT("enter")); }

void ATrendoCharacter::NotifyActorBeginOverlap(AActor* Other)
{
	Super::NotifyActorBeginOverlap(Other);
	if (ATrendoGameMode* GM = TrendoGM(this)) GM->Touch(Other);
}
""",
        ["TrendoGameMode.h"] = """
// Von Trendo erzeugt: Spielablauf – Welt aus Trendo bauen, Regeln, Kampf, Fahrzeuge, Spielstand, Menü und Anzeige.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/HUD.h"
#include "GameFramework/SaveGame.h"
#include "TrendoTypes.h"
#include "TrendoGameMode.generated.h"

class ATrendoThing; class ATrendoEnemy; class ATrendoCharacter;

/** Spielstand – gleiche Inhalte wie in Trendo (Format 1). */
UCLASS()
class UTrendoSaveGame : public USaveGame
{
	GENERATED_BODY()
public:
	UPROPERTY() int32 Version = 1;
	UPROPERTY() int32 Level = 0;
	UPROPERTY() int32 Score = 0;
	UPROPERTY() int32 Lives = 3;
	UPROPERTY() TArray<FString> Inventory;
};

/** Anzeige: Punkte, Leben, Aufgaben, Inventar, Treibstoff, Meldung; Menü und Pause. */
UCLASS()
class ATrendoHUD : public AHUD
{
	GENERATED_BODY()
public:
	virtual void DrawHUD() override;
};

UCLASS()
class ATrendoGameMode : public AGameModeBase
{
	GENERATED_BODY()
public:
	ATrendoGameMode();
	FTrendoData Data; FTrendoState State;
	UPROPERTY() TArray<ATrendoEnemy*> Enemies;
	UPROPERTY() TArray<AActor*> Spawned;
	/** Menü: menu, play, pause, settings, help. */
	FString Mode = TEXT("menu"); int32 Sel = 0; FString Back = TEXT("menu"); bool bHasSave = false;
	bool bContrast = false, bBig = false, bMap = true; int32 Volume = 100;
	static constexpr float Tile = 100.f;

	bool Playing() const { return Mode == TEXT("play"); }
	/** level.json aus Content/Trendo lesen (oder übergebene Daten für Tests). */
	bool LoadData(FString& Error);
	/** Welt bauen: Level-Raster als 3D-Blöcke oder Spielobjekte in der Open World verteilen. */
	void BuildWorld(int32 Level);
	void Touch(AActor* Other);
	void Damage(ATrendoEnemy* E);
	void Attack();
	void Dismount();
	void Hit(bool bFall = false);
	void NewGame();
	void SaveGame();
	TArray<FString> MenuItems() const;
	void Choose(const FString& Item);
	void MenuKey(const FString& Key);
	ATrendoCharacter* Hero() const;

	virtual void InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage) override;
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;
private:
	float CheckSeconds = -1; bool bShotTaken = false;
	FVector Origin = FVector::ZeroVector; FRotator StartRot = FRotator::ZeroRotator;
	FVector Place(int32 X, int32 Y) const;
	void Freeze();
	void Report();
};
""",
        ["TrendoGameMode.cpp"] = """
// Von Trendo erzeugt: Spielablauf.
#include "TrendoGameMode.h"
#include "TrendoActors.h"
#include "TrendoCharacter.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerStart.h"
#include "Kismet/GameplayStatics.h"
#include "EngineUtils.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Misc/CommandLine.h"
#include "UnrealClient.h"

DEFINE_LOG_CATEGORY_STATIC(LogTrendo, Log, All);

ATrendoGameMode::ATrendoGameMode()
{
	PrimaryActorTick.bCanEverTick = true;
	DefaultPawnClass = ATrendoCharacter::StaticClass();
	HUDClass = ATrendoHUD::StaticClass();
}

bool ATrendoGameMode::LoadData(FString& Error)
{
	FString Json; const FString File = FPaths::ProjectContentDir() / TEXT("Trendo/level.json");
	if (!FFileHelper::LoadFileToString(Json, *File)) { Error = TEXT("level.json fehlt: ") + File; return false; }
	return FTrendoData::Parse(Json, Data, Error);
}

void ATrendoGameMode::InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage)
{
	Super::InitGame(MapName, Options, ErrorMessage);
	FString Error; if (!LoadData(Error)) UE_LOG(LogTrendo, Error, TEXT("TRENDO-FEHLER %s"), *Error);
}

ATrendoCharacter* ATrendoGameMode::Hero() const { return Cast<ATrendoCharacter>(UGameplayStatics::GetPlayerPawn(this, 0)); }

void ATrendoGameMode::BeginPlay()
{
	Super::BeginPlay();
	State.Reset(Data);
	bHasSave = UGameplayStatics::DoesSaveGameExist(TEXT("Trendo"), 0);
	if (bHasSave)
		if (UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::LoadGameFromSlot(TEXT("Trendo"), 0)); S && S->Version <= 1 && Data.Levels.IsValidIndex(S->Level))
		{ State.Level = S->Level; State.Score = S->Score; State.Lives = FMath::Max(1, S->Lives); State.Inventory = S->Inventory; }
	Mode = Data.bMenu ? TEXT("menu") : TEXT("play");
	if (Data.Levels.Num() > 0) BuildWorld(State.Level);
	if (FParse::Param(FCommandLine::Get(), TEXT("trendocheck"))) { CheckSeconds = 4.f; Mode = TEXT("play"); }
}

FVector ATrendoGameMode::Place(int32 X, int32 Y) const
{
	const TArray<FString>& Rows = Data.Levels[State.Level].Rows;
	if (Data.World == TEXT("openworld"))
	{
		// Open World: Objekte vor dem Startpunkt auf dem Gelände verteilen (Höhe per Strahl nach unten)
		FVector P = Origin + StartRot.RotateVector(FVector(400.f + X * 300.f, (Y - Rows.Num() / 2) * 300.f, 0));
		FHitResult Hit; FCollisionQueryParams Q; if (APawn* H = UGameplayStatics::GetPlayerPawn(this, 0)) Q.AddIgnoredActor(H);
		for (AActor* A : Spawned) Q.AddIgnoredActor(A);
		if (GetWorld()->LineTraceSingleByChannel(Hit, P + FVector(0, 0, 50000), P - FVector(0, 0, 50000), ECC_WorldStatic, Q)) P.Z = Hit.Location.Z + 60.f;
		return P;
	}
	return Origin + StartRot.RotateVector(FVector(X * Tile, 0, 0)) + FVector(0, 0, (Rows.Num() - 1 - Y) * Tile);
}

void ATrendoGameMode::BuildWorld(int32 Level)
{
	for (AActor* A : Spawned) if (IsValid(A)) A->Destroy();
	Spawned.Empty(); Enemies.Empty(); State.Level = Level; State.bDoorsOpen = false;
	if (AActor* Start = FindPlayerStart(nullptr)) { Origin = Start->GetActorLocation() - FVector(0, 0, 90); StartRot = FRotator(0, Start->GetActorRotation().Yaw, 0); }
	const bool bOpen = Data.World == TEXT("openworld");
	const TArray<FString>& Rows = Data.Levels[Level].Rows;
	FVector StartAt = Origin + FVector(0, 0, 200);
	for (int32 Y = 0; Y < Rows.Num(); Y++)
		for (int32 X = 0; X < Rows[Y].Len(); X++)
		{
			const TCHAR C = Rows[Y][X];
			if (C == '.' || (bOpen && C == '#')) continue;
			const FVector P = Place(X, Y);
			if (C == 'S') { StartAt = P + FVector(0, 0, 120); continue; }
			if (C == 'E')
			{
				FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
				ATrendoEnemy* E = GetWorld()->SpawnActor<ATrendoEnemy>(P + FVector(0, 0, 10), FRotator::ZeroRotator, SP);
				E->MaxHp = E->Hp = FMath::Clamp(FMath::RoundToInt(Data.Prop(Level, X, Y, TEXT("hp"), 1)), 1, 9);
				E->SetActorScale3D(FVector(E->MaxHp > 1 ? 1.4f : 0.9f));
				Enemies.Add(E); Spawned.Add(E); continue;
			}
			FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
			ATrendoThing* T = GetWorld()->SpawnActor<ATrendoThing>(P, FRotator::ZeroRotator, SP);
			FLinearColor Col(0.35f, 0.4f, 0.5f); float Size = 1.f; bool bSolid = false;
			switch (C)
			{
			case '#': bSolid = true; break;
			case 'o': Col = FLinearColor(1.f, 0.8f, 0.1f); Size = 0.4f; break;
			case '^': Col = FLinearColor(0.9f, 0.3f, 0.3f); Size = 0.6f; break;
			case 'Z': Col = FLinearColor(0.2f, 0.9f, 0.3f); Size = 1.2f; break;
			case 'F': Col = FLinearColor(1.f, 0.6f, 0.1f); Size = 0.5f; break;
			case 'C': Col = FLinearColor(0.2f, 0.9f, 0.9f); Size = 0.5f; break;
			case 'K': Col = FLinearColor(1.f, 0.85f, 0.2f); Size = 0.4f; break;
			case 'D': Col = FLinearColor(0.55f, 0.35f, 0.2f); bSolid = true; break;
			case 'G': Col = FLinearColor(0.66f, 0.33f, 0.97f); Size = 0.4f; break;
			case 'N': Col = FLinearColor(0.08f, 0.72f, 0.65f); Size = 0.8f; break;
			case 'V': Col = FLinearColor(0.98f, 0.45f, 0.09f); Size = 0.9f; break;
			default: break;
			}
			T->Setup(C, X, Y, Col, Size, bSolid);
			if (C == 'V') T->Kind = Data.Text(Level, X, Y, TEXT("kind"), TEXT("wagen"));
			Spawned.Add(T);
		}
	if (ATrendoCharacter* H = Hero())
	{
		H->SetActorLocation(StartAt); H->Checkpoint = StartAt; H->ApplyAbilities();
		if (bOpen) H->SetActorLocation(Origin + FVector(0, 0, 120)); // Open World: am Startpunkt, Spielobjekte davor
		H->SetActorRotation(StartRot); if (AController* C = H->GetController()) C->SetControlRotation(StartRot + FRotator(-12, 0, 0));
		H->Checkpoint = H->GetActorLocation();
	}
}

void ATrendoGameMode::Touch(AActor* Other)
{
	ATrendoCharacter* H = Hero(); if (!H || !Playing()) return;
	if (ATrendoEnemy* E = Cast<ATrendoEnemy>(Other))
	{
		if (H->GetVelocity().Z < -100.f && H->GetActorLocation().Z > E->GetActorLocation().Z + 50.f) { Damage(E); H->LaunchCharacter(FVector(0, 0, 500), false, true); }
		else Hit();
		return;
	}
	ATrendoThing* T = Cast<ATrendoThing>(Other); if (!T) return;
	const TCHAR C = T->Tile.Len() > 0 ? T->Tile[0] : '.';
	switch (C)
	{
	case 'o': State.Coins++; State.Score += 10; T->Destroy(); break;
	case 'K': T->Destroy(); if (State.Inventory.Num() >= 0) { int32 Left = 0; for (AActor* A : Spawned) if (IsValid(A) && A != T) if (ATrendoThing* K = Cast<ATrendoThing>(A); K && K->Tile == TEXT("K")) Left++; if (Left == 0) State.bDoorsOpen = true; } break;
	case '^': Hit(); break;
	case 'F': H->LaunchCharacter(FVector(0, 0, 1100), false, true); break;
	case 'C': if (!H->Checkpoint.Equals(T->GetActorLocation() + FVector(0, 0, 100), 1.f)) { H->Checkpoint = T->GetActorLocation() + FVector(0, 0, 100); SaveGame(); } break;
	case 'G': { const FString N = Data.Text(State.Level, T->TileX, T->TileY, TEXT("name"), TEXT("Kristall")); State.Inventory.Add(N); State.Items++; State.Note = N + TEXT(" eingesammelt"); T->Destroy(); break; }
	case 'N': State.TalkTo(Data, T->TileX, T->TileY); break;
	case 'V': if (H->Vehicle.IsEmpty() && H->NoEnter <= 0) { H->SetVehicle(T->Kind == TEXT("ufo") ? TEXT("ufo") : TEXT("wagen")); State.Note = H->Vehicle == TEXT("ufo") ? TEXT("Ufo: Leertaste halten – E steigt aus") : TEXT("Wagen: schnell, fängt einen Treffer ab – E steigt aus"); T->Destroy(); } break;
	case 'Z':
		State.Score += 10;
		if (State.Level + 1 < Data.Levels.Num()) { BuildWorld(State.Level + 1); SaveGame(); }
		else { State.bWon = true; UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); State.Note = TEXT("Gewonnen!"); }
		break;
	default: break;
	}
}

void ATrendoGameMode::Damage(ATrendoEnemy* E)
{
	if (!IsValid(E) || E->Hp <= 0) return;
	E->Hp--; if (E->Hp > 0) return;
	State.Defeated++; State.Score += E->MaxHp > 1 ? 10 : 2; if (E->MaxHp > 1) State.Note = TEXT("Boss besiegt!");
	Enemies.Remove(E); E->Destroy();
}

void ATrendoGameMode::Attack()
{
	ATrendoCharacter* H = Hero(); if (!H || Data.Weapon.IsEmpty()) return;
	const FVector Fwd = H->GetActorForwardVector(), At = H->GetActorLocation();
	if (Data.Weapon == TEXT("schwert"))
	{
		for (ATrendoEnemy* E : TArray<ATrendoEnemy*>(Enemies))
		{
			const FVector D = E->GetActorLocation() - At;
			if (FVector(D.X, D.Y, 0).Size() < 180.f && FMath::Abs(D.Z) < 120.f && FVector::DotProduct(D.GetSafeNormal2D(), Fwd) > 0.2f) Damage(E);
		}
	}
	else
	{
		FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		if (ATrendoShot* S = GetWorld()->SpawnActor<ATrendoShot>(At + Fwd * 70.f, FRotator::ZeroRotator, SP)) S->Dir = Fwd;
	}
}

void ATrendoGameMode::Dismount()
{
	ATrendoCharacter* H = Hero(); if (!H || H->Vehicle.IsEmpty()) return;
	FActorSpawnParameters SP; SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	ATrendoThing* T = GetWorld()->SpawnActor<ATrendoThing>(H->GetActorLocation() - H->GetActorForwardVector() * 150.f, FRotator::ZeroRotator, SP);
	T->Setup('V', -1, -1, FLinearColor(0.98f, 0.45f, 0.09f), 0.9f, false); T->Kind = H->Vehicle; Spawned.Add(T);
	H->SetVehicle(FString()); H->NoEnter = 0.5f; State.Note = TEXT("Ausgestiegen");
}

void ATrendoGameMode::Hit(bool bFall)
{
	ATrendoCharacter* H = Hero(); if (!H) return;
	if (!H->Vehicle.IsEmpty() && !bFall) { H->SetVehicle(FString()); H->Safe = 0.75f; State.Note = TEXT("Fahrzeug kaputt – zu Fuß weiter"); return; }
	if (H->Safe > 0 && !bFall) return;
	H->SetVehicle(FString()); State.Lives--;
	if (State.Lives <= 0) { State.bOver = true; UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); State.Note = TEXT("Verloren – Esc, Neu beginnen"); }
	else H->Respawn();
}

void ATrendoGameMode::SaveGame()
{
	UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::CreateSaveGameObject(UTrendoSaveGame::StaticClass()));
	S->Level = State.Level; S->Score = State.Score; S->Lives = State.Lives; S->Inventory = State.Inventory;
	UGameplayStatics::SaveGameToSlot(S, TEXT("Trendo"), 0);
}

void ATrendoGameMode::NewGame()
{
	UGameplayStatics::DeleteGameInSlot(TEXT("Trendo"), 0); bHasSave = false;
	State.Reset(Data); BuildWorld(0);
}

TArray<FString> ATrendoGameMode::MenuItems() const
{
	if (Mode == TEXT("menu")) { TArray<FString> L; if (bHasSave) L.Add(TEXT("Fortsetzen")); L.Append({ TEXT("Spielen"), TEXT("Einstellungen"), TEXT("Steuerung") }); return L; }
	if (Mode == TEXT("pause")) return { TEXT("Weiter"), TEXT("Neu beginnen"), TEXT("Einstellungen"), TEXT("Zum Menü") };
	if (Mode == TEXT("settings")) return { FString::Printf(TEXT("Lautstärke: %d %%"), Volume), FString(TEXT("Hoher Kontrast: ")) + (bContrast ? TEXT("an") : TEXT("aus")), FString(TEXT("Große Schrift: ")) + (bBig ? TEXT("an") : TEXT("aus")), FString(TEXT("Karte: ")) + (bMap ? TEXT("an") : TEXT("aus")), TEXT("Zurück") };
	return { TEXT("Zurück") };
}

void ATrendoGameMode::Choose(const FString& Item)
{
	FString W = Item; Item.Split(TEXT(":"), &W, nullptr);
	if (W == TEXT("Fortsetzen") || W == TEXT("Weiter")) Mode = TEXT("play");
	else if (W == TEXT("Spielen") || W == TEXT("Neu beginnen")) { NewGame(); Mode = TEXT("play"); }
	else if (W == TEXT("Einstellungen") || W == TEXT("Steuerung")) { Back = Mode; Mode = W == TEXT("Einstellungen") ? TEXT("settings") : TEXT("help"); Sel = 0; }
	else if (W == TEXT("Zum Menü")) { Mode = TEXT("menu"); Sel = 0; }
	else if (W == TEXT("Lautstärke")) Volume = (Volume + 25) % 125;
	else if (W == TEXT("Hoher Kontrast")) bContrast = !bContrast;
	else if (W == TEXT("Große Schrift")) bBig = !bBig;
	else if (W == TEXT("Karte")) bMap = !bMap;
	else if (W == TEXT("Zurück")) { Mode = Back; Sel = 0; }
}

void ATrendoGameMode::MenuKey(const FString& Key)
{
	if (Mode == TEXT("play")) { if (Key == TEXT("escape")) { Mode = TEXT("pause"); Sel = 0; } return; }
	const TArray<FString> It = MenuItems();
	if (Key == TEXT("up")) Sel = (Sel + It.Num() - 1) % It.Num();
	else if (Key == TEXT("down")) Sel = (Sel + 1) % It.Num();
	else if (Key == TEXT("enter")) Choose(It[FMath::Min(Sel, It.Num() - 1)]);
	else if (Key == TEXT("escape")) { if (Mode == TEXT("pause")) Mode = TEXT("play"); else if (Mode == TEXT("settings") || Mode == TEXT("help")) { Mode = Back; Sel = 0; } }
}

void ATrendoGameMode::Freeze() { for (ATrendoEnemy* E : Enemies) if (IsValid(E)) E->bFrozen = !Playing(); }

void ATrendoGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Freeze();
	if (Playing() && !State.bWon && !State.bOver)
	{
		State.Seconds += DeltaSeconds; State.CheckRules(Data);
		if (State.bDoorsOpen) for (AActor* A : Spawned) if (ATrendoThing* T = Cast<ATrendoThing>(A); IsValid(T) && T->Tile == TEXT("D")) T->Destroy();
	}
	if (CheckSeconds > 0 && (CheckSeconds -= DeltaSeconds) <= 0)
	{
		// Startlauf (-trendocheck): mit -trendoshot erst ein Bildschirmfoto nach Saved/Screenshots, dann Bericht und Ende
		if (!bShotTaken && FParse::Param(FCommandLine::Get(), TEXT("trendoshot"))) { bShotTaken = true; FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir() / TEXT("Screenshots/trendo-start.png"), false, false); CheckSeconds = 1.5f; }
		else Report();
	}
}

void ATrendoGameMode::Report()
{
	ATrendoCharacter* H = Hero();
	const bool bGround = H && H->GetCharacterMovement()->IsMovingOnGround();
	int32 Live = 0; for (AActor* A : Spawned) if (IsValid(A)) Live++;
	UE_LOG(LogTrendo, Display, TEXT("TRENDO-SPIEL Welt=%s Level=%d Objekte=%d Gegner=%d Figur=%s Boden=%s Z=%.0f"), *Data.World, State.Level, Live, Enemies.Num(), H ? TEXT("ja") : TEXT("nein"), bGround ? TEXT("ja") : TEXT("nein"), H ? H->GetActorLocation().Z : 0.f);
	UE_LOG(LogTrendo, Display, TEXT("%s"), H && bGround && Live > 0 ? TEXT("TRENDO-OK Spiel läuft, Figur steht") : TEXT("TRENDO-FEHLER Spiel nicht spielbar"));
	FGenericPlatformMisc::RequestExit(false);
}

void ATrendoHUD::DrawHUD()
{
	Super::DrawHUD();
	ATrendoGameMode* GM = GetWorld() ? GetWorld()->GetAuthGameMode<ATrendoGameMode>() : nullptr; if (!GM || !Canvas) return;
	UFont* Font = GEngine->GetMediumFont(); const float S = GM->bBig ? 1.6f : 1.15f;
	const FLinearColor Accent = GM->bContrast ? FLinearColor::Yellow : FLinearColor(1.f, 0.8f, 0.1f);
	if (GM->Playing())
	{
		const ATrendoCharacter* H = GM->Hero();
		FString T = FString::Printf(TEXT("Punkte %d   Leben %d"), GM->State.Score, GM->State.Lives);
		if (GM->Data.Levels.Num() > 1) T += FString::Printf(TEXT("   Level %d/%d"), GM->State.Level + 1, GM->Data.Levels.Num());
		if (H && H->Vehicle == TEXT("ufo")) T += FString::Printf(TEXT("   Treibstoff %d s"), FMath::CeilToInt(H->Fuel));
		if (GM->State.Inventory.Num() > 0) T += TEXT("\nInventar: ") + FString::Join(GM->State.Inventory, TEXT(", "));
		T += TEXT("\n") + GM->State.Quests(GM->Data) + GM->State.Note;
		if (GM->bContrast) DrawRect(FLinearColor(0, 0, 0, 0.8f), 0, 0, 640 * S, 140 * S);
		DrawText(T, FLinearColor::White, 20, 20, Font, S);
		return;
	}
	DrawRect(FLinearColor(0, 0, 0, GM->bContrast ? 0.94f : 0.8f), 0, 0, Canvas->SizeX, Canvas->SizeY);
	const FString Title = GM->Mode == TEXT("menu") ? GM->Data.Name : GM->Mode == TEXT("pause") ? FString(TEXT("Pause")) : GM->Mode == TEXT("settings") ? FString(TEXT("Einstellungen")) : FString(TEXT("Steuerung"));
	float W, Hh; GetTextSize(Title, W, Hh, Font, S * 2.f); DrawText(Title, FLinearColor::White, (Canvas->SizeX - W) / 2, 80, Font, S * 2.f);
	float Y = 180;
	if (GM->Mode == TEXT("help"))
		for (const TCHAR* L : { TEXT("WASD oder Pfeile laufen · Maus umsehen · Leertaste springen · Umschalt sprinten"), TEXT("X, J oder linke Maustaste angreifen · E aussteigen · Esc Pause"), TEXT("Controller: Stick laufen · A springen · X angreifen · B aussteigen · Start Pause") })
		{ GetTextSize(L, W, Hh, Font, S); DrawText(L, FLinearColor::White, (Canvas->SizeX - W) / 2, Y, Font, S); Y += 34 * S; }
	const TArray<FString> It = GM->MenuItems();
	for (int32 i = 0; i < It.Num(); i++)
	{
		GetTextSize(It[i], W, Hh, Font, S * 1.3f);
		if (i == GM->Sel) DrawRect(Accent, Canvas->SizeX / 2 - 200, Y - 6, 400, Hh + 12);
		DrawText(It[i], i == GM->Sel ? FLinearColor::Black : FLinearColor::White, (Canvas->SizeX - W) / 2, Y, Font, S * 1.3f);
		Y += Hh + 22;
	}
}
""",
        ["TrendoTests.cpp"] = """
// Von Trendo erzeugt: Unreal-Automatisierungstests (Session Frontend → Automation, oder Kommandozeile „Automation RunTests Trendo“).
#include "Misc/AutomationTest.h"
#include "TrendoTypes.h"
#include "TrendoGameMode.h"
#include "Kismet/GameplayStatics.h"

#if WITH_DEV_AUTOMATION_TESTS

static const TCHAR* TrendoTestJson = TEXT("{\"name\":\"Test\",\"world\":\"openworld\",\"speed\":5,\"lives\":3,\"weapon\":\"schwert\",\"menu\":true,\"abilities\":[\"doppelsprung\"],")
	TEXT("\"levels\":[{\"name\":\"L\",\"rows\":[\"..........\",\"SGNE.V...Z\",\"##########\"]}],\"props\":[{\"key\":\"3,1:hp\",\"value\":2}],")
	TEXT("\"texts\":[{\"key\":\"1,1:name\",\"value\":\"Apfel\"},{\"key\":\"2,1:need\",\"value\":\"Apfel\"},{\"key\":\"2,1:thanks\",\"value\":\"Lecker!\"},{\"key\":\"2,1:give\",\"value\":\"leben\"}],")
	TEXT("\"rules\":[{\"when\":\"gegenstaende\",\"n\":1,\"act\":\"punkte\",\"title\":\"Finder\"}]}");

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoDatenTest, "Trendo.Daten", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoDatenTest::RunTest(const FString& Parameters)
{
	FTrendoData D; FString Error;
	TestTrue(TEXT("level.json wird gelesen"), FTrendoData::Parse(TrendoTestJson, D, Error));
	TestEqual(TEXT("Welt"), D.World, FString(TEXT("openworld")));
	TestTrue(TEXT("Doppelsprung"), D.Has(TEXT("doppelsprung")));
	TestEqual(TEXT("Waffe"), D.Weapon, FString(TEXT("schwert")));
	TestEqual(TEXT("Boss-Trefferpunkte"), D.Prop(0, 3, 1, TEXT("hp"), 1), 2.f);
	TestEqual(TEXT("Gegenstand"), D.Text(0, 1, 1, TEXT("name"), TEXT("Kristall")), FString(TEXT("Apfel")));
	FTrendoData Bad;
	TestFalse(TEXT("kaputte Datei abgelehnt"), FTrendoData::Parse(TEXT("{kaputt"), Bad, Error));
	TestFalse(TEXT("ohne Level abgelehnt"), FTrendoData::Parse(TEXT("{\"levels\":[]}"), Bad, Error));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoRegelnTest, "Trendo.Regeln", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoRegelnTest::RunTest(const FString& Parameters)
{
	FTrendoData D; FString Error; FTrendoData::Parse(TrendoTestJson, D, Error);
	FTrendoState S; S.Reset(D);
	TestEqual(TEXT("Aufgabe offen"), S.Quests(D), FString(TEXT("Finder: 0/1\n")));
	S.TalkTo(D, 2, 1); TestEqual(TEXT("Figur ohne Apfel"), S.Lives, 3); TestEqual(TEXT("sagt Hallo"), S.Note, FString(TEXT("Hallo!")));
	S.Inventory.Add(TEXT("Apfel")); S.Items++;
	TestEqual(TEXT("Regel erfüllt"), S.CheckRules(D), 1); TestEqual(TEXT("Belohnung"), S.Score, 10);
	TestEqual(TEXT("Aufgabe erledigt"), S.Quests(D), FString(TEXT("✓ Finder\n")));
	S.TalkTo(D, 2, 1); TestEqual(TEXT("Figur belohnt"), S.Lives, 4); TestEqual(TEXT("Dank"), S.Note, FString(TEXT("Lecker!"))); TestEqual(TEXT("Apfel abgegeben"), S.Inventory.Num(), 0);
	TestEqual(TEXT("Regel feuert nur einmal"), S.CheckRules(D), 0);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTrendoSpielstandTest, "Trendo.Spielstand", EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FTrendoSpielstandTest::RunTest(const FString& Parameters)
{
	UTrendoSaveGame* S = Cast<UTrendoSaveGame>(UGameplayStatics::CreateSaveGameObject(UTrendoSaveGame::StaticClass()));
	S->Level = 1; S->Score = 77; S->Lives = 2; S->Inventory.Add(TEXT("Apfel"));
	TestTrue(TEXT("gespeichert"), UGameplayStatics::SaveGameToSlot(S, TEXT("TrendoTest"), 0));
	UTrendoSaveGame* L = Cast<UTrendoSaveGame>(UGameplayStatics::LoadGameFromSlot(TEXT("TrendoTest"), 0));
	TestNotNull(TEXT("geladen"), L);
	if (L) { TestEqual(TEXT("Level"), L->Level, 1); TestEqual(TEXT("Punkte"), L->Score, 77); TestEqual(TEXT("Inventar"), L->Inventory.Num(), 1); TestEqual(TEXT("Format"), L->Version, 1); }
	UGameplayStatics::DeleteGameInSlot(TEXT("TrendoTest"), 0);
	return true;
}

#endif
""",
    };
}
