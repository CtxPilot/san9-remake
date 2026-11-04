# 剧本与军事 AI 基准

`military-baseline`是[军事 AI 重做计划](../../specs/military-ai-plan.md) M0 的隔离测量入口。使用当前生产 AI 和公共会话操作，不切换军事策略，不读取原版资源，不生成文件哈希。

## 可复现测量

先创建配置，例如`local/generated/verification/military-ai-m0/281-config.json`：

```json
{
  "contentPath": "../../content/scenarios/fictional/scenario-19",
  "reportPath": "281-baseline.json",
  "codeRevision": "填写 git rev-parse HEAD 的结果及影响测量的未提交改动",
  "seed": 281,
  "startXun": 0,
  "xuns": 36,
  "repetitions": 3,
  "warmupPasses": 1,
  "otherForceOrdersVisible": true,
  "otherForceTacticsVisible": true
}
```

路径相对于配置文件目录解析。`contentPath`接受已有转换包目录或独立剧本文件；报告须位于内容目录以外，不能覆盖输入配置。`codeRevision`由调用者登记，不自动声称工作区干净。281年内容为虚构类`scenario-19`，使用前核对内容元数据；不凭文件序号推断年份。

```powershell
dotnet build tools/scenario-benchmark/ScenarioBenchmark.csproj -c Release
dotnet tools/scenario-benchmark/bin/Release/net10.0/ScenarioBenchmark.dll military-baseline local/generated/verification/military-ai-m0/281-config.json
```

仅允许Release构建。使用生产默认设置、全自动控制、显式种子和两项情报设置。`startXun`在同一确定性开局先推进指定旬数，再编码精确检查点；可用`startXun: 12, xuns: 1`测量自然形成的固定局面。每次重复均使用`SaveService.Decode`恢复同一检查点，不使用会重新抽取随机种子的玩家加载入口。终局、未完成十天或非完整下令边界均报错，不把空转或半旬算作样本。

先单列首次完整轨迹，再执行指定数量的预热及正式重复；不自动增加采样或以稳定性阈值阻塞开发。默认一次预热、三次重复，可按问题缩减；未充分预热时照实解释耗时趋势，不把结果称作稳定热态。完成退出0；非法配置、轨迹不一致或执行异常退出2。成本判断同时使用节点／对象检查量、算法复杂度、用时、分配和进程内存；只有具体退化或关键取舍未明时才扩大采样。

测量时不要与测试、构建或其他基准并发。报告包含运行时、CPU标识、操作系统、GC模式、分层编译／动态PGO环境设置、规则版本、输入配置、实际起始日期和逐轮／逐旬原始值。保持生产运行时配置，不关闭PGO或强制GC。整旬是`PrepareAdvanceOperation`加十次`AdvanceDayOperation`，与客户端逐日入口一致；准备含全部AI与命令提交，每日结算含移动、战斗和其他每日规则。每个对应旬比较完整事件，整轮比较最终会话状态，检查在计时外完成。

## 计量边界

- 内容加载、初始化、检查点构造／恢复、结果比较、工作汇总和文件I/O不计入整旬；加载、初始化与每轮恢复单列。
- 每轮新建会话，重置其业务查询缓存；JIT、PGO和进程工作区保留。测量是运行时预热后相同冷恢复条件的成本，不声称已测业务缓存热态。
- 按工作记录AI耗时、尝试／提交／拒绝及路线工作量。工作耗时包含在准备阶段中，不能再次加到总时间。未被各工作包围的AI准备成本仍在整旬和准备时间内。
- `PathSearchDiagnostics`嵌套作用域互斥。总寻路次数、普通扩展和端点扩展分别为`unscoped*`加全部`work`相应计数；`unscopedMaximumExpanded`及`unscopedWorkspaceNodes`只描述外层作用域，不能当作全旬峰值。
- `allocatedBytes`为计时段当前线程累计托管分配；`managedHeapBytes`、`workingSetBytes`是轮末非强制读数，含内容和保留的参考事件／最终状态，不是峰值、常驻保留量或独立AI内存。`maximumUnits`在每次操作后观察，不能表示日内瞬时峰值。
- 正式汇总保留各轮总耗时、整旬中位数、线性插值P95、最大值和平均分配。完整轨迹重复次数与旬样本数分别表达，所有正式样本均纳入，不只选最快结果。

当前入口支持连续轨迹、自然推进固定局面及四种情报配置；交通与编组等最小对照见下文。基准结果不证明新AI质量、实际画面等待体验或十年长局验收。

## 查询与编组基础实验

`military-foundations`运行自制固定夹具，报告索引、投送、编组、普通交锋算术模型和路线代价。配置示例保存为`local/generated/verification/military-ai-m0/foundations-config.json`：

```json
{
  "reportPath": "foundations.json",
  "codeRevision": "填写代码基线及有关未提交改动",
  "contentPath": "../../content/scenarios/fictional/scenario-19",
  "seed": 281
}
```

```powershell
dotnet tools/scenario-benchmark/bin/Release/net10.0/ScenarioBenchmark.dll military-foundations local/generated/verification/military-ai-m0/foundations-config.json
```

复用上文Release构建。`contentPath`可省略，仅运行公开自制夹具；提供转换包时另记录开局和自然推进12旬后的物理对象／部队／目标数量、一次观察投影成本和精确存档恢复。路径解析及报告不覆盖输入的规则同上。

- 16／128／2048对象各执行16次局部查询，对照直接扫描、共享桶索引、改变一个对象后更新与冷重建。报告实际检查量、桶引用、构建／查询／更新用时和分配；这些是查询压力，不能代替2048队完整战斗旬。
- 投送夹具比较后方直接出征、先运前沿再进军、公共调将恢复可用及下次下令窗口。前沿账面有兵但无将不能组队；近处与远程同时对照伴侣行程参考门，参考门不进入运行时资格。
- 编组在同一候选求值上限下比较固定阵形和有界替代，二者必须通过公共命令准备。物理适配、可见兵法针对性和左右等价由Application回归展开。
- 算术模型另限定伤害调用次数，使用Core两次普通成对交锋；无兵法、无援军、固定士气、已知敌人。首个出击损失与公共日结对照，记录绝对误差。它不预测胜率，当前不采用在线推演；日结对照的费用单列。候选求值数和伤害计算数不是相等CPU成本。
- 同一96×24粗格地图，对陆20／水20及陆20／水2各比较1／4／16目的地。旧公共粗估逐目标调用，新场按真实水陆比例共享搜索；固定一次预热和两次交替计时，不自适应加轮。记录调用、扩展、初始化格数、分配、路线保留长度和最大ETA差。旧工作区使用池化，新场分配数组；初始化数表示清理／构造的工作量，不等于分配对象数。结果只针对粗格模型，实际接近仍需公共导航。

除路线有固定预热外，其余是有界单次观测，首轮包含JIT成本，不凭首轮用时给算法排名。采用结论结合工作量、复杂度、用时和内存，在[计划7.1](../../specs/military-ai-plan.md#71-m0m1-实现与验收2026-11-04)维护。工具不自动修改在线策略或扩大采样。

本次规则v245／存档138的证据位于忽略目录`local/generated/verification/military-ai-m0/`：`m0-m1-281-config.json`／`m0-m1-281-baseline.json`为首次加一次重复36旬，`m0-m1-foundations-config.json`／`m0-m1-foundations.json`为基础实验，`test-results/`为实际TRX。报告包含机器、运行时、构建、输入与代码说明；不提交本地内容或报告。

旧参数入口仍用于显式`march`／`combat`导航压力构造，见[导航验收](../../docs/pathfinding-verification.md)；其单次测量不替代上述重复基准。
