# 自动工作历史审计

2026-10-23—24的20份日期式笔记合并为以下证据主题。各条保留原记录定位、样本、静态条件、诊断反例、哈希和IDA回读；记录中的产品状态只代表当时。正式修复／回归另见[历史对齐记录](../../../../docs/history/audits/ai-alignment-20261023-24.md)，当前交付状态查[实施计划](../../../../specs/original-ai/implementation-plan.md)。

- [建设与命令契约](construction.md)
- [人事与战略组织](organization.md)
- [工作选择与组合](planning.md)
- [进攻与部署](military.md)
- [撤退、驻军与输送](support.md)
- [公共响应](responses.md)
- [跨主题接线与续核](integration.md)

事实正文从[AI研究索引](../../../ai.md)进入；研究整理不提高原作动态实验的覆盖等级，也不重新打开已决定暂缓的实验。

## 共同基线

本批记录的研究者为Codex，核对基线为`b6c9824603530bf1c53b3e1c2e76d357f598b0b5`及各次当时工作区；共享提交号不证明并行期间产品代码完全相同。版本、样本SHA-256、主库及工具身份统一查[AI取证身份](../evidence.md#取证附录实验入口与账本)和[SRC-SAN9PK-TC-101](../../../sources/SRC-SAN9PK-TC-101.md)。本批使用IDA9.4／Hex-Rays9.4.0.260915；原作新增证据为静态回读，重制夹具不替代原游戏动态实验。

有效诊断复用当时Application／Core测试项目，通过公共资格与实际接受核对；局部选择器、预算组件和静态推导在各项单列。Windows、.NET10.0及过滤运行命令是共同环境，不逐项复制。默认产物目录为`local/generated/verification/automatic-work-audit/`；设施映射另用自己的目录。探针均为忽略的历史产物，已退出正式测试项目，不是当前默认测试入口，输出文件也不保证仍在。

“通过”仅表示记录中相应断言通过；记录修复前错误行为的通过不代表产品正确，按原作预期失败的诊断是偏差复现。未通过输入前置或编译的批次不计证据；排错和重跑过程交Git追溯，只保留最终日志筛选及影响结论的纠正。除明确另列的历史正式回归，局部研究不声称完成全解决方案、Headless、内容重转、画面或长期对局验收，也不自动重启已暂缓实验。

### 统一取证批次

下表均记录当时实际对盘与标注；本次文档精简没有新增IDA操作。统一批次不得在各分工重复计数。主库保留原注释，实际追加均有全文回读、重新反编译和前后字节／块哈希检查后原位保存；没有更名、改类型／边界或程序字节。各记录仍保留自身函数、追加数、注释标记和独有摘要。

| 批次 | 对盘范围 | 实际追加／账本 |
| --- | --- | --- |
| 首轮、条件续核、选人、建设 | 首轮3段；续核15段3083字节；选人9段2750字节；建设13段3915字节 | 各记录内保留函数SHA-256与注释标记 |
| 方向／范围 | 18函数4850字节 | 6条，[scope账本](../../../archive/ida-write-records/automatic-work-scope-20261023.json)；建设1、人事4、救援1 |
| 军事支援 | 21函数8289字节 | 8条，[support账本](../../../archive/ida-write-records/automatic-work-support-20261023.json)；部署4、撤回3、输送成员表1 |
| 剩余条件 | 21函数主段7544字节；含尾块33段8105字节 | 12条，[remaining账本](../../../archive/ida-write-records/automatic-work-remaining-20261024.json)；战略／来源条件、响应及分粮更正共享 |
| 分阶段留兵 | 21函数主段8281字节；含尾块35段8681字节 | 8条，[garrison账本](../../../archive/ida-write-records/automatic-work-garrison-reserve-20261024.json)；进攻3、部署1、输送4 |
| 设施目录映射 | 12函数及已有尾块 | 2条，[mapping账本](../../../archive/ida-write-records/automatic-work-sortie-mapping-20261024.json) |

## 历史记录定位

历史IDA注释及写入账本记录的是当时的文件路径，不改写为虚假的新写入记录。按原文件身份查下表；原RE编号及注释标记均保留。各专题页内仍保留逐节稳定定位。

| 原文件身份 | 现正文 |
| --- | --- |
| `2026-10-23-automatic-work-construction-audit.md` | [记录](construction.md#note-2026-10-23-automatic-work-construction-audit) |
| `2026-10-23-automatic-work-continuation-audit.md` | [记录](integration.md#note-2026-10-23-automatic-work-continuation-audit) |
| `2026-10-23-automatic-work-contract-audit.md` | [记录](construction.md#note-2026-10-23-automatic-work-contract-audit) |
| `2026-10-23-automatic-work-deployment-audit.md` | [记录](military.md#note-2026-10-23-automatic-work-deployment-audit) |
| `2026-10-23-automatic-work-implementation-audit.md` | [记录](integration.md#note-2026-10-23-automatic-work-implementation-audit) |
| `2026-10-23-automatic-work-personnel-audit.md` | [记录](organization.md#note-2026-10-23-automatic-work-personnel-audit) |
| `2026-10-23-automatic-work-retreat-audit.md` | [记录](support.md#note-2026-10-23-automatic-work-retreat-audit) |
| `2026-10-23-automatic-work-schemes-audit.md` | [记录](planning.md#note-2026-10-23-automatic-work-schemes-audit) |
| `2026-10-23-automatic-work-selection-audit.md` | [记录](planning.md#note-2026-10-23-automatic-work-selection-audit) |
| `2026-10-23-automatic-work-transport-audit.md` | [记录](support.md#note-2026-10-23-automatic-work-transport-audit) |
| `2026-10-24-automatic-work-alignment-fixes.md` | [记录](../../../../docs/history/audits/ai-alignment-20261023-24.md#note-2026-10-24-automatic-work-alignment-fixes) |
| `2026-10-24-automatic-work-attack-reserve-audit.md` | [记录](military.md#note-2026-10-24-automatic-work-attack-reserve-audit) |
| `2026-10-24-automatic-work-composition-audit.md` | [记录](planning.md#note-2026-10-24-automatic-work-composition-audit) |
| `2026-10-24-automatic-work-deployment-reserve-audit.md` | [记录](military.md#note-2026-10-24-automatic-work-deployment-reserve-audit) |
| `2026-10-24-automatic-work-garrison-reserve-audit.md` | [记录](support.md#note-2026-10-24-automatic-work-garrison-reserve-audit) |
| `2026-10-24-automatic-work-remaining-audit.md` | [记录](planning.md#note-2026-10-24-automatic-work-remaining-audit) |
| `2026-10-24-automatic-work-response-audit.md` | [记录](responses.md#note-2026-10-24-automatic-work-response-audit) |
| `2026-10-24-automatic-work-sortie-mapping-audit.md` | [记录](military.md#note-2026-10-24-automatic-work-sortie-mapping-audit) |
| `2026-10-24-automatic-work-strategy-audit.md` | [记录](organization.md#note-2026-10-24-automatic-work-strategy-audit) |
| `2026-10-24-automatic-work-transport-reserve-audit.md` | [记录](support.md#note-2026-10-24-automatic-work-transport-reserve-audit) |
