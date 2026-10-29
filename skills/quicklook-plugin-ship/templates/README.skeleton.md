# QuickLook.Plugin.{FormatName}

[English](#english) | 中文

---

## 中文

### 简介

一个用于预览 {FormatFullName}（{Extension}）文件的 [QuickLook](https://github.com/QL-Win/QuickLook) 插件。

{一段关于格式的简介，说明用途和特点}

### 功能特性

- 实时 {Format} 预览
- {功能 1}
- {功能 2}
- 自动隐藏控制栏
- 适配 QuickLook 暗色/亮色主题
- 中英双语支持

### 安装

1. 从 [Releases](https://github.com/{user}/{repo}/releases) 下载 `.qlplugin` 文件
2. 确保 QuickLook 正在运行
3. 选中 `.qlplugin` 文件，按空格键预览
4. 点击"安装"按钮
5. 重启 QuickLook

### 系统要求

- Windows 10/11
- [QuickLook](https://github.com/QL-Win/QuickLook) 4.x
- [WebView2 Runtime](https://developer.microsoft.com/zh-cn/microsoft-edge/webview2/)（Windows 10/11 通常已预装）

### 从源码构建

```bash
git clone https://github.com/{user}/{repo}.git
cd {repo}
dotnet build -c Release
```

打包为 `.qlplugin`：

```powershell
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

### 快捷键

| 操作 | 快捷键 |
|------|--------|
| 播放/暂停 | 点击按钮 |
| 跳转 | 点击/拖拽进度条 |
| 音量 | 鼠标滚轮 |

### 项目结构

```
├── Plugin.cs                      # IViewer 入口
├── {Format}Panel.xaml(.cs)        # WebView2 播放面板
├── Resources/Web/                 # Web 资源
│   ├── {sdk}.js                   # 格式 SDK
│   └── player.html                # 播放器 UI
├── Samples/                       # 测试用示例文件
├── Translations.config            # 中英文翻译
├── Scripts/
└── *.csproj / *.sln
```

---

## English

### Introduction

A [QuickLook](https://github.com/QL-Win/QuickLook) plugin for previewing {FormatFullName} ({Extension}) files.

{Brief description of the format and its use cases}

### Features

- Real-time {Format} preview
- {Feature 1}
- {Feature 2}
- Auto-hide control bar
- QuickLook Dark/Light theme support
- Bilingual support (Chinese/English)

### Installation

1. Download `.qlplugin` from [Releases](https://github.com/{user}/{repo}/releases)
2. Ensure QuickLook is running
3. Select the `.qlplugin` file and press Space
4. Click "Install"
5. Restart QuickLook

### Requirements

- Windows 10/11
- [QuickLook](https://github.com/QL-Win/QuickLook) 4.x
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11)

### Build from source

```bash
git clone https://github.com/{user}/{repo}.git
cd {repo}
dotnet build -c Release
```

Package as `.qlplugin`:

```powershell
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

### Keyboard & Mouse Shortcuts

| Action | Shortcut |
|--------|----------|
| Play / Pause | Click button |
| Seek | Click/drag progress bar |
| Volume | Mouse wheel |

### Project Structure

```
├── Plugin.cs                      # IViewer entry point
├── {Format}Panel.xaml(.cs)        # WebView2 player panel
├── Resources/Web/                 # Web assets
│   ├── {sdk}.js                   # Format SDK
│   └── player.html                # Player UI
├── Samples/                       # Sample files for testing
├── Translations.config            # i18n translations
├── Scripts/
└── *.csproj / *.sln
```

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md)

## License

[GPL-3.0](LICENSE)
