# 设施行程关系预览

独立、只读的前端工具。以都市、港口、关隘及其他已建设施为节点，按静态地形参考行程展示限时可达关系；地域仅用于底图、边界和行政所属城市，不把未建设地域作为军事节点。默认281年，可切换转换包中的其他剧本来确定建筑配置。研究结论和后续改造边界见[EXP-0002](../../research/experiments/EXP-SAN9PK-0002.md#facility-travel)。

在仓库根目录启动，无需安装前端依赖：

```powershell
python tools/map-network-preview/serve.py
```

浏览器打开 <http://127.0.0.1:28119>；可用 `--port` 指定端口。运行窗口内按 Ctrl+C 停止。

- 连线截止时间默认60日，滑块可在5～65日调整；参考行程不超过上限才显示，不按每节点数量或全图总边数截断。
- 都市和其他设施使用相同的节点与估时方式；不要求同城或地域接壤。
- A到C的路线即使经过或临近B，也保留A→C；不做第三设施过滤、传递约简或城防绕行。
- 实线是限时可达关系示意，不是实际路径轨迹；双向合并显示，箭头保留仅单向处于上限内的情况。未连线表示超过上限或在静态通行图上不可达，不代表游戏中永久不可达。
- 右侧按参考时间显示可达设施及往返估时。所属城市关系单独用虚线图层表示，不参与可达资格。
- 滚轮缩放、拖动平移、地图点选、城市目录和设施搜索保持可用。节点图形及文字随缩放保持屏幕大小。

## 估时口径

固定引用内容包中的井阑（陆上，ID 8）＋楼船（水上，ID 22），只用作统一参考，不受玩家默认阵形影响。机动力从对应内容定义读取，当前分别为45和50。模型按四向粗格Dijkstra求路：每格5细单位，累计地形通行代价×5÷对应陆／水机动力；使用导航阻断格，不以地域出口替代导航。

每个来源建立最多65日的代价场，时间代价用共同整数尺度计算，保留范围内全部设施关系。建筑中心与静态阻断格重叠时，仅允许将其作为起终点，不把它变成贯通其他建筑的通路。已有设施不会因为属于某城市而自动形成边；途经其他设施不创建入城、停靠或分段命令。

这是地图研究用的静态地形估时，未包含阵形通行净空、真实出征出口、建筑避让和单位占用、战斗、外交、停留或补给，不能替代Core正式命令估时或出兵资格。不使用势力、控制权、兵力、武将和AI决策。参考阵形是本研究的选择，不是原作AI固定使用的阵形。

## 数据边界

仅使用 `local/generated/content/` 下的转换包。建筑来自剧本 `map.json`；当前内容契约把地形、导航阻断、阵形移动参数、地域和所属城市引用存于 `scenario.json`，工具只提取这些地图定义，不访问 `forces`、`settlements`、`officers` 等状态集合。名称来自转换后的文本表，建筑和地域身份分别保存。前端数据不含势力、归属、兵力或武将。

派生数据仅放在忽略目录 `local/generated/map-network-preview/`。预览文件缓存依赖地图、剧本地理定义、文本输入哈希与提取器结构版本；输入变化时原位更新。提取或估时算法改变须递增 `prepare.py` 的 `SCHEMA`，当前为8。这是离线预览缓存，不是尚未实现的游戏运行时关系表。

## 研究统计

旧`analyze.py`依赖已退出的军事城市表，现随该表删除，不再从当前内容包生成旧进攻来源对照。历史统计结果与证据保留在[EXP-0002](../../research/experiments/EXP-SAN9PK-0002.md#facility-travel)；复现当时的统计须使用对应Git版本及其内容格式，不为历史工具保留产品字段。

全量建表初步计时：

```powershell
python tools/map-network-preview/benchmark.py --scenario 19 --runs 5
```

对比65日与不限时的同一Dijkstra实现；每种模式预热一次，交替次序各测5轮，搜索在所有设施端点确定后提前结束，存在不可达端点时则耗尽队列。计时包含通行图与工作区建立、全部来源搜索和结果排序，不含文件读取、JSON写出、完整路线构造或AI规则；工作量统计另测，避免诊断开销混入计时。输出 `local/generated/map-network-preview/full-table-benchmark-19.json`，记录样本／脚本哈希、环境、每轮耗时及确定性核对。结果与限制见[统一实验记录](../../research/experiments/EXP-SAN9PK-0002.md#full-table-timing)，不改变预览65日上限及60日默认值。

C#独立探针只依赖.NET标准库，不引用或修改游戏Core／AI：

```powershell
python tools/map-network-preview/benchmark.py --scenario 19 --export-csharp
dotnet build tools/map-network-preview/csharp/FacilityTravelBenchmark.csproj -c Release
dotnet tools/map-network-preview/csharp/bin/Release/net10.0/FacilityTravelBenchmark.dll local/generated/map-network-preview/csharp-benchmark-input-19.json local/generated/map-network-preview/full-table-benchmark-csharp-19.json 10
```

输入包含相同静态地图及Python预期结果，输出全部关系的逐项核对、展开量、逐轮耗时、累计线程分配量和环境／源码哈希。C#每模式首次调用另记，随后预热3次，交替次序各计时10轮；验证在计时之外，进程启动、编译、加载和序列化不计入。通行图每次建表时建立，距离数组和优先队列在该次建表的各来源间复用；仅保存设施端点代价，不构造完整路线。分配量不是常驻或峰值内存，首次调用按65日后全量的顺序执行，不是两次独立冷启动。

## 验证

```powershell
node --test tools/map-network-preview/graph.test.js
python tools/map-network-preview/travel_test.py
python tools/map-network-preview/verify.py
```

测试覆盖精确截止边界、方向合并、身份引用、地形绕行、隔断、陆水机动力、阻断端点、途经其他设施仍保留关系，以及真实剧本建筑差异和数据边界。不修改游戏Core、AI、内容契约、存档或原始剧本，不读取原版EXE或S9文件。
