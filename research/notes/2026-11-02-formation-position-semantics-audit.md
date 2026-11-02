# 阵位代码、候选索引与行列保存专项排查（2026-11-02）

- 取证ID：`AI-FORMATION-POSITION-AUDIT-20261102`；作者：Codex。
- 关联：RE-SAN9PK-0017／0076／0082／0126；承接[刘表救援核查](2026-11-02-liubiao-relief-order-formation-audit.md)。
- 范围：原版阵位生产者与几何消费者、重制导入、AI排位、出征草案／预设、公共部署命令及详情投影。下文保留修正前调查事实，后续授权实现见下节。
- 结论类型：原作静态事实、重制实现／公共诊断事实分别列出；未做原游戏同局面动态复现。

## 后续修正（2026-11-02）

用户随后授权修正。规则v241、存档136、命令脚本161、剧本世界152／组合包244已采用完整`FormationPosition`，部署、世界、详情、存档与显式命令脚本共享列和行位；单将后备按中排行位及水平中心选位，不再把原版几何代码当作候选索引。主将变更保留幸存成员位置。普通入城显示进军，强制返回显示撤退并取服务据点目标；输送临时接敌的既有目的地显示约定保留。据点交战状态、四类用词及按内容读取兵法名一并修正。下文失败结果是修正前诊断，不能当作当前缺陷状态。

永久回归见`tests/San9.Application.Tests/FormationPositionRegressionTests.cs`、`OriginalAiMilitary.SupportPositionTests.cs`、`UiLabelRegressionTests.cs`及既有Importer／继任测试；覆盖左右列往返、六种单将入口与候选行组合、显示命令／目标、据点状态优先级、兵法改名及非原版ID。最终解决方案7024项及独立Headless43项通过；未做原游戏动态或Godot画面验收。用户提供的旧存档保持只读，不猜测迁移。

## 结论

1. **直接错读仍是同一处代码，但有两个正式入口。**`OriginalAiMilitary.Equipment.cs`的单人后备把几何代码4当成`CaptureRanks[4]`。除58组2城市救援外，51的非城市来源搬空转移同样触发。仅在第5候选位置恰好是中列时不暴露错误；它不是长蛇或刘表的专属问题。
2. **另有完整位置丢失。**玩家草案和预设保留行、列，但提交出征时只转换为`BattleRank`。世界只保存成员行位，详情窗口再按主将／人物ID和阵形候选列重排。因此同一行的左右选择不能进入部署命令，AI的准确候选位置也不能被详情还原。这与上一项的代码／索引混淆不同，修复仅调整单人后备的行位不足以保证中央站位。
3. **本轮未发现其他直接的几何代码／候选索引混用。**普通AI主计划的候选索引、导入的`code%3`／`code/3`转换、草案换阵及预设行列保留均有对应链路；不把未覆盖的Mod布局、水上规则或完整对局宣称为已穷尽。

## 编号与所有权

| 数据 | 含义 | 合法用法 |
| --- | --- | --- |
| 原版阵形候选索引 | 最多五个人员位置中的第几项 | 先经`4487F0`读取该阵形的几何代码 |
| 原版几何代码 | 3×3行优先的0～8；9表示无位置 | 列=`code%3`，行=`code/3`；4是中列中央 |
| 重制`CaptureRanks[index]` | 该阵形第index个候选位置的前／中／后行 | 按候选索引查行，不接受几何代码 |
| 重制草案／预设`FormationSlot` | 完整列、行 | 本身不需要再查候选索引；目前提交时列信息被丢弃 |
| 重制`MemberRanks` | 成员前／中／后行 | 能供行位规则使用，不能唯一还原完整位置 |

## 原作生产者排查

样本与工具沿用前篇：繁中PK1.0.1.0、PE32／i386、基址`0x400000`，2636800字节，SHA-256 `d20794aeff67301ec2bf8c3becb1e9944c68c6c0588fbfd4bf04e8597f0e5028`；Windows、IDA9.5／Hex-Rays9.5.0.261026、IDA Domain API0.5.1，经MCP读取项目主库`research/analysis/San9PK-TC-101.i64`。

本轮逐一检查计划位置保存器`48BA30`的9个直接调用点，归属7个函数：

| 生产者 | 位置参数来源 | 裁决 |
| --- | --- | --- |
| `4A2A88`，58主计划两处分支 | 排位起始候选索引经`4487F0`转换 | 参数是几何代码，普通／异民族起始索引策略独立 |
| `4B9DA0`，9／59主计划两处分支 | 同样先用阵形表转换候选索引 | 与重制主路径按`CaptureRanks[start+i]`取行对应 |
| `4B9630`、`4B96C0` | 同样先读取候选位置代码 | 未出现把字面量4解释为第5候选位置的依据 |
| `4AD7C0` | 候选索引经阵形表转换 | 独立工作10的证据，不扩大标准旬调度采用范围 |
| `4B9AF0` | 字面量4，直接保存 | 单人后备固定中央；重制错读点 |
| `4BB670` | 字面量4，直接保存 | 51选择单人后写中央；`4BBA00`分流普通输送或后备出阵 |

`4BBA00`在非城市来源本次派兵等于全部驻兵时调用`4B9AF0`，因此51和58共享该错误不是仅凭调用关系推测，后文另做公共命令运行。

人物位置写入器`44CCE0`的其他直接调用也核对了用途：建设／输送／出征执行器从计划复制完整代码；`448870`按阵形表分配；小兵转正、人物初始化、被俘、失势迁移、部队清理写9清空，不是候选索引。交互准备`4E7DB0/4E8400/4E8720/4E8CC0/4E9140/4E9230`在索引和几何代码之间显式转换；`4E8720`保存完整代码，最终经`48BD20 → 44CCE0`写人物，`590AD0`按完整行列放置。

本轮对21段关键函数共10684字节重新对照登记磁盘EXE，全部一致，逐项摘要见本地账本；其他人物清空调用为只读补查，不计入该21段对盘数。没有把库中旧名称当作数据语义证明。

## 重制生产链与范围

| 链路 | 结果与限制 |
| --- | --- |
| `San9ScenarioFormationRecord.GetDeploymentSlotPosition` | 先取`DeploymentSlotCodes[officerIndex]`，再模3／除3；没有直接把index当几何代码 |
| `MilitaryContentCompiler`／`ScenarioContentCompiler` | 分别从同一解析坐标生成候选行位数组和显示行列数组；两者下标都是候选索引 |
| AI普通／异民族主计划 | 取武力排序、起始候选索引及对应行；现有Attack／Deploy／FulfillRequest的普通／异民族六项对照通过。没有把这里正常的数组索引替换为固定Middle |
| AI单人后备 | 两入口均使用`CaptureRanks[4]`；公共运行复现见下节 |
| 草案和预设 | `DeploymentDraft.Positions`与`DeploymentPresetMember.Position`保存行列；换阵／保留／预设应用的现有对照通过 |
| `DeploymentTargets.CreateCommand` | `Positions`仅以Row转换为`Ranks`，Column未进入命令；同一行不同位置产生完全相同命令 |
| `MilitaryMovement.ApplyDeployment` | 只保留命令行位或候选数组默认行，没有完整位置可以继续保存 |
| `UnitDetails.Read → FormationBoardView` | 详情成员不设置完整`Position`；按行、主将、ID排序后，在该行候选列中重新分配，不能恢复原位置 |
| 船阵／建设／普通输送 | 船阵候选行目前统一编译Middle；普通建设／输送走固定阵形和候选行默认值。未发现第二处把几何字面量当候选索引的生产代码；完整左右位置仍受上述公共信息丢失影响 |

本地已有190年转换包提供直接对照：鱼鳞0候选几何代码为`1,3,4,5,7`，长蛇6为`0,3,4,7,8`。因此长蛇第5候选位置确为后右，其行是Rear；后列单将由详情重排到该行首个候选列，成为截图后中。即使把行修正为Middle，详情仍会先选中左代码3，不能据此宣称已经固定中央代码4。建设13／输送14的中列候选依次为左、中、右，单将几何中央也不能只靠Middle唯一表达。这些是转换包及显示代码事实，不新增原作动态观察。

## 可重复诊断

复用现有`OriginalAiMilitaryTests.Fixture/Harness`、`SupportReliefFixture`、非城市搬空转移夹具及`DeploymentDraftTests.View`，临时加入`FormationPositionAuditProbeTests.cs`，未复制仓库或创建新的审计工程。测试结束后该文件已移出正式测试目录，保存在忽略目录`local/generated/research/formation-position-audit-20261102/`，没有遗留失败用例。

### 固定中央不应依赖第5候选位置

对58组2、51非城市搬空后备分别只改变候选行数组的第5项为Front／Middle／Rear，其余计划和局面固定。每次实际执行工作、提交真实公共命令，并核对公共执行后的世界行位与命令相同；要求不抽规划随机数、恰好一个单将出阵、来源确为预期建筑且任务为Return。

| 入口 | 第5项Front | 第5项Middle | 第5项Rear |
| --- | --- | --- | --- |
| 58组2 | 实际Front，中央断言失败 | 实际Middle，通过对照 | 实际Rear，中央断言失败 |
| 51搬空非城市 | 实际Front，中央断言失败 | 实际Middle，通过对照 | 实际Rear，中央断言失败 |

六项均由公共引擎接受并形成真实世界状态；四项失败准确复现固定中央被候选数组内容支配，两个Middle是掩盖错误的正常对照。Front／Middle变体是用于隔离根因的自定义合法内容，不宣称原版默认长蛇有这些不同布局。

### 同行左右位置丢失

使用同一草案、成员1、鱼鳞式布局100、同一目标、命令ID和序号。先移到`(0,1)`生成命令，再只改到`(2,1)`生成命令。两次Ranks都是Middle，完整命令JSON完全相同，“不同列应产生不同部署内容”的断言失败。该项验证到命令边界，未声称运行Godot或两个原游戏局面；后续世界与详情只能读取这个相同输入，无法还原差别。

### 实际检查结果

- Application定向29项：24通过、5失败。其中新增诊断7项为2正常对照通过、5缺陷断言失败；其余22项现有普通／异民族主计划、换阵和预设对照全部通过。这是调查结果，不是产品修复后的全绿验证。
- Importer定向50项全部通过：阵形档案、内容编译及源档案字段转换。
- 原版静态对盘、IDA注释回读及文档引用检查完成；没有运行默认解决方案、Headless或画面验收，没有重转包。

复现时将归档的临时C#文件放回Application测试目录，运行：

```powershell
dotnet test tests/San9.Application.Tests/San9.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~FormationPositionAudit_|FullyQualifiedName~LowWarBarbarianDeploymentStartsAtTheFrontWhileOrdinaryDeploymentKeepsItsOffset|FullyQualifiedName~DeploymentPresetTests|FullyQualifiedName~FormationChangeResetsPositionsAndRosterConfirmationRetainsValidPositions' --logger 'trx;LogFileName=formation-position-audit.trx' --results-directory local/generated/research/formation-position-audit-20261102
dotnet test tests/San9.Importer.San9PkTc101.Tests/San9.Importer.San9PkTc101.Tests.csproj --no-restore --filter 'FullyQualifiedName~San9ScenarioFormationArchiveTests|FullyQualifiedName~MilitaryContentCompilerTests|FullyQualifiedName~San9ScenarioArchiveTests' --logger 'trx;LogFileName=formation-import-audit.trx' --results-directory local/generated/research/formation-position-audit-20261102
```

两份TRX及探针源码均在上述忽略目录。后续修复应保留几何位置的明确所有权与命令／世界／查询的一致性，避免按具体阵形或武将给详情窗口补特殊列。

## 主库标注及回读

本轮向`4BB670`、`4E8720`各追加一条普通地址注释，分别注明51的字面量4是几何中央，以及交互准备保留完整位置的生产链和重制列信息丢失；均关联本记录及既有RE编号，静态高置信度。2／2全文回读、重新反编译去注释／空白正文一致，主库原位保存。没有改名、类型、边界或程序字节变更。实际原注释、追加内容、字节摘要及回读保存在`local/generated/research/formation-position-audit-20261102/ida-write-record.json`。
