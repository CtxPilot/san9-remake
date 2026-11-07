# 历史验收与设计记录

这里只保留阶段退出证据、重要反例和有对照条件的测量。版本、命令、测试计数与限制均指当时基线，输出路径不保证仍存在；当前使用入口见[文档总览](../README.md)。普通修复过程由仍保留的Git历史追溯；退役进度快照、重复审计及旧方案详解已清理，不保证每份旧过程文档仍可检出。

## 核心玩法阶段

| 范围 | 记录 |
| --- | --- |
| 时间、命令与持久化 | [G01](stages/g01-verification.md)、[G02](stages/g02-verification.md) |
| 经营、人事与治理，含底座审计收口 | [G03](stages/g03-verification.md)、[G04](stages/g04-verification.md) |
| 行军、战斗与完整军事手段 | [G05](stages/g05-verification.md)、[G06](stages/g06-verification.md)、[G07](stages/g07-verification.md) |
| 外交与计略 | [G08](stages/g08-verification.md) |
| 人物生命周期与单挑 | [G09](stages/g09-verification.md) |
| 事件、终局与结局 | [G10阶段收口](stages/g10-verification.md)、[事件覆盖核对](stages/g10-event-coverage.md) |
| 原版AI | [OAI-01～05阶段出口](stages/original-ai-phase-exits.md) |

历史通过只证明所列版本；录像已撤下，不恢复其日常要求。阶段目标与后续范围由[路线图](../../specs/plans/roadmap.md)维护。

## 有独立证据的专项记录

- [C#审计](audits/csharp-audit-2026-09-23.md)：2026-09-23基线的复现输入、已修复P1/P2及暂停时未修复P3，不作为当前缺陷清单。
- [自动工作对齐](audits/ai-alignment-20261023-24.md)：2026-10-23—24的产品修复、契约和正式回归；原作条件留在研究专题。
- [研究疑点复核](audits/research-questions-20260906.md)：历史裁决及当时验证；当前未知只在研究队列维护。
- [AI实现审计](audits/ai-implementation-audit-20261017.md)：逐工作边界及实际完整内容验证。
- [军事外交验收](audits/military-diplomacy-verification.md)：攻击确认、宣战及背盟的早期接线证据；移动算法已被替代。
- [寻路静态对齐审计](audits/pathfinding-original-parity-audit.md)：原作规则与产品实现的当时差异，不等于原游戏动态全分支验收。
- [地图与运行态验证摘要](rendering/map-rendering-verification.md)：数据／图像对照、实际引擎检查及同机性能测量。

## 性能与退出方案

- [导航测量](performance/pathfinding-verification.md)、[剧本压力基线](performance/scenario-performance-verification.md)；现行旬推进成本见[当前测量](../ai-performance-records.md)。
- [旧自主增强AI测量](performance/ai-prototype-performance-records.md)与[退出摘要](../../specs/archive/autonomous-ai/README.md)：保留失败原因和受控实验，不再维护旧排期／状态／验收体系。
- [旧粗格A*取舍摘要](design/pathfinding-legacy-design.md)；当前设计见[粗细两级导航](../../specs/navigation-redesign.md)。

来源、原作事实与推断继续保存在[研究区](../../research/README.md)。只有重复过程和被取代说明交给Git；独有反例、未决证据和关键测量不因实现退出而丢弃。

公开正文中的仓库提交引用已映射到历史清理后的等价提交；研究ID、样本／函数字节哈希及原始测量产物、IDA账本保持原记录语义。历史基线中的产品代码没有因清理而改变。
