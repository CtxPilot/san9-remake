# 原版AI：异常、采用行为与研究边界

[AI研究索引](../../ai.md) · [采用规格](../../../specs/original-ai/README.md)。本文保存原作事实、推断与证据；历史接入核对不是当前产品进度。

<a id="research-boundaries"></a>
## 异常、采用行为与研究边界

条件与控制流已经确认，不等于原作行为合理。下面登记影响重制取舍的异常；产品要求只在新规格维护。其余研究状态由唯一未决表统一表达。

| 已确认原作行为 | 证据与采用去向 |
| --- | --- |
| 全局分组先赋外交排名，再对同一有效表清零 | [排名规则](control.md#strategy-groups)；[B05](../../../specs/original-ai/behavior.md#b05)保留排名，非零旧档生成路径仍未知、停止追查 |
| 59保存两个来源后结束；9／59最终兵量门未落实下限 | [计划状态](military.md#military-plan-state)、[动态实验](evidence.md#experiment-r06)；[B19～B20](../../../specs/original-ai/behavior.md#b19)续行不同来源并落实最终下限 |
| 22额外弹下一目的地，用尽来源时弹原表头 | [调动消费](domestic-personnel.md#personnel-movement)；[B11](../../../specs/original-ai/behavior.md#b11)只消费实际处理项 |
| 62排序合格对手表，却从另一统计表取目标 | [战略目标](control.md#strategy-modes)；[B03](../../../specs/original-ai/behavior.md#b03)从实际排序表取值 |
| 配额包含原军团俘虏、低八位截取，组织变化不统一重算 | [战略与配额](control.md#strategy)及[轮转](control.md#turn-roster-mutation)；[B09～B10](../../../specs/original-ai/behavior.md#b09)改为在职统计、宽整数及依赖失效 |
| 33预测、后备与排序载荷不同，生产失败仍可能扣私有预算 | [赠与](missions-diplomacy.md#re-0117-玩家与自动生产者)；[B14～B15](../../../specs/original-ai/behavior.md#b14)按提交反馈记账并统一正式载荷 |
| 34高政治使者预估，实际按距离换使者 | [攻击请求](missions-diplomacy.md#re-0120-攻击请求)；[B16](../../../specs/original-ai/behavior.md#b16)以实际使者复验 |
| 33／36可行分数还要求不超过100 | [配对契约](inputs.md#pair-score-contract)；[B17](../../../specs/original-ai/behavior.md#b17)取消该上限，原作事实保持闭区间 |
| 58准备改写对方临时命令目标 | [部署准备](military.md#re-0080-事实准备阶段)；[B21](../../../specs/original-ai/behavior.md#b21)预测私有，真实转换归公共阶段 |
| 58评分及新等待读取人员行程J，计划再加一次J | [时限输入](military.md#deployment-time-inputs)；[B22](../../../specs/original-ai/behavior.md#b22)改读战斗出发等待并只计一次；正常军事候选J非零尚未动态验证 |
| 57中间组将驻兵与部队数相加 | [旬级撤回](military.md#re-0085-事实三组部队候选与选择顺序)；[B23](../../../specs/original-ai/behavior.md#b23)统一兵量口径、同工作部队只提交一次 |
| 部分选阵后备不经过资金过滤，空路线可通过多个谓词 | [选阵](military.md#siege-formation-selection)、[路线](inputs.md#navigation)；[B24](../../../specs/original-ai/behavior.md#b24)区分不可达、有效机动力及可负担性 |

2026-10-16用户已确认[行为B01～B30](../../../specs/original-ai/behavior.md)和[人物C01～C09](../../../specs/original-ai/character-rules.md)。本表只导航采用差异，不改变原作事实或实验等级；每日响应的原作动态对照及既定R06额外实验仍暂缓，不能把产品规格或后续产品测试当作原作实验通过。

<a id="automatic-work-implementation-audit-20261023"></a>
### 自动工作实现复核

2026-10-24确认的26类遗漏已修复，后续R1／R2／R3采用v181替代选择；最终状态和验证只在[修复汇总](../../../docs/history/audits/ai-alignment-20261023-24.md#note-2026-10-24-automatic-work-alignment-fixes)维护。下列报告保存发现时的输入与诊断，不能把历史“当前偏差”重新列为待办。原作静态事实、动态限制和B20／B25采用差异分别保留。

| 专项 | 证据入口 |
| --- | --- |
| 首轮、续核与选人 | [初次实现核对](audits/integration.md#note-2026-10-23-automatic-work-implementation-audit)、[条件续核](audits/integration.md#note-2026-10-23-automatic-work-continuation-audit)、[选人与后备](audits/planning.md#note-2026-10-23-automatic-work-selection-audit) |
| 建设、人事及计略 | [目标失败与建设](audits/construction.md#note-2026-10-23-automatic-work-construction-audit)、[方向契约](audits/construction.md#note-2026-10-23-automatic-work-contract-audit)、[人事](audits/organization.md#note-2026-10-23-automatic-work-personnel-audit)、[计略外交](audits/planning.md#note-2026-10-23-automatic-work-schemes-audit) |
| 军事支援 | [撤回57](audits/support.md#note-2026-10-23-automatic-work-retreat-audit)、[部署58](audits/military.md#note-2026-10-23-automatic-work-deployment-audit)、[输送51](audits/support.md#note-2026-10-23-automatic-work-transport-audit) |
| 分阶段留兵 | [汇总与统一取证](audits/support.md#note-2026-10-24-automatic-work-garrison-reserve-audit)、[主库账本](../../archive/ida-write-records/automatic-work-garrison-reserve-20261024.json) |
| 战略、响应、组合及来源条件 | [剩余专项汇总](audits/planning.md#note-2026-10-24-automatic-work-remaining-audit)、[主库账本](../../archive/ida-write-records/automatic-work-remaining-20261024.json) |
| 版本映射及战斗续修 | [设施分类](audits/military.md#note-2026-10-24-automatic-work-sortie-mapping-audit)、[盟友易主后旧攻击](../../notes/2026-10-24-allied-capture-stale-attack-audit.md)、[战斗汇总](../../notes/2026-10-24-combat-behavior-audit.md) |

26类修复不穷尽全部出兵状态；旧档策略及错误命令的使用限制见各修复记录。其他写入记录：[支援](../../archive/ida-write-records/automatic-work-support-20261023.json)、[方向／范围](../../archive/ida-write-records/automatic-work-scope-20261023.json)。

<a id="version-differences"></a>
### 日版修正与繁中基线差异

以下另属日版1.3，样本身份和跨版本方法仍由RE-0150登记；不能混入繁中PK 1.0.1的现行规则。

<a id="re-0150-自动离间增加候选门和按阈值区间筛选"></a>
#### 离间候选门：撤销错误的版本归属

**撤回“日版新增忠诚70／智力90目标门及军师配对筛选”的结论。**2026-10-15恢复繁中回调`0x4B10C0`的独立指令／函数边界后，`0x4B13D0`直接传入该回调，已经要求当前忠诚≥70且当前智力≤90；繁中配对链也已有真实公式／军师意见的区间筛选。原始EXE与主库相应字节一致。先前把未识别回调当作功能不存在，不能形成跨版本差分；旧锚点保留以免历史引用失效。

两版公共离间成功公式的独立差分仍按[RE-0150](../../findings/RE-SAN9PK-0150.md)保留；不能由候选门差分撤销而一并否定公式差异。本次只改繁中主库错误注释，没有把日版地址套入繁中。

<a id="re-0150-自动撤退三组优先级改变但与异民族击破撤退无关"></a>
#### 旬级撤回优先级的版本差异

繁中自动撤退命令对普通势力按“中间组达到阈值前 → 第三组 → 第一组达到半阈值前”取部队。日版 `0x4A6270` 改为先取第三组并累计其成本，再尝试中间组，最后才取第一组；准备函数 `0x4A6A80` 仍建立相同三组和目标阈值。势力虚槽 `+0x48` 已由繁中主库独立闭合为异民族标志，而两版的异民族分支都直接取中间组，故普通势力三组排序差分与“异民族消灭敌部队后撤退”无关。三组的正式业务名仍未闭合，不据此设计新的重制规则。

2026-09-23补充：繁中三组完整条件、两套不同目标量及行军时间语义已在[RE-0085](../../findings/RE-SAN9PK-0085.md)补证。上文“半阈值”具体指有效目标指向该据点的类型38部队现役总兵力的一半，并非中间组补防阈值的一半；异民族虽然使用中间容器，但采用独立候选筛选器。

**修正意图推断，仅供参考：**结合繁中分组条件，日版可能希望先召回远离本据点、未交战且主目标仍为有效野外部队的第三组，再将已决定回撤的兵力纳入后续需求判断，减少额外召回附近部队补防。这是由候选用途和选择顺序差分推导的解释，不是官方明确说明，也不是已验证的日版运行时效果。第三组累计量具体写入哪个字段、怎样影响后续各组预算，仍需日版定向复核；不能据此宣称日版已经消除重复召回。

本节只记录日版差异，不自动改变本页繁中基线或新规格的采用行为。

取证身份：RE-SAN9PK-0150。

<a id="corrections"></a>
### 历史冲突的有效裁决

| 旧结论 | 当前有效结论 | 证据身份 |
| --- | --- | --- |
| 撤退／追击扫描是孤立代码 | 每日阶段先撤退后追击；旧数据定义遮蔽了相对调用 | RE-0087 → RE-0090 |
| 响应公式读取野心 | 读取性格；相性／义理／野心／性格的字段顺序已由人物样本及运行时核对 | RE-0081 → RE-0090 |
| 部署所有成员使用同一比较器 | 主将用身份／官职／统率键，其余按与主将相性距离补入 | RE-0079 → 本页定向补证 |
| 阵形没命中规则就任意随机 | 步兵占优分支仅鱼鳞／鹤翼二选一；船型按可用性与路线标志固定优先 | RE-0075及本页定向补证 |
| 战力比含义未明 | 响应R使用基础战力乘兵力／100后的总评估值之比，至少1；不是单兵战力或兵力比 | RE-0081后续汇编补证 |
| 工厂没有6／7所以电脑不买卖 | 工厂没有6／7仍成立，工作61另行生成买卖命令 | RE-0097 → RE-0190及本页定向补证 |
| 工作63是终止空操作 | 它是最后一批的搜索／训练 | RE-0077 → 本页定向补证 |
| 内部类型与委任方针无关 | 两个字段不同，但工作62根据方针更新内部类型／目标 | RE-0123 → 本页定向补证 |
| bit8是未知难度位 | 全局策略分组写入的修饰标志；初高级另有独立字段 | RE-0120/0123/0174 → 本页定向补证 |
| 撤回三组全排序、共用累计预算、距离10 | 第三组保留枚举顺序；两套累计量；5／10是预计行军时间 | RE-0085的2026-09-23补证 |
| 原作完全不预测路线或逐日完整推演 | 粗格求路后按普通／地形族4分别换算时间；不逐日重放移动 | RE-0080的2026-10-12补证 |

新增纠错：经营0～3／训练5的半数是**检查预算**，逐人按能力抽签，非确定取半；军师与人物能力保护也不能从辅助函数旧名称推断。工厂10/60存在但标准批次不请求它们；9的操作10/60另属编号域，建设／占空点仍可由9产生。

“没有AI、只有死规则”是资料作者评价；本样本有固定优先级、候选排序、路径、阈值和受限随机，没有证据据此声称通用博弈树搜索。静态基线收口不等于完成全局动态对照或产品交付。

<a id="open-questions"></a>
### 研究状态与保留缺口

静态决策基线已收口，目前无阻塞已定义基线实施的未恢复策略分支。下表保留AI-R编号以追溯调查；规则只在主题正文维护。原作未知、暂缓实验和产品取舍不互相替代。

| ID | 结论入口 | 当前状态与限制 |
| --- | --- | --- |
| AI-R01／R10 | [策略分组与外交排名](control.md#strategy-groups)、[实验](evidence.md#experiment-r01) | 加载后33筛选、36出使及更新清零已证；正常旧档非零排名的生成／留存未知，用户已停止追查。重制采用[独立排名生命周期](../../../specs/original-ai/behavior.md#已采用差异外交排名不随策略分组清零) |
| AI-R02 | [模式保留与存档输入](control.md#strategy-modes) | 静态关闭：显式生产0/1/2/3/5/6，存档可输入4；普通重置未清元数据的留存问题随R01停止追查 |
| AI-R03 | [人员配额与留兵](control.md#strategy) | 关闭：集合、最低门、量化、低8位截取及32位有符号运算已闭合；不假定守恒或饱和 |
| AI-R04 | [经营0～5／61／63](domestic-personnel.md#economy)、[失败消费](control.md#schedule) | 关闭：候选、人数、概率、资源公式及准备／验证失败的不同消费位置已闭合 |
| AI-R05 | [人事12～17／24／55／56](domestic-personnel.md#personnel)、[任官实验](evidence.md#experiment-r05) | 任官疑点关闭：同一自然旬两势力内层0、外层1，实际写回2／14人；不是独立重放或所有边界对照 |
| AI-R06 | [军事](military.md#military)、[实验](evidence.md#experiment-r06) | 静态闭合；59丢弃第二来源及9城市9999／非城市4999、59城市9999实际出阵已证。低容量为受控注入，59非城市及成对重复未实测；采用[合理续行与最终下限](../../../specs/original-ai/behavior.md#已采用差异攻击请求续行与最终出征兵力)，余项不阻塞实施 |
| AI-R07 | [建设及独立10／60](military.md#automatic-construction) | 关闭：工厂存在但标准批次不请求；与工作9内操作10／60分开 |
| AI-R08 | [路线与时间](inputs.md#navigation) | 关闭：失败ETA0、零速率未保护、正式剧本机动力正值、调用上下文和节点模板失效已核对；Mod安全另属重制选择 |
| AI-R09 | [八计略](missions-diplomacy.md#strategy-actions) | 静态选择关闭：地域门、军师上下文、排序、随机、保留量与消费已闭合；不等于跨版本逐随机字节复演 |
| AI-R11 | [公共响应](unit-responses.md#responses) | 静态闭合；入口纠正为467190，建筑候选表为空。原作动态对照暂缓，不阻塞[已采用行为](../../../specs/original-ai/behavior.md#已采用行为每日追击与撤退)；EXP-0001不证明这些分支 |
| AI-R12 | [待决事件](unit-responses.md#computer-prompt-policy) | 静态关闭：RE-0181的24类及8+7展开项已区分玩家门、电脑选择、公共提交与终局 |
| AI-R13 | [静态收口账本](../../archive/ida-write-records/original-ai-closure-20261016.json)，AI-CLOSURE-20261016-N | 39工作逐项核对准备／生产／续行／选择槽及实际消费者，补查虚调用、深层getter和9个误分类回调；此前301函数清单仅作库存，不作为完整性证明 |
| AI-R14 | [共享边界与组合核对](evidence.md#boundary-composition-evidence)，AI-BOUNDARY-20261016-O | 定向静态完成：共享查询、副作用、轮转、缓存、列表／随机及25／26失效引用候选门；组合推演不冒充原作动态覆盖，不重开既定取舍 |
| AI-R15 | [部署时限](military.md#deployment-time-inputs)、[配对评分](inputs.md#pair-score-contract)，AI-SPEC-READINESS-20261016-P | 规格前定向静态补齐：区分J／D、目标输入与新队输出、配对上下限及单候选回调；人员半数口径、主将容量与异民族权限复核一致。原作动态边界未新增，已定暂缓项不重开 |
| AI-R16 | [独立工厂入口](military.md#standalone-entry-audit)、[许攸官渡回读](../../findings/RE-SAN9PK-0130.md#guandu-ai-readback)，AI-ENTRY-GUANDU-20261016-Q | 原始字节扫描未发现误标为数据的额外10／60构造入口，12张表155项不请求；许攸前置早已由RE-0130闭合，本次独立复核并纠正RE-0106及C09旧待办，普通电脑曹操不满足玩家根势力门 |

<a id="formation-design-pending"></a>
#### 选阵策略待考虑项（2026-10-25）

用户明确决定：以下两项只列待考虑，**暂不实现，保留当前行为**。建议尚未采用，不作为已批准规则或当前缺陷修复要求；无阵攻城队资格及异民族主计划排位已在v194修复，不受此次暂缓影响。

| 待考虑项 | 当前保留行为 | 备选建议，尚未采用 |
| --- | --- | --- |
| 58迎敌规则阵不可用时的后备 | 角色不可用后继续其他类别判断，可能额外抽步兵骰，最终按现有通用排序后备；默认正式内容不触发这类候选失效 | 先按兵法和敌情确定分支，再筛该分支合法候选；耗尽后按对部队效果统一后备，不跨类别重试。鱼鳞／鹤翼均可用时才随机，只有一种时直接使用 |
| 异民族拥有多个专属陆阵／船阵时的选择 | 在可用非通用阵形中按ID升序取首；默认每族只有唯一专属陆阵／异船 | 由内容分别声明专属陆阵、船阵优先顺序，按资格和费用逐项尝试；只有一种时直接使用，多种却未声明顺序时在内容校验提示补齐 |

当前行为、原作适用边界及公共诊断见[选阵续核](../../notes/2026-10-25-ai-formation-implementation-audit.md#continuation)。这两项属于产品策略选择，不重新打开已闭合的原作静态分支，也不自动启动实现或新一轮实验。

本轮异常的采用去向见[异常登记](#research-boundaries)，已由新规格确认；原作生成路径未知、未做动态实验与产品采用差异继续分别表达。

### 公共依赖与研究范围

公共命令资格、费用、效果、成长、占领、继任、外交谈判及存档不是第二套AI策略。其领域原始证据继续保存在RE-0078/0084/0095～0098/0110～0119/0121～0124/0130～0143/0174/0181中；本文迁入其自动选择部分，其余保留原职责。历史AI原型设计与性能记录属于重制实验，不作原版事实来源；研究整理不要求重现原型缺陷，也不宣布采用原版作弊或取消既有公共修复。

许攸特殊登用属于[公共官渡事件](../../findings/RE-SAN9PK-0130.md#2026-09-20官渡专项登用前置的独立生产链)，完整前置已闭合；身份研究RE-0106的历史待办不能覆盖较新结论。仅玩家曹操根势力及同根执行者可能满足事件门，玩家委任军团仍可复用；普通电脑曹操不获得额外必成特权。采用边界见[C09](../../../specs/original-ai/character-rules.md#c09)。
