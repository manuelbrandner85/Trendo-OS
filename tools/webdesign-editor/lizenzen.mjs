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
writeFileSync("dist/LIZENZEN.txt", "Trendo Web Design Studio – Drittanbieter-Komponenten im Editor-Bündel (assets/editor.js, assets/style.css)\n\n" + texts.join("\n\n") + "\n");
writeFileSync("dist/LIZENZEN.md", "| Paket | Lizenz |\n|---|---|\n" + rows.join("\n") + "\n");
console.log(rows.length + " Pakete; nicht freizügig: " + (bad.join(", ") || "keine"));
