# 特色装备图标来源

以下图像由项目使用内置 imagegen 生成，不以参考游戏的画面、标志或素材作为输入。它们是原创素材，不代表项目已取得《三国志 IX》或其他相关作品的改编授权。

| 文件 | 主题 | 状态 |
| --- | --- | --- |
| `src/San9.Client.Godot/assets/equipment/xuanwen-ji.png` | 玄纹长戟，项目自创 | 样稿，尚未接入物品或界面 |
| `src/San9.Client.Godot/assets/equipment/featured-atlas.png` | 40件项目自创物品的8列5行图集 | 已按固定物品 ID 与格位映射接入名单和详情；仍需实机逐项检查 |
| `src/San9.Client.Godot/assets/equipment/red-hare.png` | 赤兔马，独立绘制 | 已接入；隔离战局中名单及详情显示正常 |
| `src/San9.Client.Godot/assets/equipment/fangtian-ji.png` | 方天画戟，独立绘制 | 已接入；隔离战局中名单及详情显示正常 |
| `src/San9.Client.Godot/assets/equipment/liuxing-biao.png` | 流星镖，项目自创 | 替换图集中不合时代风格的星形投掷刃；隔离战局中名单及详情显示正常 |
| `src/San9.Client.Godot/assets/equipment/green-dragon-blade.png` | 青龙偃月刀，演义名物的独立造型 | 已接入；隔离战局中装备名单显示正常 |
| `src/San9.Client.Godot/assets/equipment/serpent-spear.png` | 蛇矛，演义名物的独立造型 | 已接入；隔离战局中装备名单显示正常 |
| `src/San9.Client.Godot/assets/equipment/dilu.png` | 的卢，独立设计的灰马 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/jueying.png` | 绝影，独立设计的深骝马 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/zhaohuang-feidian.png` | 爪黄飞电，独立设计的浅金色马 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/yitian-jian.png` | 倚天剑，独立设计的铁剑 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/qinggang-jian.png` | 青釭剑，独立设计的青灰色剑 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/qixing-dao.png` | 七星宝刀，独立设计的环首刀 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/cixiong-jian.png` | 雌雄一对剑，独立设计的双剑 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/guding-dao.png` | 古锭刀，独立设计的环首刀 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/tieji-shemao.png` | 铁脊蛇矛，独立设计的长柄矛 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/sanjian-dao.png` | 三尖刀，独立设计的三尖长兵器 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/shuangtie-ji.png` | 双铁戟，独立设计的双持短戟 | 已接入；待实机检查 |
| `src/San9.Client.Godot/assets/equipment/da-fu.png` | 大斧，独立设计的长柄斧 | 已接入；待实机检查 |

玄纹长戟生成提示词：

> Use case: historical-scene. Asset type: a single original item icon for a Three Kingdoms era strategy game. Depict one fictional long ji polearm, called 玄纹长戟, with a restrained dark iron blade and subtle geometric bronze inlay; historically grounded silhouette, no fantastical shapes. Isolated centered object with generous clear margin, hand-painted classical Chinese historical illustration, subdued warm directional light, fine realistic material detail, no text, no interface frame, no logo, no people. Square composition, consistent with a muted antique strategy-game inventory. Original design; do not reproduce any existing game asset.

生成于 2026-11-12，使用内置 imagegen；请求了透明背景，但结果实际包含近乎不透明的浅褐色底，因此保留为未接入的样稿。

图集生成于 2026-11-12，使用内置 imagegen，未输入参考图片，按描述生成；其提示词为：

> Create an ORIGINAL game inventory icon atlas, 8 columns by 5 rows, exactly 40 perfectly aligned equal square cells, no text, no labels, no numerals, no frames overlapping cells. Ancient Chinese late Han military museum objects, hand-painted realistic miniature style, consistent subdued earth pigments and bronze light, each centered isolated on a dark charcoal background, generous margin, every cell distinct. Row 1 left to right: engraved black iron long halberd, blue-grey straight sword, long ring-pommel dao, steel spear, red-tipped iron spear, heavy iron halberd, straight sword with leather scabbard, feather-engraved dao. Row 2: iron battle axe, long spear, heavy northern halberd, ornate short sword, iron lamellar helmet, cloud-engraved helmet, black lamellar armour, blue-grey scale armour. Row 3: red-tied lamellar armour, black-maned horse, dapple-grey horse, chestnut horse, blue-grey maned horse, pale horse, dark bay horse, bronze tiger tally. Row 4: cloud-pattern bronze tally, mountain-pattern bronze tally, paired-tiger tally, swallow-tailed throwing dart, star-shaped throwing dart, black feathered needle, sling with stone, bamboo military travel scroll. Row 5: defensive tactics bamboo scroll, supply manuscript, geographical atlas scroll, generalship manuscript, warm jade pendant, green jade seal, bronze bird-shaped drinking vessel, ancient patterned bronze mirror. View each object individually without hands or people. Avoid any reference-game art, logos or copied layouts. Strict uniform square grid and exact categories in each cell.

赤兔马和方天画戟图标生成于 2026-11-12，使用内置 imagegen，未输入参考图片，分别以枣红战马和双月牙戟为主题，要求汉末背景、暗色底、无人物与文字、不复制游戏素材。旧名物图标匹配同时检查物品名称，避免模组复用编号时错误显示。

流星镖图标生成于 2026-11-12，使用内置 imagegen，未输入参考图片；提示词要求单枚窄长铁镖、羽饰、暗色底、无星形刃与游戏标识。造型为游戏设计，并非对具体出土文物的复原。

青龙偃月刀和蛇矛图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；分别要求单刃长柄刀和曲刃长矛、暗色底、无人物文字或游戏标识。这是对演义名物的游戏化想象，不作为汉末武器形制的史实复原。

的卢和绝影图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；提示词分别要求白额灰马与黑鬃深骝马、简洁马具、暗色底、无骑手文字或游戏标识。马匹毛色和马具为游戏中的视觉设计，不视为可考的个体复原。

爪黄飞电图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；提示词要求浅金色马、浅色下肢、简洁马具、暗色底、无骑手和游戏标识。毛色与马具是游戏中的视觉设计，不作为历史个体或汉末马具的复原。

倚天剑与青釭剑图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；提示词分别要求铁质双刃直剑和青灰色双刃直剑、简洁铜饰、暗色底、无人物文字或游戏标识。图像是独立的游戏化设计，不声称复原历史实物或参考作品造型。

七星宝刀、雌雄一对剑和古锭刀图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；提示词分别要求带七枚铜饰的环首刀、交叉双剑和古朴环首刀，均使用暗色底、无人物文字或游戏标识。造型为独立游戏设计，不声称复原历史实物或参考作品造型。

铁脊蛇矛、三尖刀、双铁戟和大斧图标生成于 2026-11-13，使用内置 imagegen，未输入参考图片；提示词分别要求带中脊的波形矛、三尖长兵器、一对短戟和单刃大斧，均使用暗色底、无人物文字或游戏标识。造型为独立游戏设计，不声称复原历史实物或参考作品造型。

其他旧名物和未配置图标的模组物品目前无对应图标，界面缺图时隐藏图像；旧名物逐件图标仍待制作。
