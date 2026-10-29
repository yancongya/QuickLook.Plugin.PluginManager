# QuickLook.Plugin.PluginManager

预览 QuickLook 自身数据文件夹时打开的插件管理面板。

## 中文

### 简介

一个用来管理 QuickLook 插件的预览插件：在资源管理器里选中 QuickLook 的数据文件夹并按空格，即可打开管理面板。

### 功能特性

- 查看已安装的用户插件与内置插件，含版本与状态
- 安全卸载用户插件（由 QuickLook 下次启动时完成删除）
- 浏览 QuickLook Wiki 插件目录（现行与历史插件分组，本地缓存）
- 从 GitHub Releases 安装/更新插件，带包校验与官方安装器引导
- 查看 `QuickLook.config`、插件配置与 `QuickLook.Exception.log`
- 修复操作：打开插件目录、重启 QuickLook、清理残留、解除下载文件锁定

### 使用方法

1. 在资源管理器中进入 `%APPDATA%\pooi.moe\QuickLook`
2. 选中该文件夹（或其中任意文件），按空格
3. 插件管理面板打开

安装插件会先把 `.qlplugin` 下载到临时目录，并给出配合 QuickLook 官方安装器完成安装的步骤。安装/卸载后需重启 QuickLook。

### 安装

1. 从 [Releases](https://github.com/yancongya/QuickLook.Plugin.PluginManager/releases) 下载 `.qlplugin` 文件
2. 确保 QuickLook 正在运行
3. 选中 `.qlplugin` 文件，按空格键预览
4. 点击"安装"按钮
5. 重启 QuickLook

### 系统要求

- Windows 10/11
- [QuickLook](https://github.com/QL-Win/QuickLook) 4.x
- 无 WebView2 依赖（原生 WPF 面板）

### 操作

| 操作 | 方式 |
|------|------|
| 刷新 | 右上角刷新按钮（按当前页签执行对应刷新） |
| 检查更新 | 已安装页每行的检查更新按钮 |
| 搜索目录 | 可安装页左上角搜索框（名称 / 描述） |
| 隐藏已安装 | 可安装页工具行勾选框 |
| 主题切换 | 右上角主题按钮（跟随 QuickLook） |

### 从源码构建

```powershell
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

产出 `QuickLook.Plugin.PluginManager.qlplugin`。

### 项目结构

```
├── Plugin.cs                       # IViewer 入口（Priority 100，只接管数据根目录路径）
├── PluginManagerPanel.xaml(.cs)    # 四页签管理面板（已安装 / 可安装 / 配置日志 / 修复）
├── Models/                         # PluginInfo / CatalogEntry 等数据模型
├── Services/                       # 扫描、Wiki 目录、GitHub 下载、配置日志、修复
├── Localization/                   # Loc / LocExtension 双语机制
├── Translations.config             # 中英文文案
├── Samples/（无）                   # 管理插件不带素材
└── Scripts/                        # build.ps1 / pack-zip.ps1 / update-version.ps1
```

## English

### Introduction

A QuickLook plugin that opens a management panel when you preview QuickLook's
own data folder.

### Features

- See installed user and built-in plugins, with versions and status
- Safely uninstall user plugins (finished by QuickLook on the next start)
- Browse the QuickLook Wiki catalog (active and legacy plugins)
- Install or update from GitHub releases, with package validation and official
  installer handoff
- Read `QuickLook.config`, plugin configs and `QuickLook.Exception.log`
- Repair actions: open plugin folder, restart QuickLook, clean leftovers,
  unblock downloaded files

### Usage

1. In Explorer, navigate to `%APPDATA%\pooi.moe\QuickLook`.
2. Select the folder (or any file inside it) and press Space.
3. The plugin manager opens.

Installing a plugin downloads the `.qlplugin` to a temporary folder and shows
how to finish with QuickLook's official installer. Restart QuickLook after
installing or uninstalling.

### Installation

1. Download the `.qlplugin` file from [Releases](https://github.com/yancongya/QuickLook.Plugin.PluginManager/releases)
2. Make sure QuickLook is running
3. Select the `.qlplugin` file and press Space to preview
4. Click the "Install" button
5. Restart QuickLook

### System requirements

- Windows 10/11
- [QuickLook](https://github.com/QL-Win/QuickLook) 4.x
- No WebView2 dependency (native WPF panel)

### Operations

| Action | How |
|--------|-----|
| Refresh | Top-right refresh button (acts on the active tab) |
| Check updates | Per-row check button on the Installed tab |
| Search catalog | Search box at the left of the Available tab toolbar |
| Hide installed | Checkbox on the Available tab toolbar |
| Theme switch | Top-right theme button (follows QuickLook) |

### Build from source

```powershell
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

The package is `QuickLook.Plugin.PluginManager.qlplugin`.

### Project structure

```
├── Plugin.cs                       # IViewer entry (Priority 100, data-root paths only)
├── PluginManagerPanel.xaml(.cs)    # Four-tab management panel
├── Models/                         # PluginInfo / CatalogEntry models
├── Services/                       # Scan, Wiki catalog, GitHub download, config/log, repair
├── Localization/                   # Loc / LocExtension bilingual mechanism
├── Translations.config             # Chinese/English strings
└── Scripts/                        # build.ps1 / pack-zip.ps1 / update-version.ps1
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md)

## Credits

- [QuickLook](https://github.com/QL-Win/QuickLook) by QL-Win

## License

[GPL-3.0](LICENSE)
