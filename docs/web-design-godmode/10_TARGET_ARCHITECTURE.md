# Zielarchitektur
Trendo (Avalonia) → Studio CORE-07 → Bereich „Visueller Editor“ → NativeWebView (`file://…/Assets/webdesign/index.html`) → Puck-Editor (React) mit Bausteinen (Inhalt, Aufbau, 3D) →
Brücke (`invokeCSharpAction` ↔ `InvokeScript`) → `WebDesign` (Trendo.Core): eine Datenquelle `seite.trendo.json` je Projekt → Export `veroeffentlichen/` → vorhandene Qualitätsprüfung/Vorschau/Code-Editor des Studios.
Geplant: Zeitleiste (Theatre-Stand), Tokens/Breakpoints, GrapesJS-Import, KI-Team-Anbindung (Claude-Login), Hosting-Adapter.
