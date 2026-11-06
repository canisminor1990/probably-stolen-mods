# Agents

CanisMinor 的《深空当铺 / Probably Stolen》模组仓库。每个模组是 `src/` 下的一个工程，Release 构建会装进游戏的 `Mods` 文件夹。

做事前先读对应技能：

- 新建或修改模组：[.agents/skills/create-mod/SKILL.md](.agents/skills/create-mod/SKILL.md)
- 发布或更新创意工坊：[.agents/skills/publish-workshop/SKILL.md](.agents/skills/publish-workshop/SKILL.md)

玩家说明只改 `src/<Mod>/README.md`（英文、`---`、中文）。不要手改 `Description.txt`，也不要手改根 `README.md` 里 `<!-- mods-table -->` 那一块。改完跑 `npm run desc`。工坊编号在 `package.json` 的 `mods` 里，用 `npm run steam-ids` 从创意工坊页面回填。

## 始终遵守

- 用户没有明确要求时，不提交、不推送、不运行 `steamcmd`，也不运行 `npm run workshop-desc`。
- 创意工坊分两步，不要同时跑。`steamcmd` 上传模组文件。`npm run workshop-desc` 才把英文和中文说明分开写上。`steamcmd` 登录会把 Steam 客户端顶下线（`Session Replaced`），客户端不会自己重连。文件传完后，等用户在 Steam 窗口重新登录，再跑 `workshop-desc`。
- 游戏引用和 `0Harmony` 保持 `Private=false`。安装包里不要出现游戏 DLL、Harmony DLL、`psd`、`Reference`。
- `preview.png` 放在模组工程旁边，安装后与 `manifest.xml` 同级。放进 `Textures/` 会被当成精灵。
- `Description.txt` 只留在源码里，不打进安装包。
- 清单 `<Name>` 和 `<Description>` 先中文，后英文。作者是 `CanisMinor`。
- 玩家能在游戏里看到的改动（新贴图、新行为）先核对 `<ModVersion>`。还没改就先升版本，再构建。只改 README 截图、不再另升一号，只要当前版本还没发过。
- DLL 或 PNG 改完后，必须完全退出游戏再启动。游戏开着时加载的是旧 DLL。
- 本机 `Mods/<ModInstallName>` 与创意工坊物品的 `<ID>` 相同时，游戏用本地副本。
- 删除用 `trash`。不要用 `rm`、`rmdir`、`del /s`、`Remove-Item -Recurse`。
- 不要读取 Steam 的 `config.vdf`，也不要打印登录凭据。
