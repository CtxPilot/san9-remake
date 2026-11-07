# 三国志 9 重制

使用 Godot 与 C# 重建《三国志 IX》的策略体验。原作提供研究和设计基线；产品采用的行为由规格定义，代码使用独立的现代实现。

## 许可与版权

本仓库自行编写的重制代码、工具、测试及文档采用 GNU GPL v3.0（`GPL-3.0-only`）开源，详见 [LICENSE](LICENSE)；第三方组件与字体遵循各自许可证。开源范围不含原版程序、游戏资源、转换后的原作资源及私有研究样本。

本项目为非官方重制。[《三国志 IX》](https://store.steampowered.com/app/628050/)的名称、商标及原作内容归光荣特库摩（KOEI TECMO）及相关权利人所有，本项目与其无隶属或背书关系。GPL 许可不授予原作内容的使用或再分发权；使用原作资源须自行准备合法取得的游戏副本。

## 当前状态

核心玩法 G00–G10 已完成阶段验收，覆盖经营、人事、治理、行军、战斗、外交、计略、人物生命周期、事件与结局。阶段结果见[历史验收索引](docs/history/README.md)，当前规则见[行为规格](specs/behavior/README.md)。历史通过记录仅证明其标注的版本。

客户端已接入主菜单与正式新局、玩家势力及开局设定、地图与情报浏览、经营／人事／军事／计略／外交／军团操作、逐日推进、存读档及事件和战斗演出。启动和剧本开场影片、剧情影片及场景音乐已有接入；可选媒体按内容声明与用户设置播放。具体接线、未完成项及画面／试听验收边界由[表现层计划](specs/plans/presentation.md)和[UI 文档](docs/ui/README.md)维护，接线完成不等于所有画面已经验收。

AI 采用[原版决策基线](specs/original-ai/README.md)：OAI-01～05 已接入经营、人事、组织、军事、计略、外交与公共响应；OAI-06 原作对照和十年全图验收尚未完成，G11／G12 不能据此标为完成。旧自主增强方案已归档，不约束当前开发。

人才提拔培养为独立支线；玩家录像已延期并撤下。其他未采用和后续范围见[路线图](specs/plans/roadmap.md)。存档、精确恢复和显式命令脚本继续维护。

## 快速开始

在仓库根目录执行。需要 .NET 10 正式版 SDK、Godot 4.7.2 Mono；研究侧 Python 工具按各自入口准备依赖。

```powershell
dotnet build San9Remake.slnx
dotnet test San9Remake.slnx
```

构建和测试可独立使用；`dotnet test` 自带构建。测试职责、局部入口及独立 Headless 测试的触发条件见[测试说明](tests/README.md)。

### 准备内容

原版素材、转换包及字体不随仓库分发。开发环境将 `LXGWWenKaiMonoGBLite-Medium.ttf` 放入 `src/San9.Client.Godot/assets/`，许可证见[字体说明](src/San9.Client.Godot/assets/README.md)。

选择包含 `San9PK.exe`、S9 数据及剧本的同一个原版目录；当前支持已登记且通过指纹校验的繁中 PK 1.0.1.0。本地样本通常位于 `private/game-files/`：

```powershell
dotnet run --project src/San9.Tools -- import-textures --game-dir "private/game-files" --output "local/generated/content/shared/textures"
dotnet run --project src/San9.Tools -- import-content --game-dir "private/game-files" --output "local/generated/content"
dotnet run --project src/San9.Tools -- verify-content --content-root "local/generated/content"
```

音频和影片另按[音频准备](docs/audio-preparation.md)及[电影准备](docs/movie-preparation.md)处理；完整流程、最小转换范围与发行版内置准备入口见[内容管线](docs/client-content.md)。普通游戏加载只消费转换包。

### 运行客户端

用 Godot Mono 打开 `src/San9.Client.Godot/project.godot`，构建 C# 并运行主场景。无参进入开局菜单；直接进入指定剧本和玩家势力：

```powershell
& "<Godot Mono可执行文件>" --path "src/San9.Client.Godot" -- --scenario scenario-4 --player-force 6
```

中键拖动、滚轮缩放，左键选择、右键查看情报；小地图和设施名单可定位，F3 打开调试面板。操作约定见[战略界面](docs/ui/strategy-ui.md)。

### 无界面运行

```powershell
dotnet run --project src/San9.Headless -- verify --fixture --xuns 36 --output local/generated/verification/headless/public
```

玩家读档、精确恢复、JSONL 命令脚本、报告与退出码见[Headless 使用说明](docs/headless.md)。

### 桌面发布

推送 `v*` 标签或手动运行 GitHub Actions 的 **Desktop release**，构建 Windows x64、Linux x64、macOS ARM64 发布包。发布包准备字体并内置内容转换入口，不附带原版素材或转换内容；详见[桌面导出与自动发布](docs/client-content.md#桌面导出与自动发布)。

## 工程结构

| 目录 | 职责 |
| --- | --- |
| `src/San9.Core/` | 确定性领域状态与规则 |
| `src/San9.Application/` | 会话、用例、查询与表现投影 |
| `src/San9.Content/` | 独立内容契约与组合校验 |
| `src/San9.Importer.San9PkTc101/` | 原版识别、解析及转换 |
| `src/San9.Rendering/` | 引擎无关的地图合成、坐标与动画 |
| `src/San9.Client.Godot/` | 输入、UI、声音及绘制 |
| `src/San9.Headless/`、`src/San9.Tools/` | 无界面运行、转换与静态导出 |
| `tests/` | 自动化验证 |
| `local/generated/` | 忽略的转换包、缓存与验证产物 |

Core 与 Application 不依赖 Godot 或原版格式；普通客户端构建不引用 Importer，正式导出的内容准备入口可调用 Importer。分层细节见[架构决策](specs/decisions/DEC-PROJECT-0001-layered-architecture.md)。

## 文档入口

- [文档总览](docs/README.md)：按问题查找使用说明、实现约定、规格与历史记录。
- [协作规则](AGENTS.md)、[工程流程](docs/development/engineering-workflow.md)与[测试说明](tests/README.md)。
- [规格索引](specs/README.md)、[行为规格](specs/behavior/README.md)、[设计决策](specs/decisions/README.md)与[路线图](specs/plans/roadmap.md)。
- [研究入口](research/README.md)、[研究结论](research/findings/README.md)与[来源登记](research/SOURCES.md)：证据与推断不自动成为产品要求。
- [本地私有文件规则](private/README.md)与[本地目录](local/README.md)。
