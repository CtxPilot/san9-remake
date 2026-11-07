# 旗帜生成实验操作入口

结论、样本身份、指标口径和未采用方案见[研究记录](../../notes/2026-10-28-procedural-flags.md)。这里只维护当前可执行命令；旧多列／两向预览的准备和捕获命令已撤下，不用当前验证器重验历史格式。

## 输入和启动

从仓库根目录运行 PowerShell。需要 Python、Godot 4 .NET（本机验证为 4.7.2）、私有 `private/game-files/{San9PK.exe,G_Flag.s9,G_Name.s9}`、Windows KaiU／SimKai 字体，以及保存的母材 `local/generated/research/flag-generation/imagegen/cloth-neutral-v1.png`。母材哈希见研究记录；它和字体、源资源均不在提交内，提示词不能保证重建同一图片。

```powershell
$flagRoot = Join-Path $PWD 'local/generated/research/flag-generation'
$flagPython = Join-Path $flagRoot 'venv/Scripts/python.exe'
python -m venv "$flagRoot/venv"
& $flagPython -m pip install -r research/experiments/flag-generation/requirements.txt
& $flagPython research/experiments/flag-generation/prepare_modern_preview.py
godot --headless --editor --path "$flagRoot/modern-godot" --quit
godot --path "$flagRoot/modern-godot"
```

已有虚拟环境及有效素材时复用，不为提交重复准备。`godot` 换成本机可执行文件；准备支持 `--data-dir`、`--output`、`--font`（界面）、`--emblem-font`（旗字）、`--generated-master`，输出限于 `local/generated/`。它仅准备独立工程，不转换或启动游戏。

方向按钮／数字 0–7／左右键同步切换两列；空格暂停、`G` 网格、Esc 退出。右上角为原版旗色和自动轮换，下方为风幅、布面／边饰色与字色。大图和原尺寸共用素材与相位；任意新配色的原版列仅作源色参照，自定义不落盘。

## 捕获与检查

沿用上面的变量；准备输入未变时直接捕获，不重新转换。捕获需实际图形渲染，不能添加 `--headless`。窗口采样完成后自动退出，成功标记为 `FLAG_DIRECTION_CAPTURE_OK` 和 `FLAG_CUSTOM_CAPTURE_OK`。

```powershell
$flagCapture = Join-Path $flagRoot 'validation-capture'
New-Item -ItemType Directory -Force -Path $flagCapture | Out-Null
godot --path "$flagRoot/modern-godot" --resolution 1600x1000 -- "--capture=$flagCapture" 1> "$flagCapture/engine.log" 2> "$flagCapture/engine.errors.log"
& $flagPython research/experiments/flag-generation/verify_modern_capture.py --capture "$flagCapture"
& $flagPython research/experiments/flag-generation/verify_custom_capture.py --capture "$flagCapture"
```

两个验证器分别写 `verification.json` 和 `custom-verification.json`，不会互相覆盖。捕获还含对照图、APNG／GIF、八向／51 色、144 张风幅采样、校准相位、配色与控制审计。仅检查自定义交互时加 `--capture-scope=custom`，只运行自定义验证器。

数值测试入口如下；同一代码及输入已有通过结果时复用，不因提交重复跑。

```powershell
& $flagPython -m unittest discover -s research/experiments/flag-generation -p 'test_*.py'
```

## 脚本职责

| 文件 | 作用 |
| --- | --- |
| `prepare_modern_preview.py` | 源参照合成、共用母图、512px 字样、投影与运动元数据 |
| `calibrate_alignment.py` | 从正反面轮廓和字样总体变化拟合一套共用连续位移场 |
| `godot/` | 当前双列八向场景、交互与共用 shader；没有布料求解器 |
| `verify_modern_capture.py` | 重复／周期／零风、八向、51 色、轮换和风幅响应；打包对照产物 |
| `verify_custom_capture.py` | 新配色、字色、原版参照不变及预设恢复 |
| `measure_alignment.py` | 历史校准前后正面去均值轮廓变化对照，默认比较 `color-capture/` 与 `alignment-capture/` |
| `run_experiment.py`、`render_from_basis.py` | 初始像素拟合和只读共用模型的新字／配色生成；前者还提供源解码与合成辅助 |
| `rebuild_flag_material.py` | 历史分层轮廓、边饰与连续明暗算法；当前准备不调用，保留独立数值测试 |
| `test_*.py` | 12 项数值／布局检查；不是游戏画面验收 |

初始像素实验可独立复现，不需要生图母材：

```powershell
& $flagPython research/experiments/flag-generation/run_experiment.py
& $flagPython research/experiments/flag-generation/render_from_basis.py --text 雲 --face 20A0C0 --border EDB847
```

它们默认在 `$flagRoot` 保存 `shared-basis.npz`、`base-inputs.npz`、`metrics.json`、对照和扩展示例，仍非产品依赖。分层算法没有保留旧预览入口；其独有指标与本地报告在研究记录中索引。当前素材／运动与过去各轮不同，不跨实验比较像素分数，也不将生图步骤描述为确定性重建。
