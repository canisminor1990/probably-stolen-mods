---
name: create-mod
description: >-
  在 probably-stolen-mods 仓库里新建或修改《深空当铺 / Probably Stolen》模组：工程、清单、安装、本地化与版本号。
  Use when creating a mod, adding a src project, editing manifest.xml, or building and installing a Probably Stolen mod.
---

# 创建模组

官方行为以游戏和 [probably-stolen-modding](https://github.com/dragonpan2/probably-stolen-modding) 为准。本仓库不使用游戏目录里的 `create-mod.bat`，新模组加在 `src/` 下，共用根目录的 `Directory.Build.props` 和 `Directory.Build.targets`。

仓库应放在游戏目录内（与 `Probably Stolen.exe` 同级的 `probably-stolen-mods`）。`GameDir` 默认是仓库的上一级。换位置时用 `/p:GameDir=` 或环境变量 `GameDir`。

## 新增一个模组

1. 建 `src/<PascalName>/`。`<PascalName>` 同时作为程序集名、命名空间和 `ModInstallName`。
2. 写 `<PascalName>.csproj`、`manifest.xml`、`ModEntry.cs`、`README.md` 和 `preview.png`。`README.md` 先写英文，用一行 `---` 隔开，再写中文。不要手写 `Description.txt`。
3. 在 `probably-stolen-mods.slnx` 的 `/src/` 文件夹里加上这个工程。
4. 在根目录 `package.json` 增加 `build:<kebab-name>`，并在 `mods` 里加一项：`workshopTitle` 用创意工坊标题。已发布的再写 `steamId`。
5. 跑 `npm run desc`。它会生成 `Description.txt`，并刷新根 `README.md` 的模组表。不要手改那张表。

现成例子：`src/StorageThemes`（带贴图和本地化），`src/SmugglerBayTabs`（只有代码和预览图）。

### 工程

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>MyMod</AssemblyName>
    <RootNamespace>MyMod</RootNamespace>
    <ModInstallName>MyMod</ModInstallName>
  </PropertyGroup>
  <ItemGroup>
    <ModFile Include="manifest.xml" />
    <None Remove="preview.png" />
  </ItemGroup>
</Project>
```

有资源时把 `ModFile` 写成 `manifest.xml;Textures\**;Sounds\**;Localization\**;LootTables\**;Books\**`，并 `Exclude="Textures\**\*.psd"`。再用 `None Remove` 排除 `preview.png`、`psd\**`、`Reference\**`。`preview.png` 由共享目标单独复制到安装根目录。

目标框架 `netstandard2.1`、游戏 DLL 引用和 `Private=false` 已在 `Directory.Build.props`。不要把这些 DLL 复制进模组，也不要再引用一份 `0Harmony`。

### 清单

`<ID>` 只用小写字母、数字、`_`、`-`、`.`。`<GameVersions>` 抄本仓库现有模组，游戏更新后再改，不要猜。

```xml
<?xml version="1.0" encoding="utf-8"?>
<Manifest>
  <ID>my_mod</ID>
  <Name>中文名 / English Name</Name>
  <Author>CanisMinor</Author>
  <ModVersion>1.0.0</ModVersion>
  <Description>中文说明。 English description.</Description>
  <GameVersions>
    <Version>0.46</Version>
  </GameVersions>
  <Prerequisites />
  <Assemblies>
    <Assembly>MyMod.dll</Assembly>
  </Assemblies>
  <Capabilities />
</Manifest>
```

`<Capabilities />` 对物品、Harmony、配置、本地化、自定义 UI 足够。只有读写任意文件、网络、进程、动态代码、原生代码，或用字符串反射 Unity / .NET / Harmony 时才声明能力。优先 `typeof(游戏类型)` 和字面方法名。不要 `Harmony.UnpatchAll()` 传 `null`，不要补丁 `ModLoader` 或 `ModTrust*`。

玩家能看到的改动：先看 `<ModVersion>`。功能用次版本（`1.1.0`），修复用修订号（`1.0.1`）。升完再构建。

### 入口

```csharp
namespace MyMod;

public class ModEntry : IMod
{
    internal static ModLog Log { get; private set; }

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Log.Log("My Mod ready.");
    }

    public void OnEnable() { }
    public void OnDisable() { }
}
```

用 `ModLog`，不要用 `Debug.Log`。`[HarmonyPatch]` 由加载器自动应用，不要自己 `PatchAll()`。有原生钩子时用钩子。

设置用 `ModHelper.GetConfig(manifest.ID)`，在 `Init` 里 `Define`。分区标题会原样显示，需要双语时写成 `中文 / English`。标签用 `() => ModHelper.GetLocalized(...)`。只在启动时读取的项加 `requiresRestart: true`。

本地化是 `Localization/zh.csv` 和 `Localization/en.csv`。贴图像素图放 `Textures/<主题>/`，文件名就是游戏里的精灵名，例如 `smuggler_bay_large.png` 只进暗格。源文件 `psd/` 和对照图 `Reference/` 不安装。

玩家看到的说明只写在 `README.md`。`npm run desc` 把它转成 `Description.txt`（BBCode，英文和中文用 `[hr][/hr]` 分开）。这个文件不放进 `ModFile`。格式照 `src/StorageThemes/README.md`。

## 构建与本机验证

在仓库根目录：

```text
npm run build
npm run build:<kebab-name>
```

等价于 `dotnet build probably-stolen-mods.slnx -c Release`。成功后 DLL、清单、`ModFile` 和 `preview.png` 出现在 `<游戏>\Mods\<ModInstallName>`。

然后完全退出游戏再启动。模组菜单里启用；本地开发可开开发者模式，否则哈希变化后要点一次审核并启用。`dev.txt` 放在游戏目录可开 F8 / F9，不要放进模组包。

发布前在 F8 运行 `mod-scan <模组 ID>`，确认声明的能力盖住扫描结果，且没有拒绝项。
