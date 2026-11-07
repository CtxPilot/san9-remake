# 发行许可补充文件

保存 NuGet 包未附带的许可原文；包内已有的声明和 .NET 运行时声明由 `tools/prepare-release-notices.ps1` 按导出依赖收集。升级依赖时核对版本、许可和内置资源声明，同步脚本与对应文件。

第三方许可文本保留上游排版，`.gitattributes`对这些文本关闭空白格式检查；本项目自行编写的说明仍遵循仓库约定。

- `OpenccNetLib-1.6.2/`：[上游提交](https://github.com/laisuk/OpenccNet/tree/c86b0c9dfa719112a69d664c8de43ff6d51b5bba/OpenccNetLib)的 `LICENSE`、`THIRD_PARTY_NOTICES.md`。
- `ZstdSharp.Port-0.8.8/`：[上游提交](https://github.com/oleg-st/ZstdSharp/blob/2cd0c019693bc786a5fe5c3be94e107b24e7267e/LICENSE)的 `LICENSE`，以及其移植来源 [Zstandard 1.5.7](https://github.com/facebook/zstd/blob/v1.5.7/LICENSE) 的 BSD 许可。
- `CC0-1.0.txt`：[SPDX v3.26.0](https://github.com/spdx/license-list-data/blob/v3.26.0/text/CC0-1.0.txt) 的许可原文，用于 PeterO.Cbor、PeterO.Numbers 和 PeterO.URIUtility。
