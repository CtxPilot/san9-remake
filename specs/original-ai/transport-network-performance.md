# 常驻交通网络性能实测

实验 ID：OAI-TRANSIT-PERF-20261105。日期：2026-11-05。执行者：Codex。

本记录针对当前工作区 v246 的开局完整建网实现，采用每日行动力50、60日范围。仅测量，不修改生产算法；依赖更新、按需重建与工作数组复用尚未据此实施。此前 v245 的按需来源缓存结果不能作为本次相同条件的对照组。

## 输入与复现

- 环境：Windows，AMD Ryzen 5 9600X，6核12线程，.NET 10.0.4，Release，默认分层编译；单线程测量，未固定CPU频率。
- 代码：基点 `e161e4780d085ffdfa1758746d9cac4e9501ac62` 加当前未提交的交通网／工作9修改，运行规则版本 `san9-core-g10-v246`。
- 内容：`local/generated/content/scenarios/historical/scenario-0`，184年2月，随机种子42，全自动；200×200粗格、7势力、700人物。源内容共有93处据点，开局有效物理节点91个；未启用对象不入网。参考有向边1191条。
- `scenario.json` SHA-256：`e9f58573e62382bf4e37ef606fc1b82e14854091bf93cfb8a8d4ae3d99519586`。
- `content.json` SHA-256：`12b7532bae5985fee34487a402303c3fdbf85bde5de507bf56b9bc15cecdc96c`。
- 本地专用探针：[项目](../../local/generated/verification/traffic-performance/TrafficPerformance.csproj)、[源码](../../local/generated/verification/traffic-performance/Program.cs)、[测量JSON](../../local/generated/verification/traffic-performance/report.json)。探针源码 SHA-256：`50b75d65d349c539efe67263f52b0f09e9eb2b2a37c20126bf3565010d01ea9f`。这些文件位于忽略目录，不进入产品或默认测试入口。

从仓库根目录运行：

```powershell
dotnet run --project local/generated/verification/traffic-performance/TrafficPerformance.csproj -c Release -- C:/home/game/san9/remake
dotnet run --project src/San9.Headless/San9.Headless.csproj -c Release -- verify --content-package local/generated/content/scenarios/historical/scenario-0 --output local/generated/verification/traffic-performance/campaign --xuns 12 --seed 42 --strategy autonomous
```

探针先创建真实会话，再单独建网3次；每个测量项开始前回收垃圾，时间只包围被测操作。增删几何各测3次：在公共建设位置检查允许的空地加入一种6号设施，再移除它；这是几何输入模拟，不冒充公共建设命令验收。分配量为当前线程累计分配，不是峰值或常驻内存。额外持有一份完整网络，比较强制GC前后存活内存估算常驻体积。

## 观察事实

下表为最后一轮的原始测量，三次样本只用于报告范围和中位数，不构成稳定基准分布。分层编译与冷热阶段会影响耗时；两次进程运行均完成相同工作量。

| 操作 | 耗时 | 累计分配／搜索工作 |
| --- | --- | --- |
| 真实新游戏启动，含建网及其他会话初始化 | 618.39 ms | 155.17 MiB，91次搜索 |
| 单独完整建网3次 | 187.60～220.26 ms，中位199.68 ms | 中位134.55 MiB；每次91次搜索、263165格展开 |
| 加入一处设施后的完整重建 | 113.35～138.39 ms，中位120.71 ms | 中位136.07 MiB；92次搜索、265526格展开 |
| 移除该设施后的完整重建 | 112.42～119.92 ms，中位112.55 ms | 中位134.54 MiB；91次搜索、263165格展开 |
| 几何不变的更新检查1000次 | 合计35.85 ms，平均35.85 μs | 合计21.37 MiB，0次搜索，返回同一对象 |
| 12个分散来源重新计算相同参考结果 | 10.63 ms | 17.65 MiB，12次搜索、33373格展开 |
| 同样12个来源读取已有边并汇总行程，重复10000组 | 合计3.29 ms | 合计112字节，0次搜索；每组均值约0.33 μs |
| 入边列表及每条正向边查询，100000组 | 合计100.59 ms | 合计8.39 MiB，0次搜索 |

完整图常驻体积估算为665992字节，约0.635 MiB。参考查询对照逐条核对可达目标数量、参考日数、水陆代价和路径格序列，完全一致；不仅比较耗时或目标数量。查表项只是同精度参考数据的消费，不能代表动态通行与实际队伍细步导航的成本。

真实会话推进12旬的观察：

- 探针单次累计旬处理1609.30 ms，单旬42.74～299.95 ms。前7旬进攻尚无实际提交，不作为军事寻路收益证据；第8～12旬出现7次工作9提交。
- 前8旬复用开局同一个网络；第9旬出现实际施工设施，ID90、类型7、位置 `(482,397)`，节点91→92、几何修订1→2；之后3旬保持修订2，没有逐旬重建。
- 第9旬总处理299.95 ms，工作诊断中的AI决策148.44 ms，分配约164.43 MiB。该旬同时包含新设施建网及其他模拟工作，两者的差值不能全部归因于重建。
- 独立 Headless 验证成功完成12旬、3次恢复及命令重放一致性检查，包含1次施工开始、1次施工完成、3次据点占领。其12旬处理均值145.88 ms、最大315.04 ms；工具总耗时10960 ms包含恢复、重放等验证，不能当作12旬纯运行成本。
- Headless工作9汇总：72次调度，7次提交，合计371.41 ms；仍有927次寻路、2943847格展开，单次最大117.45 ms。这些是工作诊断的剩余路径搜索，不能归为交通参考网查表。

Headless原始结果：[报告](../../local/generated/verification/traffic-performance/campaign/report.json)。本次成功证明被覆盖流程的恢复／重放一致，不等同于当前工作区全部功能回归通过。

## 推断与决策依据

1. **高置信度：常驻网确实能消除重复参考搜索。** 相同12来源结果从12次搜索、33373格展开变为0次搜索，而且逐条结果相同。不能把该微基准的倍率外推为整套AI的加速倍率；尚未进行同策略、同输入、仅切换是否复用的完整AI对照。
2. **中等置信度：当前地图的开局成本可控。** 完整关系只需约0.64 MiB常驻空间，构建为十分之一秒量级；冷启动还包含其他初始化。只覆盖一种实际地图规模，不能承诺Mod大地图的上限。
3. **高置信度：当前重建最突出的资源浪费是临时分配。** 每个来源重新分配整张40000格的工作数组；仅代价、距离、前驱和水陆累计数组合计约32字节／格，91来源累计约111 MiB，解释了大部分134.55 MiB分配。实测未测峰值和GC停顿，不能把累计分配写成常驻或峰值占用。
4. **后续建议，尚未实施：先消除每来源整图工作数组的反复分配，再决定更新粒度。** 若设施不改变参考地形通行，新增节点可以补新节点出边与原节点入边，拆除可更新端点和途经关系；若改变障碍或地形，则必须按真实依赖失效。当前每次约0.11～0.14秒的整网重建尚不迫使引入复杂增量算法，但也不适合在高频建设下无条件保留。
5. **剩余AI热点需另测。** 实际队伍条件、动态障碍和最终公共命令仍需复核。要继续降低军事决策开销，应分析这些927次搜索的相同输入复用、有效下界或走廊收益，保持实际通行语义一致；本次没有实现或宣称这些优化。

## 281年36旬补测

实验 ID：OAI-TRANSIT-PERF-281-20261105。日期、机器及规则版本与上文相同。本次按用户指定，改用虚构剧本内容ID19、种子281、全自动，从281年1月1日推进至282年1月1日；开局35个有效势力、700人物、0部队、96个有效交通节点。

测量探针在一个独立进程中完整运行3轮，每轮重新创建相同初始会话。首轮单列为冷运行观察，后两轮共72个旬样本汇总，不强制逐旬GC。每旬分别测量公共 `PrepareAdvanceOperation` 和10次 `AdvanceDayOperation` 的 `Apply`，合计为整旬时间，包含AI、每日模拟、会话投影和交通网更新；排除内容加载、会话创建、摘要、控制台与文件输出。完整验证另起进程，串行执行，不与性能探针并发。

| 指标 | 首轮 | 后两轮汇总 |
| --- | ---: | ---: |
| 36旬耗时 | 22.495 s | 平均14.310 s（13.886／14.733 s） |
| 整旬平均 | 624.86 ms | 397.49 ms |
| 整旬P95，线性插值 | 1222.28 ms | 593.33 ms |
| 最大单旬 | 1513.39 ms | 625.40 ms |
| 旬初准备平均 | 447.94 ms | 308.35 ms |
| 十天模拟平均 | 176.91 ms | 89.14 ms |
| 每旬累计分配平均 | 248.09 MiB | 246.27 MiB |

会话创建含开局建网：首轮715.37 ms，后两轮154.34／218.58 ms；不并入上表整旬时间。开局96次单源搜索、278208格展开。每年仅第21旬发生一次几何重建：新增施工设施后节点96→97、几何修订1→2；该重建所在的每日公共操作在后两轮为99.41／122.20 ms，包含当天其他规则，不能视为单独建网耗时。其余操作复用网络；日采样部队峰值34，年末15队。

后两轮工作9诊断平均每年7.410 s，约占整旬总时间51.8%，每年115次提交。性能探针的寻路诊断每年均为33132次、95087805格展开；三轮计数和最终会话诊断摘要完全一致，最终摘要SHA-256为 `FA6421962CC117EC2D4F9F28B567BD5B194B99671CC803CA8D5D8F3191EA6A62`。

独立Headless验证成功完成36旬、9次恢复及命令重放一致性检查，覆盖150次部队出征、40次据点占领、1次施工开始与完成。其主会话旬处理合计20.777 s、平均577.15 ms、P95 746.34 ms、最大797.25 ms；工具总耗时76.734 s包含另一会话的逐操作比较、恢复及重放，不与纯性能探针时间混算。该验证运行的工作9有25408次搜索、91918125格展开，是该运行自身的诊断计数，不与探针合并。

**推断：** 此轨迹下，持续开销集中在旬初准备及工作9，交通网几何更新频率低，不能用整网重建解释大部分年度耗时。先定位进攻预估中的实际搜索，比仅优化低频设施更新更有针对性。与 [v234的281年记录](../../docs/ai-performance-records.md#281剧本全年对照)涉及不同规则和军事轨迹，未作同策略对照，不能据两个版本的耗时直接认定交通网优化倍率或退化倍率。本次也不代表Godot画面、渲染或动画耗时。

复现入口：

```powershell
dotnet run --project local/generated/verification/traffic-performance/281-36/probe/YearPerformance.csproj -c Release -- C:/home/game/san9/remake
dotnet run --project src/San9.Headless/San9.Headless.csproj -c Release -- verify --content-package local/generated/content/scenarios/fictional/scenario-19 --output local/generated/verification/traffic-performance/281-36/campaign --xuns 36 --seed 281 --strategy autonomous
```

- [纯性能逐旬原始报告](../../local/generated/verification/traffic-performance/281-36/probe-report.json)、[暖运行汇总](../../local/generated/verification/traffic-performance/281-36/summary.json)、[探针源码](../../local/generated/verification/traffic-performance/281-36/probe/Program.cs)、[Headless验证报告](../../local/generated/verification/traffic-performance/281-36/campaign/report.json)。探针源码SHA-256：`8f7e2692b990f99060f2088278d226921a939311cda06298d2d00df4e6e2c044`。
- 281年 `scenario.json` SHA-256：`611051e56939cab71a9ce6d5047360665d53ff9e13c1904af78181c82cfc8b46`；`content.json` SHA-256：`7c511290d9c3adfb280b893b74bd6b8c8a834e83c7f5540575ff9bd55d575669`。
- 置信度：计数、网络复用及最终状态重复性高；耗时为中等，仅一个进程的两轮暖运行，最大值是观察结果，不是时延上限。未修改生产代码或据此宣称完整功能回归完成。

## v247：限定网内候选并撤除逐日预演

2026-11-05，用户明确普通进攻只考虑当前设施交通网能直接到达的设施，参考数据进一步替代公共估计暂缓，撤除出兵前逐日预演。v247删除网外逐对补查，保留网内来源资格、当前公共路线、实际编成的出发位置及60日粗格行程检查。不是通过减少导航展开预算实现加速。

沿用上节281年剧本、种子281、Release、独立进程3轮36旬的相同测量方法，后两轮共72个旬样本。原v246结果及探针保留，新探针只改变输出目录；两次测量之间生产代码变化为上述范围收口、预演撤除及规则版本更新。

| 指标 | v246 | v247 |
| --- | ---: | ---: |
| 首轮36旬 | 22.495 s | 19.236 s |
| 暖运行36旬平均 | 14.310 s | 7.891 s（8.196／7.586 s） |
| 整旬平均 | 397.49 ms | 219.20 ms |
| 整旬P95 | 593.33 ms | 280.16 ms |
| 最大单旬 | 625.40 ms | 300.21 ms |
| 旬初准备平均 | 308.35 ms | 131.23 ms |
| 十天模拟平均 | 89.14 ms | 87.97 ms |
| 每旬累计分配平均 | 246.27 MiB | 215.75 MiB |
| 工作9诊断每年平均 | 7.410 s | 0.909 s |
| 每年寻路诊断次数 | 33132 | 11210 |
| 每年寻路诊断展开格数 | 95087805 | 7232402 |

v247三轮计数及最终诊断摘要一致：每年110次工作9提交，部队日采样峰值34、年末9队；参考网全程96节点，没有几何重建。最终诊断摘要SHA-256为 `C127D6E5BA06A65718CB20A9ABC1DA50745B3497DE6DEA558E5E817A937A7323`。

**解释边界：** 候选范围收窄及预演撤除改变了实际决策，v246每年115次工作9提交、年末15队且有一次设施新增，与v247轨迹不同。因此397→219 ms是这次规则修正后的实际年度耗时变化，不能宣称同一军事轨迹的纯计算加速，亦未拆分两项修改各自贡献。暖运行时间下降主要集中在准备阶段，与减少进攻预估搜索的代码变化一致；不能据此证明参考路径进一步复用已经完成。

验证：默认解决方案7155项与独立Headless项目43项全部通过。新增回归区分参考网无边但实际快速阵形可达的目标、指定目标不放宽网外范围，以及普通出兵无需先逐日穿越后续窄口；网内慢阵形超60日仍拒绝、白马式港口和自动控制恢复继续覆盖。独立真实内容Headless验证完成36旬、9次恢复及命令重放一致性检查，覆盖151次部队出征、42次据点占领；[原始报告](../../local/generated/verification/traffic-performance/v247/281-36/campaign/report.json)成功且无首次失败。工具总耗时58.283 s包含恢复、逐操作比较和重放，不是纯运行耗时。

本地证据：[探针源码](../../local/generated/verification/traffic-performance/v247/probe/Program.cs)、[三轮逐旬报告](../../local/generated/verification/traffic-performance/v247/281-36/probe-report.json)、[暖运行汇总](../../local/generated/verification/traffic-performance/v247/281-36/summary.json)。探针源码SHA-256：`36384a5bb01ec4c0101453dd3c2d275d8a74ff75c8841743299d93c7b4ef1f07`。输入包哈希与上节相同。

```powershell
dotnet run --project local/generated/verification/traffic-performance/v247/probe/YearPerformance.csproj -c Release -- C:/home/game/san9/remake
dotnet run --project src/San9.Headless/San9.Headless.csproj -c Release -- verify --content-package local/generated/content/scenarios/fictional/scenario-19 --output local/generated/verification/traffic-performance/v247/281-36/campaign --xuns 36 --seed 281 --strategy autonomous
```

## v248：普通进攻直接消费交通边摘要

2026-11-05，按用户采用的近似原版流程，普通工作9使用参考路线的水陆费用、地形集合、地域顺序及实际相交中间设施完成来源预估和选阵前上下文。摘要费用从离开来源占地后计至进入目标占地前，速度变化只换算同一路线；参考路径受中间敌设施阻断或基础编成无所需船型时，才为已有网内目标查询替代路线。最终编成仍执行原有公共连通性、出发位置及粗格行程检查。

本轮没有扩大相同条件公共查询结果的复用：`MilitaryRouteQueryContext` 的键、存储及生命周期，`OriginalAiTerrainRoutes` 的公共地形路线缓存，军事工作的 `Refresh` 失效逻辑，以及公共粗格搜索算法均未修改。仅在地图增加内部地形ID读取，供建网生成通用摘要；没有调整搜索预算。相关文件的本地[源码指纹](../../local/generated/verification/traffic-performance/v248/unchanged-public-query-files.json)在验证结束时核对一致。

沿用281年剧本、种子281、Release、独立进程三轮36旬，首轮单列、后两轮72个旬样本汇总；新旧探针仅输出目录不同。

| 指标 | v247 | v248 |
| --- | ---: | ---: |
| 首轮36旬 | 19.236 s | 17.905 s |
| 暖运行36旬平均 | 7.891 s | 6.913 s（6.946／6.879 s） |
| 整旬平均 | 219.20 ms | 192.02 ms |
| 整旬P95 | 280.16 ms | 252.42 ms |
| 最大单旬 | 300.21 ms | 264.66 ms |
| 旬初准备平均 | 131.23 ms | 112.37 ms |
| 十天模拟平均 | 87.97 ms | 79.65 ms |
| 每旬累计分配平均 | 215.75 MiB | 211.29 MiB |
| 工作9诊断每年平均 | 0.909 s | 0.645 s |
| 每年寻路诊断次数 | 11210 | 8494 |
| 每年寻路诊断展开格数 | 7232402 | 4672943 |

v248会话启动含建网：首轮806.78 ms，后两轮223.99／223.90 ms，启动排除在整旬表之外。三轮最终状态摘要一致，SHA-256为 `8433D40CE21AF85A12F866A6E7CAD817B1466328B906C9D9A95BA3E3EF4C5BFF`；计数每年一致，工作9提交121次、部队峰值34、年末10队，全年96节点且未重建。

**解释边界：** 前期路线及摘要的采用口径发生变化，最终轨迹与v247不同（其110次工作9提交、年末9队）。因此219→192 ms是本方案的实际年度观察，不是同轨迹纯缓存加速，也不能把全部收益归到工作9；每日成本随局面变化。仅一个进程两轮暖运行，未固定CPU频率，最大值不是上限。

默认解决方案7159项和独立Headless测试43项通过。新增回归覆盖真实穿过／同粗格擦边设施、地域顺序、首尾占地费用裁切、水陆速度换算零搜索，以及暖网下因兵力不足放弃候选时零公共路线搜索。原有地形选阵、网内绕行、最终编成、拒绝和自动控制恢复继续覆盖。

新版独立真实内容Headless验证完成36旬、9次恢复及命令重放一致性检查，覆盖160次部队出征、50次据点占领；[报告](../../local/generated/verification/traffic-performance/v248/281-36/campaign/report.json)成功且无首次失败。工具总耗时59.043 s包含恢复、比较和重放，不是纯年度耗时。

本地证据：[三轮逐旬报告](../../local/generated/verification/traffic-performance/v248/281-36/probe-report.json)、[汇总](../../local/generated/verification/traffic-performance/v248/281-36/summary.json)、[探针源码](../../local/generated/verification/traffic-performance/v248/probe/Program.cs)。探针SHA-256：`0dc8df3ff32e2af36b50bd3e6a54ebcdd109fa51fb28a1bce452d11af94659d6`；输入包与上节相同。

```powershell
dotnet run --project local/generated/verification/traffic-performance/v248/probe/YearPerformance.csproj -c Release -- C:/home/game/san9/remake
dotnet run --project src/San9.Headless/San9.Headless.csproj -c Release -- verify --content-package local/generated/content/scenarios/fictional/scenario-19 --output local/generated/verification/traffic-performance/v248/281-36/campaign --xuns 36 --seed 281 --strategy autonomous
```

## 新旧城市关系对照与单向边

实验 ID：OAI-TRANSIT-CITY-COMPARISON-20261105。日期：2026-11-05。代码为当前v251交通网／工作9实现。本次只读测量，没有修改产品代码、内容包、时间阈值或存档，也未推进游戏日期；用于评估旧军事邻接退出的集合变化，不作策略收益验收。

### 比较口径与集合变化

旧表指该次测量时产品导入、现已退出的 `MilitaryCityConnections`，全50城224条有向边，与原作EXE的211条表分开。新网直接取生产 `GameSession.Start` 构建的城市到城市参考边，条件为每日行动力50、陆上5×5／水上4×4、等速、60日；不多跳，不施加敌我、途经设施通行及实际出兵资格。

本地已有20个正式剧本的实际开局均有49个有效城市节点，倭为休眠定义；这些开局的城市边集、参考日数及水陆费用一致。共同端点比较排除旧表指向／来自休眠倭的10条边。全50城补充比较只在独立几何副本中纳入休眠定义，不代表正式游戏已启用该势力。另以相同条件扩大到4096日解释缺边，其60日截取与生产网络逐条一致。

| 比较范围 | 旧表 | 新网 | 保留 | 新增 | 缺失 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 实际开局共同49城 | 214 | 286 | 203 | 83 | 11 |
| 全50城定义 | 224 | 288 | 205 | 83 | 19 |
| 45个非异民族城市 | 192 | 258 | 185 | 73 | 7 |

共同49城的旧边保留率94.86%，总边数净增33.64%。扩张集中在中原：陈留出边5→12、许昌6→13、汝南4→11、濮阳4→10。新增边参考行程44～60日；其中21条的参考摘要实际穿过其他城市占地，21条含水路费用，两项可重叠。途经某城不证明该城必经，参考边也不等于当前可执行的进攻关系。

共同49城缺失的11条全部在扩大搜索后可达，原因是超过60日：武威→天水62日，江陵↔江夏61／61日，江州↔交趾67／67日，柴桑↔山越69／70日，桂阳↔山越74／73日，柴桑↔江陵76／77日。纳入倭后保留北海↔倭54／54日；其余8条旧边为襄平↔倭84／84日、下邳↔倭62／62日、秣陵↔倭80／81日、吴↔倭74／74日。

只改变统一时间阈值不能恢复旧集合。以下仍比较实际共同49城，其余参考条件不变：

| 参考范围／日 | 边数 | 保留旧边 | 新增 | 缺失旧边 |
| ---: | ---: | ---: | ---: | ---: |
| 50 | 188 | 171 | 17 | 43 |
| 55 | 220 | 181 | 39 | 33 |
| 60 | 286 | 203 | 83 | 11 |
| 77 | 450 | 214 | 236 | 0 |

### 单向边的端点原因

旧派生表全部双向，新网出现天水→武威及柴桑→寿春两条单向边。这里比较的是 `SettlementTransitRoute.Reference` 的原始建边费用，区别于另行裁去源、目标内部路段的摘要费用。

当前参考搜索以来源中心格为起点，进入目标占地覆盖的任一粗格便到达；正反向独立选择路径和到达点，没有约定去程的到达点必须成为回程的出城点。本次两端城市占地均为25×25细格，天水→武威到达武威东南角 `(102,82)`，武威中心为 `(92,72)`，相隔横2粗格、竖2粗格；反向从武威出发时选的是东侧路径，城内最后一格为 `(102,72)`。天水两向均使用东侧，目标到达点 `(147,277)` 与中心 `(137,277)` 相隔2粗格。因此两端所谓“半个城”的费用不能直接抵消。

只将目标改为中心点，保留相同地形、来源中心、参考移动条件及搜索算法，得到：

| 方向 | 原目标占地费用 | 原参考日数 | 中心目标费用 | 中心目标参考日数 |
| --- | ---: | ---: | ---: | ---: |
| 天水→武威 | 3000 | 60 | 3200 | 64 |
| 武威→天水 | 3100 | 62 | 3200 | 64 |
| 柴桑→寿春 | 3000 | 60 | 3200 | 64 |
| 寿春→柴桑 | 3100 | 62 | 3200 | 64 |

端点附近每粗格费用50，参考每日行动力也是50。相对中心目标，正向少200点、反向少100点，恰好落在60日阈值两侧。这里是独立选路后的最优费用对照，不能将其误写为同一中心间路线在两端截断。两组地形均可双向通行，当前单向边来自参考端点与范围口径；这不证明所有潜在方向差异都具有同一原因，也未据此改变端点或补反向边。

### 证据与结论边界

结论是：交通网表达一定参考时间内的投送关系，旧表表达经城市竞争及绕行校准的局部城市联系。保留属城映射、只替换城市间关系，也会扩大部分关联集合、移除部分长途旧边并引入方向差异；不能称为关系集合的等价替换。没有军事工作执行、整体战局或真实行军结果的成对验证。

本地忽略证据：[完整关系与逐城对照](../../local/generated/verification/traffic-comparison/comparison.md)、[20剧本原始报告](../../local/generated/verification/traffic-comparison/report.json)、[四方向端点及路径](../../local/generated/verification/traffic-comparison/asymmetry.json)、[比较探针](../../local/generated/verification/traffic-comparison/Program.cs)、[端点探针](../../local/generated/verification/traffic-comparison/AsymmetryProbe.cs)。原始报告保存20个输入 `scenario.json` 的SHA-256，详细边集及生成物不提交。

```powershell
dotnet run --project local/generated/verification/traffic-comparison/TrafficComparison.csproj -c Release -- C:/home/game/san9/remake
python local/generated/verification/traffic-comparison/build_report.py
dotnet run --project local/generated/verification/traffic-comparison/TrafficComparison.csproj -c Release -- C:/home/game/san9/remake asymmetry
```
