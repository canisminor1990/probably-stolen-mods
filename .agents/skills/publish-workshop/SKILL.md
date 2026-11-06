---
name: publish-workshop
description: >-
  发布或更新《深空当铺》创意工坊（AppId 4348910）：steamcmd 上传文件，workshop-desc 分开写英文和中文说明。
  Use when the user asks to publish, upload, or update a Probably Stolen Steam Workshop item or its description.
---

# 发布到创意工坊

只在用户这次明确要求发布或更新时运行 `steamcmd`。游戏里没有上传按钮。创意工坊只对正式版有效。

官方步骤见 [模组指南的创意工坊一节](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.zh-CN.md)。下面是这台机器和这个仓库的具体做法。

## 上传前

1. 玩家能看到的改动已经写入 `<ModVersion>`。没有就先升版本。
2. 在仓库根目录执行 `npm run build` 或该模组的 `build:<kebab-name>`。
3. 确认安装目录 `<游戏>\Mods\<ModInstallName>` 里有 `manifest.xml`、`<AssemblyName>.dll`、`preview.png`。不要有 `psd`、`Reference`、`Description.txt`、游戏 DLL 或 `0Harmony.dll`。`preview.png` 小于 1 MB，并且在模组根目录，不在 `Textures/`。
4. 玩家说明改在 `src/<Mod>/README.md`。跑 `npm run desc` 生成 `Description.txt`，不要另写一套。

本机已安装的同 ID 模组会盖过创意工坊副本。上传成功不等于本机正在跑工坊那一份。

## 更新顺序

文件和说明是两步。游戏先完全退出。

1. 新贴图或新行为还没写进 `<ModVersion>` 就先升版本，再 `npm run build`。只在 README 里加截图、而这一版还没上传过，就不要为截图再加一号。
2. `npm run desc`。`README.md` 里单独成行的 `![标题](https://raw.githubusercontent.com/.../screenshots/x.png)` 会变成 `[img]`。`---` 变成 `[hr][/hr]`。
3. 改 VDF 的 `changenote`，带上版本号，先中文，后英文。
4. `steamcmd` 上传安装目录里的文件。成功标志是 `Committing update...Success.`
5. 停。`steamcmd +login` 会把正在开着的 Steam 客户端顶下线，日志是 `Session Replaced`，而且不会自动重连。这时 `npm run workshop-desc` 会得到 `loggedOn=False` 和 `NoConnection`。不要连着重试。请用户在 Steam 窗口重新登录。
6. 客户端日志重新出现 `Logged On` 之后，再跑 `npm run workshop-desc -- <PascalName>`。它不上传 DLL 和贴图。按 `[hr][/hr]` 拆开：英文写到 `english`，标题用英文名；中文写到 `schinese`，标题是 `中文名 / English Name`。两边都打印 `ok` 才算写上。

不要同时跑 `steamcmd` 和 `workshop-desc`。VDF 里的单段 `description` 只是文件上传时的默认说明；语言页以第 6 步为准。

`scripts/workshop-desc` 必须自带空的 `Directory.Build.props` 和 `Directory.Build.targets`，免得吃到仓库根上的游戏引用。根上的 `ImplicitUsings` 关掉之后，`Directory` 会指到游戏程序集里的命名空间，工具编不过。用 Facepunch.Steamworks 2.3.3 自带的 `steam_api64.dll`。不要换成游戏目录里那份，那份没有 `SteamAPI_Init`。

## 说明和编号

`package.json` 的 `steam` 是作者页和 AppId。`mods.<PascalName>` 有 `workshopTitle`，已发布的还有 `steamId`。

- `npm run desc`：README 转成 `Description.txt`，并刷新根 README 的模组表。
- `npm run steam-ids`：抓 `https://steamcommunity.com/id/<vanity>/myworkshopfiles/?appid=4348910`，按标题对上模组，把 `steamId` 写回 `package.json` 和模组表。用 `curl.exe -k`。不要加 `browsefilter=mysubmissions`。不要用需要 Web API key 的 `QueryFiles`。
- `npm run workshop-desc`：只更新已有 `steamId` 的物品。用户没要求更新创意工坊时不要跑。

## VDF

每个模组一个文件，放在仓库根目录，名字用清单 ID，例如 `storage_themes.vdf`。

```text
"workshopitem"
{
	"appid"		"4348910"
	"publishedfileid"		"0"
	"contentfolder"		"<游戏>\\Mods\\<ModInstallName>"
	"previewfile"		"<游戏>\\Mods\\<ModInstallName>\\preview.png"
	"visibility"		"0"
	"title"		"中文名 / English Name"
	"description"		"<Description.txt 的全文>"
	"changenote"		"1.1.0：中文变更。 English change."
}
```

- `contentfolder` 是安装目录，不是 `src/`。
- 路径里的反斜杠写成 `\\`。说明文本里的双引号要转义。
- `publishedfileid` 为 `0` 时创建新物品。`steamcmd` 会把新 ID 写回这个文件，之后用同一个文件就是更新。不要丢掉写回后的 ID。
- `visibility`：`0` 公开，`1` 仅好友，`2` 隐藏。用户说发布或更新、又没指定范围时用 `0`。
- `title` 与清单 `<Name>` 一致。
- `changenote` 带上版本号，先中文，后英文。

已发布的例子：`storage_themes.vdf`，物品 `3830120722`。

## 上传

`steamcmd` 在 `C:\steamcmd\steamcmd.exe`。用已缓存的登录，不要带密码，也不要打开 `config.vdf`：

```text
C:\steamcmd\steamcmd.exe +login canisminor1990 +workshop_build_item "<仓库>\<mod_id>.vdf" +quit
```

成功的标志是 `Committing update...Success.`。前面如果出现 `Info request for AppId 7 returned error Timeout`，只要后面有 Success 就忽略。

退出码 `7` 且输出是 `Update complete, launching...` 时，steamcmd 刚更新完自己。把同一条命令再跑一次。

登录失败就停。请用户在可见窗口里执行一次 `steamcmd +login canisminor1990`。不要查找或编造密码。

第一次上传若提示法律协议，让用户打开 https://steamcommunity.com/sharedfiles/workshoplegalagreement 接受后再传。

## 上传后

把页面地址告诉用户：

```text
https://steamcommunity.com/sharedfiles/filedetails/?id=<publishedfileid>
```

`steamcmd` 写回了新的 `publishedfileid` 时，保留该 VDF。用户要求提交时再把它和版本号一起提交。用户没要求就不要提交。

新物品发布后，把 `steamcmd` 写回的 `publishedfileid` 放进 `package.json` 的 `mods.<PascalName>.steamId`，再跑 `npm run steam-ids` 或 `npm run index`。不要手改根 README 的模组表。订阅者要重开游戏才会拿到新文件。
