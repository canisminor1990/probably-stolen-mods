import { existsSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { writeModsTable } from "./readme-index.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const pkg = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
const srcDir = join(root, "src");

function listMods() {
	return readdirSync(srcDir, { withFileTypes: true })
		.filter((entry) => entry.isDirectory())
		.map((entry) => entry.name)
		.filter((name) => existsSync(join(srcDir, name, "README.md")));
}

function steamLine(mod) {
	const meta = pkg.mods?.[mod];
	if (!meta?.steamId) return "";
	const title = meta.workshopTitle || mod;
	const url = `https://steamcommunity.com/sharedfiles/filedetails/?id=${meta.steamId}`;
	return `Steam Workshop: [${title}](${url}) · [Description.txt](Description.txt) (BBCode)`;
}

function looksLikePath(text) {
	return /[\\/]|\.\w{2,4}$/.test(text);
}

function mdInlineToBbcode(text) {
	let s = text;
	s = s.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, "[img]$2[/img]");
	s = s.replace(/\[([^\]]+)\]\(([^)]+)\)/g, "[url=$2]$1[/url]");
	s = s.replace(/`([^`]+)`/g, "[i]$1[/i]");
	s = s.replace(/\*\*(.+?)\*\*/g, "[b]$1[/b]");
	s = s.replace(/(^|[^*\n])\*([^*\n]+)\*/g, "$1[i]$2[/i]");
	return s;
}

function bbcodeInlineToMd(text) {
	let s = text;
	s = s.replace(/\[url=([^\]]+)\]([\s\S]*?)\[\/url\]/gi, "[$2]($1)");
	s = s.replace(/\[b\]([\s\S]*?)\[\/b\]/gi, "**$1**");
	s = s.replace(/\[i\]([\s\S]*?)\[\/i\]/gi, (_, inner) =>
		looksLikePath(inner) ? `\`${inner}\`` : `*${inner}*`,
	);
	return s;
}

export function markdownToBbcode(markdown) {
	const lines = markdown.replace(/\r\n/g, "\n").split("\n");
	const out = [];
	let i = 0;

	const flushList = (items, ordered) => {
		if (items.length === 0) return;
		const tag = ordered ? "olist" : "list";
		out.push(`[${tag}]`);
		for (const item of items) out.push(`[*]${mdInlineToBbcode(item)}`);
		out.push(`[/${tag}]`);
	};

	while (i < lines.length) {
		const line = lines[i];

		if (/^Steam Workshop\b/.test(line)) {
			i += 1;
			continue;
		}

		if (/^```/.test(line)) {
			i += 1;
			const body = [];
			while (i < lines.length && !/^```/.test(lines[i])) {
				body.push(lines[i]);
				i += 1;
			}
			if (i < lines.length) i += 1;
			out.push("[code]" + body.join("\n") + "[/code]");
			continue;
		}

		if (/^---+$/.test(line.trim())) {
			out.push("[hr][/hr]");
			i += 1;
			continue;
		}

		const h = /^(#{1,3})\s+(.+)$/.exec(line);
		if (h) {
			const level = Math.min(h[1].length, 3);
			out.push(`[h${level}]${mdInlineToBbcode(h[2].trim())}[/h${level}]`);
			i += 1;
			continue;
		}

		const img = /^!\[([^\]]*)\]\(([^)]+)\)$/.exec(line.trim());
		if (img) {
			out.push(`[img]${img[2]}[/img]`);
			i += 1;
			continue;
		}

		const ul = /^-\s+(.+)$/.exec(line);
		if (ul) {
			const items = [];
			while (i < lines.length) {
				const m = /^-\s+(.+)$/.exec(lines[i]);
				if (!m) break;
				items.push(m[1]);
				i += 1;
			}
			flushList(items, false);
			continue;
		}

		const ol = /^\d+\.\s+(.+)$/.exec(line);
		if (ol) {
			const items = [];
			while (i < lines.length) {
				const m = /^\d+\.\s+(.+)$/.exec(lines[i]);
				if (!m) break;
				items.push(m[1]);
				i += 1;
			}
			flushList(items, true);
			continue;
		}

		out.push(mdInlineToBbcode(line));
		i += 1;
	}

	return out.join("\n").replace(/\n{3,}/g, "\n\n").replace(/^\n+/, "").replace(/\n+$/, "\n");
}

export function bbcodeToMarkdown(bbcode, workshopLine = "") {
	let text = bbcode.replace(/\r\n/g, "\n");

	text = text.replace(/\[code\]([\s\S]*?)\[\/code\]/gi, (_, body) => `\n\`\`\`\n${body.trim()}\n\`\`\`\n`);
	text = text.replace(/\[img\](.*?)\[\/img\]/gi, "![]($1)");
	text = text.replace(/\[hr\]\[\/hr\]/gi, "\n---\n");
	text = text.replace(/\[h([123])\]([\s\S]*?)\[\/h\1\]/gi, (_, n, title) => `${"#".repeat(Number(n))} ${title.trim()}`);

	text = text.replace(/\[olist\]([\s\S]*?)\[\/olist\]/gi, (_, body) => {
		const items = [...body.matchAll(/\[\*\]([^\[]*)/g)].map((m) => m[1].replace(/^\s+|\s+$/g, ""));
		return items.map((item, idx) => `${idx + 1}. ${item}`).join("\n");
	});
	text = text.replace(/\[list\]([\s\S]*?)\[\/list\]/gi, (_, body) => {
		const items = [...body.matchAll(/\[\*\]([^\[]*)/g)].map((m) => m[1].replace(/^\s+|\s+$/g, ""));
		return items.map((item) => `- ${item}`).join("\n");
	});

	text = bbcodeInlineToMd(text);

	const lines = text.split("\n");
	const out = [];
	let inserted = !workshopLine;
	for (const line of lines) {
		out.push(line);
		if (!inserted && /^!\[.*\]\(https?:/.test(line.trim())) {
			out.push("");
			out.push(workshopLine);
			inserted = true;
		}
	}
	if (!inserted && workshopLine) {
		out.splice(2, 0, "", workshopLine);
	}

	return out.join("\n").replace(/\n{3,}/g, "\n\n").replace(/^\n+/, "").replace(/\n+$/, "\n");
}

function writeIfChanged(path, content) {
	const next = content.endsWith("\n") ? content : content + "\n";
	const prev = existsSync(path) ? readFileSync(path, "utf8").replace(/\r\n/g, "\n") : null;
	if (prev === next) return false;
	writeFileSync(path, next, "utf8");
	return true;
}

function convertMod(mod, mode) {
	const dir = join(srcDir, mod);
	const mdPath = join(dir, "README.md");
	const txtPath = join(dir, "Description.txt");

	if (mode === "desc") {
		if (!existsSync(mdPath)) throw new Error(`Missing ${mdPath}`);
		const bb = markdownToBbcode(readFileSync(mdPath, "utf8"));
		const changed = writeIfChanged(txtPath, bb);
		console.log(`${changed ? "wrote" : "ok   "} ${mod}/Description.txt`);
		return;
	}

	if (mode === "readme") {
		if (!existsSync(txtPath)) throw new Error(`Missing ${txtPath}`);
		const md = bbcodeToMarkdown(readFileSync(txtPath, "utf8"), steamLine(mod));
		const changed = writeIfChanged(mdPath, md);
		console.log(`${changed ? "wrote" : "ok   "} ${mod}/README.md`);
		return;
	}

	throw new Error(`Unknown mode ${mode}`);
}

const mode = process.argv[2];
if (mode !== "desc" && mode !== "readme") {
	console.error("Usage: node scripts/convert-docs.mjs desc|readme [ModName...]");
	process.exit(1);
}

const requested = process.argv.slice(3);
const mods = requested.length > 0 ? requested : listMods();
for (const mod of mods) convertMod(mod, mode);
if (mode === "desc") writeModsTable();
