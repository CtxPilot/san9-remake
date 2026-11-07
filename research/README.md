# 研究区

保存来源、可复现实验和逆向证据；产品采用范围由 `specs/` 决定。记录要求见[研究规范](../docs/development/research-method.md)，主库、标注及回读要求见[逆向规范](../docs/development/reverse-engineering.md)。

## 主要入口

- [原版 AI 权威研究](ai.md)：按控制、输入、内政人事、军事、计略外交及公共响应维护事实；历史审计按证据主题合并；旧 RE 保留取证身份及标注账本。
- [小兵培养权威研究](findings/RE-SAN9PK-0149.md)：统一触发、生成、成长、全部事件、训练菜单、随机与出师证据，已移除旧重复正文。
- [新武将权威研究](findings/RE-SAN9PK-0202.md)：统一创建、独立登记文件、秘籍、批量生年和开局处理；详细未决项只在该专题维护。
- [逆向结论索引](findings/README.md)：按主题查找 `RE-*`。
- [原版命令执行顺序扩查](notes/2026-11-02-original-command-resolution-order-audit.md)：日模拟各命令组、登庸／计略能力键、探索／外交政治队列、士气与移动阶段排序，以及同值时的单位表顺序。
- [统一研究队列](open-questions.md)：当前证据边界、范围触发与后置实验；历史“待验证”不直接作为当前待办。
- [来源登记](SOURCES.md)、[S9 文件清单](sources/s9-file-inventory.md)、[EXE 外部 DLL 契约](sources/exe-dll-analysis-inventory.md)、[第三方仓库](sources/upstream-repositories.md)。
- [军事邻城、设施行程与实际进攻范围实验](experiments/EXP-SAN9PK-0002.md)：统一保存校准、简单通道、动态前线与设施时间表研究，以及当前采用、内存／性能边界和后续AI改造方向。
- [原版旬推进性能采样](experiments/original-turn-performance/README.md)：原版六轮同档采集，保留完整AI、日模拟与交互边界、侧栏动画归因及逐势力明细；重制版实测见[旬推进性能记录](../docs/ai-performance-records.md)。
- [旗帜基础素材与动态生成](notes/2026-10-28-procedural-flags.md)：共用母材、字样和连续变形的八向对照，支持原版 51 色及自定义配色；布料模拟已撤销，未接入游戏。[实验入口](experiments/flag-generation/README.md)。
- [开局专项](topics/scenario-startup.md)：原作生命周期、一次性处理与首旬证据；产品采用见新游戏规格。
- [程序分析归档](archive/program-analysis/program-analysis-roadmap.md)、[来源覆盖账本](archive/program-analysis/program-analysis-coverage.md)。

## 目录与维护

| 目录 | 内容 |
| --- | --- |
| `sources/` | `SRC-*` 来源登记及文件／仓库清单 |
| `experiments/`、`findings/` | 按[模板](../specs/templates/README.md)记录的 `EXP-*` 实验、`RE-*` 结论 |
| `topics/` | 跨RE的权威专题、[开局研究](topics/scenario-startup.md)及[字段证据](topics/scenario-field-evidence.md) |
| `formats/`、`tools/` | [SQUARE源格式](formats/square.md)、[IDA数据库恢复](tools/ida-database-recovery.md) |
| `notes/` | 分析、冲突与专项核对；含[战略底图生成研究](notes/2026-10-18-procedural-strategy-terrain.md)，当前游戏暂用原版底图 |
| `guides/`、`patch-points/` | [优化伴侣说明](guides/san9pkhard-changes.md)等阅读资料、第三方补丁候选及验证状态；事实仍引用 RE／来源 |
| `archive/` | 已完成路线及证据；`ida-write-records/` 为写入回读账本，`code-recovery/` 为函数恢复证据 |
| `symbols/`、`scripts/` | 可追溯符号摘要、可复现研究脚本；不作为产品运行依赖 |
| `analysis/`、`../private/` | 忽略的项目 IDA 主库、私有样本、截图及第三方资料；统一用仓库相对路径引用 |

先登记来源，区分事实、推断与待验证；共享身份和证据只维护一处，后续记录补充差异并链接基线。大型样本、差分和生成物放入忽略目录，只提交哈希、方法及自行撰写的结论。研究成熟不自动新增产品规格；采用条件见[规格维护](../specs/README.md#规格维护)。
