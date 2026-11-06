using System.Text.Json;
using Steamworks;
using Steamworks.Ugc;

const int descriptionMax = 8000;

var root = Environment.GetEnvironmentVariable("PS_MODS_ROOT");
var names = new List<string>();
for (var i = 0; i < args.Length; i++)
{
	if (args[i] == "--root" && i + 1 < args.Length)
	{
		root = args[++i];
		continue;
	}
	if (!args[i].StartsWith("-", StringComparison.Ordinal))
		names.Add(args[i]);
}

root = Path.GetFullPath(root ?? System.IO.Directory.GetCurrentDirectory());
var pkgPath = Path.Combine(root, "package.json");
if (!File.Exists(pkgPath))
{
	Console.Error.WriteLine("Missing package.json. Pass --root <mods workspace>.");
	return 1;
}

var pkg = JsonDocument.Parse(File.ReadAllText(pkgPath));
var appId = pkg.RootElement.GetProperty("steam").GetProperty("appId").GetUInt32();
var mods = pkg.RootElement.GetProperty("mods");
var srcDir = Path.Combine(root, "src");
if (names.Count == 0)
{
	foreach (var folder in System.IO.Directory.GetDirectories(srcDir))
	{
		var name = Path.GetFileName(folder);
		if (mods.TryGetProperty(name, out var meta) && meta.TryGetProperty("steamId", out _))
			names.Add(name);
	}
	names.Sort(StringComparer.Ordinal);
}

var jobs = new List<Job>();
foreach (var name in names)
{
	if (!mods.TryGetProperty(name, out var meta) || !meta.TryGetProperty("steamId", out var idEl))
	{
		Console.Error.WriteLine($"skip  {name}: no mods.steamId");
		continue;
	}
	if (!ulong.TryParse(idEl.GetString(), out var fileId) || fileId == 0)
	{
		Console.Error.WriteLine($"skip  {name}: bad steamId");
		continue;
	}

	var descPath = Path.Combine(srcDir, name, "Description.txt");
	if (!File.Exists(descPath))
	{
		Console.Error.WriteLine($"skip  {name}: missing Description.txt (run npm run desc)");
		continue;
	}

	var parts = SplitLocalized(File.ReadAllText(descPath));
	if (parts.English.Length == 0 || parts.Chinese.Length == 0)
	{
		Console.Error.WriteLine($"skip  {name}: Description.txt needs english + [hr] + chinese");
		continue;
	}
	if (parts.English.Length > descriptionMax || parts.Chinese.Length > descriptionMax)
	{
		Console.Error.WriteLine($"skip  {name}: a language is over {descriptionMax} characters");
		continue;
	}

	var titles = TitlesFromReadme(Path.Combine(srcDir, name, "README.md"), parts);
	jobs.Add(new Job(name, fileId, parts.English, parts.Chinese, titles.English, titles.Chinese));
}

if (jobs.Count == 0)
{
	Console.Error.WriteLine("No workshop descriptions to update.");
	return 1;
}

try
{
	SteamClient.Init(appId);
}
catch (Exception ex)
{
	Console.Error.WriteLine("SteamAPI init failed. Open Steam, log in as the workshop author, and quit Probably Stolen.");
	Console.Error.WriteLine(ex.Message);
	return 1;
}

try
{
	if (!SteamClient.IsValid)
	{
		Console.Error.WriteLine("Steam is not running or this account cannot use app " + appId + ".");
		return 1;
	}

	Console.WriteLine($"Steam {SteamClient.SteamId}  app {appId}");
	var failed = 0;
	foreach (var job in jobs)
	{
		if (!await PushLanguage(job, "english", job.EnglishTitle, job.English))
			failed += 1;
		if (!await PushLanguage(job, "schinese", job.ChineseTitle, job.Chinese))
			failed += 1;
	}
	return failed == 0 ? 0 : 1;
}
finally
{
	SteamClient.Shutdown();
}

static async Task<bool> PushLanguage(Job job, string language, string title, string description)
{
	Console.Write($"  {job.Name} {language} ({title}) … ");
	var editor = new Editor(job.FileId)
		.InLanguage(language)
		.WithDescription(description)
		.WithChangeLog($"Update {language} description");
	if (!string.IsNullOrWhiteSpace(title))
		editor = editor.WithTitle(title);
	var result = await editor.SubmitAsync();

	if (result.Success)
	{
		Console.WriteLine("ok");
		return true;
	}

	Console.WriteLine(result.Result);
	return false;
}

static Localized SplitLocalized(string bbcode)
{
	var text = bbcode.Replace("\r\n", "\n").Trim();
	var split = text.Split(new[] { "[hr][/hr]" }, 2, StringSplitOptions.None);
	if (split.Length < 2)
		return new Localized("", "");
	return new Localized(split[0].Trim(), split[1].Trim());
}

static Localized TitlesFromReadme(string readmePath, Localized parts)
{
	var en = HeadingFromBbcode(parts.English);
	var zh = HeadingFromBbcode(parts.Chinese);
	if (File.Exists(readmePath))
	{
		var md = File.ReadAllText(readmePath).Replace("\r\n", "\n");
		var cut = md.IndexOf("\n---\n", StringComparison.Ordinal);
		var enMd = cut < 0 ? md : md[..cut];
		var zhMd = cut < 0 ? "" : md[(cut + 5)..];
		en = HeadingFromMarkdown(enMd) ?? en;
		zh = HeadingFromMarkdown(zhMd) ?? zh;
	}
	en ??= "";
	zh ??= "";
	if (!string.IsNullOrEmpty(en) && !string.IsNullOrEmpty(zh) && !zh.Contains(" / "))
		zh = $"{zh} / {en}";
	return new Localized(en, zh);
}

static string? HeadingFromMarkdown(string text)
{
	var match = System.Text.RegularExpressions.Regex.Match(text, @"^#\s+(.+)$", System.Text.RegularExpressions.RegexOptions.Multiline);
	return match.Success ? match.Groups[1].Value.Trim() : null;
}

static string? HeadingFromBbcode(string text)
{
	var match = System.Text.RegularExpressions.Regex.Match(text, @"\[h1\](.*?)\[/h1\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	return match.Success ? match.Groups[1].Value.Trim() : null;
}

internal readonly record struct Job(string Name, ulong FileId, string English, string Chinese, string EnglishTitle, string ChineseTitle);
internal readonly record struct Localized(string English, string Chinese);
