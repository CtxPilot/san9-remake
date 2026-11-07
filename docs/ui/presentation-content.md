# 界面与演出内容契约

本页维护显示资源角色、引用约束及重建依赖。转换／发布见[内容管线](../client-content.md)，交互与领域规则由相关BEH维护。源文件编号、调色板和偏移只由Importer解释，客户端按语义角色加载；历史包号与测试流水不作为重建依据。

## 开局与目录资源

`shared/opening.json`唯一保存城市中心和逐行区间分区，绘图与命中共用；剧本页通过整页投影和制作期配准匹配烘焙标记，势力页按真实初始归属绘色，不维护逐城覆盖表或第二套城市范围。两页保留各自底图／补图／色标，倭岛保留岛、海、框并排除大陆边角，不合回蓝海框。

开局定义必填`quitPrompt`，`settings`与`audio`声明美术、循环音乐／进入声及语义文件引用。创作／部署／取消部署按钮、`joinForcePrompt`、`newForces`八项按钮布局、文字锚点、头像和背景均须声明；`opening-city-marker`用于都市金色／禁用标记，势力与都市分别使用`opening-panel`和`opening-city-panel`。规则见[新游戏](../../specs/behavior/BEH-UI-0011-new-game.md)，源布局见[RE-0195](../../research/findings/RE-SAN9PK-0195.md)与[RE-0202](../../research/findings/RE-SAN9PK-0202.md#2026-11-01开局增兵年龄与原版布局复核)。

## 目录与地图元数据

| 字段／目录 | 契约 |
| --- | --- |
| `Information.MapContextExcludedFacilityIds` | 必填设施类型集合，空无额外排除，未知／重复引用拒绝。障碍标牌和悬浮由`FacilityRule.IsWork`派生，不与右键排除混用 |
| `ProvinceNameKeys`、`IsUnranked`、`DescriptionKey` | 州名映射可省略并显示“—”；无爵位标记至多一项，说明必填可空，非空文本引用闭合。源州／爵位编号不进入运行时 |
| `Information.Tactics`／`Formations` | 必填条目引用规则ID和名称键，名称非空、说明可空；不保存按势力派生的可用表。TC101保留41兵法／28阵形元数据，完整定义不等于全部可选 |
| 官职／爵位说明 | 文武分组、提前分配、君主平衡偏好与`GovernorPriority`表达规则语义，不按说明文字或ID推导；忠诚使用公共薪俸整数折半规则 |
| 都市`EnabledTacticIds` | 合并剧本位域与制作期新局追加，去重并保留落石／射台。地图与目录同读规则；初始态变化需重新建局，刷新窗口不能改变旧世界 |
| 阵形授权 | `IsCommon`与当前生效`FormationGrants`并集去重，排除固定建设／输送配对；授权显式声明`Always`或`Barbarian`。身份进入世界和存档，恢复不重新派生；资格由Core共享 |
| 表现身份 | `FormationCatalogDefinition.MapAppearanceId`引用地图外观，`TacticCatalogDefinition.BattleAppearanceId`必填可空。玩法身份可独立于外观；船效果从`AutomaticTactics`及名称派生，非空引用闭合 |
| 阵形`Presentation` | 必填可空，声明展示网格、默认阵位、视野半径／角度与底图；校验几何、重复位和资源。视野展示不增加额外索敌规则 |

目录当前候选及三页效果见[情报界面](information-browser.md)，阵形、水陆与运行历史见[领域输入](../client-content.md#领域输入与派生数据)。源授权映射和原版候选范围留Importer，不限制Mod身份集合。

## 地图单位与动画

| 角色／定义 | 契约与资源所有权 |
| --- | --- |
| `MapUnitBody`与`MapUnitAttack` | 声明规模布局、八向待机／攻击、可选帝王身体、状态与地利；人物旗字独立，君主帅旗，多挂点及深度按身体定义。近／远攻击声明动作、弹体与发数，同一变体供时长、绘制与真实帧发射使用 |
| 水陆与朝向 | 水阵独立保存，基础船由规则声明；有效阵型用于跨岸、战斗与显示。初始Facing来自内容，Core按净移动更新，战略待机不按目标重算 |
| `Retirement`与退场声 | 可空八向终止动作、附加效果、延迟／淡出／节拍及独立声音族；可显式关闭。对象删除后有限视觉继续持有，完成释放，不让资源可见性决定死亡结算 |
| `ConstructionClips`与`ConstructionCompleteSound` | 前者必填八向集合，无动作可空集合；后者为地图共用必需完工声。施工成员来自真实建设关系及已呈现事实，循环声、动画、完工脉冲分别消费 |
| `Battle.ReinforcementEffect` | 必填可空补兵特效，非空校验图片、帧参数和声音；只按成功救援事实播放，不借异常状态效果 |
| `FormationAppearance` | 必填中央组替换集合，声明目标阵型、中央兵模及布局来源阵型，独立作者可空集合。运行时从基础阵型派生数量／坐标，不维护第二份完整布局 |

攻击配方分别声明身体／弹体启动、齐射间隔和动作日阻塞，普通身体与弹体寿命独立；同阶段实际准入对象等待最长者。地图碰撞消费领域footprint，不读身体布局。详细行为及TC101映射见[地图动画](../../specs/behavior/BEH-RENDER-0003-map-ambient-animation.md)、[RE-0156](../../research/findings/RE-SAN9PK-0156.md)、[退场／施工证据](../../research/findings/RE-SAN9PK-0139.md#2026-10-24部队退场动画喊话与音效复核)。

## 命令页面与显示资源

| 页面／角色 | 声明与校验 |
| --- | --- |
| `command-*`及摘要 | 六组分类／叶子三段四态按钮、拼接框、说明插图、条件卡和三类摘要；`ui.command.*`为分类／条件，`ui.command-hint.*`为底栏用途，实际可用性与拒绝原因由当前公共查询提供 |
| 阶段与确认框 | 阶段字图八帧、静止四态、进度框／填充及`dialog-frame/yes/no`。源确认框在Importer无缩放拼成216×136九宫格，保留逐色Alpha，客户端平铺边和中心；不为每个窗口导新图，不接受旧别名 |
| `deployment-*` | 外框、字段、装饰、陆水标签、数字键盘、勾选／总括／决定及方针状态。背景裁切遮罩转Alpha，纹理就绪且尺寸变化时重建临时背景，窗口退出释放 |
| `DefaultDeploymentFormationId` | 可空，非空引用可选陆阵，不能为建设、输送或无阵；只影响草案初选，不改变公共命令或AI选阵 |
| `target-*` | 标题／底框、四态一览与中止、目标标记、八帧路线点、两季四帧中继旗和部队悬浮卡；`BuildingTargetOffsets`按显示类型完整声明 |
| 目标遮罩 | Rendering按地形四角高差生成黑色与地利红色共用遮罩，shader定色，不导出中间`target-mesh-*`贴图 |
| 路线／中继动画 | 路线点32×32、加色权重192/256，按路径序号错相；中继帧统一76×68，旗杆锚点(38,67)，底部中心定位、142ms换帧，按季节和对象深度绘制 |
| 标记与拾取 | `icon-commanded`只显示当前玩家己方本旬已下令队伍；重叠名单`map-object-picker-*`／`map-object-row-*`为必需角色。下令标记不替代每日执行历史 |
| 光圈／箭头／情报标记 | 命令与记录小地图脉冲各五帧，红蓝目标箭各四帧；`BuildingArrowOffsets`、摘要／选中标记及喊话布局由皮肤声明 |
| 光标 | `Cursors`引用PNG和像素热点，八向滚屏idle／active共16项；核引用／尺寸／热点，不能表达的反色像素拒绝转换。共享租约交给Godot系统光标 |
| 设置与存读档 | `environment-label-*`／`environment-choice-*`复用公共窗口；`save-load-*`声明背景／槽／页签／备注／确认角色，`scenario.title`为必需预览，头像复用Face。存档格式与摘要读取归[存读档规格](../../specs/behavior/BEH-UI-0007-save-load-browser.md) |

源码编号、调色板与裁切证据见[RE-0073](../../research/findings/RE-SAN9PK-0073.md)、[RE-0175](../../research/findings/RE-SAN9PK-0175.md#真实光标素材接入2026-09-27)、[RE-0177](../../research/findings/RE-SAN9PK-0177.md)、[RE-0178](../../research/findings/RE-SAN9PK-0178.md)及[RE-0179](../../research/findings/RE-SAN9PK-0179.md)。具体选择、草案、输入锁和下令顺序见[战略界面规格](../../specs/behavior/BEH-UI-0001-initial-strategy-screen.md)。

## 消息、事件与媒体

动态消息以人物姓／名／肖像、类型化对象、快照属性及具名历史绑定，不依赖源倒序编号；`MessageBinding.Return`显式提供归队类型，不从当前选择推断。运行消息库排除已确认资料记录，发布只清理旧清单拥有文件，不删除用户自建文件；就绪表示绑定闭合，不表示所有原窗口／台词已有消费者。显式诊断保留源适配的哈希、决定及移除字节，严格原文研究导出不变，详见[消息契约](messages.md)。

| 表现定义 | 必需内容与边界 |
| --- | --- |
| 巡察／`personnel` | 巡察消息、NPC、地域和语义声；召来／移动／探索／登用的出发、推荐、结果、俘虏／携城及九类中止／归队／退款角色。姓名、人物和地点通过引用绑定，源号留Importer。提交前无副作用，显示只消费一次结果，见[巡察](patrol-presentation.md)和[人物行程](../../specs/behavior/BEH-PERSON-0003-recall-movement-and-action-reservation.md) |
| `military`／`demolition` | 出阵、增援、建设、输送、攻击、进军、撤退、方针、输送改令及撤除确认／对白／声。按真实主将和前后事实生成，取消／拒绝不播，不通过表现重扣资源或重建载荷 |
| `decisions` | 窗口族、语义选项、消息／角色、前置／结果、确认、NPC和词汇；按实际受询势力当前控制者答复，候选／费用由规则提供。结局独立于普通待答 |
| `Battle.Duel` | 男女／左右动作、准备、助阵、地形／季节背景、必杀／胜负、消息／声、旗位和局部粒子；Interface声明面板、头像、体力与对白，局部效果显式Additive／Opacity，`label.health`必需 |
| `ending` | 地图分层、阶段、插图／遮幅／继任／正文、时间与曲渐停、厂牌／标题；四人及方针确认后一次`EndingOperation`，消费冻结结果而不推进日期 |
| 记录与`chronicle` | 记录面板／任务面板／记录页、年表输入按钮和消息角色分别声明；年表有独立持久事实，进行记录只属客户端会话，等级在创建时确定，客户端过滤不补发历史。发生时归属、类型、位置及失效原因保留，不追溯改写 |
| `mapAudio.executionMusic` | 普通／高爵位战斗、普通／高爵位进军及内政五角色，战略季节曲独立；循环会谈与单次插图由消息明确区分 |
| 可空`capture` | 胜负和跨势力接收消息、按设施类型的插图／专曲开关、胜负曲及占领声；接收消息必需，专曲单次结束回地图 |

`ISceneMedia`只接受语义BGM／影片与取消令牌，`SilentSceneMedia`为无媒体实现；客户端影片由`SceneMoviePlayer`消费，见[电影契约](../movie-preparation.md)。地图、会谈和插图各自持有音乐所有权，详见[战略声音](strategy-ui.md#地图音乐与进行面声音)。

单挑背景／对白／面板保留逐色Alpha，地图染色#072746、179/256，不加模糊；兵法特写指定图保留原Alpha，地图染色#101922、217/256，不加不透明暗底或抠除黑像素。正常／跳过／关闭沿同一领域结果，不重演规则；证据见[RE-0194](../../research/findings/RE-SAN9PK-0194.md#原作截图后的背景渐隐复核)。事件、攻占及结局渠道分别见[世界事件](../../specs/behavior/BEH-UI-0010-world-event-presentation.md)和[结局规格](../../specs/behavior/BEH-SCENARIO-0002-ending-resolution.md#结局演出与返回标题)，历史消息求值／资源检查不代替画面或试听验收。

## 最小重建范围

按实际字段或像素变化选择，复用未变世界、图片、音频和影片；单纯改消费者或文档不转换。不存在用旧包号判断所有内容失效的规则。

| 变化 | 范围 |
| --- | --- |
| 开局美术／布局引用 | 对应`library/g_mmap`、`library/g_start2`、`library/g_strtpk`及`import-content --rebuild opening`，不导未变世界 |
| 地图身体、动作、退场、施工或攻击配方 | `import-textures --rebuild map`；新语义引用需要时再更新剧本表现，其他纹理单元和媒体复用 |
| 玩法阵形／兵法到现有外观的身份绑定、授权或规则字段 | `import-content --rebuild worlds`或对应数据转换，不重建未变图片 |
| 命令／记录／设置／存读档皮肤、光标 | 对应skin或cursors单元；新词条／消息角色只更新数据和共享消息，不重建图片 |
| 外交人物／阴影像素 | `library/g_gaikou`并更新表现；答复／条件／意见仅变化时只更新数据 |
| 单挑／兵法Alpha或美术 | 对应`library/g_ikki`／`library/g_zoomin`；语义布局、染色或绑定变化再更新数据 |
| 结局美术 | `library/g_ending`及skin；布局／消息绑定变化更新数据，其他地图和媒体复用 |
| 世界事件／兵法／消息／年表／退出托管角色 | 更新相应`presentation`、`battle`、`missions`、`progress`、`chronicle`或`exitMenu`及引用闭包；无像素／音频变化不转图声 |

### 阵型差异外观预设

已有共享纹理包可仅重建`FormationAppearance`引用，不读取原始图片，保留包全局转换版本及其他资源，无需重建剧本世界：

```powershell
dotnet run --project src/San9.Tools -- import-textures --game-dir "private/game-files" --output "local/generated/content/shared/textures" --rebuild map/formation-appearance
```

定向入口仍校验包结构与版本身份。行为见[阵型差异外观](../../specs/behavior/BEH-RENDER-0003-map-ambient-animation.md#阵型差异外观)，完整转换能力及锁／取消／失败发布规则见[内容管线](../client-content.md#转换与运行)。
