/* Trendo-Testplugin (eigener Code): meldet sich als VST3-Fabrik und stuerzt beim ersten Methodenaufruf ab –
   prueft, dass ein Plugin-Absturz isoliert bleibt. Markierung nur, wenn TRENDO_MARKE gesetzt ist. */
#include <windows.h>
#include <stdio.h>
static void mark(const char* what)
{
    char p[MAX_PATH]; DWORD n = GetEnvironmentVariableA("TRENDO_MARKE", p, MAX_PATH);
    FILE* f = 0;
    if (n == 0 || n >= MAX_PATH) return;
    if (fopen_s(&f, p, "a") == 0 && f) { fputs(what, f); fputs("\n", f); fclose(f); }
}
static long __stdcall crash(void* self) { volatile int* z = (int*)0; mark("Methode aufgerufen - Absturz"); *z = 1; return 0; }
static void* vtbl[32];
static struct { void** vt; } factory = { vtbl };
__declspec(dllexport) int InitDll(void) { mark("InitDll"); return 1; }
__declspec(dllexport) int ExitDll(void) { return 1; }
__declspec(dllexport) void* GetPluginFactory(void) { int i; mark("GetPluginFactory"); for (i = 0; i < 32; i++) vtbl[i] = (void*)crash; return &factory; }
