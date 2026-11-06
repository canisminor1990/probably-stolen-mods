import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const srcDir = join(root, "src");
const readmePath = join(root, "README.md");
const begin = "<!-- mods-table -->";
const end = "<!-- /mods-table -->";

function loadPkg() {
	return JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
}

function listMods() {
	return readdirSync(srcDir, { withFileTypes: true })
		.filter((entry) => entry.isDirectory())
		.map((entry) => entry.name)
		.filter((name) => existsSync(join(srcDir, name, "README.md")))
		.sort((a, b) => a.localeCompare(b));
}

function githubRepo(pkg) {
	const url = String(pkg.repository?.url || "https://github.com/canisminor1990/probably-stolen-mods")
		.replace(/^git\+/, "")
		.replace(/\.git$/, "");
	return url.replace(/\/+$/, "");
}

function githubBranch(pkg) {
	return pkg.repository?.branch || "main";
}

function previewUrl(pkg, mod) {
	const preview = join(srcDir, mod, "preview.png");
	if (!existsSync(preview)) return "";
	const rel = `src/${mod}/preview.png`;
	return `${githubRepo(pkg)}/blob/${githubBranch(pkg)}/${rel}?raw=true`;
}

const coverWidth = 96;

function escapeCell(text) {
	return String(text || "")
		.replace(/\r\n/g, "\n")
		.replace(/\s+/g, " ")
		.trim()
		.replace(/\|/g, "\\|");
}

function escapeAttr(text) {
	return String(text || "")
		.replace(/&/g, "&amp;")
		.replace(/"/g, "&quot;")
		.replace(/</g, "&lt;");
}

function parseModReadme(markdown) {
	const text = String(markdown || "").replace(/\r\n/g, "\n");
	const splitAt = text.search(/\n---\n/);
	const en = splitAt === -1 ? text : text.slice(0, splitAt);
	const zh = splitAt === -1 ? "" : text.slice(splitAt + 5);

	const enTitle = /^#\s+(.+)$/m.exec(en)?.[1].trim() || "";
	const description =
		en
			.split("\n")
			.map((line) => line.trim())
			.find(
				(line) =>
					line &&
					!line.startsWith("#") &&
					!line.startsWith("![") &&
					!/^Steam Workshop\b/.test(line),
			) || "";

	const zhHeading = /^#\s+(.+)$/m.exec(zh)?.[1].trim() || "";
	const bilingual = zhHeading.split(/\s+\/\s+/);
	let zhTitle = "";
	if (bilingual.length >= 2) zhTitle = bilingual[0].trim();
	else if (/[\u4e00-\u9fff]/.test(zhHeading)) zhTitle = zhHeading;

	return { enTitle, zhTitle, description };
}

function displayTitle(enTitle, zhTitle, fallback) {
	const en = enTitle || fallback;
	if (zhTitle) return `${en} / ${zhTitle}`;
	return en;
}

function buildTable(pkg) {
	const lines = [
		"| Cover | Folder | Title | Description | Steam |",
		"|-------|--------|-------|-------------|-------|",
	];

	for (const mod of listMods()) {
		const parsed = parseModReadme(readFileSync(join(srcDir, mod, "README.md"), "utf8"));
		const title = displayTitle(parsed.enTitle, parsed.zhTitle, mod);
		const coverSrc = previewUrl(pkg, mod);
		const cover = coverSrc
			? `<img src="${coverSrc}" width="${coverWidth}" alt="${escapeAttr(parsed.enTitle || mod)}">`
			: "";
		const steamId = pkg.mods?.[mod]?.steamId;
		const steam = steamId
			? `[Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=${steamId})`
			: "";

		lines.push(
			`| ${cover} | [${mod}](src/${mod}) | ${escapeCell(title)} | ${escapeCell(parsed.description)} | ${steam} |`,
		);
	}

	return lines.join("\n");
}

function replaceTable(readme, table) {
	const block = `${begin}\n${table}\n${end}`;
	if (readme.includes(begin) && readme.includes(end)) {
		return readme.replace(new RegExp(`${begin}[\\s\\S]*?${end}`), block);
	}

	const heading = /^## Mods[ \t]*$/m.exec(readme);
	if (!heading) {
		throw new Error("README.md is missing a ## Mods section");
	}

	const start = heading.index + heading[0].length;
	const rest = readme.slice(start);
	const next = /^## /m.exec(rest);
	const insertAt = start + (next ? next.index : rest.length);
	return `${readme.slice(0, start)}\n\n${block}\n\n${readme.slice(insertAt)}`;
}

export function writeModsTable() {
	const pkg = loadPkg();
	const prev = existsSync(readmePath) ? readFileSync(readmePath, "utf8").replace(/\r\n/g, "\n") : "";
	if (!prev) throw new Error("Missing README.md");
	const next = replaceTable(prev, buildTable(pkg));
	if (next === prev) {
		console.log("ok    README.md mods table");
		return false;
	}
	writeFileSync(readmePath, next.endsWith("\n") ? next : next + "\n", "utf8");
	console.log("wrote README.md mods table");
	return true;
}

const invoked = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (invoked) writeModsTable();
