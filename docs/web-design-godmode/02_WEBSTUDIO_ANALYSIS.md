# Webstudio (webstudio-is/webstudio, main ee27776, 2026-11-07)
- Aufbau: Monorepo, App `apps/builder` (Remix), 42 Pakete u. a. `css-engine`, `design-system`, `sdk`, `react-sdk`, `sdk-components-*`, `project-build`, `prisma-client`, `postgrest`, `multiplayer-protocol`, `trpc-interface`.
- Datenmodell: Instanzbaum mit Props, Stilquellen je Breakpoint, Design-Tokens; Persistenz über Postgres/Prisma, Mehrbenutzer-Synchronisation.
- **Lizenz AGPL-3.0 in allen Paketen** (css-data enthält MIT-Drittanteil). Für ein weitergegebenes Trendo hieße Übernahme: Offenlegungspflicht des abgeleiteten Werks. → **Kein Code übernommen.**
- Verwertbar nur als allgemeine Konzepte (nicht schutzfähig): Breakpoints als eigene Stil-Ebenen, Tokens, Instanzbaum → clean-room für Module 04/05.
- Zusätzlich ungeeignet: braucht Datenbank-Server und Web-Hosting-Infrastruktur.
