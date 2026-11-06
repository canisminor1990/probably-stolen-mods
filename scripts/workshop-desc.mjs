import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const project = join(root, "scripts", "workshop-desc", "WorkshopDesc.csproj");
const exe = join(root, "scripts", "workshop-desc", "bin", "Release", "WorkshopDesc.exe");
const requested = process.argv.slice(2).filter((arg) => !arg.startsWith("-"));

function run(command, args, options = {}) {
	const result = spawnSync(command, args, {
		cwd: root,
		stdio: "inherit",
		shell: false,
		...options,
	});
	if (result.status !== 0) process.exit(result.status ?? 1);
}

const descArgs = ["scripts/convert-docs.mjs", "desc", ...requested];
run(process.execPath, descArgs);

console.log("\n== workshop-desc ==");
run("dotnet", ["build", project, "-c", "Release", "--nologo"]);
if (!existsSync(exe)) {
	console.error(`Missing ${exe}`);
	process.exit(1);
}

const pkg = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
const appId = pkg.steam?.appId;
if (!appId) {
	console.error("package.json steam.appId is missing");
	process.exit(1);
}
writeFileSync(join(dirname(exe), "steam_appid.txt"), String(appId), "utf8");

run(exe, ["--root", root, ...requested], { cwd: dirname(exe) });
