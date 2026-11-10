# 兵种系统及旧版内容包升级版权核查

- 核查日期：2026-11-09；基线：`9f6954a0383d0e3020c9901238355b02c28b0018`。
- 范围：下列 99 个拟提交文件的 Git 暂存差异；本记录自身不计入摘要。`git diff --cached --binary` 的 SHA-256：`e7fb256355d349aee2a88525907d6009c86ec0d10bcce2a148ed9801882ce4ce`。
- 方法：检查暂存路径、文件类型、UTF-8、依赖变化、版权标记及新增文本，并核对迁移实现所使用的资源来源。临时验证目录及本机输入均未纳入暂存。

## 结果与限制

- 暂存文件仅包含 C# 和 Markdown；没有原版程序、图片、音频、字体、剧本／转换包、IDA 数据库、密钥或第三方依赖。测试内容包保留在忽略目录，不随 PR 分发。
- 新增规则、工具及说明为本次编写，沿用仓库已有接口和 TC101 数据解释；升级工具在用户本机读取已转换包并生成副本，不附带原作资源。旧包内的素材及生成副本仍归相关权利人所有。
- 仓库 [README](../../../README.md#许可与版权) 将项目自行编写部分标为 GPL-3.0-only，并明确不授予原作素材许可。未发现本次暂存内容中可确认的未经许可第三方素材或大段反编译代码；这不是全网相似性鉴定，也未审核仓库历史或发行物。
- 未取得或核实光荣特库摩对本项目的授权；名称、商标、作品改编及各地适用法律的问题不能凭本次扫描排除。[美国版权局的游戏说明](https://www.copyright.gov/register/tx-games.html)区分游戏规则与可能受保护的文字、美术表达；具体案件和适用法仍需独立判断。本记录不保证零法律风险。

## 核查文件清单

```text
README.md
research/notes/legacy-remake-content-migration.md
specs/behavior/BEH-WAR-0007-troop-formations-research.md
specs/behavior/README.md
src/San9.Application/AI/MixedDeploymentSelection.cs
src/San9.Application/AI/OriginalAiMilitary.Equipment.cs
src/San9.Application/AI/OriginalAiMilitary.cs
src/San9.Application/AI/OriginalAiResearch.cs
src/San9.Application/AI/OriginalAiTurnRunner.cs
src/San9.Application/DeploymentPreset.cs
src/San9.Application/GameSession.cs
src/San9.Application/Persistence/StrictJson.cs
src/San9.Application/Presentation/DeploymentDraft.Presets.cs
src/San9.Application/Presentation/DeploymentDraft.Tactics.cs
src/San9.Application/Presentation/DeploymentDraft.cs
src/San9.Application/Presentation/DeploymentTargets.cs
src/San9.Application/Presentation/MilitaryTargets.cs
src/San9.Application/Presentation/OccurrenceFeedback.cs
src/San9.Application/Presentation/ResearchDisplay.cs
src/San9.Application/ScenarioSessionDefinition.cs
src/San9.Application/SessionProtocol.cs
src/San9.Client.Godot/UI/Information/FormationBoardView.cs
src/San9.Client.Godot/UI/Start/OpeningSettingsView.cs
src/San9.Client.Godot/UI/Strategy/DeploymentDetachmentsView.cs
src/San9.Client.Godot/UI/Strategy/DeploymentView.cs
src/San9.Client.Godot/UI/System/FunctionsMenuView.cs
src/San9.Client.Godot/UI/System/ResearchView.cs
src/San9.Client.Godot/UI/System/SystemMenuPanel.cs
src/San9.Content/Packages/LegacyImageTableMigration.cs
src/San9.Content/Scenarios/MixedTroopPreset.cs
src/San9.Content/Scenarios/ScenarioAuthoring.cs
src/San9.Core/Domain/Military/DetachmentState.cs
src/San9.Core/Domain/Military/MilitaryRulebook.cs
src/San9.Core/Domain/Military/NavalTrainingDefinition.cs
src/San9.Core/Domain/Military/ResearchProjectDefinition.cs
src/San9.Core/Domain/Military/SiegeEquipmentDefinition.cs
src/San9.Core/Domain/Military/TacticMath.cs
src/San9.Core/Domain/Military/TacticRule.cs
src/San9.Core/Domain/Military/TroopTypeDefinition.cs
src/San9.Core/Domain/Military/UnitFormations.cs
src/San9.Core/Domain/Military/UnitState.cs
src/San9.Core/Domain/NewGameSettings.cs
src/San9.Core/Domain/RuntimeState.cs
src/San9.Core/Simulation/Combat/BattleAftermath.cs
src/San9.Core/Simulation/Combat/CombatExchangeDamage.cs
src/San9.Core/Simulation/Combat/MilitaryCombat.cs
src/San9.Core/Simulation/Combat/MilitaryEngagements.cs
src/San9.Core/Simulation/Combat/MixedTroopCombat.cs
src/San9.Core/Simulation/Combat/TacticCombat.cs
src/San9.Core/Simulation/Construction/FacilityConstruction.cs
src/San9.Core/Simulation/Construction/FacilityDemolition.cs
src/San9.Core/Simulation/DomainEvent.cs
src/San9.Core/Simulation/Events/Historical/SunMarriage.cs
src/San9.Core/Simulation/GameCommand.cs
src/San9.Core/Simulation/Governance/CorpsControl.cs
src/San9.Core/Simulation/Governance/ForceCreation.cs
src/San9.Core/Simulation/Governance/ForceTransfer.cs
src/San9.Core/Simulation/Governance/SettlementAffiliation.cs
src/San9.Core/Simulation/Military/FormationEligibility.cs
src/San9.Core/Simulation/Military/MilitaryOrders.cs
src/San9.Core/Simulation/Military/MilitaryResearch.cs
src/San9.Core/Simulation/Movement/MilitaryArrivalForecast.cs
src/San9.Core/Simulation/Movement/MilitaryMovement.cs
src/San9.Core/Simulation/Movement/MilitaryNavigation.cs
src/San9.Core/Simulation/Movement/MilitaryOrderQuery.cs
src/San9.Core/Simulation/Personnel/PersonnelDefection.cs
src/San9.Core/Simulation/Personnel/PersonnelTransitions.cs
src/San9.Core/Simulation/ResolutionPipeline.cs
src/San9.Core/Simulation/SimulationEngine.cs
src/San9.Core/Simulation/WorldInvariants.cs
src/San9.Core/Simulation/WorldMutation.cs
src/San9.Importer.San9PkTc101/Packages/LegacyOpeningUpgrade.cs
src/San9.Importer.San9PkTc101/Packages/LegacyPresentationUpgrade.cs
src/San9.Importer.San9PkTc101/Packages/LegacyScenarioDataUpgrade.cs
src/San9.Importer.San9PkTc101/Packages/LegacyScenarioStateUpgrade.cs
src/San9.Importer.San9PkTc101/Packages/LegacyTextureSkinUpgrade.cs
src/San9.Importer.San9PkTc101/Packages/TexturePackageImporter.cs
src/San9.Importer.San9PkTc101/Scenarios/ConstructionContentCompiler.cs
src/San9.Importer.San9PkTc101/Scenarios/MilitaryContentCompiler.cs
src/San9.Importer.San9PkTc101/Scenarios/ScenarioContentCompiler.cs
src/San9.Tools/LegacyContentUpgrade.cs
src/San9.Tools/Program.cs
tests/San9.Application.Tests/DeploymentTacticConfigurationTests.cs
tests/San9.Application.Tests/OriginalAiActions.ResearchTests.cs
tests/San9.Application.Tests/OriginalAiMilitary.MixedDeploymentTests.cs
tests/San9.Content.Tests/LegacyImageTableMigrationTests.cs
tests/San9.Core.Tests/DetachmentAllocationTests.cs
tests/San9.Core.Tests/FacilityDemolitionTests.cs
tests/San9.Core.Tests/MilitaryCombatTests.cs
tests/San9.Core.Tests/MilitaryMovementTests.cs
tests/San9.Core.Tests/ResearchProjectTests.cs
tests/San9.Core.Tests/SunMarriageTests.cs
tests/San9.Core.Tests/TacticCombatTests.cs
tests/San9.Importer.San9PkTc101.Tests/ConstructionContentCompilerTests.cs
tests/San9.Importer.San9PkTc101.Tests/ImportOperationTests.cs
tests/San9.Importer.San9PkTc101.Tests/LegacyPresentationUpgradeTests.cs
tests/San9.Importer.San9PkTc101.Tests/LegacyScenarioStateUpgradeTests.cs
tests/San9.Importer.San9PkTc101.Tests/LegacyTextureSkinUpgradeTests.cs
tests/San9.Importer.San9PkTc101.Tests/MilitaryContentCompilerTests.cs
```

## 运行验证后的追加变更（2026-11-09）

追加修改仅涉及 `DeploymentDetachmentsView.cs`、`DeploymentTacticMenu.cs` 与 `DeploymentView.cs`：调整自有界面样式、类别名称和帮助文字，沿用仓库现有主题与控件；未引入图片、字体、游戏资源、第三方代码或依赖。运行时临时下载的 Godot 编辑器与字体、测试存档和内容包均不进入提交。此前原版权利和授权不确定性的说明继续适用。

## 阵形列表提示与预览补充（2026-11-09）

后续变更仅增加自有阵位格子绘制、解锁提示及相关测试，使用已存在的阵位数据，不引入图片或其他第三方素材。验证使用隔离的本地升级内容包与存档，均不包含在提交中；原版权利和授权的限制仍同前述。

## 武将阵形页与说明补充（2026-11-09）

新增阵形说明由本次编写并依据项目实际规则描述；武将阵形页和引擎检查复用仓库现有控件及玩家本机转换内容，不包含原作图像、文本或测试内容包。原版权利与授权的限制继续适用。

## 装备系统工作区复核（2026-11-09）

检查当前装备改动及新增未跟踪文件：代码、中文规格和 Godot 文本场景均为本次编写；测试使用本机旧内容包，输入与生成包均位于提交范围之外。新装备名称、数值和界面由本项目定义，参考作品仅用于玩法构思，未引入参考作品的程序、图片、音频、字体或原文。当前清单和暂存状态仍可能变化，提交前须重新检查暂存差异及文件类型；原版权利人授权、商标与改编问题仍未核实，不能据此保证零法律风险。

2026-11-10 补核：[光荣特库摩公告](https://www.koeitecmo.co.jp/news/docs/news_20250627.pdf)说明《三國志曹操伝》相关新作品采用 IP 许可。该公告不能证明本项目已获授权，也不能单独决定本项目的法律性质；公开 PR 前仍需确认权利与项目维护者的接受条件。旧内容包和游戏运行截图可能包含受保护素材，仅用于本机验证，不加入 Git。

装备系统当前暂存范围共 97 个文件，包含 92 个 C# 文件、4 个 Markdown 文件和 1 个 Godot 文本场景；未发现二进制文件、内容包或截图。排除本审核记录后的 `git diff --cached --binary` SHA-256 为 `db4bceeb7509f8d0fae7947aba9adf626269b90ad61c61a000301daa56fdfdea`。这是路径与差异范围核对，不是对原版权利或全部源码相似性的法律鉴定；后续更改须重新核对。

后续装备迁移修正仅涉及类别校验、冲突拒绝、升级报告、自编测试和使用说明；本机验证内容及产生的报告仍位于忽略目录。该修正不引入外部素材或依赖，前述授权限制仍然适用。

玉玺信望修正仅调整仓库现有计算与装备状态的联动，并补充自编测试和行为规格；未引入原作文本、素材或第三方程序。该核查不改变上述权利与授权限制。

装备兵法引用的后续校验及其测试为本项目自编代码，不包含测试内容包或第三方素材；原版权利人授权仍未核实。

非服役武将的旧装备迁移修正及对应测试和规格为本项目自编；验证使用的旧内容包、生成包和游戏画面不纳入提交。前述授权限制仍然适用。

坐骑及宝物固定等级的状态校验和测试为本项目自编，不引入外部文本、素材或依赖；上述原版权利与授权限制不变。

旧宝物兵法引用校验和主副武器不兼容提示测试均为本项目自编，测试数据为虚构内容，不包含本机内容包或第三方素材；原版权利与授权限制不变。

## PR 当前完整差异范围复核（2026-11-10）

以 PR 基线 `9f6954a0383d0e3020c9901238355b02c28b0018` 和本地提交 `0db6910b` 对照，共 186 个变更文件：177 个 C# 文件、7 个 Markdown 文件、2 个 Godot 文本场景；`git diff --numstat` 未显示二进制差异，也没有依赖清单变化。排除此审核记录后的完整二进制差异 SHA-256 为 `72c1e4741d0ce0d51332eda581acc2405cfa62c1ba4e5b97718dc81f3f7d084f`。这项复核确认了待提交路径和文件类型，不能证明外部权利授权，也不替代对历史代码或全部相似内容的法律鉴定；后续修改须重新复核。
