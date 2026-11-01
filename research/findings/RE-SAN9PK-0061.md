# RE-SAN9PK-0061：自创武将君主的新局城市兵役转化与增兵

- 状态：规则正文已于2026-11-01并入[新武将权威研究RE-0202](RE-SAN9PK-0202.md#自创君主城市兵役转化与增兵)。本页保留原取证身份和标注记录。
- 原取证日期：2026-08-22；直接调度与适用对象纠错：2026-09-06。
- 原来源：`SRC-SAN9PK-TC-101`，样本身份沿[来源登记](../sources/SRC-SAN9PK-TC-101.md)；原分析工具IDA9.3／Hex-Rays／IDA MCP。
- 原证据：静态调用、字段与公式核对，及用户观察的开局士兵差值；观察未登记君主ID的限制已迁入RE-0202。

## 适用人物与当前重制范围

见[权威正文](RE-SAN9PK-0202.md#自创君主城市兵役转化与增兵)与[既有范围决策](../../specs/decisions/DEC-SCOPE-0001-scenario-scope.md)。本页不再重复公式或维护另一份规则。

## IDA 写入与回读

- 恢复 `0x456C30..0x456D09` 的完整函数边界，并命名为 `initialize_primary_force_leader_cities`。
- 将 `0x43A360`、`0x43A3B0`、`0x4373D0`、`0x43FDE0` 分别命名为 `get_city_draft_population`、`subtract_city_draft_population`、`add_city_soldiers`、`get_force_leader`，并应用 `San9CityRecord`、`San9ForceRecord`、`San9PersonRecord` 参数和返回类型。
- 将 `San9CityRecord` 的已确认尾部字段收敛到 `+0x1BC..+0x1EC`，保留未确认区域，总大小仍为 `0x1F0`；`resolve_person_current_city` 的返回类型同步为 `San9CityRecord *`。
- 在上述函数及 `sub_44C230` 写入事实和限制注释；后者只确认虚表 `+0x4C` 返回值位于 700–799，不强加业务名称。
- 强制重新反编译后，50 项遍历、筛选链、兵役人口字段、转换公式和城市/势力/人物类型均回读成功；未修改样本程序字节。

## 2026-09-06 调度与适用对象纠错

直接调度证据、运行时ID门、历史误判撤销及适用反例已迁入[权威正文](RE-SAN9PK-0202.md#自创君主城市兵役转化与增兵)，本标题保留供旧引用定位。

本轮向 `0x456C30/0x45C120/0x561430/0x44C230` 同步上述事实及旧结论撤销，保存后回读并重新反编译。见[疑点复核写入清单](../archive/ida-write-records/research-questions-audit-2026-09-06.json)。没有恢复新的函数边界。
