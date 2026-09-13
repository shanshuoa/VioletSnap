<p align="center">
  <img src="ScreenshotTool.App/Assets/ZhouTianCaptureIcon.png" width="128" alt="周天截图图标" />
</p>

<h1 align="center">周天截图 · ZhouTian Capture</h1>

<p align="center">面向 Windows 的截图、贴图、标注、OCR 与翻译工具。</p>

## 功能

- F1 自由截图，支持多显示器、窗口识别、移动及八方向调整选区。
- F3 从剪贴板贴图，支持拖动、缩放、透明度和多贴图管理。
- 矩形、圆角矩形、直线、箭头、画笔、文字及马赛克标注。
- 标注撤销、重做、选中、移动、缩放、删除及二次编辑。
- 中文及英文 OCR，并尽可能保留原始分行和段落结构。
- OpenAI、DeepSeek、Kimi、通义千问、小米等兼容服务翻译。
- 可配置翻译语言、快捷键、开机自启动与 API 模型。
- 复制、保存和贴图时统一合成原图、翻译内容及标注。

## 系统要求

- Windows 10 或 Windows 11 x64
- Visual Studio 2022，或 .NET 8 SDK 与 Windows Desktop SDK

## 依赖说明

普通用户直接安装 Release 中的自包含安装包即可，无需另外安装 .NET 或下载 GitHub Packages。

开发和构建源码需要：

- .NET 8 SDK
- Windows 10/11 SDK 与 WPF Desktop Runtime 工具链
- `CommunityToolkit.Mvvm` 8.4.0（执行 `dotnet restore` 时由 NuGet 自动恢复）
- Inno Setup 6.7.3 或更高版本（仅在重新生成安装程序时需要）

## 构建

```powershell
dotnet restore ScreenshotTool.sln
dotnet build ScreenshotTool.sln -c Release
```

运行契约测试：

```powershell
dotnet run --project ScreenshotTool.Tests/ScreenshotTool.Tests.csproj -c Release
dotnet run --project ScreenshotTool.UiTests/ScreenshotTool.UiTests.csproj -c Release
```

生成 Windows x64 自包含版本：

```powershell
dotnet publish ScreenshotTool.App/ScreenshotTool.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

安装程序脚本位于 `Installer/ZhouTianCapture.iss`，可使用 Inno Setup 6.7 或更高版本编译。

## 隐私和 API Key

- 截图、贴图与本地 OCR 默认在用户电脑上处理。
- 翻译时只会把待翻译文字发送到用户选择的第三方服务商。
- API Key 使用 Windows 当前用户凭据加密后保存在本地，不提交到仓库。
- 导出设置不会包含 API Key；也可在设置页面主动清除密钥。

## 项目信息

- 产品：周天截图（ZhouTian Capture）
- 作者及维护者：周天
- 发布者：Zhou Tian
- 当前版本：1.0.0

为兼容旧版本，项目内部仍保留部分 `ScreenshotTool` 命名空间和本地数据目录名称。

## 开源许可

本项目采用 [GNU General Public License v3.0](LICENSE) 发布。您可以在 GPLv3 条款下运行、研究、修改和分发本软件；发布修改版或衍生版本时，必须继续提供相应源代码并保留许可证及版权声明。

本软件不提供任何形式的质量担保。第三方 OCR、AI、翻译服务的费用、内容及数据处理规则由对应服务商负责。

Copyright © 2026 周天.
