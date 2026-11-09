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
