import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { writeModsTable } from "./readme-index.mjs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const pkgPath = join(root, "package.json");
const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
const steam = pkg.steam || {};
const vanity = steam.vanity || "canisminor";
const appId = steam.appId || 4348910;
const srcDir = join(root, "src");

function httpGet(url) {
	try {
		const res = execFileSync(
			process.platform === "win32" ? "curl.exe" : "curl",
			["-k", "-sL", "-A", "Mozilla/5.0", "-H", "Accept: text/html", "--max-time", "30", url],
			{ encoding: "utf8", maxBuffer: 20 * 1024 * 1024 },
		);
		if (typeof res === "string" && res.length > 200) return res;
	} catch {
		// fall through to fetch
	}
	return null;
}

async function httpGetAsync(url) {
	const viaCurl = httpGet(url);
	if (viaCurl) return viaCurl;
	try {
		const res = await fetch(url, { headers: { "User-Agent": "Mozilla/5.0" }, redirect: "follow" });
		const text = await res.text();
		if (text && text.length > 200) return text;
	} catch {
		// both transports failed
	}
	throw new Error(`Failed to download ${url}`);
}

function parseWorkshopItems(html) {
	const items = [];
	const re = /id="sharedfile_(\d+)"[\s\S]{0,4000}?workshopItemTitle[^>]*>([^<]+)/g;
	for (const match of html.matchAll(re)) {
		items.push({ id: match[1], title: match[2].trim() });
	}
	return items;
}

function norm(text) {
	return String(text || "")
		.toLowerCase()
		.replace(/\s+/g, " ")
		.trim();
}

function manifestName(mod) {
	const path = join(srcDir, mod, "manifest.xml");
	if (!existsSync(path)) return "";
	const match = /<Name>([^<]+)<\/Name>/.exec(readFileSync(path, "utf8"));
	return match ? match[1].trim() : "";
}

function spacedName(mod) {
	return mod.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function aliases(mod, meta) {
	const manifest = manifestName(mod);
	const halves = manifest.split(/\s+\/\s+/).map((part) => part.trim());
	return new Set(
		[meta?.workshopTitle, manifest, ...halves, mod, spacedName(mod)]
			.map(norm)
			.filter(Boolean),
	);
}

function listMods() {
	return Object.keys(pkg.mods || {}).filter((name) => existsSync(join(srcDir, name)));
}

async function fetchAllItems() {
	const items = [];
	const seen = new Set();
	for (let page = 1; page <= 20; page += 1) {
		const url =
			page === 1
				? `https://steamcommunity.com/id/${encodeURIComponent(vanity)}/myworkshopfiles/?appid=${appId}`
				: `https://steamcommunity.com/id/${encodeURIComponent(vanity)}/myworkshopfiles/?appid=${appId}&p=${page}`;
		const html = await httpGetAsync(url);
		const pageItems = parseWorkshopItems(html);
		if (pageItems.length === 0) break;
		let added = 0;
		for (const item of pageItems) {
			if (seen.has(item.id)) continue;
			seen.add(item.id);
			items.push(item);
			added += 1;
		}
		if (added === 0 || pageItems.length < 10) break;
	}
	return items;
}

const items = await fetchAllItems();
if (items.length === 0) {
	console.error(`No workshop items found for steamcommunity.com/id/${vanity} app ${appId}`);
	process.exit(1);
}

console.log(`Found ${items.length} workshop item(s) for ${vanity}:`);
for (const item of items) console.log(`  ${item.id}  ${item.title}`);

pkg.mods = pkg.mods || {};
const used = new Set();
let changed = false;

for (const mod of listMods()) {
	const meta = pkg.mods[mod] || {};
	const names = aliases(mod, meta);
	const match = items.find((item) => names.has(norm(item.title)));
	if (!match) {
		console.warn(`No workshop match for ${mod} (tried: ${[...names].join(", ")})`);
		continue;
	}
	used.add(match.id);
	if (meta.steamId !== match.id || meta.workshopTitle !== match.title) {
		pkg.mods[mod] = { ...meta, steamId: match.id, workshopTitle: match.title };
		changed = true;
		console.log(`Set ${mod} → ${match.id} (${match.title})`);
	} else {
		console.log(`Ok  ${mod} → ${match.id}`);
	}

	const readmePath = join(srcDir, mod, "README.md");
	if (existsSync(readmePath)) {
		const prev = readFileSync(readmePath, "utf8");
		const next = prev.replace(
			/(Steam Workshop:.*sharedfiles\/filedetails\/\?id=)\d+/,
			`$1${match.id}`,
		);
		if (next !== prev) writeFileSync(readmePath, next, "utf8");
	}
}

for (const item of items) {
	if (!used.has(item.id)) console.warn(`Unmatched workshop item: ${item.id} ${item.title}`);
}

if (changed) {
	writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n", "utf8");
	console.log("Updated package.json mods");
}

writeModsTable();
