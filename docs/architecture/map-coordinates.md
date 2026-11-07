# 地图坐标与显示投影

## 坐标单位

`MapCell`表示地形方格；`WorldPosition`表示精细位置。两者都不表示屏幕像素。共享的`MapCoordinates`定义每格5个精细单位，保留已核实原作尺度；地图尺寸不写死为200或1000。

格中心转换为`(5*x+2, 5*y+2)`；从非负精细位置取所在格为`(x/5, y/5)`的整数商。例：格(184,26)中心是(922,132)，格(199,199)中心是(997,997)；精细位置不必恰好在格中心。

据点内容现有X/Y明确保留为地图格锚点，通过类型化Cell/Position访问器取得格和精细中心；不再复制一份可独立漂移的据点坐标。Importer先验证剧本坐标及无效槽，再将未建设地域转换为PersonPlace.Position，将活动部队转换为UnitState.Position。人物旅行规则只接收PersonPlace中的WorldPosition，不能传入裸格坐标。

UnitState自有存档保存精细位置；部队情报和战略全图通过Cell读取所在格，避免地图标记放大五倍。地图表现以逻辑位置为输入进行日内子步插值；视觉位置不写回权威位置，见[地图动作](../../specs/behavior/BEH-RENDER-0003-map-ambient-animation.md#个体跟随移动尾迹与遮挡层级)。原作存档导入不在支持范围内，不增加读取入口或兼容分支。

源版本的200×200范围和无效哨兵由TC101 Importer负责；Core只校验位置非负及计算溢出。每格5单位是明确的运行时尺度，不是对象数量或地图尺寸限制。

## 人事计时与军事路线

人事距离、倒计时和日期偏移由[人事行程规格](../../specs/behavior/BEH-PERSON-0003-recall-movement-and-action-reservation.md#距离倒计时与结算日期)维护；不得把部队地形行军公式套到使者任务，或用人事距离公式替代路线规划。原作坐标取证见[RE-0110](../../research/findings/RE-SAN9PK-0110.md)和[RE-0112](../../research/findings/RE-SAN9PK-0112.md)。
