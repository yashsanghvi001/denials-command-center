// Fails the build when UI code breaks two house rules: colours must come from the daisyUI theme
// (so they work in both the light and dark themes), and user-facing text must not contain em or en dashes.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const sourceDir = new URL("../src", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");

const rules = [
  { pattern: /\bbadge-neutral\b/, reason: "badge-neutral is near-black in the dark theme; use an untoned soft badge" },
  { pattern: /\btext-neutral(?!-content)\b/, reason: "text-neutral is unreadable on dark cards; use text-base-content" },
  { pattern: /\b(?:text|bg|border|ring|fill|stroke)-(?:black|white)\b/, reason: "fixed black/white ignores the theme; use base-* tokens" },
  {
    pattern: /\b(?:text|bg|border|ring|fill|stroke|from|to|via)-(?:slate|gray|zinc|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3}\b/,
    reason: "Tailwind palette colours ignore the theme; use daisyUI tokens (primary, success, warning, error, info, base-*)",
  },
  { pattern: /#[0-9a-fA-F]{3,8}\b/, reason: "hex colours ignore the theme; use daisyUI tokens" },
  { pattern: /[–—]/, reason: "no em or en dashes in the UI; use plain words such as None, Not set or '1 to 25'" },
];

function sourceFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.(tsx?|css)$/.test(name) ? [path] : [];
  });
}

const problems = sourceFiles(sourceDir).flatMap((file) =>
  readFileSync(file, "utf8")
    .split("\n")
    .flatMap((line, index) =>
      rules.filter((rule) => rule.pattern.test(line)).map((rule) => `${relative(sourceDir, file)}:${index + 1}  ${rule.reason}`),
    ),
);

if (problems.length > 0) {
  console.error(`UI rule check failed:\n${problems.join("\n")}`);
  process.exit(1);
}
console.log("UI rule check passed.");
