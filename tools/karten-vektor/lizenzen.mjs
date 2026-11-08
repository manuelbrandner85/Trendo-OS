// Lizenzdatei für das ausgelieferte Editor-Bündel: alle Laufzeit-Pakete (npm ls --omit=dev) mit Version, Lizenz und vollem Lizenztext.
import { execSync } from "node:child_process";
import { readFileSync, readdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";
const tree = JSON.parse(execSync("npm ls --omit=dev --all --json", { encoding: "utf8", maxBuffer: 64 << 20 }));
const seen = new Map();
(function walk(deps) { for (const [name, d] of Object.entries(deps || {})) { if (!d.version || seen.has(name + "@" + d.version)) continue; seen.set(name + "@" + d.version, name); walk(d.dependencies); } })(tree.dependencies);
const rows = [], texts = [], bad = [];
for (const [key, name] of [...seen].sort()) {
  const dir = join("node_modules", name); if (!existsSync(dir)) continue;
  const pkg = JSON.parse(readFileSync(join(dir, "package.json"), "utf8"));
  const lic = typeof pkg.license === "string" ? pkg.license : pkg.license?.type || "UNBEKANNT";
  if (!/^(MIT|ISC|Apache-2\.0|BSD-[23]-Clause|0BSD|BlueOak-1\.0\.0|\(MIT OR Apache-2\.0\)|MIT OR Apache-2\.0)$/.test(lic)) bad.push(key + " " + lic);
  rows.push(`| ${key} | ${lic} |`);
  const f = readdirSync(dir).find((x) => /^(licen[sc]e|copying)(\.(md|txt))?$/i.test(x));
  texts.push(`----- ${key} (${lic}) -----\n` + (f ? readFileSync(join(dir, f), "utf8").trim() : "(kein Lizenztext im Paket; Lizenz laut package.json: " + lic + ")"));
}
writeFileSync("dist/LIZENZEN.txt", "Trendo Karten-Studio – Drittanbieter-Komponenten im Vektorkarten-Bündel (assets/karte.js, assets/style.css)\n\n" + texts.join("\n\n") + "\n");
writeFileSync("dist/LIZENZEN.md", "| Paket | Lizenz |\n|---|---|\n" + rows.join("\n") + "\n");
console.log(rows.length + " Pakete; nicht freizügig: " + (bad.join(", ") || "keine"));
// Zusätzlich im Bündel (keine npm-Pakete): Schriften und Symbole aus protomaps/basemaps-assets
import { appendFileSync } from "node:fs";
appendFileSync("dist/LIZENZEN.txt", "\n----- Noto Sans (Glyphen aus protomaps/basemaps-assets, fonts/) – SIL Open Font License 1.1 -----\n" + readFileSync("src/assets/glyphs/OFL.txt", "utf8").trim() + "\n\n----- Kartensymbole (protomaps/basemaps-assets, sprites/v4) – MIT, abgeleitet von tangrams/icons (https://github.com/tangrams/icons/blob/master/LICENSE.md) -----\n");
appendFileSync("dist/LIZENZEN.md", "| Noto Sans (Glyphen, basemaps-assets) | OFL-1.1 |\n| Kartensymbole (basemaps-assets/sprites v4) | MIT |\n");
