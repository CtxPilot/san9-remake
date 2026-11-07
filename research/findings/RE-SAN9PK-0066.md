# RE-SAN9PK-0066：KoeiMPEG 同步播放边界与原作影片格式

- 状态：静态调用链、41 个影片样本格式与 Windows Media Foundation 全量解码已确认
- 样本：`SRC-SAN9PK-TC-101`
- 关联：[RE-SAN9PK-0062](RE-SAN9PK-0062.md)、[RE-SAN9PK-0065](RE-SAN9PK-0065.md)

## 已确认事实

### 主程序调用边界

- 主 EXE 从 `KoeiMPEG.dll` 导入九个播放 API：`MPEGMoviePlay`、
  `MPEGMovieCheckFinish`、`MPEGMovieIsPlaying`、`MPEGMovieSetSoundVolume`、
  `MPEGMoviePause`、`MPEGMovieReStart`、`MPEGMovieExit`、
  `MPEGMovieNotifyOwnerMessage` 与 `MPEGMovieSetSan9MsgLoop`。
- `play_game_movie (0x415FC0)` 先按影片编号取得相对文件名，再解析完整路径；它向
  `MPEGMovieSetSan9MsgLoop` 注册无参数回调，然后同步调用
  `MPEGMoviePlay(hwnd, path, mode, soundEnabled)`。返回后才恢复原作窗口、焦点与播放前状态。
- 返回路径在 `0x4161D4` 无条件调用 `ShowWindow(main_window, SW_SHOWNORMAL)`，随后依次调用
  `UpdateWindow`、`SetForegroundWindow` 与 `SetFocus`。因此外部将主窗口改为原生最大化后，
  每次影片返回都会由这条固定调用主动切回普通窗口；该变化不是用户窗口操作。
- 回调 `pump_one_game_message_for_movie (0x414B90)` 每次最多取得并分派一条游戏消息。
  因而新播放器必须在同步播放调用内部继续泵送游戏消息，不能将 `MPEGMoviePlay` 简化为
  启动后台任务后立即返回。
- `MPEGMovieSetSoundVolume` 的旧实现将参数夹取在 `-10000..0`，对应 DirectShow
  `IBasicAudio` 使用的百分之一分贝范围。
- 旧 DLL 导入 DirectShow COM 与 USER32 并创建自己的播放窗口；主程序自身没有把其视频帧
  合成进游戏 framebuffer 的路径。

### 影片容器与编码

- 经用户授权，对目标安装目录中的 41 个影片文件进行了只读遍历；未复制、修改或登记影片
  内容。全部文件扩展名为 `.mpg`，文件头均为 `RIFF....AVI `，实际容器是 AVI。
- 41 个文件的流元数据完全一致：视频为 WMV3、640×480、30/1 fps；音频为 MP3、
  44100 Hz、双声道。
- 独立 Win32 探针通过 `IMFByteStream` 写入 `MF_BYTESTREAM_CONTENT_TYPE=video/avi`，再由
  Media Foundation Source Reader 请求 RGB32 与 PCM 输出。41 个文件均完成全部视频样本和
  全部音频样本解码，不依赖文件扩展名，也未调用或加载 FFmpeg 解码库。
- `MF_SOURCE_READER_FIRST_VIDEO_STREAM` 与 `MF_SOURCE_READER_FIRST_AUDIO_STREAM` 是 API
  选择器，不是 `ReadSample` 返回的真实流号；该 AVI 集的真实流号为视频 0、音频 1。
  Toolkit 实现通过枚举原生流类型取得真实流号，不把选择器当作流号。

## Toolkit 接管边界

- Toolkit 仅替换主 EXE 的九个 IAT 槽位；PE 导入仍使原 DLL 在进程启动时加载，但播放期间
  不调用其导出代码，也不创建第二个可见播放窗口。
- 输入只接受上述已确认的 AVI/WMV3/MP3、640×480@30、44.1 kHz 双声道格式。容器、流、
  解码器或设备初始化任一失败时清理媒体状态、恢复游戏画面并跳过，不回退旧播放器。
- Source Reader 输出 BGRA/RGB32 和 16-bit PCM；XAudio2 已播放采样数构成独立真实时间主钟。
  影片不读取 Toolkit 的游戏时钟，因此不受游戏 2×～4× 速度倍率影响。
- D3D11 presenter 在既有设备与 swap chain 上增加独立 BGRA8 影片纹理；影片与 RGB555 游戏
  帧共用窗口缩放、黑边和交换链。影片期间只缓存游戏 framebuffer，结束后恢复最近游戏帧。
- 同步播放期间继续泵送消息；左键释放与 Esc 被播放器消费并请求停止。失焦或最小化停止
  XAudio2 source voice，恢复焦点后从同一采样时钟继续；关闭消息请求立即停止但仍向原窗口
  过程传递。

## 验证

- 独立状态/时序测试覆盖重复播放拒绝、暂停来源合并、恢复、停止、完成状态、500 ms
  预缓冲、晚帧丢弃、音频欠载重缓冲与音视频结束条件。
- 独立 Media Foundation 探针在 Windows x86 进程中只读全量解码 41 个目标影片，全部通过。
- Runtime Release/Win32 在 `/W4 /WX` 下为 0 警告、0 错误。
- 游戏内启动 Logo、开场、事件、结局、窗口缩放、最大化、Alt+Tab、最小化、跳过、连续播放
  及最长影片漂移仍属于目标程序端到端验收范围，不能由独立探针替代。

## 事实、推断与待验证

- 事实：当前 41 个样本均为错误扩展名的 AVI/WMV3/MP3，且 Windows Media Foundation 可
  将其完整解码为 RGB32/PCM。
- 事实：原作对 `MPEGMoviePlay` 的调用是同步的，并提供无参数的游戏消息泵回调。
- 高置信度推断：旧播放器的独立 DirectShow 窗口与 Toolkit D3D11 游戏交换链交替覆盖同一
  区域，是现代系统播放时闪烁的结构性来源；从同一 swap chain 呈现可消除该窗口竞争。
- 待验证：目标游戏端最长影片的实测音画漂移、焦点切换后的连续性，以及全部剧情入口是否
  都只经过这九个导入槽位。

## 播放平滑性复测修正

- 首版现代链仍在同步消息/呈现循环中调用 Source Reader；视频队列达到上限时会同时停止
  后续音频解码。因此 WMV3 解码、窗口重绘或 D3D11 呈现的一次延迟可能耗尽约 250 ms 的
  XAudio2 队列，实测表现为画面偶发顿挫和音频断开。
- 修正后 Source Reader 由独立 MTA 解码线程驱动，解码队列最多保留约 1.5 秒媒体；主线程
  只泵送消息、向 XAudio2 提交最多 48 个 PCM 缓冲，并按音频采样时钟选择视频帧。视频呈现
  延迟不再阻塞音频解码。
- 起播缓冲提高到 500 ms。若队列仍在流结束前耗尽，播放器停止音频主钟，补足 500 ms 后
  从同一累计采样位置恢复；此时允许画面短暂停住，不让 XAudio2 直接产生断裂。

## IDA 写入与回读

| 地址 | 名称 | 证据 |
| --- | --- | --- |
| `0x415FC0` | `play_game_movie` | 编号/路径解析、状态保存、消息回调注册与同步播放调用 |
| `0x414BC0` | `get_movie_relative_path` | `0..0x25` 编号分派到38项影片相对名；2026-11-07修正旧上界注释 |
| `0x4157F0` | `resolve_movie_file_path` | 生成传给 `MPEGMoviePlay` 的路径缓冲区 |
| `0x414B90` | `pump_one_game_message_for_movie` | 无参数回调内取得并分派一条 `MSG` |

以上名称与 `[RE-SAN9PK-0066]` 事实注释已写入当前主数据库
`research/analysis/San9PK-TC-101.i64` 并原位保存。通过 IDA API 回读四个函数入口，确认新名称、
函数边界和回调签名已持久化；未修改样本程序字节或函数边界。

## 2026-11-07：启动影片、结局与同名别本核对

本次仍使用`SRC-SAN9PK-TC-101`项目主库；输入为2636800字节，SHA-256为`d20794aeff67301ec2bf8c3becb1e9944c68c6c0588fbfd4bf04e8597f0e5028`，x86、映像基址`0x400000`，IDA 9.5、Hex-Rays 9.5.0.261026。只读比较原始影片并抽取画面，没有启动原作。

### 文件内容

| 文件 | 内容核对 | 场景 |
| --- | --- | --- |
| `klogo_pk.mpg`、`KOEILOGO.mpg` | SHA-256一致，完全相同，约5秒KOEI厂商标志 | PK启动选择前者 |
| `op_pk.mpg`、`Opening.mpg` | 均约91.3秒，抽查六个时点的主体镜头相同；88秒片尾前者含“威力加强版”，后者仅显示“三国志IX”。文件及音视频压缩流哈希均不同，不是完全重复文件 | PK启动选择前者；后者呈基础版片头样式，不据此推定未登记基础版EXE的调用 |
| `ed_pk.mpg`、`Ending.mpg` | SHA-256一致，完全相同，约120秒，包含结局制作人员表 | PK结局选择前者；已有调用见[RE-0181](RE-SAN9PK-0181.md#24-图像声音影片) |
| `theme.mpg` | 独立的约19.1秒主题展示动画 | PK启动的标志之后、正式片头之前 |

核对时生成过私有文件哈希与六个画面对照，临时产物已按用户要求清理；判定依据保留于上表。六个画面对照不表示两份片头逐帧完全相同。

### 原版调用与解释性标注

`414BC0`实际接受编号0至37，表`64EC50`前四项依次为`KLOGO_PK`、`THEME`、`OP_PK`、`ED_PK`；38项中没有`KOEILOGO`、`Opening`、`Ending`三个别名。`47E970`的状态1001、1002、1003依次请求影片0、1、2，闭合启动顺序；状态1002将配置位`0x80`作为播放模式参数并随后置位，本次不延伸判定该参数的完整播放器语义。是否实际播放仍受既有电影配置与资源可用性约束。

实际修改：更正主库`414BC0`旧注释的上界并补充前四项及别名结论；在`47E970`追加启动顺序与theme用途的函数注释。两处均带本RE引用，API写入后逐项回读一致并保存主库；未改变函数名称、类型、边界或程序字节。
