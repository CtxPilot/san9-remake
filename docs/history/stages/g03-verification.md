# G03 经营与兵员循环验收

> 历史记录：版本、命令、能力与限制均以正文标注的验收时点为准，不作为当前使用说明或重复验收要求。当前入口见[文档总览](../../README.md)。

初版验收日期：2026-09-12。原G03a、G03b按六项经营范围验收；该范围排除的买卖未被安排后续归属，不能据此声称原作八项经营完整。2026-09-20已补齐城市商人与粮食买卖的规则及公开协议，详见本文末节。行为依据为[BEH-ECONOMY-0001](../../../specs/behavior/BEH-ECONOMY-0001-domestic-and-economic-cycle.md)，基础计算沿用[BEH-DOMESTIC-0001](../../../specs/behavior/BEH-DOMESTIC-0001-base-development-effect.md)。本轮没有启动游戏，正式经营菜单仍待后续界面阶段。特殊对白也按用户确认延后至UI接入时处理，触发时机已记入[RE-0095](../../../research/findings/RE-SAN9PK-0095.md)，本轮不扩展对白事件或表现。

## 实际行为与边界

商业、开垦、巡察、修筑、征兵、训练支持1–5名参与者，共享资格、占用和行动校验；多人组合使用规范化人物对与独立随机流。征兵在兵员、资金和驻军容量之间取可执行数量，训练更新士气与兵法熟练度。君主军团与子军团的独立金粮账户统一路由，资源事件记录请求量、实际量、短缺和上限损失。

旬前置更新发放俸禄，每日扣粮；经过至少30日后，季度结算人口、预备兵员、信望和资金，七月结算收获。收入使用人口平方根，信望使用超过300的民心与人口乘积，两者公式不同。开局、30日门、七月、跨年、零资源、上限和多人随机均有针对性验证。

审计后恢复原作俸禄双向概率忠诚和异族季度/七月补给，取消欠薪必减1、支付不增忠诚及统一普通经济的选择；内政排序改为身份优先、能力与ID分离。当前规则与验证见[审计修复验收](#2026-09-12经营底座审计收口)。粮食不足扣至零并报告，饥饿伤亡仍属于战争后勤范围。简单策略仅选择可执行经营命令，尚不是完整经营或战略AI；出现欠薪不能把确定性验收解释为策略质量达标。

## 内容链路与协议

源数据、转换包、会话复制与查询核对覆盖700名保留人物（源800槽）、25势力、25军团、94据点、76官职；核对相性、习得兵法、官职、人物位置、基础信望、驻军容量与粮耗资格。组合表为巡察12、商业7、开垦5、修筑5、训练12对，使用实际人物ID。190年初始无野战部队，不能据此声称已验收真实部队后勤。粮耗排除集合为据点107；初始总俸禄1232，董卓势力首次扣款444。

研究依据及本轮静态核对、IDA注释回读记录见[RE-0096](../../../research/findings/RE-SAN9PK-0096.md)与[RE-0098](../../../research/findings/RE-SAN9PK-0098.md)。没有新增原作动态实验。审计报告在忽略目录`local/generated/verification/roadmap-g03/input-audit.json`。

内容schema为12，规则版本为`san9-core-g03-v1`，存档/操作日志格式为4；录像格式仍为2，摘要版本仍为1。新经济定义进入内容指纹，新世界字段进入保存恢复；旧内容包须重新转换，旧规则存档明确拒绝，不提供迁移分支。

## 实际验证

完整解决方案480项通过（Core104、Content29、Importer183、Application63、Client93、Headless8）；随后新增3项组合测试并运行Core，107项全部通过。Python领域契约验证器测试3项通过。纯文档链接与差异另作检查。

固定种子190、36旬，以下四种运行全部成功；每种均比较10个恢复点、最终存档字节、状态与事件，并完成操作日志和录像重放。公开场景两种策略均覆盖六类命令。

| 内容与策略 | 内政命令 | 等待 | 欠薪金额 | 恢复点 | 验收总耗时 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 公开夹具，脚本 | 72 | 0 | 0 | 10 | 674 ms |
| 公开夹具，简单策略 | 72 | 0 | 0 | 10 | 694 ms |
| 190年，脚本 | 884 | 16 | 1224 | 10 | 69937 ms |
| 190年，简单策略 | 874 | 26 | 1840 | 10 | 70304 ms |

本次短缺全部为俸禄，粮食短缺与资源上限损失均为零。耗时包含恢复/重放对照及报告写入，不是游戏单次推进耗时或性能门槛。公开夹具指纹为`B6F81E64A57AFF2578F274A49F4BCE9FCCDC65D3FC3A8A48528869B304EB96FA`，190年为`456691863684DEC8E27B2F396C3A2FA94D7E85D679F89064ADF6B735F609A8D7`。

复现命令（生成物均不入库；190年包先按[内容管线](../../client-content.md)重新转换）：

```powershell
dotnet run --project src/San9.Headless -- verify --fixture --xuns 36 --output local/generated/verification/roadmap-g03/public-scripted
dotnet run --project src/San9.Headless -- verify --fixture --strategy simple --xuns 36 --output local/generated/verification/roadmap-g03/public-policy
dotnet run --project src/San9.Headless -- verify --scenario local/generated/content/scenario-190-live/scenario.json --xuns 36 --output local/generated/verification/roadmap-g03/private-scripted
dotnet run --project src/San9.Headless -- verify --scenario local/generated/content/scenario-190-live/scenario.json --strategy simple --xuns 36 --output local/generated/verification/roadmap-g03/private-policy
```

每个输出目录保留`report.json`、命令/事件日志、录像与恢复对照产物。领域契约检查仍仅报告既有`BEH-UI-0002..0006`未分类问题（归G13），没有新增G03问题；不能将该检查描述为通过，报告为`local/generated/verification/roadmap-g03/core-domain-contracts.json`。

## 2026-09-20城市商人与粮食买卖补齐

- 修正范围遗漏：将原先排除后未归属的城市商人刷新、买进和卖出纳入G03经营规则。常驻必在、其他源城市每旬20%在场，使用位每旬恢复；当前粮价沿全局世界状态，城市不保存独立价格。商人图标由当前在场状态派生；该显示方式区别于原作仅标识常驻的图标。
- `GameSession.GetFoodTradeQuote`提供当前账户、粮价、数量上限和不可交易原因；`FoodTradeCommand`提交方向、城市、1至5名人物与粮食量。上限按有效政治的执行能力换算，买进向上取整成本、卖出向下取整收入；成功占用全部参与者与同城共用交易机会，不成长、不随机加倍。提交按实时状态重新验证，失败零写回。独立军团账户与控制权限共用现有规则；未新增AI主动交易策略。
- 33项Core及5项Application用例覆盖严格概率、常驻短路、无主城、设施排除、两方向整数边界、执行政治、多人／占用、库存容量、共享旬次数、独立军团权限、动态图标、严格内容及指纹、恢复不重抽和完整录像。`dotnet test San9Remake.slnx --no-restore --verbosity quiet`全量2951项通过：Core2044、Application384、Content74、Importer338、Godot98、Headless13。
- 真实190源完整内存编译和校验保留25势力／94据点／700人物；50城市逐项映射7常驻／43流动，设施不配置商人。真实洛阳公开买粮、保存恢复及录像通过，证据见[RE-0097复核](../../../research/findings/RE-SAN9PK-0097.md#2026-09-20城市交易接入复核)。三条IDA注释保存回读通过。产物位于忽略的`local/generated/verification/g03-food-trade/`。
- 当前剧本schema98／组合102，规则`san9-core-g10-v35`，保存／日志76。旧包须原位重转；完整运行包和买卖菜单／画面仍待界面阶段，未启动游戏，不将公开会话和投影测试当作视觉验收。


## 2026-09-12经营底座审计收口

历史规则v18恢复俸禄的义理／信望／相性概率忠诚、异民族季度资金赋值及七月粮食目标，并将内政排序中的君主身份、能力与ID破平分开。v19另在新局入口清除玩家选中异民族势力的特殊身份，读档不重新归一化；公式与采用边界见[经济规格](../../../specs/behavior/BEH-ECONOMY-0001-domestic-and-economic-cycle.md)、[RE-0098](../../../research/findings/RE-SAN9PK-0098.md)及[RE-0021](../../../research/findings/RE-SAN9PK-0021.md#2026-09-12新局选择异民族势力的标记清除)。

v18的190年内容、种子190年度脚本运行36旬，接受874条经营命令、1837项会话操作，10次磁盘恢复及重复日志均一致；发薪加忠诚99次、欠薪减忠诚77次，异民族季度资金／七月粮食事件20／5次。1840次短缺均为俸禄，粮食短缺与饱和损失为零。25势力、25军团、94据点、700人物、120宝物保持完整；初始无野战队，野战兵粮食目标由公开夹具另验。

内容指纹`F52AF21CC8560F579F2BB37DB9FDA9DCDA03534C4FD6F7ABAF8589B446472D23`，终态哈希`E1D7FB41B53A17F1AF6EB5A650E92F84F333AAC4FF965BC78DB6F3E5C3AE5C17`；报告目录`local/generated/verification/g01-g04-audit/private-policy/`。119142ms包含恢复、重放和并行验收，不作为性能基线。

v19解决方案763项通过，覆盖玩家选择、源内容不变、跨季度／七月、保存恢复和随机一致；没有重跑上述v18年度脚本。固定驻军十年夹具不等于完整AI十年对局。没有原作动态或Godot画面验收；当时的录像结果不恢复为当前要求。
