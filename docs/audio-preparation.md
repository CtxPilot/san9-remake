# 音乐与音效导入

声音导入是显式操作；普通`import-content`、`import-client`及局部重建只生成声音引用，不复制或转码BGM、音效，也不转换视频。已有最终资源原位复用，避免开发时重复处理媒体。首次建立完整可运行内容包时，另行执行声音导入；音频缺失仍由运行时加载及`verify-content`拒绝，不生成假音频或静默掩盖资源缺失。

## 命令与输出

```powershell
dotnet run --project src/San9.Tools -- import-audio --game-dir "private/游戏文件" --output local/generated/content --group all
```

`--group`支持`bgm`、`effects`及默认`all`。BGM读取原版`BGM`目录的WMA，使用用户环境PATH中的`ffmpeg`转换为Ogg Vorbis Q5、44.1kHz双声道，直接输出到最终内容包`shared/music/`；音效从`SE`读取WAV、Ogg Vorbis或MP3，按原格式复制到`shared/sound-effects/`，不重新编码。音效导入不需要FFmpeg。分组只读取对应源目录；已有可播放音乐的复制也不依赖FFmpeg。命令针对内容根目录，独立剧本包使用该包目录作为输出根；两种组织方式统一引用根下的`shared/music/`与`shared/sound-effects/`，不重建世界、纹理或表现引用。

找不到FFmpeg时，BGM转换与独立的电影转换打印跳过原因并正常返回，已有媒体保持不变；`all`中的音效复制仍执行。找到FFmpeg但启动失败、缺少编码器或转换失败时报错。工具不下载或捆绑FFmpeg。发行界面的完整 `prepare-content` 流程提供音频和影片的独立开关（命令行对应 `--audio`／`--movies`，均默认 `true`）。关闭音频后不处理音乐及音效，保留已有文件；首次准备仍需必需音频通过完整校验。至少选择一项媒体时，在转换前检查 FFmpeg；两项都关闭不检查。需要转码却缺失时报错并允许选择可执行文件后重试，不沿用独立媒体命令的跳过行为；见[发行版内置转换入口](client-content.md#发行版内置转换入口)。

原始WMA存在时，它是该BGM的权威输入，安装目录中同基名的旧Ogg／MP3不作为中间依赖，引用固定为转换后的`.ogg`。只有已准备音乐、没有WMA时，保留WAV／Ogg Vorbis／MP3格式并复制到最终包；同基名存在多种已准备格式时报错。音效沿用同样的格式唯一性要求，不按扩展名优先级猜测。

27首音乐与170个音效的英文语义名称唯一维护在[AudioAssetCompiler](../src/San9.Importer.San9PkTc101/Interface/AudioAssetCompiler.cs)，如`map-spring.ogg`、`diplomacy.ogg`与`construction.wav`。共享声音按用途命名，多个角色引用同一物理文件；未知用途保留小写源名。来源及原表纠正见[完整音频命名复核](../research/findings/RE-SAN9PK-0187.md)。原版路径与编号留在Importer，客户端只读取最终内容包。

## 转码与复用

BGM采用`-map 0:a:0 -vn -af asetpts=N/SR/TB -c:a libvorbis -q:a 5 -ar 44100 -ac 2 -map_metadata -1`。源音乐为ASF／WMA v2，已经有损；二次有损转换不能提升音质。Q5为可变码率质量设置，不保证固定码率或与源相同体积。不调整响度、提高采样率、截断静音或推导循环点，不能据此认为首尾循环无缝。

部分源音乐时间戳不连续，`asetpts`按实际解码采样数重建连续时间戳。每个转换输出均使用FFmpeg严格完整解码验证，通过后才替换最终文件。`shared/media-conversion.json`记录源内容、参数与输出的SHA-256；依赖一致且输出完好时不重转，源、参数或输出变化时只更新对应文件。没有转换记录的已有产物在首次显式导入时转换一次。音效及已有可播放音乐按字节复用，不对未变文件重复写入。

转换仅创建目标旁的临时文件，成功、失败或取消均清理，不在原版目录写入转换后音乐，也不保留独立音视频中间目录。转换记录、最终媒体及原版素材均留在忽略目录，不提交。电影参数和分组见[电影准备与接入](movie-preparation.md)。
