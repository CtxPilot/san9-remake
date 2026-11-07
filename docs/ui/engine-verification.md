# Godot 引擎交互检查

以下入口按受影响功能选择，需先构建客户端；有内容参数的检查复用现有转换包。它们验证控件、输入和生命周期，不代表画面、试听或完整玩法验收。历史画面脚本见[画面验证](client-verification.md)，通用验证取舍见[测试说明](../../tests/README.md)。

资料编辑器保存刷新可用无资源引擎检查验证，覆盖保存不重建当前表单／不丢失控件输入，以及隐藏页切回时刷新；不代表画面验收：

```powershell
godot --headless --path src/San9.Client.Godot res://Diagnostics/Verification/SharedEditorRefreshVerification.tscn
```

成功输出`SHARED_EDITOR_REFRESH_OK`并退出。

联携关系表检查设置`SAN9_TACTIC_LINK_CONTENT`为现有内容根目录，运行`godot --headless --path src/San9.Client.Godot res://Diagnostics/Verification/TacticLinkEditorVerification.tscn`，可验证联携关系表的初次显示、按人物筛选、选中修改、删除及搜索选人后添加。仅改变内存草稿，不发布玩家资料；成功输出`TACTIC_LINK_EDITOR_OK`，不代表画面验收。

涉及工作流窗口或地图回调生命周期时，可在客户端编译后运行无资源依赖的引擎检查（将 `godot` 替换为本机 Godot .NET 可执行文件）：

```powershell
godot --headless --path src/San9.Client.Godot res://Diagnostics/Verification/WorkflowLifecycleVerification.tscn
```

成功输出 `WORKFLOW_LIFECYCLE_OK` 并以 0 退出；覆盖窗口锁、退出顺序、地图回调所有权，以及模态窗口重开、嵌套、初始化期间创建子窗口、动态控件、原始输入隔离、Tab焦点约束、关闭时排除退出控件的焦点恢复和祖先返回路由。`COMPANION_DRAFT_OK`另验证伴侣面板待提交数值、隐藏页预设与重置、无变化不写入及保存失败后的草稿保留；不代表画面或完整命令交互验收。

设置`SAN9_OPENING_CONTENT`为已有转换内容根目录（含281年剧本），运行同一headless入口可单独检查真实开局控件：用未完成的读取任务核对首帧禁用状态，再检查悬停转移焦点、禁用控件不抢焦点及数据就绪后的部署状态。成功输出`OPENING_CONTROLS_OK`并退出；该模式不运行其他工作流检查，不创建游戏世界或操作桌面窗口。例如PowerShell：

```powershell
$env:SAN9_OPENING_CONTENT = (Resolve-Path local/generated/content).Path
godot --headless --path src/San9.Client.Godot res://Diagnostics/Verification/WorkflowLifecycleVerification.tscn
Remove-Item Env:SAN9_OPENING_CONTENT
```

设置`SAN9_MODAL_CONTENT`为已有转换包目录后，同一入口另检查真实环境设定的保存失败提示，以及终局标题菜单到存档行的鼠标选择和读取回调，成功时追加`MODAL_CONTENT_OK`。检查使用独立存档目录`local/generated/verification/modal-content/`，不读取用户存档；不创建真实战败事件，也不替代画面验收。例如PowerShell：

```powershell
$env:SAN9_MODAL_CONTENT = (Resolve-Path local/generated/content/scenarios/historical/scenario-0).Path
godot --headless --path src/San9.Client.Godot res://Diagnostics/Verification/WorkflowLifecycleVerification.tscn
Remove-Item Env:SAN9_MODAL_CONTENT
```

设置`SAN9_UNIT_GROUP_CONTENT`为已有转换包目录，运行`WorkflowLifecycleVerification.tscn`可单独检查多选常驻名单：实际鼠标点击刘备行后保留其余行对象，检查名单／滚动视口／条目的边界、独立选择状态、隐藏恢复及重复刷新，并通过原有皮肤的“决定”按钮提交重叠名单临时选择。成功输出`UNIT_GROUP_LIST_OK`；有图形设备时同时保存`local/generated/verification/unit-group-list.png`，无图形设备时仅检查布局与输入。该检查不覆盖完整战略地图下令流程。

设置`SAN9_UI_PREFERENCES_CONTENT`为已有转换包目录后，运行上面的`WorkflowLifecycleVerification.tscn`入口，可检查真实存读档页鼠标翻页／中止共享、情报与都市编辑的地图／一览互通、地图起始时详情候选初始化及仅关闭时统一保存。成功输出`UI_PREFERENCES_OK`；该模式只做本功能的引擎交互检查，不代表画面验收，不读写用户偏好文件。
